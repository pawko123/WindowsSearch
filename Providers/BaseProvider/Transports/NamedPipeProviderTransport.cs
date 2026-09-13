using System.IO.Pipes;
using BaseProvider.Abstractions;
using BaseProvider.Models;

namespace BaseProvider.Transports;

public sealed class NamedPipeProviderTransport : IProviderTransport
{
    private readonly string pipeName;

    public NamedPipeProviderTransport(string endpoint, int timeoutSeconds)
    {
        pipeName = NormalizePipeName(endpoint);
    }

    public ProviderTransportKind TransportKind => ProviderTransportKind.NamedPipe;

    public async Task RunAsync(Func<ProviderSearchRequest, CancellationToken, Task<ProviderSearchResponse>> handler, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync(cancellationToken);

            try
            {
                var requestBytes = await TransportMessageCodec.ReadFrameAsync(server, cancellationToken);
                var request = TransportMessageCodec.Deserialize<ProviderSearchRequest>(requestBytes);
                var response = await handler(request, cancellationToken);
                var responseBytes = TransportMessageCodec.Serialize(response);
                await TransportMessageCodec.WriteFrameAsync(server, responseBytes, cancellationToken);
            }
            finally
            {
                if (server.IsConnected)
                {
                    server.Disconnect();
                }
            }
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string NormalizePipeName(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "base_provider";
        }

        const string prefix = @"\\.\pipe\";
        return endpoint.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? endpoint[prefix.Length..]
            : endpoint;
    }
}
