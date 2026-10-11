namespace WindowsSearch.Benchmark.Core.Models;

public sealed class SystemResourceSample
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double CpuPercentage { get; set; }
    public double HubWorkingSetMb { get; set; }
    public double HubPrivateMemoryMb { get; set; }
    public double TotalWorkingSetMb { get; set; }
    public double TotalPrivateMemoryMb { get; set; }
    public int ProviderProcessCount { get; set; }
}
