using System.IO;
using ScottPlot;
using WindowsSearch.Benchmark.Core.Models;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;

namespace WindowsSearch.Benchmark.Reporting.Charts;

public static class ChartGenerator
{
    private static readonly Color[] Palette =
    [
        Colors.SteelBlue, Colors.ForestGreen, Colors.IndianRed,
        Colors.DarkOrange, Colors.Purple, Colors.DarkCyan, Colors.GoldenRod
    ];

    // =========================================================================
    // STAGE 1: Transport Latency (With & Without Outliers)
    // =========================================================================
    public static void GenerateSensitivityTransportChart(IReadOnlyList<ConfigurationBenchmarkSummary> summaries, string outputPath, bool useFiltered = false)
    {
        var plot = new Plot();
        plot.Title(useFiltered
            ? "Stage 1: Transport IPC Latency - Provider Call Avg vs Total Search (Filtered)"
            : "Stage 1: Transport IPC Latency - Provider Call Avg vs Total Search (Raw)");
        plot.YLabel("Latency (ms)");

        var barsProv = new List<Bar>();
        var barsTotal = new List<Bar>();
        var tickPositions = new List<double>();
        var tickLabels = new List<string>();

        double groupSpacing = 2.6;
        double barWidth = 0.8;

        for (int i = 0; i < summaries.Count; i++)
        {
            var s = summaries[i];
            double groupCenter = i * groupSpacing;
            tickPositions.Add(groupCenter + 0.4);
            tickLabels.Add(s.Transport.ToString());

            // Bar 1: Pure Provider IPC Call Roundtrip Latency
            var provStats = s.AverageProviderDurationStats;
            var provVal = provStats.Mean > 0 ? provStats.Mean : s.AverageProviderDurationMs;
            var provErr = Math.Min(provStats.MarginOfError95, provVal);
            barsProv.Add(new Bar
            {
                Position = groupCenter,
                Value = provVal,
                Error = provErr,
                FillColor = Colors.CornflowerBlue,
                Label = $"{provVal:F2}ms"
            });

            // Bar 2: Total Search Latency (End-to-End Hub Search masked by slowest provider)
            var stats = useFiltered ? s.FilteredDurationStats : s.TotalDurationStats;
            var error = Math.Min(stats.MarginOfError95, stats.Mean);
            barsTotal.Add(new Bar
            {
                Position = groupCenter + barWidth,
                Value = stats.Mean,
                Error = error,
                FillColor = Colors.DarkOrange,
                Label = $"{stats.Mean:F1}ms"
            });
        }

        if (barsProv.Count > 0)
        {
            var pProv = plot.Add.Bars(barsProv);
            pProv.LegendText = "Provider Call (Avg)";

            var pTotal = plot.Add.Bars(barsTotal);
            pTotal.LegendText = "Total Search (Mean)";

            plot.Axes.Bottom.SetTicks(tickPositions.ToArray(), tickLabels.ToArray());
            plot.Axes.Margins(bottom: 0, left: 0.1, right: 0.1);
            var allBars = barsProv.Concat(barsTotal).ToList();
            double maxVal = Math.Max(10, allBars.Max(b => b.Value + b.Error));
            plot.Axes.SetLimitsY(0, maxVal * 1.5);

            plot.Legend.Alignment = Alignment.UpperRight;
            plot.Legend.Orientation = Orientation.Horizontal;
            plot.ShowLegend();
        }

        SavePlot(plot, outputPath);
    }

