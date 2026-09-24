using System.IO;
using System.IO.Pipes;
using System.Net;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;

namespace Hub.Services.Providers.Transports;

public sealed class NamedPipeProviderClient : IProviderClient
{
    private readonly string pipeName;
    private readonly int timeoutSeconds;
    private readonly IMessageSerializer serializer;

    public NamedPipeProviderClient(string endpoint, int timeoutSeconds, IMessageSerializer serializer)
    {
        pipeName = NormalizePipeName(endpoint);
        this.timeoutSeconds = Math.Max(1, timeoutSeconds);
        this.serializer = serializer;
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

    private async Task SendAsync<T>(Stream stream, T payload, CancellationToken cancellationToken)
    {
        var data = serializer.Serialize(payload);
        var lengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(data.Length));
        await stream.WriteAsync(lengthBytes, cancellationToken);
        await stream.WriteAsync(data, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private async Task<T> ReceiveAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = await ReadExactlyAsync(stream, 4, cancellationToken);
        var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBuffer, 0));
        var payload = await ReadExactlyAsync(stream, length, cancellationToken);
        return serializer.Deserialize<T>(payload) ?? throw new InvalidOperationException("Provider returned an empty payload.");
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
