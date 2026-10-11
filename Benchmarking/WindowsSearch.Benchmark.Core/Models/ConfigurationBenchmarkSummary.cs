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
    public MetricStatistics AggregationStats { get; set; } = new();
    public MetricStatistics ImageResolutionStats { get; set; } = new();
    public MetricStatistics CpuPercentageStats { get; set; } = new();
    public MetricStatistics HubWorkingSetMbStats { get; set; } = new();
    public MetricStatistics TotalWorkingSetMbStats { get; set; } = new();
    public int TotalWireBytes { get; set; }
    public Dictionary<string, MetricStatistics> ProviderDurationStats { get; set; } = new();
    public Dictionary<string, MetricStatistics> ProviderWireBytesStats { get; set; } = new();
}
