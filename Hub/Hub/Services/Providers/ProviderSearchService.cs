using System.IO;
using System.Diagnostics;
using Hub.Models.App;
using Hub.Models.Providers;
using Hub.Models.Results;
using Hub.Models.Settings;
using Hub.Services.Settings;
using Hub.Services.Providers.Transports;

namespace Hub.Services.Providers;

public sealed class ProviderSearchService
{
    private readonly ProviderSettingsService providerSettingsService = new();

    public async Task<IReadOnlyList<ProviderCategoryResultUi>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var results = new List<ProviderCategoryResultUi>();
        var providers = providerSettingsService.LoadAll();

        foreach (var provider in providers)
        {
            var providerDirectory = Path.GetDirectoryName(provider.SettingsPath) ?? AppContext.BaseDirectory;
            var executablePath = Path.Combine(providerDirectory, $"{provider.ProviderName}.exe");
            if (!File.Exists(executablePath))
            {
                continue;
            }

            var process = StartProviderProcess(executablePath, providerDirectory);
            try
            {
                var request = new ProviderSearchRequest
                {
                    Query = query,
                    Limit = limit,
                    Settings = new Dictionary<string, string>(provider.Document.Settings, StringComparer.OrdinalIgnoreCase)
                };

                var client = CreateClient(provider.Document);
                var response = await client.SearchAsync(request, cancellationToken);

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
            catch
            {
                // Ignore one provider failure and keep showing other results.
            }
            finally
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch { }

                process.Dispose();
            }
        }

        return results;
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