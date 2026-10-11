namespace WindowsSearch.Common.Models.Benchmark;

public sealed class ProviderBenchmarkMetrics
{
    public string ProviderName { get; set; } = string.Empty;
    public double RequestSerializationMs { get; set; }
    public int RequestPayloadBytes { get; set; }
    public double TransportRoundtripMs { get; set; }
    public double ProviderExecutionMs { get; set; }
    public double ResponseDeserializationMs { get; set; }
    public int ResponsePayloadBytes { get; set; }
    public double AggregationMs { get; set; }
    public int ItemCount { get; set; }
    public int CategoryCount { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
}
