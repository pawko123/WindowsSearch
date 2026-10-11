namespace WindowsSearch.Benchmark.Core.Models;

public sealed class ProviderTransportGainSummary
{
    public string ProviderName { get; set; } = string.Empty;
    public double NamedPipeMeanMs { get; set; }
    public double HttpMeanMs { get; set; }
    public double GrpcMeanMs { get; set; }
    public double GainPercentVsHttp { get; set; }
    public double GainPercentVsGrpc { get; set; }
}
