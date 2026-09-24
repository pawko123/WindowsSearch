using System.Net.Http;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;

namespace Hub.Services.Providers.Transports;

public sealed class HttpProviderClient : IProviderClient
{
    private readonly HttpClient httpClient;
    private readonly string searchEndpoint;
    private readonly IMessageSerializer serializer;

    public HttpProviderClient(string endpoint, int timeoutSeconds, IMessageSerializer serializer)
    {
        httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds))
        };

        searchEndpoint = BuildSearchEndpoint(endpoint);
        this.serializer = serializer;
    }

    public ProviderTransportKind TransportKind => ProviderTransportKind.Http;

    public async Task<ProviderSearchResponse> SearchAsync(ProviderSearchRequest request, CancellationToken cancellationToken)
    {
        var data = serializer.Serialize(request);
        using var content = new ByteArrayContent(data);
        
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(serializer.ContentType);

        using var response = await httpClient.PostAsync(searchEndpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return serializer.Deserialize<ProviderSearchResponse>(responseData) ?? new ProviderSearchResponse();
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