    public static void GenerateSensitivityTransportProviderCallsChart(IReadOnlyList<ConfigurationBenchmarkSummary> summaries, string outputPath)
    {
        var plot = new Plot();
        plot.Title("Stage 1: Pure Provider IPC Call Latency Zoom (Demonstrating Real Transport Gain)");
        plot.YLabel("Average Provider Call Latency (ms)");

        var bars = new List<Bar>();
        var tickPositions = new List<double>();
        var tickLabels = new List<string>();

        for (int i = 0; i < summaries.Count; i++)
        {
            var s = summaries[i];
            var provStats = s.AverageProviderDurationStats;
            var provVal = provStats.Mean > 0 ? provStats.Mean : s.AverageProviderDurationMs;
            var provErr = Math.Min(provStats.MarginOfError95, provVal);

            bars.Add(new Bar
            {
                Position = i,
                Value = provVal,
                Error = provErr,
                FillColor = Palette[i % Palette.Length],
                Label = $"{provVal:F2}ms"
            });
            tickPositions.Add(i);
            tickLabels.Add(s.Transport.ToString());
        }

        if (bars.Count > 0)
        {
            plot.Add.Bars(bars);
            plot.Axes.Bottom.SetTicks(tickPositions.ToArray(), tickLabels.ToArray());
            plot.Axes.Margins(bottom: 0, left: 0.1, right: 0.1);
            plot.Axes.SetLimitsY(0, Math.Max(2, bars.Max(b => b.Value + b.Error) * 1.15));
        }

        SavePlot(plot, outputPath);
    }

    public static void GenerateProviderTransportGainsChart(IReadOnlyList<ProviderTransportGainSummary> gains, double overallVsHttp, double overallVsGrpc, string outputPath)
    {
        var plot = new Plot();
        plot.Title("Stage 1: NamedPipe IPC Speedup vs HTTP & gRPC per Provider (% Faster)");
        plot.YLabel("NamedPipe Speedup (% Faster)");
        var barsHttp = new List<Bar>();
        var barsGrpc = new List<Bar>();
        var tickPositions = new List<double>();
        var tickLabels = new List<string>();

        double groupSpacing = 2.6;
        double barWidth = 0.8;

        for (int i = 0; i < gains.Count; i++)
        {
            var g = gains[i];
            double groupCenter = i * groupSpacing;
            tickPositions.Add(groupCenter + 0.4);
            tickLabels.Add(g.ProviderName.Replace("Provider", ""));

            barsHttp.Add(new Bar
            {
                Position = groupCenter,
                Value = g.GainPercentVsHttp,
                FillColor = Colors.CornflowerBlue,
                Label = $"{g.GainPercentVsHttp:+0.0;-0.0}%"
            });

            barsGrpc.Add(new Bar
            {
                Position = groupCenter + barWidth,
                Value = g.GainPercentVsGrpc,
                FillColor = Colors.MediumSeaGreen,
                Label = $"{g.GainPercentVsGrpc:+0.0;-0.0}%"
            });
        }

        if (gains.Count > 0)
        {
            int overallIdx = gains.Count;
            double overallCenter = overallIdx * groupSpacing;
            tickPositions.Add(overallCenter + 0.4);
            tickLabels.Add("OVERALL");

            barsHttp.Add(new Bar
            {
                Position = overallCenter,
                Value = overallVsHttp,
                FillColor = Colors.CornflowerBlue,
                Label = $"{overallVsHttp:+0.0;-0.0}%"
            });

            barsGrpc.Add(new Bar
            {
                Position = overallCenter + barWidth,
                Value = overallVsGrpc,
                FillColor = Colors.MediumSeaGreen,
                Label = $"{overallVsGrpc:+0.0;-0.0}%"
            });
        }

        if (barsHttp.Count > 0)
        {
            var pHttp = plot.Add.Bars(barsHttp);
            pHttp.LegendText = "NamedPipe vs HTTP (Speedup %)";

            var pGrpc = plot.Add.Bars(barsGrpc);
            pGrpc.LegendText = "NamedPipe vs gRPC (Speedup %)";

            plot.Axes.Bottom.SetTicks(tickPositions.ToArray(), tickLabels.ToArray());
            plot.Axes.Margins(left: 0.08, right: 0.08);

            var allGainsBars = barsHttp.Concat(barsGrpc).ToList();
            double minY = Math.Min(0, allGainsBars.Min(b => b.Value));
            double maxY = Math.Max(10, allGainsBars.Max(b => b.Value));
            double topY = maxY > 0 ? maxY * 1.7 : 25;
            double bottomY = minY < 0 ? minY * 1.25 : 0;
            plot.Axes.SetLimitsY(bottomY, topY);

            var hLine = plot.Add.HorizontalLine(0);
            hLine.Color = Colors.Gray;
            hLine.LinePattern = LinePattern.Dashed;
            hLine.LineWidth = 1.2f;

            plot.Legend.Alignment = Alignment.UpperLeft;
            plot.Legend.Orientation = Orientation.Horizontal;
            plot.ShowLegend();
        }

        SavePlot(plot, outputPath);
    }

