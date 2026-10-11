using System.IO.Pipes;
using BaseProvider.Abstractions;
using WindowsSearch.Common.Models;

namespace BaseProvider.Transports;

public sealed class NamedPipeProviderTransport : IProviderTransport
{
    private readonly string pipeName;

    public NamedPipeProviderTransport(string endpoint)
    {
        pipeName = NormalizePipeName(endpoint);
    }

    public ProviderTransportKind TransportKind => ProviderTransportKind.NamedPipe;

    public async Task RunAsync(Func<ProviderSearchRequest, CancellationToken, Task<ProviderSearchResponse>> handler, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream server;
            try
            {
                server = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await server.WaitForConnectionAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await server.DisposeAsync();
                break;
            }
            catch
            {
                await server.DisposeAsync();
                continue;
            }

            _ = HandleClientConnectionAsync(server, handler, cancellationToken);
        }
    }

    private static async Task HandleClientConnectionAsync(
        NamedPipeServerStream server,
        Func<ProviderSearchRequest, CancellationToken, Task<ProviderSearchResponse>> handler,
        CancellationToken cancellationToken)
    {
        await using (server)
        {
            try
            {
                while (server.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    byte[] requestBytes;
                    try
                    {
                        requestBytes = await TransportMessageCodec.ReadFrameAsync(server, cancellationToken);
                    }
                    catch (EndOfStreamException)
                    {
                        break;
                    }

                    var request = TransportMessageCodec.Deserialize<ProviderSearchRequest>(requestBytes);
                    var response = await handler(request, cancellationToken);
                    var responseBytes = TransportMessageCodec.Serialize(response);
                    await TransportMessageCodec.WriteFrameAsync(server, responseBytes, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Pipe disconnected or closed by client
            }
            finally
            {
                if (server.IsConnected)
                {
                    try { server.Disconnect(); } catch { }
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
