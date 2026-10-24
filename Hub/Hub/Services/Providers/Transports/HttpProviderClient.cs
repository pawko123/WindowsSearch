using System.Diagnostics;
using System.Net.Http;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Models.Benchmark;
using WindowsSearch.Common.Serialization;
using WindowsSearch.Common.Security;
using WindowsSearch.Common.Logging;

namespace Hub.Services.Providers.Transports;

public sealed class HttpProviderClient : IProviderClient
{
    private readonly HttpClient httpClient;
    private readonly string searchEndpoint;
    private readonly IMessageSerializer serializer;
    private readonly EncryptionKind encryptionKind;
    private readonly byte[] masterKey;
    public ProviderTransportKind TransportKind => ProviderTransportKind.Http;
    public ProviderBenchmarkMetrics? LastMetrics { get; private set; }

    public HttpProviderClient(string endpoint, int timeoutSeconds, IMessageSerializer serializer, EncryptionKind encryptionKind = EncryptionKind.None, byte[]? masterKey = null)
    {
        httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds))
        };

        searchEndpoint = BuildSearchEndpoint(endpoint);
        this.serializer = serializer;
        this.encryptionKind = encryptionKind;
        this.masterKey = masterKey ?? [];
    }

    public async Task<ProviderSearchResponse> SearchAsync(ProviderSearchRequest request, CancellationToken cancellationToken)
    {
        var metrics = new ProviderBenchmarkMetrics();
        var swRoundtrip = Stopwatch.StartNew();

        var wireBytes = ClientMessageCodec.EncodeRequest(request, serializer, encryptionKind, masterKey, metrics, "HttpProviderClient");

        using var content = new ByteArrayContent(wireBytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(serializer.ContentType);

        using var response = await httpClient.PostAsync(searchEndpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        swRoundtrip.Stop();

        metrics.TransportRoundtripMs = swRoundtrip.Elapsed.TotalMilliseconds;

        var searchResponse = ClientMessageCodec.DecodeResponse<ProviderSearchResponse>(responseData, serializer, encryptionKind, masterKey, metrics, "HttpProviderClient");
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
            return "http://127.0.0.1:5000/search";
        }

        string rawUri;
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            rawUri = new Uri(uri, "search").ToString();
        }
        else
        {
            rawUri = new Uri($"http://{endpoint.TrimStart('/')}").ToString().TrimEnd('/') + "/search";
        }

        return rawUri.Replace("localhost", "127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }
}
