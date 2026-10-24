using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WindowsSearch.Benchmark.Core.Models;

namespace WindowsSearch.Benchmark.Reporting.Exporters;

public static class JsonMetricsExporter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Export<T>(string filePath, T data)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(data, Options);
        File.WriteAllText(filePath, json);
    }
}
