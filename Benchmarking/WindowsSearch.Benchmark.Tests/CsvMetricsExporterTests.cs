using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;
using System.IO;
using WindowsSearch.Benchmark.Core.Models;
using WindowsSearch.Benchmark.Reporting.Exporters;
using WindowsSearch.Common.Models.Benchmark;

namespace WindowsSearch.Benchmark.Tests;

public class CsvMetricsExporterTests
{
    [Fact]
    public void ExportRuns_CreatesFileWithHeaderAndRows()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_runs_{Guid.NewGuid():N}.csv");
        try
        {
            var runs = new List<SingleRunBenchmarkResult>
            {
                new()
                {
                    RunId = "run_1",
                    Iteration = 1,
                    IsWarmup = false,
                    Transport = ProviderTransportKind.NamedPipe,
                    Serialization = SerializationKind.Json,
                    SearchMode = ProviderSearchMode.ConcurrentStream,
                    ImageResolver = ImageResolverKind.ConcurrentDictionary,
                    UseCache = true,
                    Query = "test",
                    Report = new SearchBenchmarkReport
                    {
                        TotalWallClockMs = 12.34,
                        TotalAggregationMs = 1.2,
                        TotalImageResolutionMs = 2.3
                    },
                    ResourceSample = new SystemResourceSample
                    {
                        CpuPercentage = 5.5,
                        HubWorkingSetMb = 45.2,
                        TotalWorkingSetMb = 85.6
                    }
                }
            };

            CsvMetricsExporter.ExportRuns(tempFile, runs);

            Assert.True(File.Exists(tempFile));
            var lines = File.ReadAllLines(tempFile);
            Assert.True(lines.Length >= 2);
            Assert.Contains("RunId,Iteration", lines[0]);
            Assert.Contains("NamedPipe", lines[1]);
            Assert.Contains("12.34", lines[1]);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void ExportSummaries_CreatesFileWithStats()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"test_summaries_{Guid.NewGuid():N}.csv");
        try
        {
            var summaries = new List<ConfigurationBenchmarkSummary>
            {
                new()
                {
                    Transport = ProviderTransportKind.NamedPipe,
                    Serialization = SerializationKind.Json,
                    SearchMode = ProviderSearchMode.ConcurrentStream,
                    ImageResolver = ImageResolverKind.ConcurrentDictionary,
                    UseCache = true,
                    Query = "test",
                    TotalDurationStats = new MetricStatistics { Mean = 15.0, Median = 14.5 },
                    AggregationStats = new MetricStatistics { Mean = 1.0 },
                    ImageResolutionStats = new MetricStatistics { Mean = 2.0 },
                    CpuPercentageStats = new MetricStatistics { Mean = 3.5 },
                    HubWorkingSetMbStats = new MetricStatistics { Mean = 50.0 },
                    TotalWorkingSetMbStats = new MetricStatistics { Mean = 100.0 },
                    TotalWireBytes = 512,
                    TriesCount = 4,
                    TimeToFirstResultStats = new MetricStatistics { Mean = 0.5 },
                }
            };

            CsvMetricsExporter.ExportSummaries(tempFile, summaries);

            Assert.True(File.Exists(tempFile));
            var lines = File.ReadAllLines(tempFile);
            Assert.True(lines.Length >= 2);
            Assert.Contains("DurationMean", lines[0]);
            Assert.Contains("Tries", lines[0]);
            Assert.Contains("TtfrMean", lines[0]);
            Assert.Contains("15.00", lines[1]);
            Assert.Contains("512", lines[1]);
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
