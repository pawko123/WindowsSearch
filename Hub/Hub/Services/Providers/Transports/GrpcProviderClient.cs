using Grpc.Core;
using Grpc.Net.Client;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;

namespace Hub.Services.Providers.Transports;

public sealed class GrpcProviderClient : IProviderClient
{
    private readonly Method<ProviderSearchRequest, ProviderSearchResponse> searchMethod;
    private readonly GrpcChannel channel;
    private readonly int timeoutSeconds;

    static GrpcProviderClient()
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    public GrpcProviderClient(string endpoint, int timeoutSeconds, IMessageSerializer serializer)
    {
        channel = GrpcChannel.ForAddress(NormalizeEndpoint(endpoint));
        this.timeoutSeconds = Math.Max(1, timeoutSeconds);
        searchMethod = new Method<ProviderSearchRequest, ProviderSearchResponse>(
            MethodType.Unary,
            "hub.Provider",
            "Search",
            new Marshaller<ProviderSearchRequest>(serializer.Serialize, serializer.Deserialize<ProviderSearchRequest>),
            new Marshaller<ProviderSearchResponse>(serializer.Serialize, serializer.Deserialize<ProviderSearchResponse>));
    }

    public ProviderTransportKind TransportKind => ProviderTransportKind.Grpc;

    public async Task<ProviderSearchResponse> SearchAsync(ProviderSearchRequest request, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        var callOptions = new CallOptions(deadline: deadline, cancellationToken: cancellationToken);
        var response = await channel.CreateCallInvoker().AsyncUnaryCall(searchMethod, null, callOptions, request).ResponseAsync;
        return response;
    }

    public ValueTask DisposeAsync()
    {
        channel.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string NormalizeEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "http://127.0.0.1:5001";
        }

        var uriStr = Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            ? uri.ToString().TrimEnd('/')
            : $"http://{endpoint.Trim().TrimEnd('/')}";

        return uriStr.Replace("localhost", "127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }
}
