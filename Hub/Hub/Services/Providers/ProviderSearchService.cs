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
using System.IO.Abstractions;
using WindowsSearch.Common.Logging;
using Hub.Services.Providers.Strategies;
using System.Runtime.CompilerServices;

namespace Hub.Services.Providers;

public sealed class ProviderSearchService : IDisposable
{
    private readonly IFileSystem _fileSystem;

    public ProviderSearchService(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
        providerSettingsService = new ProviderSettingsService(_fileSystem);
    }

    private readonly ProviderSettingsService providerSettingsService;
    private readonly ConcurrentDictionary<string, Process> _runningProviders = new(StringComparer.OrdinalIgnoreCase);
    
    private ProviderTransportKind? _lastTransportKind;
    private SerializationKind? _lastSerialization;

    private string GetProviderIconPath(string providerDirectory)
    {
        string iconPath = _fileSystem.Path.Combine(providerDirectory, "Icons", "icon.png");
        if (!_fileSystem.File.Exists(iconPath))
        {
            iconPath = _fileSystem.Path.Combine(AppContext.BaseDirectory, "Assets", "fallback-provider-icon.png");
        }
        return iconPath;
    }

    public IReadOnlyList<ProviderSearchOutcome> GetActiveProviderOutcomes()
    {
        var providers = providerSettingsService.LoadAll().Where(p => p.Settings.IsEnabled).ToList();
        var outcomes = new List<ProviderSearchOutcome>();
        foreach (var provider in providers)
        {
            var providerDirectory = _fileSystem.Path.GetDirectoryName(provider.SettingsPath) ?? AppContext.BaseDirectory;
            outcomes.Add(new ProviderSearchOutcome
            {
                ProviderName = provider.ProviderName,
                IconPath = GetProviderIconPath(providerDirectory)
            });
        }
        return outcomes;
    }

    public async IAsyncEnumerable<ProviderSearchOutcome> SearchAsync(string query, int limit, AppSettings currentHubSettings, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_lastTransportKind != currentHubSettings.Provider.ProviderTransportKind || _lastSerialization != currentHubSettings.Provider.ProviderSerialization)
        {
            if (_lastTransportKind != null)
            {
                AppLogger.Info($"[ProviderSearchService] Transport/Serialization settings changed. Restarting all providers.");
                Dispose();
            }
            _lastTransportKind = currentHubSettings.Provider.ProviderTransportKind;
            _lastSerialization = currentHubSettings.Provider.ProviderSerialization;
        }

        var providers = providerSettingsService.LoadAll();
        var activeProviders = providers.Where(p => p.Settings.IsEnabled).ToList();
        
        var activeProviderNames = activeProviders.Select(p => p.ProviderName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var key in _runningProviders.Keys.ToList())
        {
            if (!activeProviderNames.Contains(key))
            {
                if (_runningProviders.TryRemove(key, out var oldProcess))
                {
                    KillProcessSafe(oldProcess);
                }
            }
        }

        var providerContexts = activeProviders.ToDictionary(p => p.ProviderName, p => p);

        Func<string, CancellationToken, Task<ProviderSearchOutcome>> singleSearchFunc = (name, token) => ExecuteSingleProviderSearchAsync(name, providerContexts, query, limit, currentHubSettings, token);

        IProviderSearchStrategy strategy = currentHubSettings.Provider.SearchMode switch
        {
            ProviderSearchMode.ConcurrentBlocking => new ConcurrentBlockingSearchStrategy(),
            ProviderSearchMode.ConcurrentStream => new ConcurrentStreamSearchStrategy(),
            _ => new SequentialSearchStrategy()
        };

        var providerNames = activeProviders.Select(p => p.ProviderName).ToList();
        
        await foreach (var outcome in strategy.SearchAsync(providerNames, singleSearchFunc, cancellationToken))
        {
            yield return outcome;
        }
    }


    private async Task<ProviderSearchOutcome> ExecuteSingleProviderSearchAsync(
        string providerName, 
        Dictionary<string, ProviderSettingsModel> providerContexts,
        string query, 
        int limit, 
        AppSettings currentHubSettings, 
        CancellationToken token)
    {
        var provider = providerContexts[providerName];
        var providerDirectory = _fileSystem.Path.GetDirectoryName(provider.SettingsPath) ?? AppContext.BaseDirectory;
        var executablePath = _fileSystem.Path.Combine(providerDirectory, $"{provider.ProviderName}.exe");
        
        var outcome = new ProviderSearchOutcome
        {
            ProviderName = provider.ProviderName,
            IconPath = GetProviderIconPath(providerDirectory)
        };

        if (!_fileSystem.File.Exists(executablePath))
        {
            return outcome;
        }

        var endpoint = currentHubSettings.Provider.ProviderTransportKind switch
        {
            ProviderTransportKind.Http => provider.Settings.Endpoints.Http,
            ProviderTransportKind.Grpc => provider.Settings.Endpoints.Grpc,
            _ => provider.Settings.Endpoints.NamedPipe
        };

        if (!_runningProviders.TryGetValue(provider.ProviderName, out var process) || process.HasExited)
        {
            if (process != null)
            {
                process.Dispose();
            }
            process = StartProviderProcess(executablePath, providerDirectory, currentHubSettings.Provider.ProviderTransportKind, endpoint, currentHubSettings.Provider.ProviderSerialization);
            _runningProviders[provider.ProviderName] = process;
        }

        try
        {
            var request = new ProviderSearchRequest
            {
                Query = query,
                Limit = limit,
                SettingsYaml = ProviderSettingsYaml.Serialize(provider.Settings),
                TimeoutMs = currentHubSettings.Provider.ProviderTimeoutSeconds * 1000
            };

            var serializer = MessageSerializerFactory.Create(currentHubSettings.Provider.ProviderSerialization);
            var client = CreateClient(currentHubSettings.Provider.ProviderTransportKind, endpoint, currentHubSettings.Provider.ProviderTimeoutSeconds, serializer);
            
            var response = await client.SearchAsync(request, token);
            
            var results = new List<ProviderCategoryResultUi>();
            foreach (var category in response.Categories)
            {
                var section = new ProviderCategoryResultUi
                {
                    Name = category.Name,
                    Weight = provider.Settings.Weight,
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
            outcome.Categories = results;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            if (_runningProviders.TryRemove(provider.ProviderName, out var deadProcess))
            {
                KillProcessSafe(deadProcess);
            }
        }

        return outcome;
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

    private string? ResolveProviderPath(string providerDirectory, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        return _fileSystem.Path.IsPathRooted(relativePath)
            ? relativePath
            : _fileSystem.Path.GetFullPath(_fileSystem.Path.Combine(providerDirectory, relativePath));
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