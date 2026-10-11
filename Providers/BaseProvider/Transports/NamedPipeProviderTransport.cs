using System.Collections.Concurrent;
using System.IO.Pipes;
using BaseProvider.Abstractions;
using WindowsSearch.Common.Logging;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Pipes;

namespace BaseProvider.Transports;

public sealed class NamedPipeProviderTransport : IProviderTransport
{
    private readonly string pipeName;
    private readonly ConcurrentDictionary<Task, bool> activeConnections = new();

    public ProviderTransportKind TransportKind => ProviderTransportKind.NamedPipe;

    public NamedPipeProviderTransport(string endpoint)
    {
        pipeName = PipeFrameCodec.NormalizePipeName(endpoint, "base_provider");
    }

    public async Task RunAsync(Func<ProviderSearchRequest, CancellationToken, Task<ProviderSearchResponse>> handler, CancellationToken cancellationToken)
    {
        AppLogger.Info($"[NamedPipeProviderTransport] Listening on pipe: {pipeName}");

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
                    PipeFrameCodec.DefaultServerPipeOptions,
                    PipeFrameCodec.DefaultPipeBufferSize,
                    PipeFrameCodec.DefaultPipeBufferSize);
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested)
            {
                AppLogger.Debug($"[NamedPipeProviderTransport] Server creation interrupted: {ex.Message}");
                break;
            }

            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await server.DisposeAsync().ConfigureAwait(false);
                break;
            }
            catch (Exception ex)
            {
                AppLogger.Debug($"[NamedPipeProviderTransport] WaitForConnection failed: {ex.Message}");
                await server.DisposeAsync().ConfigureAwait(false);
                continue;
            }

            var connectionTask = HandleClientConnectionAsync(server, handler, cancellationToken);
            activeConnections.TryAdd(connectionTask, true);
            _ = connectionTask.ContinueWith(t => activeConnections.TryRemove(t, out _), TaskScheduler.Default);
        }
    }

    public async ValueTask DisposeAsync()
    {
        var tasks = activeConnections.Keys.ToArray();
        if (tasks.Length > 0)
        {
            try
            {
                await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort wait on shutdown
            }
        }
    }

    private static async Task HandleClientConnectionAsync(
        NamedPipeServerStream server,
        Func<ProviderSearchRequest, CancellationToken, Task<ProviderSearchResponse>> handler,
        CancellationToken cancellationToken)
    {
        await using (server.ConfigureAwait(false))
        {
            try
            {
                while (server.IsConnected && !cancellationToken.IsCancellationRequested)
                {
                    byte[] requestBytes;
                    try
                    {
                        requestBytes = await TransportMessageCodec.ReadFrameAsync(server, cancellationToken).ConfigureAwait(false);
                    }
                    catch (EndOfStreamException)
                    {
                        // Client cleanly closed the connection
                        break;
                    }
                    catch (InvalidDataException ex)
                    {
                        AppLogger.Error($"[NamedPipeProviderTransport] Invalid frame received: {ex.Message}");
                        break;
                    }

                    var request = TransportMessageCodec.Deserialize<ProviderSearchRequest>(requestBytes);

                    ProviderSearchResponse response;
                    try
                    {
                        response = await handler(request, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"[NamedPipeProviderTransport] Search handler failed: {ex.Message}");
                        response = new ProviderSearchResponse();
                    }

                    var responseBytes = TransportMessageCodec.Serialize(response);
                    await TransportMessageCodec.WriteFrameAsync(server, responseBytes, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when process or query token is cancelled
            }
            catch (Exception ex)
            {
                AppLogger.Debug($"[NamedPipeProviderTransport] Connection ended: {ex.Message}");
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
}
