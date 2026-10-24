namespace WindowsSearch.Common.Models.Benchmark;

public sealed class BenchmarkControlResponse
{
    public string Action { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    public int ProcessId { get; set; }
    public SearchBenchmarkReport? Report { get; set; }
    public string? ErrorMessage { get; set; }
}
