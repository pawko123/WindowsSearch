using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;

namespace WindowsSearch.Benchmark.Core.Models;

public sealed class ConfigurationBenchmarkSummary
{
    public ProviderTransportKind Transport { get; set; }
    public SerializationKind Serialization { get; set; }
    public ProviderSearchMode SearchMode { get; set; }
    public ImageResolverKind ImageResolver { get; set; }
    public bool UseCache { get; set; }
    public string Query { get; set; } = string.Empty;
    public MetricStatistics TotalDurationStats { get; set; } = new();
    public MetricStatistics FilteredDurationStats { get; set; } = new();
    public int OutliersCount { get; set; }
    public MetricStatistics TimeToFirstResultStats { get; set; } = new();
    public int TriesCount { get; set; }
    public double ColdDurationMs { get; set; }
    public double ColdTimeToFirstResultMs { get; set; }
    public int ColdCacheMisses { get; set; }
    public MetricStatistics AggregationStats { get; set; } = new();
    public MetricStatistics ImageResolutionStats { get; set; } = new();
    public MetricStatistics SerializationCpuStats { get; set; } = new();
    public MetricStatistics CpuPercentageStats { get; set; } = new();
    public MetricStatistics HubWorkingSetMbStats { get; set; } = new();
    public MetricStatistics TotalWorkingSetMbStats { get; set; } = new();
    public int TotalWireBytes { get; set; }
    public int TotalCacheHits { get; set; }
    public int TotalCacheMisses { get; set; }
    public double CacheHitRatePercent { get; set; }
    public Dictionary<string, MetricStatistics> ProviderDurationStats { get; set; } = new();
    public Dictionary<string, MetricStatistics> ProviderWireBytesStats { get; set; } = new();
    public MetricStatistics AverageProviderDurationStats { get; set; } = new();
    public double AverageProviderDurationMs => AverageProviderDurationStats.Mean > 0
        ? Math.Round(AverageProviderDurationStats.Mean, 2)
        : (ProviderDurationStats.Count > 0 ? Math.Round(ProviderDurationStats.Values.Average(v => v.Mean), 2) : 0.0);
}