    // STAGE 2A: Serialization Wire Payload Size
    // =========================================================================
    public static void GenerateSensitivitySerializationWireBytesChart(IReadOnlyList<ConfigurationBenchmarkSummary> summaries, string outputPath)
    {
        var plot = new Plot();
        plot.Title("Stage 2A: Total Wire Payload Size by Serialization Format");
        plot.YLabel("Total Wire Payload (Bytes)");

        var bars = new List<Bar>();
        var tickPositions = new List<double>();
        var tickLabels = new List<string>();

        for (int i = 0; i < summaries.Count; i++)
        {
            var s = summaries[i];
            bars.Add(new Bar
            {
                Position = i,
                Value = s.TotalWireBytes,
                FillColor = Palette[(i + 1) % Palette.Length],
                Label = $"{s.TotalWireBytes:N0} B"
            });
            tickPositions.Add(i);
            tickLabels.Add(s.Serialization.ToString());
        }

        if (bars.Count > 0)
        {
            plot.Add.Bars(bars);
            plot.Axes.Bottom.SetTicks(tickPositions.ToArray(), tickLabels.ToArray());
        }

        SavePlot(plot, outputPath);
    }

    // =========================================================================
    // STAGE 2B: Serialization CPU Time
    // =========================================================================
    public static void GenerateSensitivitySerializationCpuChart(IReadOnlyList<ConfigurationBenchmarkSummary> summaries, string outputPath)
    {
        var plot = new Plot();
        plot.Title("Stage 2B: Serialization & Deserialization CPU Duration");
        plot.YLabel("CPU Time (ms)");

        var bars = new List<Bar>();
        var tickPositions = new List<double>();
        var tickLabels = new List<string>();

        for (int i = 0; i < summaries.Count; i++)
        {
            var s = summaries[i];
            bars.Add(new Bar
            {
                Position = i,
                Value = s.SerializationCpuStats.Mean,
                Error = s.SerializationCpuStats.MarginOfError95,
                FillColor = Palette[(i + 2) % Palette.Length],
                Label = $"{s.SerializationCpuStats.Mean:F2}ms"
            });
            tickPositions.Add(i);
            tickLabels.Add(s.Serialization.ToString());
        }

        if (bars.Count > 0)
        {
            plot.Add.Bars(bars);
            plot.Axes.Bottom.SetTicks(tickPositions.ToArray(), tickLabels.ToArray());
        }

        SavePlot(plot, outputPath);
    }

