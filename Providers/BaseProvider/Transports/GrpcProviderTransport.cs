using BaseProvider.Abstractions;
using BaseProvider.Models;
using WindowsSearch.Common.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaseProvider.Transports;

public sealed class GrpcProviderTransport : IProviderTransport
{
    private readonly int port;
    private WebApplication? app;

    public GrpcProviderTransport(string endpoint, int timeoutSeconds)
    {
        port = NormalizePort(endpoint);
    }

    public ProviderTransportKind TransportKind => ProviderTransportKind.Grpc;

    public async Task RunAsync(Func<ProviderSearchRequest, CancellationToken, Task<ProviderSearchResponse>> handler, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenLocalhost(port, listenOptions => listenOptions.Protocols = HttpProtocols.Http2);
        });

        builder.Services.AddGrpc();
        builder.Services.AddSingleton(new ProviderGrpcService(handler));

        app = builder.Build();
        app.MapGrpcService<ProviderGrpcService>();

        await app.StartAsync(cancellationToken);

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await app.StopAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (app is not null)
        {
            await app.DisposeAsync();
        }
    }

    private static int NormalizePort(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return 5001;
        }

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && uri.Port > 0)
        {
            return uri.Port;
        }

        if (int.TryParse(endpoint, out var port))
        {
            return port;
        }

        return 5001;
    }
}
