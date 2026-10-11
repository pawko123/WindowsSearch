using System.Diagnostics;
using System.Net.Http;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Models.Benchmark;
using WindowsSearch.Common.Serialization;

namespace Hub.Services.Providers.Transports;

public sealed class HttpProviderClient : IProviderClient
{
    private readonly HttpClient httpClient;
    private readonly string searchEndpoint;
    private readonly IMessageSerializer serializer;

    public ProviderTransportKind TransportKind => ProviderTransportKind.Http;
    public ProviderBenchmarkMetrics? LastMetrics { get; private set; }

    public HttpProviderClient(string endpoint, int timeoutSeconds, IMessageSerializer serializer)
    {
        httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds))
        };

        searchEndpoint = BuildSearchEndpoint(endpoint);
        this.serializer = serializer;
    }

    public async Task<ProviderSearchResponse> SearchAsync(ProviderSearchRequest request, CancellationToken cancellationToken)
    {
        var metrics = new ProviderBenchmarkMetrics();
        var swRoundtrip = Stopwatch.StartNew();

        var swSer = Stopwatch.StartNew();
        var data = serializer.Serialize(request);
        swSer.Stop();
        metrics.RequestSerializationMs = swSer.Elapsed.TotalMilliseconds;
        metrics.RequestPayloadBytes = data.Length;

        using var content = new ByteArrayContent(data);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(serializer.ContentType);

        using var response = await httpClient.PostAsync(searchEndpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        swRoundtrip.Stop();

        metrics.ResponsePayloadBytes = responseData.Length;
        metrics.TransportRoundtripMs = swRoundtrip.Elapsed.TotalMilliseconds;

        var swDeser = Stopwatch.StartNew();
        var searchResponse = serializer.Deserialize<ProviderSearchResponse>(responseData) ?? new ProviderSearchResponse();
        swDeser.Stop();
        metrics.ResponseDeserializationMs = swDeser.Elapsed.TotalMilliseconds;
        metrics.ProviderExecutionMs = searchResponse.ProviderExecutionMs;
        LastMetrics = metrics;

        return searchResponse;
    }

    public ValueTask DisposeAsync()
    {
        httpClient.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string BuildSearchEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "http://localhost:5000/search";
        }

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return new Uri(uri, "search").ToString();
        }

        return new Uri($"http://{endpoint.TrimStart('/')}").ToString().TrimEnd('/') + "/search";
    }
}
