using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Text.Json;
using Hub.Models.Providers;

namespace Hub.Services.Providers.Transports;

public sealed class NamedPipeProviderClient : IProviderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string pipeName;
    private readonly int timeoutSeconds;

    public NamedPipeProviderClient(string endpoint, int timeoutSeconds)
    {
        pipeName = NormalizePipeName(endpoint);
        this.timeoutSeconds = Math.Max(1, timeoutSeconds);
    }

    public ProviderTransportKind TransportKind => ProviderTransportKind.NamedPipe;

    public async Task<ProviderSearchResponse> SearchAsync(ProviderSearchRequest request, CancellationToken cancellationToken)
    {
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(timeoutSeconds * 1000, cancellationToken);

        await SendAsync(client, request, cancellationToken);
        return await ReceiveAsync<ProviderSearchResponse>(client, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    private static async Task SendAsync<T>(Stream stream, T payload, CancellationToken cancellationToken)
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var lengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(data.Length));
        await stream.WriteAsync(lengthBytes, cancellationToken);
        await stream.WriteAsync(data, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<T> ReceiveAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = await ReadExactlyAsync(stream, 4, cancellationToken);
        var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBuffer, 0));
        var payload = await ReadExactlyAsync(stream, length, cancellationToken);
        return JsonSerializer.Deserialize<T>(payload, JsonOptions) ?? throw new InvalidOperationException("Provider returned an empty payload.");
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