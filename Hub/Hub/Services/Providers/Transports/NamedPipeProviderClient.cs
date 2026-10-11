using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Net;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Models.Benchmark;
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
        pipeName = NormalizePipeName(endpoint);
        this.timeoutSeconds = Math.Max(1, timeoutSeconds);
        this.serializer = serializer;
    }

    public async Task<ProviderSearchResponse> SearchAsync(ProviderSearchRequest request, CancellationToken cancellationToken)
    {
        var metrics = new ProviderBenchmarkMetrics();
        var swRoundtrip = Stopwatch.StartNew();

        await communicationLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureConnectedAsync(cancellationToken);

            var swSer = Stopwatch.StartNew();
            var data = serializer.Serialize(request);
            swSer.Stop();
            metrics.RequestSerializationMs = swSer.Elapsed.TotalMilliseconds;
            metrics.RequestPayloadBytes = data.Length;

            var lengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(data.Length));
            await client!.WriteAsync(lengthBytes, cancellationToken);
            await client.WriteAsync(data, cancellationToken);
            await client.FlushAsync(cancellationToken);

            var response = await ReceiveAsync<ProviderSearchResponse>(client, metrics, cancellationToken);
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
        await communicationLock.WaitAsync();
        try
        {
            ResetConnection();
        }
        finally
        {
            communicationLock.Release();
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

        var newClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        await newClient.ConnectAsync(linkedCts.Token);
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
        var lengthBuffer = await ReadExactlyAsync(stream, 4, cancellationToken);
        var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBuffer, 0));
        var payload = await ReadExactlyAsync(stream, length, cancellationToken);
        metrics.ResponsePayloadBytes = payload.Length;

        var swDeser = Stopwatch.StartNew();
        var result = serializer.Deserialize<T>(payload) ?? throw new InvalidOperationException("Provider returned an empty payload.");
        swDeser.Stop();
        metrics.ResponseDeserializationMs = swDeser.Elapsed.TotalMilliseconds;

        return result;
    }

    private static async Task<byte[]> ReadExactlyAsync(Stream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;

        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("Unexpected end of pipe stream.");
            }

            offset += read;
        }

        return buffer;
    }

    private static string NormalizePipeName(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "hub-provider";
        }

        const string prefix = @"\\.\pipe\";
        return endpoint.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? endpoint[prefix.Length..]
            : endpoint;
    }
}
