using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Models.Benchmark;
using WindowsSearch.Common.Pipes;
using WindowsSearch.Common.Serialization;

namespace Hub.Services.Providers.Transports;

public sealed class NamedPipeProviderClient : IProviderClient
{
    private readonly string pipeName;
    private readonly int timeoutSeconds;
    private readonly IMessageSerializer serializer;
    private readonly SemaphoreSlim communicationLock = new(1, 1);
    private NamedPipeClientStream? client;

    public ProviderTransportKind TransportKind => ProviderTransportKind.NamedPipe;
    public ProviderBenchmarkMetrics? LastMetrics { get; private set; }

    public NamedPipeProviderClient(string endpoint, int timeoutSeconds, IMessageSerializer serializer)
    {
        pipeName = PipeFrameCodec.NormalizePipeName(endpoint, "hub-provider");
        this.timeoutSeconds = Math.Max(1, timeoutSeconds);
        this.serializer = serializer;
    }

    public async Task<ProviderSearchResponse> SearchAsync(ProviderSearchRequest request, CancellationToken cancellationToken)
    {
        var metrics = new ProviderBenchmarkMetrics();
        var swRoundtrip = Stopwatch.StartNew();

        await communicationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

            var swSer = Stopwatch.StartNew();
            var data = serializer.Serialize(request);
            swSer.Stop();
            metrics.RequestSerializationMs = swSer.Elapsed.TotalMilliseconds;
            metrics.RequestPayloadBytes = data.Length;

            await PipeFrameCodec.WriteFrameAsync(client!, data, cancellationToken).ConfigureAwait(false);

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
        metrics.ResponsePayloadBytes = payload.Length;

        var swDeser = Stopwatch.StartNew();
        var result = serializer.Deserialize<T>(payload) ?? throw new InvalidOperationException("Provider returned an empty payload.");
        swDeser.Stop();
        metrics.ResponseDeserializationMs = swDeser.Elapsed.TotalMilliseconds;

        return result;
    }
}
