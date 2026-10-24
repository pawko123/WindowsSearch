using WindowsSearch.Common.Logging;
using System.Diagnostics;
using WindowsSearch.Benchmark.Core.Models;
using WindowsSearch.Benchmark.Harness.Execution;
using WindowsSearch.Benchmark.Harness.Statistics;
using WindowsSearch.Benchmark.Reporting.Charts;
using WindowsSearch.Benchmark.Reporting.Exporters;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;

namespace WindowsSearch.Benchmark.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        PrintBanner();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--regenerate", StringComparison.OrdinalIgnoreCase) ||
                args[i].Equals("--re-report", StringComparison.OrdinalIgnoreCase))
            {
                var target = i + 1 < args.Length ? args[i + 1] : "thesis-sensitivity-results";
                return RegenerateReports(target);
            }
        }
        var config = ParseArguments(args);

        Console.WriteLine($"[Config] Warmup: {config.WarmupRuns} | Measured: {config.MeasurementRuns} | Limit: {config.Limit}");
        Console.WriteLine($"[Config] Transports: {string.Join(", ", config.Transports)}");
        Console.WriteLine($"[Config] Serializers: {string.Join(", ", config.Serializations)}");
        Console.WriteLine($"[Config] Search Modes: {string.Join(", ", config.SearchModes)}");
        Console.WriteLine($"[Config] Image Resolvers: {string.Join(", ", config.ImageResolvers)}");
        Console.WriteLine($"[Config] Queries: {string.Join(", ", config.Queries)}");
        var startTimestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var baseOutDir = Path.GetFullPath(config.OutputDir);
        var outDir = Path.Combine(baseOutDir, startTimestamp);
        config.OutputDir = outDir;
        Directory.CreateDirectory(outDir);
        AppLogger.Initialize("benchmark", LogLevel.Info, outDir);
        Console.WriteLine($"[Config] Output Directory: {outDir}\n");
        var stopwatch = Stopwatch.StartNew();
        var engine = new SensitivityBenchmarkEngine(config);

        engine.Progress += message => Console.WriteLine(message);

        try
        {
            var result = await engine.RunAsync();
            stopwatch.Stop();

            Console.WriteLine("\n=======================================================");
            Console.WriteLine($"  Benchmarking Complete in {stopwatch.Elapsed.TotalSeconds:F1}s!");
            Console.WriteLine($"  Total Runs: {result.AllRuns.Count}");
            Console.WriteLine($"  Total Duration Speedup (Worst -> Best):     {result.SpeedupFactor}x");
            Console.WriteLine($"  TTFR Responsiveness Speedup (First Result): {result.TimeToFirstResultSpeedup}x");
            Console.WriteLine($"  Average Per-Provider Speedup:               {result.AverageProviderSpeedup}x");
            foreach (var (p, sp) in result.PerProviderSpeedup)
            {
                Console.WriteLine($"    - {p}: {sp}x faster");
            }
            Console.WriteLine($"  Wire Payload Reduction:                     {result.WireReductionPercent}%");
            Console.WriteLine($"  Cache Acceleration Ratio:                   {result.CacheAccelerationRatio}x");
            if (result.ProviderTransportGains.Count > 0)
            {
                Console.WriteLine($"  NamedPipe Pure IPC Gain vs HTTP:            {result.OverallPureTransportGainPercentVsHttp:+0.0;-0.0}%");
                Console.WriteLine($"  NamedPipe Pure IPC Gain vs gRPC:            {result.OverallPureTransportGainPercentVsGrpc:+0.0;-0.0}%");
                Console.WriteLine($"  NamedPipe End-to-End Gain vs HTTP (Amdahl): {result.OverallProviderGainPercentVsHttp:+0.0;-0.0}%");
                Console.WriteLine($"  NamedPipe End-to-End Gain vs gRPC (Amdahl): {result.OverallProviderGainPercentVsGrpc:+0.0;-0.0}%");
            }
            Console.WriteLine("=======================================================");


            ExportAllArtifacts(outDir, result);
            Console.WriteLine("\nAll benchmark artifacts generated successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[FATAL ERROR] {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            return 1;
        }
    }

    private static void ExportAllArtifacts(string outDir, SensitivityBenchmarkResult result)
    {
        Directory.CreateDirectory(outDir);

        // 1. Export CSV
        var runsCsv = Path.Combine(outDir, "benchmark_runs.csv");
        var summariesCsv = Path.Combine(outDir, "benchmark_summaries.csv");
        var allSummaries = new List<ConfigurationBenchmarkSummary>();
        allSummaries.AddRange(result.TransportSummaries);
        allSummaries.AddRange(result.SerializationSummaries);
        allSummaries.AddRange(result.SearchModeSummaries);
        allSummaries.AddRange(result.ImageResolverSummaries);
        allSummaries.Add(result.BestSummary);
        allSummaries.Add(result.WorstSummary);

        CsvMetricsExporter.ExportRuns(runsCsv, result.AllRuns);
        CsvMetricsExporter.ExportSummaries(summariesCsv, allSummaries);
        Console.WriteLine($"[Saved] CSV Runs:           {runsCsv}");
        Console.WriteLine($"[Saved] CSV Summaries:      {summariesCsv}");

        // 2. Export JSON
        var jsonPath = Path.Combine(outDir, "benchmark_results.json");
        JsonMetricsExporter.Export(jsonPath, result);
        Console.WriteLine($"[Saved] JSON Full:          {jsonPath}");

        // 3. Export Sensitivity, Comparison, & Stage-Specific Resource Charts
        // Stage 1
        var chart1Filtered = Path.Combine(outDir, "chart_stage1_transports_filtered.png");
        ChartGenerator.GenerateSensitivityTransportChart(result.TransportSummaries, chart1Filtered, useFiltered: true);
        Console.WriteLine($"[Saved] Chart 1 (Filtered): {chart1Filtered}");
        var chart1Gains = Path.Combine(outDir, "chart_stage1_provider_transport_gains.png");
        ChartGenerator.GenerateProviderTransportGainsChart(result.ProviderTransportGains, result.OverallProviderGainPercentVsHttp, result.OverallProviderGainPercentVsGrpc, chart1Gains);
        Console.WriteLine($"[Saved] Chart 1 (Gains):    {chart1Gains}");

        var chart1Prov = Path.Combine(outDir, "chart_stage1_transports_provider_calls.png");
        ChartGenerator.GenerateSensitivityTransportProviderCallsChart(result.TransportSummaries, chart1Prov);
        Console.WriteLine($"[Saved] Chart 1 (Provider): {chart1Prov}");
        var chart1Raw = Path.Combine(outDir, "chart_stage1_transports_raw.png");
        ChartGenerator.GenerateSensitivityTransportChart(result.TransportSummaries, chart1Raw, useFiltered: false);
        Console.WriteLine($"[Saved] Chart 1 (Raw):      {chart1Raw}");
        var chart1Res = Path.Combine(outDir, "chart_stage1_resources.png");
        ChartGenerator.GenerateStageResourceChart(result.TransportSummaries, "Stage 1: Transport", chart1Res);
        Console.WriteLine($"[Saved] Chart 1 Resources:  {chart1Res}");

        // Stage 2
        var chart2A = Path.Combine(outDir, "chart_stage2_wire_bytes.png");
        ChartGenerator.GenerateSensitivitySerializationWireBytesChart(result.SerializationSummaries, chart2A);
        Console.WriteLine($"[Saved] Chart 2A (Bytes):   {chart2A}");

        var chart2B = Path.Combine(outDir, "chart_stage2_serialization_cpu.png");
        ChartGenerator.GenerateSensitivitySerializationCpuChart(result.SerializationSummaries, chart2B);
        Console.WriteLine($"[Saved] Chart 2B (CPU):     {chart2B}");

        var chart2Res = Path.Combine(outDir, "chart_stage2_resources.png");
        ChartGenerator.GenerateStageResourceChart(result.SerializationSummaries, "Stage 2: Serialization", chart2Res);
        Console.WriteLine($"[Saved] Chart 2 Resources:  {chart2Res}");

        // Stage 3
        var chart3 = Path.Combine(outDir, "chart_stage3_search_modes.png");
        ChartGenerator.GenerateSensitivitySearchModesChart(result.SearchModeSummaries, chart3);
        Console.WriteLine($"[Saved] Chart 3 (Modes):    {chart3}");

        var chart3Breakdown = Path.Combine(outDir, "chart_stage3_provider_breakdown.png");
        ChartGenerator.GenerateStage3ProviderBreakdownChart(result.SearchModeSummaries, chart3Breakdown);
        Console.WriteLine($"[Saved] Chart 3 Breakdown:  {chart3Breakdown}");

        var chart3Res = Path.Combine(outDir, "chart_stage3_resources.png");
        ChartGenerator.GenerateStageResourceChart(result.SearchModeSummaries, "Stage 3: Search Modes", chart3Res);
        Console.WriteLine($"[Saved] Chart 3 Resources:  {chart3Res}");

        // Stage 4
        var chart4A = Path.Combine(outDir, "chart_stage4_cache_penalty.png");
        ChartGenerator.GenerateSensitivityCachePenaltyChart(result.ImageResolverSummaries, chart4A);
        Console.WriteLine($"[Saved] Chart 4A (Penalty): {chart4A}");

        var chart4B = Path.Combine(outDir, "chart_stage4_resolvers_zoom.png");
        ChartGenerator.GenerateSensitivityResolverZoomChart(result.ImageResolverSummaries, chart4B);
        Console.WriteLine($"[Saved] Chart 4B (Zoom):    {chart4B}");

        var chart4C = Path.Combine(outDir, "chart_stage4_cache_hits_misses.png");
        ChartGenerator.GenerateSensitivityCacheHitsMissesChart(result.ImageResolverSummaries, chart4C);
        Console.WriteLine($"[Saved] Chart 4C (Hits):    {chart4C}");

        var chart4Res = Path.Combine(outDir, "chart_stage4_resources.png");
        ChartGenerator.GenerateStageResourceChart(result.ImageResolverSummaries, "Stage 4: Image Resolvers", chart4Res);
        Console.WriteLine($"[Saved] Chart 4 Resources:  {chart4Res}");

        // Stage 5
        var chart5 = Path.Combine(outDir, "chart_stage5_best_vs_worst.png");
        ChartGenerator.GenerateBestVsWorstComparisonChart(result.BestSummary, result.WorstSummary, result.SpeedupFactor, result.TimeToFirstResultSpeedup, chart5);
        Console.WriteLine($"[Saved] Chart 5:            {chart5}");

        var chart5Res = Path.Combine(outDir, "chart_stage5_resources.png");
        ChartGenerator.GenerateStageResourceChart([result.BestSummary, result.WorstSummary], "Stage 5: Final Comparison", chart5Res);
        Console.WriteLine($"[Saved] Chart 5 Resources:  {chart5Res}");

        var chart5TimeBudget = Path.Combine(outDir, "chart_stage5_time_budget.png");
        ChartGenerator.GenerateTimeBudgetPieChart(result.AllRuns, chart5TimeBudget);
        Console.WriteLine($"[Saved] Chart 5 Time Budget:{chart5TimeBudget}");
        // 4. Export HTML Report
        var htmlReportPath = Path.Combine(outDir, "report.html");
        HtmlReportGenerator.GenerateSensitivityReport(htmlReportPath, result);
        Console.WriteLine($"[Saved] HTML Report:        {htmlReportPath}");
        var logFileName = $"benchmarkLogs_{DateTime.Now:yyyy-MM-dd}.log";
        Console.WriteLine($"[Saved] Log File:           {Path.Combine(outDir, logFileName)}");
    }

    private static int RegenerateReports(string target)
    {
        Console.WriteLine($"[Regenerate] Target path: {target}");
        string jsonPath;
        string outDir;

        if (File.Exists(target) && target.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            jsonPath = Path.GetFullPath(target);
            outDir = Path.GetDirectoryName(jsonPath) ?? Directory.GetCurrentDirectory();
        }
        else if (Directory.Exists(target))
        {
            var directJson = Path.Combine(target, "benchmark_results.json");
            if (File.Exists(directJson))
            {
                jsonPath = Path.GetFullPath(directJson);
                outDir = Path.GetFullPath(target);
            }
            else
            {
                var subDirs = Directory.GetDirectories(target)
                    .OrderByDescending(d => d)
                    .ToList();
                var found = subDirs.FirstOrDefault(d => File.Exists(Path.Combine(d, "benchmark_results.json")));
                if (found != null)
                {
                    jsonPath = Path.Combine(found, "benchmark_results.json");
                    outDir = Path.GetFullPath(found);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[Error] Could not find 'benchmark_results.json' in '{target}' or its subdirectories.");
                    Console.ResetColor();
                    return 1;
                }
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Error] Path '{target}' does not exist.");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"[Regenerate] Loading data from: {jsonPath}");
        var jsonContent = File.ReadAllText(jsonPath);
        var result = System.Text.Json.JsonSerializer.Deserialize<SensitivityBenchmarkResult>(jsonContent, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (result == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[Error] Failed to deserialize benchmark results.");
            Console.ResetColor();
            return 1;
        }

        if (result.ProviderTransportGains.Count == 0 && result.AllRuns.Count > 0)
        {
            var queries = result.AllRuns.Select(r => r.Query).Where(q => !q.Contains("(Cold)")).Distinct().ToList();
            SensitivityBenchmarkEngine.ComputeProviderTransportGains(result, queries, result.AllRuns);
        }

        BackfillMissingStatistics(result);
        Console.WriteLine($"[Regenerate] Re-rendering charts and HTML report in: {outDir}...");
        ExportAllArtifacts(outDir, result);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n[Success] Regeneration complete!");
        Console.ResetColor();
        return 0;
    }

    private static void BackfillMissingStatistics(SensitivityBenchmarkResult result)
    {
        if (result.AllRuns.Count == 0) return;

        var allSummaries = new List<ConfigurationBenchmarkSummary>();
        allSummaries.AddRange(result.TransportSummaries);
        allSummaries.AddRange(result.SerializationSummaries);
        allSummaries.AddRange(result.SearchModeSummaries);
        allSummaries.AddRange(result.ImageResolverSummaries);
        if (result.BestSummary != null) allSummaries.Add(result.BestSummary);
        if (result.WorstSummary != null) allSummaries.Add(result.WorstSummary);

        foreach (var s in allSummaries)
        {
            var matchingRuns = result.AllRuns
                .Where(r => !r.IsWarmup &&
                            r.Transport == s.Transport &&
                            r.Serialization == s.Serialization &&
                            r.SearchMode == s.SearchMode &&
                            r.ImageResolver == s.ImageResolver &&
                            r.UseCache == s.UseCache &&
                            r.ResourceSample != null)
                .ToList();

            if (matchingRuns.Count == 0)
            {
                matchingRuns = result.AllRuns
                    .Where(r => !r.IsWarmup &&
                                r.ImageResolver == s.ImageResolver &&
                                r.UseCache == s.UseCache &&
                                r.ResourceSample != null)
                    .ToList();
            }

            if (matchingRuns.Count > 0)
            {
                if (s.HubPrivateMemoryMbStats == null || s.HubPrivateMemoryMbStats.Count == 0 || s.HubPrivateMemoryMbStats.Mean == 0)
                {
                    var hubPriv = matchingRuns.Select(r => r.ResourceSample!.HubPrivateMemoryMb).ToList();
                    s.HubPrivateMemoryMbStats = StatisticalCalculator.Calculate(hubPriv);
                }

                if (s.TotalPrivateMemoryMbStats == null || s.TotalPrivateMemoryMbStats.Count == 0 || s.TotalPrivateMemoryMbStats.Mean == 0)
                {
                    var totPriv = matchingRuns.Select(r => r.ResourceSample!.TotalPrivateMemoryMb).ToList();
                    s.TotalPrivateMemoryMbStats = StatisticalCalculator.Calculate(totPriv);
                }

                if (s.HubWorkingSetMbStats == null || s.HubWorkingSetMbStats.Count == 0 || s.HubWorkingSetMbStats.Mean == 0)
                {
                    var hubWs = matchingRuns.Select(r => r.ResourceSample!.HubWorkingSetMb).ToList();
                    s.HubWorkingSetMbStats = StatisticalCalculator.Calculate(hubWs);
                }

                if (s.TotalWorkingSetMbStats == null || s.TotalWorkingSetMbStats.Count == 0 || s.TotalWorkingSetMbStats.Mean == 0)
                {
                    var totWs = matchingRuns.Select(r => r.ResourceSample!.TotalWorkingSetMb).ToList();
                    s.TotalWorkingSetMbStats = StatisticalCalculator.Calculate(totWs);
                }
            }
        }
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
╔══════════════════════════════════════════════════════════════╗
║        WindowsSearch Hub Benchmark Harness (Master Thesis)   ║
╚══════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
    }

    private static BenchmarkConfiguration ParseArguments(string[] args)
    {
        var config = new BenchmarkConfiguration();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.Equals("--quick", StringComparison.OrdinalIgnoreCase))
            {
                config.WarmupRuns = 1;
                config.MeasurementRuns = 3;
                config.Queries = ["git", "demo"];
                config.Transports = [ProviderTransportKind.NamedPipe];
                config.Serializations = [SerializationKind.Json];
                config.SearchModes = [ProviderSearchMode.ConcurrentStream];
                config.ImageResolvers = [ImageResolverKind.ConcurrentDictionary];
                config.UseCacheOptions = [true, false];
            }
            else if (arg.Equals("--warmup", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i + 1], out var w))
            {
                config.WarmupRuns = Math.Max(0, w);
                i++;
            }
            else if (arg.Equals("--runs", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i + 1], out var r))
            {
                config.MeasurementRuns = Math.Max(1, r);
                i++;
            }
            else if (arg.Equals("--limit", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && int.TryParse(args[i + 1], out var l))
            {
                config.Limit = Math.Max(1, l);
                i++;
            }
            else if (arg.Equals("--output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                config.OutputDir = args[++i];
            }
            else if (arg.Equals("--hub-path", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                config.HubExecutablePath = args[++i];
            }
            else if (arg.Equals("--queries", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                config.Queries = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }
            else if (arg.Equals("--transports", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var list = new List<ProviderTransportKind>();
                foreach (var item in args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (Enum.TryParse<ProviderTransportKind>(item, true, out var t)) list.Add(t);
                }
                if (list.Count > 0) config.Transports = list;
            }
            else if (arg.Equals("--serializers", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var list = new List<SerializationKind>();
                foreach (var item in args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (Enum.TryParse<SerializationKind>(item, true, out var s)) list.Add(s);
                }
                if (list.Count > 0) config.Serializations = list;
            }
            else if (arg.Equals("--modes", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var list = new List<ProviderSearchMode>();
                foreach (var item in args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (Enum.TryParse<ProviderSearchMode>(item, true, out var m)) list.Add(m);
                }
                if (list.Count > 0) config.SearchModes = list;
            }
            else if (arg.Equals("--resolvers", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var list = new List<ImageResolverKind>();
                foreach (var item in args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (Enum.TryParse<ImageResolverKind>(item, true, out var resKind)) list.Add(resKind);
                }
                if (list.Count > 0) config.ImageResolvers = list;
            }
        }

        return config;
    }
}
