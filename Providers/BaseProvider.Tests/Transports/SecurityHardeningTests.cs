using System.Net;
using System.Net.Sockets;
using BaseProvider.Transports;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;

namespace BaseProvider.Tests.Transports;

public class SecurityHardeningTests
{
    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task HttpTransport_ExternalHostHeader_ReturnsForbidden()
    {
        int port = GetAvailablePort();
        var endpoint = $"http://127.0.0.1:{port}/";
        var transport = new HttpProviderTransport(endpoint);
        using var cts = new CancellationTokenSource();

        var serverTask = Task.Run(async () =>
        {
            await transport.RunAsync((req, token) => Task.FromResult(new ProviderSearchResponse()), cts.Token);
        });

        await Task.Delay(150);

        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}/search");
        request.Headers.Host = "evil-rebinding-domain.com";
        request.Content = new ByteArrayContent(TransportMessageCodec.Serialize(new ProviderSearchRequest { Query = "test" }));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        cts.Cancel();
        await transport.DisposeAsync();
        try { await serverTask.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
    }

    [Fact]
    public async Task HttpTransport_WithOriginHeader_ReturnsForbidden()
    {
        int port = GetAvailablePort();
        var endpoint = $"http://127.0.0.1:{port}/";
        var transport = new HttpProviderTransport(endpoint);
        using var cts = new CancellationTokenSource();

        var serverTask = Task.Run(async () =>
        {
            await transport.RunAsync((req, token) => Task.FromResult(new ProviderSearchResponse()), cts.Token);
        });

        await Task.Delay(150);

        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}/search");
        request.Headers.Add("Origin", "http://malicious-website.com");
        request.Content = new ByteArrayContent(TransportMessageCodec.Serialize(new ProviderSearchRequest { Query = "test" }));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        cts.Cancel();
        await transport.DisposeAsync();
        try { await serverTask.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
    }

    [Fact]
    public async Task HttpTransport_WithValidLocalhost_ReturnsOk()
    {
        int port = GetAvailablePort();
        var endpoint = $"http://127.0.0.1:{port}/";
        var transport = new HttpProviderTransport(endpoint);
        TransportMessageCodec.Initialize(new JsonMessageSerializer());
        using var cts = new CancellationTokenSource();

        var serverTask = Task.Run(async () =>
        {
            await transport.RunAsync((req, token) =>
            {
                var resp = new ProviderSearchResponse
                {
                    Categories = [new ProviderResultCategory { Name = "Secured" }]
                };
                return Task.FromResult(resp);
            }, cts.Token);
        });

        await Task.Delay(150);

        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}/search");
        request.Headers.Host = $"127.0.0.1:{port}";
        request.Content = new ByteArrayContent(TransportMessageCodec.Serialize(new ProviderSearchRequest { Query = "test" }));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var deserialized = TransportMessageCodec.Deserialize<ProviderSearchResponse>(bytes);
        Assert.Single(deserialized.Categories);
        Assert.Equal("Secured", deserialized.Categories[0].Name);

        cts.Cancel();
        await transport.DisposeAsync();
        try { await serverTask.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
    }
}
