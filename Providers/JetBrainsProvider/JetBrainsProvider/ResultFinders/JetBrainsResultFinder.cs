using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BaseProvider.Abstractions;
using BaseProvider.Models;
using CommonLogging;

namespace JetBrainsProvider.ResultFinders;

public sealed partial class JetBrainsResultFinder : IResultFinder
{
    private record AppConfig(string AppDataPrefix, string ScriptName, string ProgramFolderName, string ExeName);

    private static readonly List<AppConfig> AppConfigs =
    [
        new("PyCharm", "pycharm", "PyCharm Professional", "pycharm64.exe"),
        new("IntelliJIdea", "idea", "IntelliJ IDEA Ultimate", "idea64.exe"),
        new("Rider", "rider", "Rider", "rider64.exe"),
        new("CLion", "clion", "CLion", "clion64.exe"),
        new("WebStorm", "webstorm", "WebStorm", "webstorm64.exe"),
        new("RustRover", "rustrover", "RustRover", "rustrover64.exe"),
        new("AndroidStudio", "studio", "Android Studio", "studio64.exe"),
        new("DataGrip", "datagrip", "DataGrip", "datagrip64.exe"),
        new("DataSpell", "dataspell", "DataSpell", "dataspell64.exe"),
        new("GoLand", "goland", "GoLand", "goland64.exe"),
        new("PhpStorm", "PhpStorm", "PhpStorm", "phpstorm64.exe")
    ];

    private ProviderSearchResponse? _cachedResponse;
    private DateTime _cacheExpiration = DateTime.MinValue;

    public Task<ProviderSearchResponse> FindAsync(ProviderSearchRequest request, CancellationToken cancellationToken)
    {
        var cacheMinutesString = request.Settings.TryGetValue("cache_ttl_minutes", out var ttlStr) ? ttlStr : "5";
        if (!int.TryParse(cacheMinutesString, out var cacheMinutes))
        {
            cacheMinutes = 5;
        }

        if (_cachedResponse != null && DateTime.Now < _cacheExpiration)
        {
            AppLogger.Info($"Cache hit for query: '{request.Query}', returning {_cachedResponse.Categories.Sum(c => c.Items.Count)} total items.");
            return Task.FromResult(FilterResponse(_cachedResponse, request.Query, request.Limit));
        }

        AppLogger.Info($"Cache miss or expired for query: '{request.Query}'. Rebuilding response...");
        var newResponse = BuildResponse();
        _cachedResponse = newResponse;
        _cacheExpiration = DateTime.Now.AddMinutes(cacheMinutes);
        
        AppLogger.Info($"Rebuilt response with {newResponse.Categories.Count} categories. Cached for {cacheMinutes} minutes.");

        return Task.FromResult(FilterResponse(_cachedResponse, request.Query, request.Limit));
    }

