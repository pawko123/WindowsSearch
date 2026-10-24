using System.Diagnostics;
using System.Threading;
using Grpc.Core;
using Grpc.Net.Client;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Models.Benchmark;
using WindowsSearch.Common.Serialization;

namespace Hub.Services.Providers.Transports;

public sealed class GrpcProviderClient : IProviderClient
{
    private readonly Method<ProviderSearchRequest, ProviderSearchResponse> searchMethod;
    private readonly GrpcChannel channel;
    private readonly int timeoutSeconds;
    private readonly AsyncLocal<ProviderBenchmarkMetrics?> currentMetrics = new();

    public ProviderTransportKind TransportKind => ProviderTransportKind.Grpc;
    public ProviderBenchmarkMetrics? LastMetrics { get; private set; }

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
            new Marshaller<ProviderSearchRequest>(
                req =>
                {
                    var m = currentMetrics.Value;
                    var sw = Stopwatch.StartNew();
                    var bytes = serializer.Serialize(req);
                    sw.Stop();
                    if (m != null)
                    {
                        m.RequestSerializationMs = sw.Elapsed.TotalMilliseconds;
                        m.RequestPayloadBytes = bytes.Length;
                    }
                    return bytes;
                },
                bytes => serializer.Deserialize<ProviderSearchRequest>(bytes)),
            new Marshaller<ProviderSearchResponse>(
                res => serializer.Serialize(res),
                bytes =>
                {
                    var m = currentMetrics.Value;
                    if (m != null)
                    {
                        m.ResponsePayloadBytes = bytes.Length;
                    }
                    var sw = Stopwatch.StartNew();
                    var res = serializer.Deserialize<ProviderSearchResponse>(bytes) ?? new ProviderSearchResponse();
                    sw.Stop();
                    if (m != null)
                    {
                        m.ResponseDeserializationMs = sw.Elapsed.TotalMilliseconds;
                    }
                    return res;
                }));
    }

    public async Task<ProviderSearchResponse> SearchAsync(ProviderSearchRequest request, CancellationToken cancellationToken)
    {
        var metrics = new ProviderBenchmarkMetrics();
        currentMetrics.Value = metrics;
        var swRoundtrip = Stopwatch.StartNew();

        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        var callOptions = new CallOptions(deadline: deadline, cancellationToken: cancellationToken);
        var response = await channel.CreateCallInvoker().AsyncUnaryCall(searchMethod, null, callOptions, request).ResponseAsync;
        swRoundtrip.Stop();

        metrics.TransportRoundtripMs = swRoundtrip.Elapsed.TotalMilliseconds;
        metrics.ProviderExecutionMs = response.ProviderExecutionMs;
        LastMetrics = metrics;

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
