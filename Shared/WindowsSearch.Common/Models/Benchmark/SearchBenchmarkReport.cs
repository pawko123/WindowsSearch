namespace WindowsSearch.Common.Models.Benchmark;

public sealed class SearchBenchmarkReport
{
    public string RunId { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public double TotalWallClockMs { get; set; }
    public double TimeToFirstResultMs { get; set; }
    public string TransportKind { get; set; } = string.Empty;
    public string SerializationKind { get; set; } = string.Empty;
    public string SearchMode { get; set; } = string.Empty;
    public int ProviderTimeoutSeconds { get; set; }
    public double TotalAggregationMs { get; set; }
    public double TotalImageResolutionMs { get; set; }
    public List<ProviderBenchmarkMetrics> ProviderMetrics { get; set; } = [];
    public ImageResolverBenchmarkMetrics ImageResolverMetrics { get; set; } = new();
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
}
