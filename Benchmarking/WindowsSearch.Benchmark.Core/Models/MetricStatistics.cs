namespace WindowsSearch.Benchmark.Core.Models;

public sealed class MetricStatistics
{
    public int Count { get; set; }
    public double Mean { get; set; }
    public double Median { get; set; }
    public double P90 { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double StandardDeviation { get; set; }
    public double MarginOfError95 { get; set; }
}
