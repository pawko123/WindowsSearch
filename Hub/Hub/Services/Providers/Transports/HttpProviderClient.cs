using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Hub.Models.Providers;

namespace Hub.Services.Providers.Transports;

public sealed class HttpProviderClient : IProviderClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient httpClient;
    private readonly string searchEndpoint;

    public HttpProviderClient(string endpoint, int timeoutSeconds)
    {
        httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds))
        };

        searchEndpoint = BuildSearchEndpoint(endpoint);
    }

    public ProviderTransportKind TransportKind => ProviderTransportKind.Http;

    public async Task<ProviderSearchResponse> SearchAsync(ProviderSearchRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(searchEndpoint, request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProviderSearchResponse>(JsonOptions, cancellationToken)) ?? new ProviderSearchResponse();
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