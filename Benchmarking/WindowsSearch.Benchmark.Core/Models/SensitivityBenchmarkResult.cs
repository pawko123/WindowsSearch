namespace WindowsSearch.Benchmark.Core.Models;

public sealed class SensitivityBenchmarkResult
{
    public List<ConfigurationBenchmarkSummary> TransportSummaries { get; set; } = [];
    public List<ConfigurationBenchmarkSummary> SerializationSummaries { get; set; } = [];
    public List<ConfigurationBenchmarkSummary> SearchModeSummaries { get; set; } = [];
    public List<ConfigurationBenchmarkSummary> ImageResolverSummaries { get; set; } = [];


    // Stage 1: Per-Provider IPC Transport Comparison (Amdahl's Law Isolation)
    public List<ProviderTransportGainSummary> ProviderTransportGains { get; set; } = [];
    public double OverallProviderGainPercentVsHttp { get; set; }
    public double OverallProviderGainPercentVsGrpc { get; set; }
    // Grand Final Single Best vs Worst Comparison (across diverse queries):
    public ConfigurationBenchmarkSummary BestSummary { get; set; } = new();
    public ConfigurationBenchmarkSummary WorstSummary { get; set; } = new();
    public double SpeedupFactor { get; set; }
    public double TimeToFirstResultSpeedup { get; set; }
    public double AverageProviderSpeedup { get; set; }
    public Dictionary<string, double> PerProviderSpeedup { get; set; } = new();
    public double WireReductionPercent { get; set; }
    public double CacheAccelerationRatio { get; set; }
    public List<SingleRunBenchmarkResult> AllRuns { get; set; } = [];
}