    // =========================================================================
    // STAGE 3A: Search Mode Concurrency Latency (TTFR vs Total Latency)
    // =========================================================================
    public static void GenerateSensitivitySearchModesChart(IReadOnlyList<ConfigurationBenchmarkSummary> summaries, string outputPath)
    {
        var plot = new Plot();
        plot.Title("Stage 3A: Search Mode Concurrency (TTFR vs Total Latency)");
        plot.YLabel("Latency (ms)");

        var barsTtfr = new List<Bar>();
        var barsTotal = new List<Bar>();
        var tickPositions = new List<double>();
        var tickLabels = new List<string>();

        double groupSpacing = 2.5;
        double barWidth = 0.8;

        for (int i = 0; i < summaries.Count; i++)
        {
            var s = summaries[i];
            double groupCenter = i * groupSpacing;
            tickPositions.Add(groupCenter + 0.4);
            tickLabels.Add(s.SearchMode.ToString());

            barsTtfr.Add(new Bar
            {
                Position = groupCenter,
                Value = s.TimeToFirstResultStats.Mean,
                FillColor = Colors.CornflowerBlue,
                Label = $"{s.TimeToFirstResultStats.Mean:F1}ms"
            });

            barsTotal.Add(new Bar
            {
                Position = groupCenter + barWidth,
                Value = s.TotalDurationStats.Mean,
                Error = Math.Min(s.TotalDurationStats.MarginOfError95, s.TotalDurationStats.Mean),
                FillColor = Colors.DarkOrange,
                Label = $"{s.TotalDurationStats.Mean:F1}ms"
            });
        }

        if (barsTtfr.Count > 0)
        {
            var pTtfr = plot.Add.Bars(barsTtfr);
            pTtfr.LegendText = "First Result (TTFR)";

            var pTotal = plot.Add.Bars(barsTotal);
            pTotal.LegendText = "Total Latency (Mean)";

            plot.Axes.Bottom.SetTicks(tickPositions.ToArray(), tickLabels.ToArray());
            plot.Axes.Margins(bottom: 0, left: 0.1, right: 0.1);

            var allBars = barsTtfr.Concat(barsTotal).ToList();
            plot.Axes.SetLimitsY(0, Math.Max(10, allBars.Max(b => b.Value + b.Error) * 1.35));

            plot.Legend.Alignment = Alignment.UpperLeft;
            plot.Legend.Orientation = Orientation.Horizontal;
            plot.ShowLegend();
        }

        SavePlot(plot, outputPath);
    }

    // =========================================================================
    // STAGE 3B: Per-Provider Latency Breakdown (Amdahl's Law)
    // =========================================================================
    public static void GenerateStage3ProviderBreakdownChart(IReadOnlyList<ConfigurationBenchmarkSummary> summaries, string outputPath)
    {
        var plot = new Plot();
        plot.Title("Stage 3B: Per-Provider Latency Breakdown (Demonstrating Amdahl's Law)");
        plot.YLabel("Provider Roundtrip Latency (ms)");

        // Use the first summary (or baseline) to get the provider metrics
        var baseline = summaries.FirstOrDefault(s => s.SearchMode == ProviderSearchMode.Sequential) ?? summaries.FirstOrDefault();
        if (baseline == null || baseline.ProviderDurationStats.Count == 0)
        {
            return;
        }

        var bars = new List<Bar>();
        var tickPositions = new List<double>();
        var tickLabels = new List<string>();

        var sortedProviders = baseline.ProviderDurationStats.OrderBy(p => p.Value.Mean).ToList();

        for (int i = 0; i < sortedProviders.Count; i++)
        {
            var (name, stats) = sortedProviders[i];
            bars.Add(new Bar
            {
                Position = i,
                Value = stats.Mean,
                Error = stats.MarginOfError95,
                FillColor = Palette[i % Palette.Length],
                Label = $"{stats.Mean:F1}ms"
            });
            tickPositions.Add(i);
            tickLabels.Add(name.Replace("Provider", ""));
        }

        if (bars.Count > 0)
        {
            plot.Add.Bars(bars);
            plot.Axes.Bottom.SetTicks(tickPositions.ToArray(), tickLabels.ToArray());
        }

        SavePlot(plot, outputPath);
    }

