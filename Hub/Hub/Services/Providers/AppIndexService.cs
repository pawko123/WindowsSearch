using Hub.Models.App;
using System.Diagnostics;
using System.IO;

namespace Hub.Services.Providers;

public sealed class AppIndexService
{
    public IReadOnlyList<AppEntry> GetInstalledApps()
    {
        var apps = new List<AppEntry>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddStartMenuApps(apps, seenPaths, Environment.GetFolderPath(Environment.SpecialFolder.StartMenu));
        AddStartMenuApps(apps, seenPaths, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu));
        AddWindowsApps(apps, seenPaths, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps"));

        return apps
            .Where(app => File.Exists(app.ExecutablePath))
            .OrderBy(app => app.Name)
            .ToList();
    }

    public static void Launch(AppEntry app)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = app.ExecutablePath,
            Arguments = app.Arguments ?? string.Empty,
            UseShellExecute = true
        };

        Process.Start(processStartInfo);
    }

    private static void AddStartMenuApps(List<AppEntry> apps, HashSet<string> seenPaths, string startMenuPath)
    {
        if (string.IsNullOrWhiteSpace(startMenuPath) || !Directory.Exists(startMenuPath))
        {
            return;
        }

        foreach (var filePath in SafeEnumerateFiles(startMenuPath, "*.lnk"))
        {
            try
            {
                var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
                var shortcut = shell!.GetType().InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { filePath });
                var targetPath = shortcut!.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, shortcut, null) as string;

                if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath) || !seenPaths.Add(targetPath))
                {
                    continue;
                }

                apps.Add(new AppEntry
                {
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    Subtitle = "Start Menu",
                    ExecutablePath = targetPath,
                    Source = "Start Menu"
                });
            }
            catch
            {
                continue;
            }
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string root, string searchPattern)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            string[] files = Array.Empty<string>();
            try
            {
                files = Directory.GetFiles(dir, searchPattern);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var f in files)
                yield return f;

            string[] subdirs = Array.Empty<string>();
            try
            {
                subdirs = Directory.GetDirectories(dir);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var sd in subdirs)
            {
                stack.Push(sd);
            }
        }
    }

    private static void AddWindowsApps(List<AppEntry> apps, HashSet<string> seenPaths, string windowsAppsPath)
    {
        if (string.IsNullOrWhiteSpace(windowsAppsPath) || !Directory.Exists(windowsAppsPath))
        {
            return;
        }

        foreach (var filePath in Directory.GetFiles(windowsAppsPath, "*.exe"))
        {
            try
            {
                if (!seenPaths.Add(filePath))
                {
                    continue;
                }

                apps.Add(new AppEntry
                {
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    Subtitle = "WindowsApps",
                    ExecutablePath = filePath,
                    Source = "WindowsApps"
                });
            }
            catch
            {
                continue;
            }
        }
    }
}
