using BaseProvider.Abstractions;
using BaseProvider.Models;
using BaseProvider.Transports;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using CommonLogging;

namespace BaseProvider.Host;

public static class BaseProviderHost
{
    public static async Task RunAsync(string[] args, IResultFinder resultFinder)
    {
        var providerName = new DirectoryInfo(AppContext.BaseDirectory).Name;
        if (string.IsNullOrWhiteSpace(providerName) || providerName.Equals("bin", StringComparison.OrdinalIgnoreCase))
        {
            providerName = "UnknownProvider";
        }
        AppLogger.Initialize(providerName);
        AppLogger.Info($"Provider {providerName} starting up...");

        try
        {
            var settings = LoadSettings();
            if (args.Length > 0 && Enum.TryParse<ProviderTransportKind>(args[0], true, out var tk))
            {
                settings.Transport = tk;
            }
            if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
            {
                settings.Endpoint = args[1];
            }
            if (args.Length > 2 && !string.IsNullOrWhiteSpace(args[2]))
            {
                settings.Serialization = args[2];
            }

            AppLogger.Info($"Provider {providerName} initialized. Transport: {settings.Transport}, Endpoint: {settings.Endpoint}, Serialization: {settings.Serialization}");

            IProviderTransport transport = new ProviderTransportFactory().Create(settings);

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            await transport.RunAsync((request, token) => resultFinder.FindAsync(ApplyDefaults(settings, request), token), cts.Token);
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Provider {providerName} encountered a fatal error during startup:", ex);
            throw;
        }
    }

    private static ProviderSettings LoadSettings()
    {
        var settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.yaml");
        if (!File.Exists(settingsPath))
        {
            return new ProviderSettings();
        }

        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        return deserializer.Deserialize<ProviderSettings>(File.ReadAllText(settingsPath)) ?? new ProviderSettings();
    }

    private static ProviderSearchRequest ApplyDefaults(ProviderSettings settings, ProviderSearchRequest request)
    {
        request.Limit = request.Limit <= 0 ? 10 : request.Limit;

        foreach (var pair in settings.Settings)
        {
            if (!request.Settings.ContainsKey(pair.Key))
            {
                request.Settings[pair.Key] = pair.Value;
            }
        }

        return request;
    }
}
