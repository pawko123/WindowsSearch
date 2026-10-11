using WindowsSearch.Benchmark.Core.Models;
using WindowsSearch.Benchmark.Harness.Execution;
using WindowsSearch.Benchmark.Reporting.Charts;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Models.Benchmark;
using WindowsSearch.Common.Serialization;
using Xunit;

namespace WindowsSearch.Benchmark.Tests;

public class ProviderTransportGainsTests
{
    [Fact]
    public void ComputeProviderTransportGains_CalculatesPureAndOverallGainsCorrectly_DemonstratingAmdahlsLaw()
    {
        var result = new SensitivityBenchmarkResult();
        var queries = new List<string> { "demo" };

        var allRuns = new List<SingleRunBenchmarkResult>
        {
            // NamedPipe run:
            // Firefox: 120ms SQLite workload, 0.1ms wire -> 120.1ms total
            // JetBrains: 0.05ms memory workload, 0.1ms wire -> 0.15ms total
            new()
            {
                IsWarmup = false,
                Transport = ProviderTransportKind.NamedPipe,
                Serialization = SerializationKind.Json,
                Query = "demo",
                Report = new SearchBenchmarkReport
                {
                    ProviderMetrics =
                    [
                        new() { ProviderName = "FirefoxProvider", TransportRoundtripMs = 120.1, ProviderExecutionMs = 120.0 },
                        new() { ProviderName = "JetBrainsProvider", TransportRoundtripMs = 0.15, ProviderExecutionMs = 0.05 }
                    ]
                }
            },
            // HTTP run:
            // Firefox: 120ms SQLite workload, 17.0ms wire -> 137.0ms total
            // JetBrains: 0.05ms memory workload, 17.0ms wire -> 17.05ms total
            new()
            {
                IsWarmup = false,
                Transport = ProviderTransportKind.Http,
                Serialization = SerializationKind.Json,
                Query = "demo",
                Report = new SearchBenchmarkReport
                {
                    ProviderMetrics =
                    [
                        new() { ProviderName = "FirefoxProvider", TransportRoundtripMs = 137.0, ProviderExecutionMs = 120.0 },
                        new() { ProviderName = "JetBrainsProvider", TransportRoundtripMs = 17.05, ProviderExecutionMs = 0.05 }
                    ]
                }
            },
            // gRPC run:
            // Firefox: 120ms SQLite workload, 27.0ms wire -> 147.0ms total
            // JetBrains: 0.05ms memory workload, 27.0ms wire -> 27.05ms total
            new()
            {
                IsWarmup = false,
                Transport = ProviderTransportKind.Grpc,
                Serialization = SerializationKind.Json,
                Query = "demo",
                Report = new SearchBenchmarkReport
                {
                    ProviderMetrics =
                    [
                        new() { ProviderName = "FirefoxProvider", TransportRoundtripMs = 147.0, ProviderExecutionMs = 120.0 },
                        new() { ProviderName = "JetBrainsProvider", TransportRoundtripMs = 27.05, ProviderExecutionMs = 0.05 }
                    ]
                }
            }
        };

        SensitivityBenchmarkEngine.ComputeProviderTransportGains(result, queries, allRuns);

        Assert.Equal(2, result.ProviderTransportGains.Count);

        var firefox = result.ProviderTransportGains.First(g => g.ProviderName == "FirefoxProvider");
        var jetbrains = result.ProviderTransportGains.First(g => g.ProviderName == "JetBrainsProvider");

        // Firefox verification:
        // Pure transport: NP is ~0.1ms, HTTP is 17.0ms -> Pure Speedup ~ 16,900%
        Assert.Equal(120.0, firefox.ProviderExecutionMeanMs);
        Assert.Equal(0.1, firefox.NamedPipePureTransportMs, 1);
        Assert.Equal(17.0, firefox.HttpPureTransportMs, 1);
        Assert.True(firefox.PureGainPercentVsHttp > 10000, "Pure IPC speedup for Firefox should be > 10,000%");

        // Overall call: NP is 120.1ms, HTTP is 137.0ms -> Overall Speedup only ~ 14% (Amdahl's Law)
        Assert.Equal(120.1, firefox.NamedPipeMeanMs, 1);
        Assert.Equal(137.0, firefox.HttpMeanMs, 1);
        Assert.InRange(firefox.GainPercentVsHttp, 10.0, 20.0);

        // JetBrains verification: in-memory provider reflects massive gain even in overall call
        Assert.True(jetbrains.GainPercentVsHttp > 5000, "JetBrains overall call gain should reflect pure IPC speedup");

        // Verify overall averages across providers:
        Assert.True(result.OverallPureTransportGainPercentVsHttp > 10000);
        Assert.True(result.OverallProviderGainPercentVsHttp < result.OverallPureTransportGainPercentVsHttp);
    }

    [Fact]
    public void GenerateHtmlReport_IncludesUpdatedColumnsAndBadges()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"report_test_{Guid.NewGuid():N}.html");
        try
        {
            var result = new SensitivityBenchmarkResult
            {
                ProviderTransportGains =
                [
                    new()
                    {
                        ProviderName = "FirefoxProvider",
                        ProviderExecutionMeanMs = 120.0,
                        NamedPipePureTransportMs = 0.08,
                        HttpPureTransportMs = 17.1,
                        GrpcPureTransportMs = 27.2,
                        PureGainPercentVsHttp = 21275.0,
                        PureGainPercentVsGrpc = 33900.0,
                        NamedPipeMeanMs = 120.08,
                        HttpMeanMs = 137.1,
                        GrpcMeanMs = 147.2,
                        GainPercentVsHttp = 14.2,
                        GainPercentVsGrpc = 22.6
                    }
                ],
                OverallPureTransportGainPercentVsHttp = 21275.0,
                OverallPureTransportGainPercentVsGrpc = 33900.0,
                OverallProviderGainPercentVsHttp = 14.2,
                OverallProviderGainPercentVsGrpc = 22.6
            };

            HtmlReportGenerator.GenerateSensitivityReport(tempFile, result);

            var html = File.ReadAllText(tempFile);
            Assert.Contains("NamedPipe Pure (ms)", html);
            Assert.Contains("HTTP Pure IPC", html);
            Assert.Contains("gRPC Pure IPC", html);
            Assert.Contains("NamedPipe Total (ms)", html);
            Assert.Contains("HTTP Total (Amdahl)", html);
            Assert.Contains("gRPC Total (Amdahl)", html);
            Assert.Contains("badge badge-best", html);
            Assert.Contains("21275", html);
            Assert.Contains("14.2%", html);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
