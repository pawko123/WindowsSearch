using BaseProvider.Abstractions;
using BaseProvider.Models;
using BaseProvider.Transports;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace BaseProvider.Host;

public static class BaseProviderHost
{
    public static async Task RunAsync(string[] args, IResultFinder resultFinder)
    {
        var settings = LoadSettings();
        settings.Endpoint = args.FirstOrDefault() ?? settings.Endpoint;

        IProviderTransport transport = new ProviderTransportFactory().Create(settings);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await transport.RunAsync((request, token) => resultFinder.FindAsync(ApplyDefaults(settings, request), token), cts.Token);
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
