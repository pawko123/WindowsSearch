using System.Diagnostics;
using System.IO;
using WindowsSearch.Benchmark.Core.Models;
using WindowsSearch.Benchmark.Harness.Client;
using WindowsSearch.Benchmark.Harness.Config;
using WindowsSearch.Benchmark.Harness.Monitoring;
using WindowsSearch.Benchmark.Harness.Statistics;
using WindowsSearch.Common.Logging;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Models.Benchmark;
using WindowsSearch.Common.Serialization;

namespace WindowsSearch.Benchmark.Harness.Execution;

public sealed class BenchmarkEngine
{
    private readonly BenchmarkConfiguration configuration;
    public event Action<string>? Progress;

    public BenchmarkEngine(BenchmarkConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public async Task<(List<SingleRunBenchmarkResult> Runs, List<ConfigurationBenchmarkSummary> Summaries)> RunAsync(CancellationToken cancellationToken = default)
    {
        var allRuns = new List<SingleRunBenchmarkResult>();
        var allSummaries = new List<ConfigurationBenchmarkSummary>();

        using var offlineManager = new OfflineSettingsManager();
        if (configuration.DisableWebSearchOnBrowsers)
        {
            Log("Disabling browser web search options for deterministic local testing...");
            offlineManager.ApplyOfflineSettings();
        }

        Process? hubProcess = null;
        var spawnedHub = false;

        try
        {
            await using var client = new HubBenchmarkClient(configuration.ControlPipeName);

            try
            {
                await client.ConnectAsync(1500, cancellationToken);
                Log("Connected to existing running Hub instance.");
            }
            catch
            {
                Log("Starting Hub instance in benchmark mode...");
                hubProcess = StartHubProcess(configuration.HubExecutablePath, configuration.ControlPipeName);
                spawnedHub = true;
                await Task.Delay(2000, cancellationToken);
                await client.ConnectAsync(15000, cancellationToken);
                Log("Successfully connected to newly started Hub instance.");
            }

            var pingResponse = await client.PingAsync(cancellationToken);
            var hubPid = pingResponse.ProcessId > 0 ? pingResponse.ProcessId : (hubProcess?.Id ?? 0);
            Log($"Hub is active (PID: {hubPid}).");

            var monitor = hubPid > 0 ? new ProcessTreeMonitor(hubPid) : null;

            foreach (var transport in configuration.Transports)
            {
                foreach (var serialization in configuration.Serializations)
                {
                    foreach (var searchMode in configuration.SearchModes)
                    {
                        foreach (var imageResolver in configuration.ImageResolvers)
                        {
                            foreach (var useCache in configuration.UseCacheOptions)
                            {
                                Log($"\n--- Testing Config: {transport} | {serialization} | {searchMode} | {imageResolver} (Cache: {useCache}) ---");

                                var applyCmd = new BenchmarkControlCommand
                                {
                                    TransportKind = transport,
                                    SerializationKind = serialization,
                                    SearchMode = searchMode,
                                    ImageResolverKind = imageResolver,
                                    UseCache = useCache
                                };
                                await client.ApplySettingsAsync(applyCmd, cancellationToken);
                                await Task.Delay(300, cancellationToken);

                                foreach (var query in configuration.Queries)
                                {
                                    Log($"  Query '{query}': Running {configuration.WarmupRuns} warmup runs...");
                                    for (int w = 1; w <= configuration.WarmupRuns; w++)
                                    {
                                        var warmupResp = await client.SearchAsync(query, configuration.Limit, configuration.BypassDebounce, $"warmup_{w}", cancellationToken);
                                        allRuns.Add(new SingleRunBenchmarkResult
                                        {
                                            RunId = $"warmup_{w}",
                                            Iteration = w,
                                            IsWarmup = true,
                                            Transport = transport,
                                            Serialization = serialization,
                                            SearchMode = searchMode,
                                            ImageResolver = imageResolver,
                                            UseCache = useCache,
                                            Query = query,
                                            Report = warmupResp.Report
                                        });
                                    }

                                    Log($"  Query '{query}': Running {configuration.MeasurementRuns} measured runs...");
                                    var queryMeasuredRuns = new List<SingleRunBenchmarkResult>();

                                    for (int m = 1; m <= configuration.MeasurementRuns; m++)
                                    {
                                        var runId = $"run_{transport}_{serialization}_{query}_{m}";
                                        var resourceBefore = monitor?.Sample();

                                        var searchResp = await client.SearchAsync(query, configuration.Limit, configuration.BypassDebounce, runId, cancellationToken);

                                        var resourceAfter = monitor?.Sample() ?? resourceBefore;

                                        var result = new SingleRunBenchmarkResult
                                        {
                                            RunId = runId,
                                            Iteration = m,
                                            IsWarmup = false,
                                            Transport = transport,
                                            Serialization = serialization,
                                            SearchMode = searchMode,
                                            ImageResolver = imageResolver,
                                            UseCache = useCache,
                                            Query = query,
                                            Report = searchResp.Report,
                                            ResourceSample = resourceAfter
                                        };

                                        queryMeasuredRuns.Add(result);
                                        allRuns.Add(result);
                                    }

                                    var summary = AggregateRuns(transport, serialization, searchMode, imageResolver, useCache, query, queryMeasuredRuns);
                                    allSummaries.Add(summary);
                                    Log($"  -> Mean Duration: {summary.TotalDurationStats.Mean}ms (P50: {summary.TotalDurationStats.Median}ms, P95: {summary.TotalDurationStats.P95}ms)");
                                }
                            }
                        }
                    }
                }
            }

            if (spawnedHub)
            {
                Log("Shutting down spawned Hub instance...");
                try { await client.ShutdownAsync(cancellationToken); } catch { }
            }
        }
        finally
        {
            if (spawnedHub && hubProcess is { HasExited: false })
            {
                try { hubProcess.Kill(entireProcessTree: true); } catch { }
            }
        }

        return (allRuns, allSummaries);
    }

    private ConfigurationBenchmarkSummary AggregateRuns(
        ProviderTransportKind transport,
        SerializationKind serialization,
        ProviderSearchMode searchMode,
        ImageResolverKind imageResolver,
        bool useCache,
        string query,
        List<SingleRunBenchmarkResult> runs)
    {
        var totalDurations = runs.Where(r => r.Report != null).Select(r => r.Report!.TotalWallClockMs).ToList();
        var aggregations = runs.Where(r => r.Report != null).Select(r => r.Report!.TotalAggregationMs).ToList();
        var imageResolutions = runs.Where(r => r.Report != null).Select(r => r.Report!.TotalImageResolutionMs).ToList();
        var cpuSamples = runs.Where(r => r.ResourceSample != null).Select(r => r.ResourceSample!.CpuPercentage).ToList();
        var hubWorkingSets = runs.Where(r => r.ResourceSample != null).Select(r => r.ResourceSample!.HubWorkingSetMb).ToList();
        var totalWorkingSets = runs.Where(r => r.ResourceSample != null).Select(r => r.ResourceSample!.TotalWorkingSetMb).ToList();

        var summary = new ConfigurationBenchmarkSummary
        {
            Transport = transport,
            Serialization = serialization,
            SearchMode = searchMode,
            ImageResolver = imageResolver,
            UseCache = useCache,
            Query = query,
            TotalDurationStats = StatisticalCalculator.Calculate(totalDurations),
            AggregationStats = StatisticalCalculator.Calculate(aggregations),
            ImageResolutionStats = StatisticalCalculator.Calculate(imageResolutions),
            CpuPercentageStats = StatisticalCalculator.Calculate(cpuSamples),
            HubWorkingSetMbStats = StatisticalCalculator.Calculate(hubWorkingSets),
            TotalWorkingSetMbStats = StatisticalCalculator.Calculate(totalWorkingSets)
        };

        var allProviders = runs.Where(r => r.Report != null)
            .SelectMany(r => r.Report!.ProviderMetrics)
            .Select(p => p.ProviderName)
            .Distinct();

        foreach (var provider in allProviders)
        {
            var providerTimes = runs.Where(r => r.Report != null)
                .SelectMany(r => r.Report!.ProviderMetrics)
                .Where(p => p.ProviderName == provider)
                .Select(p => p.TransportRoundtripMs)
                .ToList();

            var providerBytes = runs.Where(r => r.Report != null)
                .SelectMany(r => r.Report!.ProviderMetrics)
                .Where(p => p.ProviderName == provider)
                .Select(p => (double)(p.RequestPayloadBytes + p.ResponsePayloadBytes))
                .ToList();

            summary.ProviderDurationStats[provider] = StatisticalCalculator.Calculate(providerTimes);
            summary.ProviderWireBytesStats[provider] = StatisticalCalculator.Calculate(providerBytes);
        }

        summary.TotalWireBytes = runs.Where(r => r.Report != null)
            .SelectMany(r => r.Report!.ProviderMetrics)
            .Sum(p => p.RequestPayloadBytes + p.ResponsePayloadBytes);

        return summary;
    }

    private void Log(string message)
    {
        Progress?.Invoke(message);
        AppLogger.Info($"[BenchmarkEngine] {message}");
    }

    private static Process StartHubProcess(string? customPath, string controlPipe)
    {
        var executablePath = customPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            executablePath = FindHubExecutable();
        }

        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new FileNotFoundException($"Hub executable not found at '{executablePath}'. Please build Hub first or specify --hub-path.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = $"--benchmark-mode {controlPipe}",
            WorkingDirectory = Path.GetDirectoryName(executablePath),
            UseShellExecute = false,
            CreateNoWindow = true
        };

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to launch Hub process.");
    }

    private static string FindHubExecutable()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidates = new[]
            {
                Path.Combine(dir, "Hub", "bin", "Debug", "net10.0-windows", "Hub.exe"),
                Path.Combine(dir, "Hub", "bin", "Release", "net10.0-windows", "Hub.exe"),
                Path.Combine(dir, "Hub", "Hub", "bin", "Debug", "net10.0-windows", "Hub.exe"),
                Path.Combine(dir, "Hub", "Hub", "bin", "Release", "net10.0-windows", "Hub.exe")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            var parent = Directory.GetParent(dir);
            if (parent is null) break;
            dir = parent.FullName;
        }

        return string.Empty;
    }
}
