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

    public static void GenerateTransportLatencyChart(IReadOnlyList<ConfigurationBenchmarkSummary> summaries, string outputPath)
    {
        var plot = new Plot();
        plot.Title("Latency by Transport & Serialization (Mean ms with 95% CI)");
        plot.YLabel("Mean Duration (ms)");

        var transports = Enum.GetValues<ProviderTransportKind>();
        var serializations = Enum.GetValues<SerializationKind>();

        var bars = new List<Bar>();
        var tickPositions = new List<double>();
        var tickLabels = new List<string>();

        double groupSpacing = serializations.Length + 1.0;
        double barWidth = 0.8;

        for (int t = 0; t < transports.Length; t++)
        {
            var transport = transports[t];
            double groupCenter = t * groupSpacing;
            tickPositions.Add(groupCenter + (serializations.Length * barWidth / 2.0));
            tickLabels.Add(transport.ToString());

            for (int s = 0; s < serializations.Length; s++)
            {
                var serialization = serializations[s];
                var matching = summaries
                    .Where(sm => sm.Transport == transport && sm.Serialization == serialization)
                    .ToList();

                var mean = matching.Count > 0 ? matching.Average(m => m.TotalDurationStats.Mean) : 0;
                var err = matching.Count > 0 ? matching.Average(m => m.TotalDurationStats.MarginOfError95) : 0;

                bars.Add(new Bar
                {
                    Position = groupCenter + (s * barWidth),
                    Value = mean,
                    Error = err,
                    FillColor = Palette[s % Palette.Length],
                    Label = serialization.ToString()
                });
            }
        }

        if (bars.Count > 0)
        {
            plot.Add.Bars(bars);
            plot.Axes.Bottom.SetTicks(tickPositions.ToArray(), tickLabels.ToArray());
        }

        SavePlot(plot, outputPath);
    }

    public static void GenerateSearchModeLatencyChart(IReadOnlyList<ConfigurationBenchmarkSummary> summaries, string outputPath)
    {
        var plot = new Plot();
        plot.Title("Search Modes vs Latency (ms)");
        plot.YLabel("Mean Duration (ms)");

        var modes = Enum.GetValues<ProviderSearchMode>();
        var bars = new List<Bar>();
        var tickPositions = new List<double>();
        var tickLabels = new List<string>();

        for (int i = 0; i < modes.Length; i++)
        {
            var mode = modes[i];
            var matching = summaries
                .Where(s => s.SearchMode == mode)
                .ToList();

            var mean = matching.Count > 0 ? matching.Average(m => m.TotalDurationStats.Mean) : 0;
            var err = matching.Count > 0 ? matching.Average(m => m.TotalDurationStats.MarginOfError95) : 0;

            bars.Add(new Bar
            {
                Position = i,
                Value = mean,
                Error = err,
                FillColor = Palette[i % Palette.Length]
            });

            tickPositions.Add(i);
            tickLabels.Add(mode.ToString());
        }

        if (bars.Count > 0)
        {
            plot.Add.Bars(bars);
            plot.Axes.Bottom.SetTicks(tickPositions.ToArray(), tickLabels.ToArray());
        }

        SavePlot(plot, outputPath);
    }

    public static void GenerateWireBytesChart(IReadOnlyList<ConfigurationBenchmarkSummary> summaries, string outputPath)
    {
        var plot = new Plot();
        plot.Title("Payload Size by Serialization Format (Total Wire Bytes)");
        plot.YLabel("Total Wire Bytes");

        var serializations = Enum.GetValues<SerializationKind>();
        var bars = new List<Bar>();
        var tickPositions = new List<double>();
        var tickLabels = new List<string>();

        for (int i = 0; i < serializations.Length; i++)
        {
            var s = serializations[i];
            var matching = summaries
                .Where(sm => sm.Serialization == s)
                .ToList();

            var meanBytes = matching.Count > 0 ? matching.Average(m => m.TotalWireBytes) : 0;

            bars.Add(new Bar
            {
                Position = i,
                Value = meanBytes,
                FillColor = Palette[i % Palette.Length]
            });

            tickPositions.Add(i);
            tickLabels.Add(s.ToString());
        }

        if (bars.Count > 0)
        {
            plot.Add.Bars(bars);
            plot.Axes.Bottom.SetTicks(tickPositions.ToArray(), tickLabels.ToArray());
        }

        SavePlot(plot, outputPath);
    }

    public static void GenerateImageResolverComparisonChart(IReadOnlyList<ConfigurationBenchmarkSummary> summaries, string outputPath)
    {
        var plot = new Plot();
        plot.Title("Image Resolver Strategy vs Mean Resolution Duration (ms)");
        plot.YLabel("Image Resolution Duration (ms)");

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

            var cacheOn = summaries
                .Where(s => s.ImageResolver == resolver && s.UseCache)
                .ToList();
            var cacheOff = summaries
                .Where(s => s.ImageResolver == resolver && !s.UseCache)
                .ToList();

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

    private static void SavePlot(Plot plot, string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (outputPath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            plot.SaveSvg(outputPath, 900, 500);
        }
        else
        {
            plot.SavePng(outputPath, 900, 500);
        }
    }
}
