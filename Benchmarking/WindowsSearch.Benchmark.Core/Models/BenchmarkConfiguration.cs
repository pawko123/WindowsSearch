using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;

namespace WindowsSearch.Benchmark.Core.Models;

public sealed class BenchmarkConfiguration
{
    public int WarmupRuns { get; set; } = 3;
    public int MeasurementRuns { get; set; } = 15;
    public List<string> Queries { get; set; } = ["git", "code", "fire", "test", "demo"];
    public int Limit { get; set; } = 5;
    public bool BypassDebounce { get; set; } = true;

    public List<ProviderTransportKind> Transports { get; set; } = Enum.GetValues<ProviderTransportKind>().ToList();
    public List<SerializationKind> Serializations { get; set; } = Enum.GetValues<SerializationKind>().ToList();
    public List<ProviderSearchMode> SearchModes { get; set; } = Enum.GetValues<ProviderSearchMode>().ToList();
    public List<ImageResolverKind> ImageResolvers { get; set; } = Enum.GetValues<ImageResolverKind>().ToList();
    public List<bool> UseCacheOptions { get; set; } = [true, false];

    public string? HubExecutablePath { get; set; }
    public string ControlPipeName { get; set; } = "hub_bench_control";
    public bool DisableWebSearchOnBrowsers { get; set; } = true;
    public string OutputDir { get; set; } = "benchmark-results";
}
