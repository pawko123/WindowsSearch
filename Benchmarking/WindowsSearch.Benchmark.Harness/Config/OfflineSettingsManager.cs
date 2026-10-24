using System.IO;
using WindowsSearch.Common.Logging;

namespace WindowsSearch.Benchmark.Harness.Config;

public sealed class OfflineSettingsManager : IDisposable
{
    private readonly string rootDirectory;
    private readonly List<string> modifiedFiles = [];
    private bool disposed;

    public OfflineSettingsManager(string? rootDirectory = null)
    {
        this.rootDirectory = ResolveRootDirectory(rootDirectory);
    }

    public void ApplyOfflineSettings()
    {
        var targetProviders = new[] { "ChromeProvider", "FirefoxProvider" };
        foreach (var provider in targetProviders)
        {
            var settingsPath = Path.Combine(rootDirectory, "Providers", provider, "settings.yaml");
            if (!File.Exists(settingsPath))
            {
                continue;
            }

            try
            {
                var backupPath = settingsPath + ".bak";
                if (!File.Exists(backupPath))
                {
                    File.Copy(settingsPath, backupPath, overwrite: true);
                }

                var content = File.ReadAllText(settingsPath);
                string updated;
                if (content.Contains("enable_search:", StringComparison.OrdinalIgnoreCase))
                {
                    var lines = File.ReadAllLines(settingsPath);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].TrimStart().StartsWith("enable_search:", StringComparison.OrdinalIgnoreCase))
                        {
                            lines[i] = "enable_search: false";
                        }
                    }
                    updated = string.Join(Environment.NewLine, lines);
                }
                else
                {
                    updated = content.TrimEnd() + Environment.NewLine + "enable_search: false" + Environment.NewLine;
                }

                File.WriteAllText(settingsPath, updated);
                modifiedFiles.Add(settingsPath);
                AppLogger.Info($"[OfflineSettingsManager] Disabled web search in: {settingsPath}");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[OfflineSettingsManager] Failed to apply offline settings to {settingsPath}:", ex);
            }
        }
    }

    public void RestoreOriginalSettings()
    {
        foreach (var settingsPath in modifiedFiles)
        {
            var backupPath = settingsPath + ".bak";
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, settingsPath, overwrite: true);
                    File.Delete(backupPath);
                    AppLogger.Info($"[OfflineSettingsManager] Restored original settings: {settingsPath}");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[OfflineSettingsManager] Failed to restore backup {backupPath}:", ex);
            }
        }

        modifiedFiles.Clear();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        RestoreOriginalSettings();
        disposed = true;
    }

    private static string ResolveRootDirectory(string? preferred)
    {
        if (!string.IsNullOrWhiteSpace(preferred) && Directory.Exists(preferred))
        {
            return Path.GetFullPath(preferred);
        }

        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (Directory.Exists(Path.Combine(dir, "Providers")) && Directory.Exists(Path.Combine(dir, "Hub")))
            {
                return dir;
            }

            var parent = Directory.GetParent(dir);
            if (parent is null)
            {
                break;
            }
            dir = parent.FullName;
        }

        return Directory.GetCurrentDirectory();
    }
}
