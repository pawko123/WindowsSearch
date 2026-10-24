using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Models.Benchmark;
using WindowsSearch.Common.Pipes;
using WindowsSearch.Common.Serialization;
using WindowsSearch.Common.Security;
using WindowsSearch.Common.Logging;

namespace Hub.Services.Providers.Transports;

public sealed class NamedPipeProviderClient : IProviderClient
{
    private readonly string pipeName;
    private readonly int timeoutSeconds;
    private readonly IMessageSerializer serializer;
    private readonly EncryptionKind encryptionKind;
    private readonly byte[] masterKey;
    private readonly SemaphoreSlim communicationLock = new(1, 1);
    private NamedPipeClientStream? client;
    public ProviderTransportKind TransportKind => ProviderTransportKind.NamedPipe;
    public ProviderBenchmarkMetrics? LastMetrics { get; private set; }

    public NamedPipeProviderClient(string endpoint, int timeoutSeconds, IMessageSerializer serializer, EncryptionKind encryptionKind = EncryptionKind.None, byte[]? masterKey = null)
    {
        pipeName = PipeFrameCodec.NormalizePipeName(endpoint, "hub-provider");
        this.timeoutSeconds = Math.Max(1, timeoutSeconds);
        this.serializer = serializer;
        this.encryptionKind = encryptionKind;
        this.masterKey = masterKey ?? [];
    }

    public async Task<ProviderSearchResponse> SearchAsync(ProviderSearchRequest request, CancellationToken cancellationToken)
    {
        var metrics = new ProviderBenchmarkMetrics();
        var swRoundtrip = Stopwatch.StartNew();

        await communicationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

            var wireBytes = ClientMessageCodec.EncodeRequest(request, serializer, encryptionKind, masterKey, metrics, "NamedPipeProviderClient");
            await PipeFrameCodec.WriteFrameAsync(client!, wireBytes, cancellationToken).ConfigureAwait(false);

            var response = await ReceiveAsync<ProviderSearchResponse>(client!, metrics, cancellationToken).ConfigureAwait(false);
            swRoundtrip.Stop();

            metrics.TransportRoundtripMs = swRoundtrip.Elapsed.TotalMilliseconds;
            metrics.ProviderExecutionMs = response.ProviderExecutionMs;
            LastMetrics = metrics;

            return response;
        }
        catch (Exception)
        {
            ResetConnection();
            throw;
        }
        finally
        {
            communicationLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        bool acquired = false;
        try
        {
            acquired = await communicationLock.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            ResetConnection();
        }
        finally
        {
            if (acquired)
            {
                communicationLock.Release();
            }
            communicationLock.Dispose();
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (client is { IsConnected: true })
        {
            return;
        }

        ResetConnection();

        var newClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeFrameCodec.DefaultClientPipeOptions);
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        await newClient.ConnectAsync(linkedCts.Token).ConfigureAwait(false);
        client = newClient;
    }

    private void ResetConnection()
    {
        if (client != null)
        {
            try { client.Dispose(); } catch { }
            client = null;
        }
    }

    private async Task<T> ReceiveAsync<T>(Stream stream, ProviderBenchmarkMetrics metrics, CancellationToken cancellationToken)
    {
        var payload = await PipeFrameCodec.ReadFrameAsync(stream, PipeFrameCodec.DefaultMaxMessageSize, cancellationToken).ConfigureAwait(false);
        return ClientMessageCodec.DecodeResponse<T>(payload, serializer, encryptionKind, masterKey, metrics, "NamedPipeProviderClient");
    }
}