    private ProviderSearchResponse FilterResponse(ProviderSearchResponse fullResponse, string query, int limit)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return fullResponse;
        }

        var response = new ProviderSearchResponse();
        foreach (var category in fullResponse.Categories)
        {
            var filteredItems = category.Items
                .Where(i => i.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(limit > 0 ? limit : int.MaxValue)
                .ToList();

            if (filteredItems.Count > 0)
            {
                response.Categories.Add(new ProviderResultCategory
                {
                    Name = category.Name,
                    IconPath = category.IconPath,
                    Items = filteredItems
                });
            }
        }

        return response;
    }

    private ProviderSearchResponse BuildResponse()
    {
        var response = new ProviderSearchResponse();
        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var toolboxScriptsPath = GetToolboxScriptsPath();
        var isScriptsInPath = toolboxScriptsPath != null && IsInPath(toolboxScriptsPath);

        foreach (var config in AppConfigs)
        {
            var appDataDir = FindNewestAppDataDir(config.AppDataPrefix);
            if (appDataDir == null) continue;

            var xmlPath = Path.Combine(appDataDir, "options", "recentProjects.xml");
            if (!File.Exists(xmlPath))
            {
                xmlPath = Path.Combine(appDataDir, "options", "recentSolutions.xml");
                if (!File.Exists(xmlPath)) continue;
            }

            var projects = ExtractProjectsFromXml(xmlPath, userHome);
            if (projects.Count == 0) continue;

            string actionPath;
            if (isScriptsInPath)
            {
                actionPath = ResolveScriptFileName(toolboxScriptsPath!, config.ScriptName);
            }
            else
            {
                actionPath = GetPhysicalExePath(config.ProgramFolderName, config.ExeName);
                if (!File.Exists(actionPath))
                {
                    AppLogger.Warn($"[JetBrainsResultFinder] Physical path '{actionPath}' not found for '{config.AppDataPrefix}'. Falling back to script '{config.ScriptName}.cmd'");
                    // Fallback to trying the script anyway if physical path is not found
                    actionPath = $"{config.ScriptName}.cmd";
                }
            }

            var category = new ProviderResultCategory
            {
                Name = config.AppDataPrefix,
                IconPath = $"Icons\\{config.AppDataPrefix}.png",
                Items = []
            };

            foreach (var project in projects)
            {
                var projectName = string.IsNullOrWhiteSpace(project.Name) ? project.Path : project.Name;

                category.Items.Add(new ProviderResultItem
                {
                    Title = projectName,
                    Subtitle = project.Path,
                    Score = 1.0,
                    ActionPath = actionPath,
                    ActionArgs = [project.Path],
                    IconPath = $"Icons\\{config.AppDataPrefix}.png"
                });
            }

            if (category.Items.Count > 0)
            {
                response.Categories.Add(category);
            }
        }

        return response;
    }

    private string? FindNewestAppDataDir(string prefix)
    {
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var jetbrainsDir = Path.Combine(roaming, "JetBrains");
        var googleDir = Path.Combine(roaming, "Google");

        var candidates = new List<string>();

        if (Directory.Exists(jetbrainsDir))
        {
            candidates.AddRange(Directory.GetDirectories(jetbrainsDir, $"{prefix}*"));
        }

        if (Directory.Exists(googleDir))
        {
            candidates.AddRange(Directory.GetDirectories(googleDir, $"{prefix}*"));
        }

        if (candidates.Count == 0) return null;

        return candidates
            .Select(c => new { Path = c, Name = Path.GetFileName(c) })
            .OrderByDescending(c => 
            {
                // Extract version, e.g., 2026.2 from PyCharm2026.2
                var versionStr = c.Name.Substring(prefix.Length);
                return Version.TryParse(versionStr, out var v) ? v : new Version(0, 0);
            })
            .FirstOrDefault()?.Path;
    }

    private List<(string Path, string Name)> ExtractProjectsFromXml(string xmlPath, string userHome)
    {
        var projects = new List<(string, string)>();
        try
        {
            var doc = XDocument.Load(xmlPath);
            var entries = doc.Descendants("entry");
            foreach (var entry in entries)
            {
                var key = entry.Attribute("key")?.Value;
                if (!string.IsNullOrEmpty(key))
                {
                    // JetBrains XML uses $USER_HOME$
                    var path = key.Replace("$USER_HOME$", userHome).Replace('/', '\\');
                    
                    if (Directory.Exists(path) || File.Exists(path))
                    {
                        string name = "";
                        var metaInfo = entry.Descendants("RecentProjectMetaInfo").FirstOrDefault();
                        var frameTitle = metaInfo?.Attribute("frameTitle")?.Value;
                        
                        if (!string.IsNullOrEmpty(frameTitle))
                        {
                            var dashIndex = frameTitle.IndexOf(" – ");
                            if (dashIndex < 0) dashIndex = frameTitle.IndexOf(" - ");
                            if (dashIndex < 0) dashIndex = frameTitle.IndexOf(" — ");
                            
                            if (dashIndex > 0)
                            {
                                name = frameTitle.Substring(0, dashIndex).Trim();
                            }
                            else
                            {
                                name = frameTitle.Trim();
                            }
                        }
                        
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            name = Path.GetFileName(path.TrimEnd('\\', '/'));
                        }

                        projects.Add((path, name));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[JetBrainsResultFinder] Failed to parse recent projects XML at '{xmlPath}'", ex);
        }
        return projects;
    }

    private string? GetToolboxScriptsPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var settingsPath = Path.Combine(localAppData, "JetBrains", "Toolbox", ".settings.json");
        
        if (!File.Exists(settingsPath)) return null;

        try
        {
            var json = File.ReadAllText(settingsPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("shell_scripts", out var scriptsEl) &&
                scriptsEl.TryGetProperty("location", out var locationEl))
            {
                return locationEl.GetString();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error($"[JetBrainsResultFinder] Failed to read or parse JetBrains Toolbox settings at '{settingsPath}'", ex);
        }
        
        return null;
    }

    private bool IsInPath(string dirPath)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var paths = pathEnv.Split(Path.PathSeparator);
        return paths.Any(p => string.Equals(p.TrimEnd('\\', '/'), dirPath.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase));
    }

    private string GetPhysicalExePath(string programFolderName, string exeName)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Programs", programFolderName, "bin", exeName);
    }

    private string ResolveScriptFileName(string toolboxScriptsPath, string scriptName)
    {
        var match = Directory.Exists(toolboxScriptsPath)
            ? Directory.GetFiles(toolboxScriptsPath)
                .Select(Path.GetFileName)
                .FirstOrDefault(f =>
                {
                    var m = ScriptFileNameRegex().Match(f ?? "");
                    return m.Success && string.Equals(m.Groups["name"].Value, scriptName, StringComparison.OrdinalIgnoreCase);
                })
            : null;

        return match ?? $"{scriptName}.cmd";
    }

    [GeneratedRegex(@"^(?<name>[A-Za-z]+)[0-9]*\.cmd$", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptFileNameRegex();
}
