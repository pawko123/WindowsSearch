using BaseProvider.Abstractions;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;
using WindowsSearch.Common.Validation;
using BaseProvider.Transports;

using WindowsSearch.Common.Logging;

namespace BaseProvider.Host;

public static class BaseProviderHost
{
    public static async Task RunAsync<TSettings>(string[] args, IResultFinder<TSettings> resultFinder)
        where TSettings : ProviderSettingsBase, new()
    {
        var providerName = new DirectoryInfo(AppContext.BaseDirectory).Name;
        if (string.IsNullOrWhiteSpace(providerName) || providerName.Equals("bin", StringComparison.OrdinalIgnoreCase))
        {
            providerName = "UnknownProvider";
        }
        var settings = LoadSettings<TSettings>();
        AppLogger.Initialize(providerName, settings.LogLevel);
        AppLogger.Info($"Provider {providerName} starting up...");

        var validationErrors = SettingsValidationHelper.Validate(settings);
        if (validationErrors.Count > 0)
        {
            AppLogger.Error($"Provider {providerName} has invalid settings.yaml: {string.Join("; ", validationErrors)}");
            throw new InvalidOperationException($"Invalid settings.yaml for provider {providerName}: {string.Join("; ", validationErrors)}");
        }

        try
        {
            var transportKind = ProviderTransportKind.NamedPipe;
            if (args.Length > 0 && Enum.TryParse<ProviderTransportKind>(args[0], true, out var tk))
            {
                transportKind = tk;
            }

            var endpoint = transportKind switch
            {
                ProviderTransportKind.Http => settings.EndpointHttp,
                ProviderTransportKind.Grpc => settings.EndpointGrpc,
                _ => settings.EndpointNamedPipe
            };

            if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
            {
                endpoint = args[1];
            }

            var serializationKind = SerializationKind.Json;
            if (args.Length > 2 && Enum.TryParse<SerializationKind>(args[2], true, out var sk))
            {
                serializationKind = sk;
            }

            AppLogger.Info($"Provider {providerName} initialized. Transport: {transportKind}, Endpoint: {endpoint}, Serialization: {serializationKind}");

            TransportMessageCodec.Initialize(MessageSerializerFactory.Create(serializationKind));

            IProviderTransport transport = new ProviderTransportFactory().Create(transportKind, endpoint);

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            _ = MonitorParentProcessAsync(cts, providerName);

            var serializer = MessageSerializerFactory.Create(serializationKind);

            await transport.RunAsync(async (request, token) =>
            {
                if (AppLogger.LogLevel <= LogLevel.Debug)
                {
                    AppLogger.Debug($"Received request: {serializer.FormatForLog(request)}");
                }

                var effectiveSettings = ResolveEffectiveSettings(settings, request.SettingsYaml, providerName);

                var response = await resultFinder.FindAsync(request, effectiveSettings, token);

                if (AppLogger.LogLevel <= LogLevel.Debug)
                {
                    AppLogger.Debug($"Sending response: {serializer.FormatForLog(response)}");
                }

                return response;
            }, cts.Token);
        }
        catch (OperationCanceledException)
        {
            AppLogger.Info($"Provider {providerName} shutting down.");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Provider {providerName} encountered a fatal error during startup:", ex);
            throw;
        }
    }

    private static async Task MonitorParentProcessAsync(CancellationTokenSource cts, string providerName)
    {
        try
        {
            await using var stdin = Console.OpenStandardInput();
            var buffer = new byte[1];
            while (await stdin.ReadAsync(buffer, cts.Token) > 0)
            {
                // Discard any unexpected input; only EOF (0) means the parent's pipe handle closed.
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            return;
        }

        AppLogger.Warn($"Provider {providerName} detected that its parent process is no longer available. Shutting down.");
        cts.Cancel();
    }

    private static TSettings LoadSettings<TSettings>() where TSettings : ProviderSettingsBase, new()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.yaml");
        return ProviderSettingsYaml.Load<TSettings>(settingsPath);
    }

    /// <summary>
    /// Settings Hub sends per-request (so edits apply without restarting the provider) are only
    /// trusted once they pass the same validation as settings.yaml at startup; otherwise this
    /// falls back to the already-validated settings loaded when the process started.
    /// </summary>
    private static TSettings ResolveEffectiveSettings<TSettings>(TSettings startupSettings, string? requestSettingsYaml, string providerName)
        where TSettings : ProviderSettingsBase, new()
    {
        var parsed = ProviderSettingsYaml.Parse<TSettings>(requestSettingsYaml);
        if (parsed is null)
        {
            return startupSettings;
        }

        var errors = SettingsValidationHelper.Validate(parsed);
        if (errors.Count > 0)
        {
            AppLogger.Warn($"Provider {providerName} received invalid settings on request: {string.Join("; ", errors)}. Falling back to settings.yaml.");
            return startupSettings;
        }

        return parsed;
    }
}
