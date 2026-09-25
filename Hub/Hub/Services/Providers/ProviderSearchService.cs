using System.IO;
using System.Diagnostics;
using System.Collections.Concurrent;
using Hub.Models.App;
using Hub.Models.Results;
using Hub.Models.Settings;
using Hub.Services.Settings;
using Hub.Services.Providers.Transports;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;
using WindowsSearch.Common.Logging;

namespace Hub.Services.Providers;

public sealed class ProviderSearchService : IDisposable
{
    private readonly ProviderSettingsService providerSettingsService = new();
    private readonly ConcurrentDictionary<string, Process> _runningProviders = new(StringComparer.OrdinalIgnoreCase);
    
    private ProviderTransportKind? _lastTransportKind;
    private SerializationKind? _lastSerialization;

    public async Task<IReadOnlyList<ProviderCategoryResultUi>> SearchAsync(string query, int limit, AppSettings currentHubSettings, CancellationToken cancellationToken)
    {
        // Detect if global transport or serialization settings changed
        if (_lastTransportKind != currentHubSettings.ProviderTransportKind || _lastSerialization != currentHubSettings.ProviderSerialization)
        {
            if (_lastTransportKind != null)
            {
                AppLogger.Info($"[ProviderSearchService] Transport/Serialization settings changed (Transport: {_lastTransportKind} -> {currentHubSettings.ProviderTransportKind}, Serialization: {_lastSerialization} -> {currentHubSettings.ProviderSerialization}). Restarting all providers.");
                Dispose();
            }
            _lastTransportKind = currentHubSettings.ProviderTransportKind;
            _lastSerialization = currentHubSettings.ProviderSerialization;
        }

        var results = new List<ProviderCategoryResultUi>();
        var providers = providerSettingsService.LoadAll();
        
        AppLogger.Info($"[ProviderSearchService] Starting search across {providers.Count} configured providers. Query: '{query}', Limit: {limit}");

        var activeProviderNames = providers
            .Where(p => p.Settings.IsEnabled)
            .Select(p => p.ProviderName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Clean up providers that were removed from configuration
        foreach (var key in _runningProviders.Keys.ToList())
        {
            if (!activeProviderNames.Contains(key))
            {
                if (_runningProviders.TryRemove(key, out var oldProcess))
                {
                    AppLogger.Info($"[ProviderSearchService] Provider '{key}' was disabled or removed from config. Killing process.");
                    KillProcessSafe(oldProcess);
                }
            }
        }

        foreach (var provider in providers)
        {
            if (!provider.Settings.IsEnabled)
            {
                continue;
            }

            var providerDirectory = Path.GetDirectoryName(provider.SettingsPath) ?? AppContext.BaseDirectory;
            var executablePath = Path.Combine(providerDirectory, $"{provider.ProviderName}.exe");
            if (!File.Exists(executablePath))
            {
                AppLogger.Warn($"[ProviderSearchService] Executable not found for provider '{provider.ProviderName}' at '{executablePath}'");
                continue;
            }

            var endpoint = currentHubSettings.ProviderTransportKind switch
            {
                ProviderTransportKind.Http => provider.Settings.EndpointHttp,
                ProviderTransportKind.Grpc => provider.Settings.EndpointGrpc,
                _ => provider.Settings.EndpointNamedPipe
            };

            if (!_runningProviders.TryGetValue(provider.ProviderName, out var process) || process.HasExited)
            {
                if (process != null)
                {
                    AppLogger.Warn($"[ProviderSearchService] Provider '{provider.ProviderName}' exited unexpectedly. Restarting... Transport: {currentHubSettings.ProviderTransportKind}, Endpoint: {endpoint}");
                    process.Dispose();
                }
                else
                {
                    AppLogger.Info($"[ProviderSearchService] Spawning provider '{provider.ProviderName}' for the first time... Transport: {currentHubSettings.ProviderTransportKind}, Endpoint: {endpoint}");
                }
                process = StartProviderProcess(executablePath, providerDirectory, currentHubSettings.ProviderTransportKind, endpoint, currentHubSettings.ProviderSerialization);
                _runningProviders[provider.ProviderName] = process;
            }

            try
            {
                var request = new ProviderSearchRequest
                {
                    Query = query,
                    Limit = limit,
                    SettingsYaml = ProviderSettingsYaml.Serialize(provider.Settings)
                };

                var serializer = MessageSerializerFactory.Create(currentHubSettings.ProviderSerialization);

                if (AppLogger.LogLevel <= LogLevel.Debug)
                {
                    AppLogger.Debug($"Sending request: {serializer.FormatForLog(request)}");
                }

                AppLogger.Info($"[ProviderSearchService] Sending query '{query}' to provider '{provider.ProviderName}' (Transport: {currentHubSettings.ProviderTransportKind}, Endpoint: {endpoint})...");
                var client = CreateClient(currentHubSettings.ProviderTransportKind, endpoint, currentHubSettings.ProviderTimeoutSeconds, serializer);
                var response = await client.SearchAsync(request, cancellationToken);
                AppLogger.Info($"[ProviderSearchService] Received response from '{provider.ProviderName}' with {response.Categories.Count} categories.");

                if (AppLogger.LogLevel <= LogLevel.Debug)
                {
                    AppLogger.Debug($"Received response: {serializer.FormatForLog(response)}");
                }

                foreach (var category in response.Categories)
                {
                    var section = new ProviderCategoryResultUi
                    {
                        Name = category.Name,
                        IconPath = ResolveProviderPath(providerDirectory, category.IconPath),
                    };

                    foreach (var item in category.Items)
                    {
                        section.Items.Add(new AppEntry
                        {
                            Name = item.Title,
                            Subtitle = item.Subtitle,
                            ExecutablePath = item.ActionPath,
                            Arguments = item.ActionArgs.Count > 0 ? string.Join(' ', item.ActionArgs.Select(a => a.Contains(' ') && !a.StartsWith('"') ? $"\"{a}\"" : a)) : null,
                            Source = provider.ProviderName,
                            IconPath = ResolveProviderPath(providerDirectory, item.IconPath),
                        });
                    }

                    if (section.Items.Count > 0)
                    {
                        results.Add(section);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw; // Rethrow to let caller handle cancellation
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[ProviderSearchService] Error communicating with provider '{provider.ProviderName}'. Removing from pool so it can restart.", ex);
                if (_runningProviders.TryRemove(provider.ProviderName, out var deadProcess))
                {
                    KillProcessSafe(deadProcess);
                }
            }
        }

        AppLogger.Info($"[ProviderSearchService] Search complete. Returning {results.Count} result categories overall.");
        return results;
    }

    private static void KillProcessSafe(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch { }
        finally
        {
            process.Dispose();
        }
    }

    public void Dispose()
    {
        foreach (var process in _runningProviders.Values)
        {
            KillProcessSafe(process);
        }
        _runningProviders.Clear();
    }

    private static string? ResolveProviderPath(string providerDirectory, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        return Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.GetFullPath(Path.Combine(providerDirectory, relativePath));
    }

    private static Process StartProviderProcess(string executablePath, string workingDirectory, ProviderTransportKind transport, string endpoint, SerializationKind serialization)
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = $"{transport} \"{endpoint}\" {serialization}",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true,
        }) ?? throw new InvalidOperationException($"Failed to start provider: {executablePath}");
    }

    private static IProviderClient CreateClient(ProviderTransportKind transportKind, string endpoint, int timeoutSeconds, IMessageSerializer serializer)
    {
        return ProviderClientFactory.Create(transportKind, endpoint, timeoutSeconds, serializer);
    }
}