using System.IO;
using WindowsSearch.Benchmark.Harness.Config;

namespace WindowsSearch.Benchmark.Tests;

public class OfflineSettingsManagerTests
{
    [Fact]
    public void ApplyOfflineSettings_ModifiesYamlAndRestoresBackup()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"bench_test_{Guid.NewGuid():N}");
        var chromeDir = Path.Combine(tempDir, "Providers", "ChromeProvider");
        var hubDir = Path.Combine(tempDir, "Hub");
        Directory.CreateDirectory(chromeDir);
        Directory.CreateDirectory(hubDir);

        var settingsPath = Path.Combine(chromeDir, "settings.yaml");
        File.WriteAllText(settingsPath, "is_enabled: true\nenable_search: true\n");

        try
        {
            using (var manager = new OfflineSettingsManager(tempDir))
            {
                manager.ApplyOfflineSettings();

                var modified = File.ReadAllText(settingsPath);
                Assert.Contains("enable_search: false", modified);
                Assert.True(File.Exists(settingsPath + ".bak"));
            }

            // After dispose, it should be restored
            var restored = File.ReadAllText(settingsPath);
            Assert.Contains("enable_search: true", restored);
            Assert.False(File.Exists(settingsPath + ".bak"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
