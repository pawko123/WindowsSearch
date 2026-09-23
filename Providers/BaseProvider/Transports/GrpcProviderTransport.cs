using Grpc.Core;
using BaseProvider.Abstractions;
using BaseProvider.Models;

namespace BaseProvider.Transports;

public sealed class GrpcProviderTransport : IProviderTransport
{
    private static readonly Method<ProviderSearchRequest, ProviderSearchResponse> SearchMethod = new(
        MethodType.Unary,
        "hub.Provider",
        "Search",
        new Marshaller<ProviderSearchRequest>(TransportMessageCodec.Serialize, TransportMessageCodec.Deserialize<ProviderSearchRequest>),
        new Marshaller<ProviderSearchResponse>(TransportMessageCodec.Serialize, TransportMessageCodec.Deserialize<ProviderSearchResponse>));

    private readonly Server server;

    public GrpcProviderTransport(string endpoint, int timeoutSeconds)
    {
        var port = NormalizePort(endpoint);
        server = new Server
        {
            Services =
            {
                ServerServiceDefinition.CreateBuilder()
                    .AddMethod(SearchMethod, async (request, context) => await _handler(request, context.CancellationToken))
                    .Build()
            },
            Ports =
            {
                new ServerPort("127.0.0.1", port, ServerCredentials.Insecure)
            }
        };
    }

    private Func<ProviderSearchRequest, CancellationToken, Task<ProviderSearchResponse>> _handler = (_, _) => Task.FromResult(new ProviderSearchResponse());

    public ProviderTransportKind TransportKind => ProviderTransportKind.Grpc;

    public async Task RunAsync(Func<ProviderSearchRequest, CancellationToken, Task<ProviderSearchResponse>> handler, CancellationToken cancellationToken)
    {
        _handler = handler;
        server.Start();

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await server.ShutdownAsync();
        }
    }

    public ValueTask DisposeAsync()
    {
        server.ShutdownAsync().GetAwaiter().GetResult();
        return ValueTask.CompletedTask;
    }

    private static int NormalizePort(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return 5001;
        }

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Port > 0)
        {
            return uri.Port;
        }

        if (int.TryParse(endpoint, out var port))
        {
            return port;
        }

        return 5001;
    }
}
