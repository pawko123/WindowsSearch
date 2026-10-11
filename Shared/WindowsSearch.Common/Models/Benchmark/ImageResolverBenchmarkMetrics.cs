namespace WindowsSearch.Common.Models.Benchmark;

public sealed class ImageResolverBenchmarkMetrics
{
    public string ResolverKind { get; set; } = string.Empty;
    public bool UseCache { get; set; }
    public int ImagesRequested { get; set; }
    public int CacheHits { get; set; }
    public int CacheMisses { get; set; }
    public double TotalResolveTimeMs { get; set; }
}
