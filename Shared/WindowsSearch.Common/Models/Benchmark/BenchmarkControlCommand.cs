namespace WindowsSearch.Common.Models.Benchmark;

public sealed class BenchmarkControlCommand
{
    public string Action { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public int Limit { get; set; } = 25;
    public bool BypassDebounce { get; set; } = true;
    public string? TransportKind { get; set; }
    public string? SerializationKind { get; set; }
    public string? SearchMode { get; set; }
    public string? ImageResolverKind { get; set; }
    public bool? UseCache { get; set; }
    public int? TimeoutSeconds { get; set; }
    public int? DebounceDelayMs { get; set; }
}