    // =========================================================================
    // STAGE 4A: Cache Macro Impact (Cold Startup vs Steady-State)
    // =========================================================================
    public static void GenerateSensitivityCachePenaltyChart(IReadOnlyList<ConfigurationBenchmarkSummary> summaries, string outputPath)
    {
        var plot = new Plot();
        plot.Title("Stage 4A: Macro Caching Impact (Cache ON vs Cache OFF)");
        plot.YLabel("Image Resolution Time (ms)");

        var resolvers = Enum.GetValues<ImageResolverKind>();
        var bars = new List<Bar>();
        var tickPositions = new List<double>();
        var tickLabels = new List<string>();

        double groupSpacing = 3.0;
        double barWidth = 0.8;

        for (int r = 0; r < resolvers.Length; r++)
        {
            var resolver = resolvers[r];
            double groupCenter = r * groupSpacing;
            tickPositions.Add(groupCenter + 0.4);
            tickLabels.Add(resolver.ToString());

            var cacheOn = summaries.Where(s => s.ImageResolver == resolver && s.UseCache).ToList();
            var cacheOff = summaries.Where(s => s.ImageResolver == resolver && !s.UseCache).ToList();

            var meanOn = cacheOn.Count > 0 ? cacheOn.Average(c => c.ImageResolutionStats.Mean) : 0;
            var meanOff = cacheOff.Count > 0 ? cacheOff.Average(c => c.ImageResolutionStats.Mean) : 0;

            bars.Add(new Bar
            {
                Position = groupCenter,
                Value = meanOn,
                FillColor = Colors.CadetBlue,
                Label = "Cache ON"
            });

            bars.Add(new Bar
            {
                Position = groupCenter + barWidth,
                Value = meanOff,
                FillColor = Colors.Coral,
                Label = "Cache OFF"
            });
        }

        if (bars.Count > 0)
        {
            plot.Add.Bars(bars);
            plot.Axes.Bottom.SetTicks(tickPositions.ToArray(), tickLabels.ToArray());
        }

        SavePlot(plot, outputPath);
    }

    // =========================================================================
    // STAGE 4B: Resolver Micro-Benchmark Zoom (Cache ON Only)
    // =========================================================================
    public static void GenerateSensitivityResolverZoomChart(IReadOnlyList<ConfigurationBenchmarkSummary> summaries, string outputPath)
    {
        var plot = new Plot();
        plot.Title("Stage 4B: In-Memory Resolver Scaling (Zoomed on Cache: ON under 100+ Icons)");
        plot.YLabel("Resolution Time (ms)");

        var cacheOnSummaries = summaries.Where(s => s.UseCache).ToList();
        var bars = new List<Bar>();
        var tickPositions = new List<double>();
        var tickLabels = new List<string>();

        for (int i = 0; i < cacheOnSummaries.Count; i++)
        {
            var s = cacheOnSummaries[i];
            bars.Add(new Bar
            {
                Position = i,
                Value = s.ImageResolutionStats.Mean,
                Error = s.ImageResolutionStats.MarginOfError95,
                FillColor = Palette[i % Palette.Length],
                Label = $"{s.ImageResolutionStats.Mean:F3}ms"
            });
            tickPositions.Add(i);
            tickLabels.Add(s.ImageResolver.ToString());
        }

        if (bars.Count > 0)
        {
            plot.Add.Bars(bars);
            plot.Axes.Bottom.SetTicks(tickPositions.ToArray(), tickLabels.ToArray());
        }

        SavePlot(plot, outputPath);
    }

