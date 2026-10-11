using WindowsSearch.Benchmark.Core.Models;

namespace WindowsSearch.Benchmark.Harness.Statistics;

public static class StatisticalCalculator
{
    public static MetricStatistics Calculate(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return new MetricStatistics();
        }

        var sorted = values.OrderBy(v => v).ToList();
        var count = sorted.Count;
        var mean = sorted.Average();
        var min = sorted[0];
        var max = sorted[^1];
        var median = GetPercentile(sorted, 0.50);
        var p90 = GetPercentile(sorted, 0.90);
        var p95 = GetPercentile(sorted, 0.95);
        var p99 = GetPercentile(sorted, 0.99);

        double stdDev = 0;
        if (count > 1)
        {
            var sumOfSquares = sorted.Sum(v => Math.Pow(v - mean, 2));
            stdDev = Math.Sqrt(sumOfSquares / (count - 1));
        }

        var marginOfError = count > 1 ? 1.96 * (stdDev / Math.Sqrt(count)) : 0;

        return new MetricStatistics
        {
            Count = count,
            Mean = Math.Round(mean, 2),
            Median = Math.Round(median, 2),
            P90 = Math.Round(p90, 2),
            P95 = Math.Round(p95, 2),
            P99 = Math.Round(p99, 2),
            Min = Math.Round(min, 2),
            Max = Math.Round(max, 2),
            StandardDeviation = Math.Round(stdDev, 2),
            MarginOfError95 = Math.Round(marginOfError, 2)
        };
    }

    private static double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        if (sortedValues.Count == 1)
        {
            return sortedValues[0];
        }

        var position = (sortedValues.Count - 1) * percentile;
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);

        if (lowerIndex == upperIndex)
        {
            return sortedValues[lowerIndex];
        }

        var fraction = position - lowerIndex;
        return sortedValues[lowerIndex] + (sortedValues[upperIndex] - sortedValues[lowerIndex]) * fraction;
    }
}
