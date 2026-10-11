using System.Diagnostics;
using WindowsSearch.Benchmark.Core.Models;
using WindowsSearch.Benchmark.Harness.Execution;
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

        var config = ParseArguments(args);

        Console.WriteLine($"[Config] Warmup: {config.WarmupRuns} | Measured: {config.MeasurementRuns} | Limit: {config.Limit}");
        Console.WriteLine($"[Config] Transports: {string.Join(", ", config.Transports)}");
        Console.WriteLine($"[Config] Serializers: {string.Join(", ", config.Serializations)}");
        Console.WriteLine($"[Config] Search Modes: {string.Join(", ", config.SearchModes)}");
        Console.WriteLine($"[Config] Image Resolvers: {string.Join(", ", config.ImageResolvers)}");
        Console.WriteLine($"[Config] Queries: {string.Join(", ", config.Queries)}");
        Console.WriteLine($"[Config] Output Directory: {Path.GetFullPath(config.OutputDir)}\n");

        var stopwatch = Stopwatch.StartNew();
        var engine = new BenchmarkEngine(config);

        engine.Progress += message =>
        {
            Console.WriteLine(message);
        };

        try
        {
            var (runs, summaries) = await engine.RunAsync();
            stopwatch.Stop();

            Console.WriteLine("\n=======================================================");
            Console.WriteLine($"  Benchmarking Complete in {stopwatch.Elapsed.TotalSeconds:F1}s!");
            Console.WriteLine($"  Total Runs: {runs.Count} | Summaries: {summaries.Count}");
            Console.WriteLine("=======================================================");

            var outDir = Path.GetFullPath(config.OutputDir);
            Directory.CreateDirectory(outDir);

            // 1. Export CSV
            var runsCsv = Path.Combine(outDir, "benchmark_runs.csv");
            var summariesCsv = Path.Combine(outDir, "benchmark_summaries.csv");
            CsvMetricsExporter.ExportRuns(runsCsv, runs);
            CsvMetricsExporter.ExportSummaries(summariesCsv, summaries);
            Console.WriteLine($"[Saved] CSV Runs:      {runsCsv}");
            Console.WriteLine($"[Saved] CSV Summaries: {summariesCsv}");

            // 2. Export JSON
            var jsonPath = Path.Combine(outDir, "benchmark_results.json");
            JsonMetricsExporter.Export(jsonPath, new { Configuration = config, Summaries = summaries, Runs = runs });
            Console.WriteLine($"[Saved] JSON Full:     {jsonPath}");

            // 3. Export Charts
            if (summaries.Count > 0)
            {
                var transportChartPng = Path.Combine(outDir, "chart_transport_latency.png");
                var transportChartSvg = Path.Combine(outDir, "chart_transport_latency.svg");
                ChartGenerator.GenerateTransportLatencyChart(summaries, transportChartPng);
                ChartGenerator.GenerateTransportLatencyChart(summaries, transportChartSvg);
                Console.WriteLine($"[Saved] Chart:         {transportChartPng}");

                var modeChartPng = Path.Combine(outDir, "chart_search_modes.png");
                var modeChartSvg = Path.Combine(outDir, "chart_search_modes.svg");
                ChartGenerator.GenerateSearchModeLatencyChart(summaries, modeChartPng);
                ChartGenerator.GenerateSearchModeLatencyChart(summaries, modeChartSvg);
                Console.WriteLine($"[Saved] Chart:         {modeChartPng}");

                var wireChartPng = Path.Combine(outDir, "chart_wire_payloads.png");
                ChartGenerator.GenerateWireBytesChart(summaries, wireChartPng);
                Console.WriteLine($"[Saved] Chart:         {wireChartPng}");

                var resolverChartPng = Path.Combine(outDir, "chart_image_resolvers.png");
                ChartGenerator.GenerateImageResolverComparisonChart(summaries, resolverChartPng);
                Console.WriteLine($"[Saved] Chart:         {resolverChartPng}");
            }

            // 4. Export HTML Report
            var htmlReportPath = Path.Combine(outDir, "report.html");
            HtmlReportGenerator.GenerateReport(htmlReportPath, summaries, runs);
            Console.WriteLine($"[Saved] HTML Report:   {htmlReportPath}");

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
                config.UseCacheOptions = [true];
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
        }

        return config;
    }
}