    // =========================================================================
    // STAGE 4C: Cache Hits vs Misses Count
    // =========================================================================
    public static void GenerateSensitivityCacheHitsMissesChart(IReadOnlyList<ConfigurationBenchmarkSummary> summaries, string outputPath)
    {
        var plot = new Plot();
        plot.Title("Stage 4C: Cache Hit / Miss Counts Across Configurations");
        plot.YLabel("Request Count");

        var bars = new List<Bar>();
        var tickPositions = new List<double>();
        var tickLabels = new List<string>();

        double groupSpacing = 2.5;
        double barWidth = 0.8;

        for (int i = 0; i < summaries.Count; i++)
        {
            var s = summaries[i];
            double groupCenter = i * groupSpacing;
            tickPositions.Add(groupCenter + 0.4);
            var resolverShort = s.ImageResolver switch
            {
                ImageResolverKind.ConcurrentDictionary => "ConcDict",
                ImageResolverKind.Dictionary => "Dictionary",
                ImageResolverKind.MemoryCache => "MemCache",
                ImageResolverKind.List => "List",
                _ => s.ImageResolver.ToString()
            };
            tickLabels.Add($"{resolverShort}\n({(s.UseCache ? "ON" : "OFF")})");

            bars.Add(new Bar
            {
                Position = groupCenter,
                Value = s.TotalCacheHits,
                FillColor = Colors.MediumSeaGreen,
                Label = "Hits"
            });

            bars.Add(new Bar
            {
                Position = groupCenter + barWidth,
                Value = s.TotalCacheMisses,
                FillColor = Colors.Crimson,
                Label = "Misses"
            });
        }

        if (bars.Count > 0)
        {
            plot.Add.Bars(bars);
            plot.Axes.Bottom.SetTicks(tickPositions.ToArray(), tickLabels.ToArray());
        }

        SavePlot(plot, outputPath);
    }

    // =========================================================================
    // STAGE 5: Final Comparison (Best vs Worst Settings)
    // =========================================================================
    public static void GenerateBestVsWorstComparisonChart(ConfigurationBenchmarkSummary best, ConfigurationBenchmarkSummary worst, double speedup, double ttfrSpeedup, string outputPath)
    {
        var plot = new Plot();
        plot.Title($"Stage 5: Final Comparison - BEST vs WORST Settings ({speedup}x Total Speedup, {ttfrSpeedup}x TTFR Speedup)");
        plot.YLabel("Duration (ms)");

        var bars = new List<Bar>
        {
            new()
            {
                Position = 0,
                Value = best.TimeToFirstResultStats.Mean,
                FillColor = Colors.LightGreen,
                Label = $"BEST First Result: {best.TimeToFirstResultStats.Mean:F1}ms"
            },
            new()
            {
                Position = 0.8,
                Value = best.TotalDurationStats.Mean,
                Error = Math.Min(best.TotalDurationStats.MarginOfError95, best.TotalDurationStats.Mean),
                FillColor = Colors.MediumSeaGreen,
                Label = $"BEST Total: {best.TotalDurationStats.Mean:F1}ms"
            },
            new()
            {
                Position = 2.0,
                Value = worst.TimeToFirstResultStats.Mean,
                FillColor = Colors.LightCoral,
                Label = $"WORST First Result: {worst.TimeToFirstResultStats.Mean:F1}ms"
            },
            new()
            {
                Position = 2.8,
                Value = worst.TotalDurationStats.Mean,
                Error = Math.Min(worst.TotalDurationStats.MarginOfError95, worst.TotalDurationStats.Mean),
                FillColor = Colors.IndianRed,
                Label = $"WORST Total: {worst.TotalDurationStats.Mean:F1}ms"
            }
        };

        plot.Add.Bars(bars);
        plot.Axes.Bottom.SetTicks(new[] { 0.4, 2.4 }, new[] { "BEST Configuration", "WORST Configuration" });

        SavePlot(plot, outputPath);
    }

