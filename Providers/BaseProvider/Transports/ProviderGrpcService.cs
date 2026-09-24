using BaseProvider.Models;
using Grpc.Core;

namespace BaseProvider.Transports;

[BindServiceMethod(typeof(ProviderGrpcServiceBinder), nameof(ProviderGrpcServiceBinder.BindService))]
public abstract class ProviderGrpcServiceBase
{
    public abstract Task<ProviderSearchResponse> Search(ProviderSearchRequest request, ServerCallContext context);
}

public static class ProviderGrpcServiceBinder
{
    public static readonly Method<ProviderSearchRequest, ProviderSearchResponse> SearchMethod = new(
        MethodType.Unary,
        "hub.Provider",
        "Search",
        new Marshaller<ProviderSearchRequest>(TransportMessageCodec.Serialize, TransportMessageCodec.Deserialize<ProviderSearchRequest>),
        new Marshaller<ProviderSearchResponse>(TransportMessageCodec.Serialize, TransportMessageCodec.Deserialize<ProviderSearchResponse>));

    public static void BindService(ServiceBinderBase serviceBinder, ProviderGrpcServiceBase serviceImpl)
    {
        serviceBinder.AddMethod(SearchMethod, (request, context) => serviceImpl.Search(request, context));
    }
}

public sealed class ProviderGrpcService(Func<ProviderSearchRequest, CancellationToken, Task<ProviderSearchResponse>> handler) : ProviderGrpcServiceBase
{
    public override Task<ProviderSearchResponse> Search(ProviderSearchRequest request, ServerCallContext context) =>
        handler(request, context.CancellationToken);
}