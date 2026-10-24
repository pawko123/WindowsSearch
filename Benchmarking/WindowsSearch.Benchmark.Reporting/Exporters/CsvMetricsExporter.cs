using System.Globalization;
using System.IO;
using System.Text;
using WindowsSearch.Benchmark.Core.Models;

namespace WindowsSearch.Benchmark.Reporting.Exporters;

public static class CsvMetricsExporter
{
    public static void ExportRuns(string filePath, IReadOnlyList<SingleRunBenchmarkResult> runs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("RunId,Iteration,IsWarmup,Transport,Serialization,SearchMode,ImageResolver,UseCache,Query,TotalWallClockMs,TotalAggregationMs,TotalImageResolutionMs,CpuPercentage,HubWorkingSetMb,TotalWorkingSetMb,HubPrivateMemoryMb,TotalPrivateMemoryMb");

        foreach (var run in runs)
        {
            var wallClock = run.Report?.TotalWallClockMs.ToString("F2", CultureInfo.InvariantCulture) ?? "0";
            var agg = run.Report?.TotalAggregationMs.ToString("F2", CultureInfo.InvariantCulture) ?? "0";
            var img = run.Report?.TotalImageResolutionMs.ToString("F2", CultureInfo.InvariantCulture) ?? "0";
            var cpu = run.ResourceSample?.CpuPercentage.ToString("F2", CultureInfo.InvariantCulture) ?? "0";
            var hubWs = run.ResourceSample?.HubWorkingSetMb.ToString("F2", CultureInfo.InvariantCulture) ?? "0";
            var totWs = run.ResourceSample?.TotalWorkingSetMb.ToString("F2", CultureInfo.InvariantCulture) ?? "0";
            var hubPriv = run.ResourceSample?.HubPrivateMemoryMb.ToString("F2", CultureInfo.InvariantCulture) ?? "0";
            var totPriv = run.ResourceSample?.TotalPrivateMemoryMb.ToString("F2", CultureInfo.InvariantCulture) ?? "0";

            sb.AppendLine(string.Join(",",
                EscapeCsv(run.RunId),
                run.Iteration,
                run.IsWarmup,
                EscapeCsv(run.Transport.ToString()),
                EscapeCsv(run.Serialization.ToString()),
                EscapeCsv(run.SearchMode.ToString()),
                EscapeCsv(run.ImageResolver.ToString()),
                run.UseCache,
                EscapeCsv(run.Query),
                wallClock,
                agg,
                img,
                cpu,
                hubWs,
                totWs,
                hubPriv,
                totPriv
            ));
        }

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    public static void ExportSummaries(string filePath, IReadOnlyList<ConfigurationBenchmarkSummary> summaries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Transport,Serialization,SearchMode,ImageResolver,UseCache,Query,Tries,ColdDuration,ColdTtfr,ColdMisses,DurationMean,TtfrMean,DurationMedian,DurationP90,DurationP95,DurationStdDev,DurationError95,AvgProviderDuration,AggregationMean,ImageResolutionMean,CpuMean,HubWorkingSetMbMean,TotalWorkingSetMbMean,HubPrivateMemoryMbMean,TotalPrivateMemoryMbMean,TotalWireBytes,CacheHits,CacheMisses,HitRatePercent");

        foreach (var s in summaries)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(s.Transport.ToString()),
                EscapeCsv(s.Serialization.ToString()),
                EscapeCsv(s.SearchMode.ToString()),
                EscapeCsv(s.ImageResolver.ToString()),
                s.UseCache,
                EscapeCsv(s.Query),
                s.TriesCount,
                s.ColdDurationMs.ToString("F2", CultureInfo.InvariantCulture),
                s.ColdTimeToFirstResultMs.ToString("F2", CultureInfo.InvariantCulture),
                s.ColdCacheMisses,
                s.TotalDurationStats.Mean.ToString("F2", CultureInfo.InvariantCulture),
                s.TimeToFirstResultStats.Mean.ToString("F2", CultureInfo.InvariantCulture),
                s.TotalDurationStats.Median.ToString("F2", CultureInfo.InvariantCulture),
                s.TotalDurationStats.P90.ToString("F2", CultureInfo.InvariantCulture),
                s.TotalDurationStats.P95.ToString("F2", CultureInfo.InvariantCulture),
                s.TotalDurationStats.StandardDeviation.ToString("F2", CultureInfo.InvariantCulture),
                s.TotalDurationStats.MarginOfError95.ToString("F2", CultureInfo.InvariantCulture),
                s.AverageProviderDurationMs.ToString("F2", CultureInfo.InvariantCulture),
                s.AggregationStats.Mean.ToString("F2", CultureInfo.InvariantCulture),
                s.ImageResolutionStats.Mean.ToString("F2", CultureInfo.InvariantCulture),
                s.CpuPercentageStats.Mean.ToString("F2", CultureInfo.InvariantCulture),
                s.HubWorkingSetMbStats.Mean.ToString("F2", CultureInfo.InvariantCulture),
                s.TotalWorkingSetMbStats.Mean.ToString("F2", CultureInfo.InvariantCulture),
                s.HubPrivateMemoryMbStats.Mean.ToString("F2", CultureInfo.InvariantCulture),
                s.TotalPrivateMemoryMbStats.Mean.ToString("F2", CultureInfo.InvariantCulture),
                s.TotalWireBytes,
                s.TotalCacheHits,
                s.TotalCacheMisses,
                s.CacheHitRatePercent.ToString("F1", CultureInfo.InvariantCulture)
            ));
        }

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
