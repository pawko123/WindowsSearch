using System.IO;
using System.Diagnostics;
using System.Collections.Concurrent;
using Hub.Models.App;
using Hub.Models.Providers;
using Hub.Models.Results;
using Hub.Models.Settings;
using Hub.Services.Settings;
using Hub.Services.Providers.Transports;
using CommonLogging;

namespace Hub.Services.Providers;

public sealed class ProviderSearchService : IDisposable
{
    private readonly ProviderSettingsService providerSettingsService = new();
    private readonly ConcurrentDictionary<string, Process> _runningProviders = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<ProviderCategoryResultUi>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var results = new List<ProviderCategoryResultUi>();
        var providers = providerSettingsService.LoadAll();
        
        AppLogger.Info($"[ProviderSearchService] Starting search across {providers.Count} configured providers. Query: '{query}', Limit: {limit}");

        var activeProviderNames = providers.Select(p => p.ProviderName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Clean up providers that were removed from configuration
        foreach (var key in _runningProviders.Keys.ToList())
        {
            if (!activeProviderNames.Contains(key))
            {
                if (_runningProviders.TryRemove(key, out var oldProcess))
                {
                    AppLogger.Info($"[ProviderSearchService] Provider '{key}' was removed from config. Killing process.");
                    KillProcessSafe(oldProcess);
                }
            }
        }

        foreach (var provider in providers)
        {
            var providerDirectory = Path.GetDirectoryName(provider.SettingsPath) ?? AppContext.BaseDirectory;
            var executablePath = Path.Combine(providerDirectory, $"{provider.ProviderName}.exe");
            if (!File.Exists(executablePath))
            {
                AppLogger.Warn($"[ProviderSearchService] Executable not found for provider '{provider.ProviderName}' at '{executablePath}'");
                continue;
            }

            if (!_runningProviders.TryGetValue(provider.ProviderName, out var process) || process.HasExited)
            {
                if (process != null)
                {
                    AppLogger.Warn($"[ProviderSearchService] Provider '{provider.ProviderName}' exited unexpectedly. Restarting...");
                    process.Dispose();
                }
                else
                {
                    AppLogger.Info($"[ProviderSearchService] Spawning provider '{provider.ProviderName}' for the first time...");
                }
                process = StartProviderProcess(executablePath, providerDirectory);
                _runningProviders[provider.ProviderName] = process;
            }

            try
            {
                var request = new ProviderSearchRequest
                {
                    Query = query,
                    Limit = limit,
                    Settings = new Dictionary<string, string>(provider.Document.Settings, StringComparer.OrdinalIgnoreCase)
                };

                AppLogger.Info($"[ProviderSearchService] Sending query '{query}' to provider '{provider.ProviderName}'...");
                var client = CreateClient(provider.Document);
                var response = await client.SearchAsync(request, cancellationToken);
                AppLogger.Info($"[ProviderSearchService] Received response from '{provider.ProviderName}' with {response.Categories.Count} categories.");

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
                            Arguments = item.ActionArgs.Count > 0 ? string.Join(' ', item.ActionArgs) : null,
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

    private static Process StartProviderProcess(string executablePath, string workingDirectory)
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        }) ?? throw new InvalidOperationException($"Failed to start provider: {executablePath}");
    }

    private static IProviderClient CreateClient(ProviderFileSettingsDocument document)
    {
        var settings = new AppSettings
        {
            ProviderTransportKind = Enum.TryParse<ProviderTransportKind>(document.Transport, true, out var transportKind)
                ? transportKind
                : ProviderTransportKind.NamedPipe,
            ProviderEndpoint = document.Endpoint,
            ProviderTimeoutSeconds = document.TimeoutSeconds,
        };

        return ProviderClientFactory.Create(settings);
    }
}