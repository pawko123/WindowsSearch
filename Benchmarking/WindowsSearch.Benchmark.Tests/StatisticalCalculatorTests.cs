using WindowsSearch.Benchmark.Harness.Statistics;

namespace WindowsSearch.Benchmark.Tests;

public class StatisticalCalculatorTests
{
    [Fact]
    public void Calculate_WithEmptyList_ReturnsZeroStats()
    {
        var stats = StatisticalCalculator.Calculate([]);
        Assert.Equal(0, stats.Count);
        Assert.Equal(0, stats.Mean);
        Assert.Equal(0, stats.Median);
    }

    [Fact]
    public void Calculate_WithSingleValue_ReturnsExactValue()
    {
        var stats = StatisticalCalculator.Calculate([42.0]);
        Assert.Equal(1, stats.Count);
        Assert.Equal(42.0, stats.Mean);
        Assert.Equal(42.0, stats.Median);
        Assert.Equal(42.0, stats.Min);
        Assert.Equal(42.0, stats.Max);
        Assert.Equal(0, stats.StandardDeviation);
    }

    [Fact]
    public void Calculate_WithMultipleValues_ComputesAccurateMetrics()
    {
        var data = new double[] { 10, 20, 30, 40, 50 };
        var stats = StatisticalCalculator.Calculate(data);

        Assert.Equal(5, stats.Count);
        Assert.Equal(30.0, stats.Mean);
        Assert.Equal(30.0, stats.Median);
        Assert.Equal(10.0, stats.Min);
        Assert.Equal(50.0, stats.Max);
        Assert.True(stats.StandardDeviation > 0);
        Assert.True(stats.MarginOfError95 > 0);
    }

    [Fact]
    public void Calculate_WithOutliers_ComputesCorrectPercentiles()
    {
        var data = Enumerable.Range(1, 100).Select(i => (double)i).ToList();
        var stats = StatisticalCalculator.Calculate(data);

        Assert.Equal(100, stats.Count);
        Assert.Equal(50.5, stats.Mean);
        Assert.Equal(50.5, stats.Median);
        Assert.Equal(1.0, stats.Min);
        Assert.Equal(100.0, stats.Max);
        Assert.True(stats.P90 >= 90);
        Assert.True(stats.P95 >= 95);
        Assert.True(stats.P99 >= 99);
    }
}
