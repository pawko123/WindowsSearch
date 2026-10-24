namespace WindowsSearch.Benchmark.Core.Models;

public sealed class ProviderTransportGainSummary
{
    public string ProviderName { get; set; } = string.Empty;

    // Provider internal workload (e.g. SQLite database or disk queries)
    public double ProviderExecutionMeanMs { get; set; }

    // Pure IPC Transport Latency (TransportRoundtripMs - ProviderExecutionMs)
    public double NamedPipePureTransportMs { get; set; }
    public double HttpPureTransportMs { get; set; }
    public double GrpcPureTransportMs { get; set; }
    public double PureGainPercentVsHttp { get; set; }
    public double PureGainPercentVsGrpc { get; set; }

    // Overall Call Latency (TransportRoundtripMs = Transport + ProviderExecutionMs)
    public double NamedPipeMeanMs { get; set; }
    public double HttpMeanMs { get; set; }
    public double GrpcMeanMs { get; set; }
    public double GainPercentVsHttp { get; set; }
    public double GainPercentVsGrpc { get; set; }

    // Aliases for explicit clarity in reports:
    public double OverallGainPercentVsHttp
    {
        get => GainPercentVsHttp;
        set => GainPercentVsHttp = value;
    }

    public double OverallGainPercentVsGrpc
    {
        get => GainPercentVsGrpc;
        set => GainPercentVsGrpc = value;
    }
}
