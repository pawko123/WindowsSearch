using System.Text.Json;
using Grpc.Core;
using Grpc.Net.Client;
using Hub.Models.Providers;

namespace Hub.Services.Providers.Transports;

public sealed class GrpcProviderClient : IProviderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Method<ProviderSearchRequest, ProviderSearchResponse> SearchMethod = new(
        MethodType.Unary,
        "hub.Provider",
        "Search",
        new Marshaller<ProviderSearchRequest>(SerializeRequest, DeserializeRequest),
        new Marshaller<ProviderSearchResponse>(SerializeResponse, DeserializeResponse));

    private readonly GrpcChannel channel;
    private readonly int timeoutSeconds;

    public GrpcProviderClient(string endpoint, int timeoutSeconds)
    {
        channel = GrpcChannel.ForAddress(NormalizeEndpoint(endpoint));
        this.timeoutSeconds = Math.Max(1, timeoutSeconds);
    }

    public ProviderTransportKind TransportKind => ProviderTransportKind.Grpc;

    public async Task<ProviderSearchResponse> SearchAsync(ProviderSearchRequest request, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        var callOptions = new CallOptions(deadline: deadline, cancellationToken: cancellationToken);
        var response = await channel.CreateCallInvoker().AsyncUnaryCall(SearchMethod, null, callOptions, request).ResponseAsync;
        return response;
    }

    public ValueTask DisposeAsync()
    {
        channel.Dispose();
        return ValueTask.CompletedTask;
    }

    private static byte[] SerializeRequest(ProviderSearchRequest request) => JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);

    private static ProviderSearchRequest DeserializeRequest(byte[] bytes) => JsonSerializer.Deserialize<ProviderSearchRequest>(bytes, JsonOptions) ?? new ProviderSearchRequest();

    private static byte[] SerializeResponse(ProviderSearchResponse response) => JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions);

    private static ProviderSearchResponse DeserializeResponse(byte[] bytes) => JsonSerializer.Deserialize<ProviderSearchResponse>(bytes, JsonOptions) ?? new ProviderSearchResponse();

    private static string NormalizeEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "http://localhost:5001";
        }

        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            ? uri.ToString().TrimEnd('/')
            : $"http://{endpoint.Trim().TrimEnd('/')}";
    }
}