    // =========================================================================
    // DUAL-AXIS STAGE RESOURCE CHARTS (CPU % on Left, RAM MB on Right)
    // =========================================================================
    public static void GenerateStageResourceChart(IReadOnlyList<ConfigurationBenchmarkSummary> summaries, string stageTitle, string outputPath)
    {
        var plot = new Plot();
        plot.Title($"{stageTitle}: System Resources (CPU % & Working Set MB)");
        plot.Axes.Left.Label.Text = "CPU Utilization (%)";
        plot.Axes.Left.MinimumSize = 65;

        var rightAxis = plot.Axes.Right;
        rightAxis.Label.Text = "Total Process Working Set (MB)";
        rightAxis.MinimumSize = 90;

        var barsLeft = new List<Bar>();
        var barsRight = new List<Bar>();
        var tickPositions = new List<double>();
        var tickLabels = new List<string>();

        double groupSpacing = 2.6;
        double barWidth = 0.8;

        for (int i = 0; i < summaries.Count; i++)
        {
            var s = summaries[i];
            double groupCenter = i * groupSpacing;
            tickPositions.Add(groupCenter + 0.4);
            tickLabels.Add(GetConfigLabel(s, stageTitle));

            var cpuError = Math.Min(s.CpuPercentageStats.MarginOfError95, s.CpuPercentageStats.Mean);
            barsLeft.Add(new Bar
            {
                Position = groupCenter,
                Value = s.CpuPercentageStats.Mean,
                Error = cpuError,
                FillColor = Colors.CornflowerBlue,
                Label = $"{s.CpuPercentageStats.Mean:F1}%"
            });

            var ramError = Math.Min(s.TotalWorkingSetMbStats.MarginOfError95, s.TotalWorkingSetMbStats.Mean);
            barsRight.Add(new Bar
            {
                Position = groupCenter + barWidth,
                Value = s.TotalWorkingSetMbStats.Mean,
                Error = ramError,
                FillColor = Colors.Teal,
                Label = $"{s.TotalWorkingSetMbStats.Mean:F0}MB"
            });
        }

        if (barsLeft.Count > 0)
        {
            var barPlotLeft = plot.Add.Bars(barsLeft);
            barPlotLeft.Axes.YAxis = plot.Axes.Left;

            var barPlotRight = plot.Add.Bars(barsRight);
            barPlotRight.Axes.YAxis = rightAxis;

            plot.Axes.Bottom.SetTicks(tickPositions.ToArray(), tickLabels.ToArray());
            plot.Axes.Margins(bottom: 0, left: 0.1, right: 0.1);

            double maxCpu = Math.Max(15, barsLeft.Max(b => b.Value + b.Error) * 1.15);
            double maxRam = Math.Max(100, barsRight.Max(b => b.Value + b.Error) * 1.15);
            plot.Axes.SetLimitsY(0, maxCpu, plot.Axes.Left);
            plot.Axes.SetLimitsY(0, maxRam, rightAxis);
        }
        SavePlot(plot, outputPath);
    }

    private static string GetConfigLabel(ConfigurationBenchmarkSummary s, string stageTitle)
    {
        if (stageTitle.Contains("Transport", StringComparison.OrdinalIgnoreCase))
        {
            return s.Transport.ToString();
        }
        if (stageTitle.Contains("Serialization", StringComparison.OrdinalIgnoreCase))
        {
            return s.Serialization.ToString();
        }
        if (stageTitle.Contains("Search Mode", StringComparison.OrdinalIgnoreCase))
        {
            return s.SearchMode.ToString();
        }
        if (stageTitle.Contains("Resolver", StringComparison.OrdinalIgnoreCase) || stageTitle.Contains("Cache", StringComparison.OrdinalIgnoreCase))
        {
            var resolverShort = s.ImageResolver switch
            {
                ImageResolverKind.ConcurrentDictionary => "ConcDict",
                ImageResolverKind.Dictionary => "Dictionary",
                ImageResolverKind.MemoryCache => "MemCache",
                ImageResolverKind.List => "List",
                _ => s.ImageResolver.ToString()
            };
            return $"{resolverShort}\n({(s.UseCache ? "ON" : "OFF")})";
        }
        if (stageTitle.Contains("Final", StringComparison.OrdinalIgnoreCase))
        {
            return s.UseCache ? "BEST" : "WORST";
        }
        return $"{s.Transport}\n{s.SearchMode}";
    }

    private static void SavePlot(Plot plot, string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        plot.Axes.Left.MinimumSize = Math.Max(plot.Axes.Left.MinimumSize, 65);
        plot.Axes.Right.MinimumSize = Math.Max(plot.Axes.Right.MinimumSize, 90);

        if (outputPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            plot.SaveSvg(outputPath, 1000, 540);
        }
        else
        {
            plot.SavePng(outputPath, 1000, 540);
        }
    }
}
