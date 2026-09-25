using System.Net;
using BaseProvider.Abstractions;
using WindowsSearch.Common.Models;

namespace BaseProvider.Transports;

public sealed class HttpProviderTransport : IProviderTransport
{
    private readonly HttpListener listener = new();
    private readonly int timeoutSeconds;

    public HttpProviderTransport(string endpoint, int timeoutSeconds)
    {
        var prefix = NormalizePrefix(endpoint);
        listener.Prefixes.Add(prefix);
        this.timeoutSeconds = Math.Max(1, timeoutSeconds);
    }

    public ProviderTransportKind TransportKind => ProviderTransportKind.Http;

    public async Task RunAsync(Func<ProviderSearchRequest, CancellationToken, Task<ProviderSearchResponse>> handler, CancellationToken cancellationToken)
    {
        listener.Start();
        cancellationToken.Register(() =>
        {
            try
            {
                listener.Stop();
            }
            catch { }
        });

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync();
                }
                catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                _ = Task.Run(async () => await HandleContextAsync(context, handler, cancellationToken), cancellationToken);
            }
        }
        finally
        {
            listener.Close();
        }
    }

    public ValueTask DisposeAsync()
    {
        listener.Close();
        return ValueTask.CompletedTask;
    }

    private async Task HandleContextAsync(HttpListenerContext context, Func<ProviderSearchRequest, CancellationToken, Task<ProviderSearchResponse>> handler, CancellationToken cancellationToken)
    {
        try
        {
            if (context.Request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) && context.Request.Url?.AbsolutePath.Equals("/settings", StringComparison.OrdinalIgnoreCase) == true)
            {
                await WriteJsonAsync(context.Response, new
                {
                    transport = TransportKind.ToString(),
                    endpoint = context.Request.Url?.GetLeftPart(UriPartial.Authority),
                    timeoutSeconds
                }, cancellationToken);
                return;
            }

            if (!context.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) || !context.Request.Url?.AbsolutePath.Equals("/search", StringComparison.OrdinalIgnoreCase) == true)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.Close();
                return;
            }

            using var mem = new MemoryStream();
            await context.Request.InputStream.CopyToAsync(mem, cancellationToken);
            var request = TransportMessageCodec.Deserialize<ProviderSearchRequest>(mem.ToArray()) ?? new ProviderSearchRequest();
            
            var response = await handler(request, cancellationToken);
            
            var responseBytes = TransportMessageCodec.Serialize(response);
            context.Response.ContentType = TransportMessageCodec.ContentType;
            context.Response.ContentLength64 = responseBytes.Length;
            await context.Response.OutputStream.WriteAsync(responseBytes, cancellationToken);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await WriteJsonAsync(context.Response, new { error = ex.Message }, cancellationToken);
        }
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, object payload, CancellationToken cancellationToken)
    {
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        response.ContentType = "application/json";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, cancellationToken);
        response.Close();
    }

    private static string NormalizePrefix(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "http://localhost:5000/";
        }

        if (!endpoint.EndsWith('/'))
        {
            endpoint += "/";
        }

        if (!endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = "http://" + endpoint;
        }

        return endpoint.EndsWith("search/", StringComparison.OrdinalIgnoreCase)
            ? endpoint
            : endpoint.TrimEnd('/') + "/";
    }
}
