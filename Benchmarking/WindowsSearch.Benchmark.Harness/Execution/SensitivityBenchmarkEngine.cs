using System.Diagnostics;
using WindowsSearch.Common.Security;
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

public sealed class SensitivityBenchmarkEngine
{
    private readonly BenchmarkConfiguration configuration;
    public event Action<string>? Progress;

    public SensitivityBenchmarkEngine(BenchmarkConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public async Task<SensitivityBenchmarkResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var result = new SensitivityBenchmarkResult();
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
            Log($"Hub is active (PID: {hubPid}).\n");

            var monitor = hubPid > 0 ? new ProcessTreeMonitor(hubPid) : null;

            // Baseline defaults:
            var baseTransport = ProviderTransportKind.NamedPipe;
            var baseSerialization = SerializationKind.Json;
            var baseMode = ProviderSearchMode.ConcurrentStream;
            var baseResolver = ImageResolverKind.ConcurrentDictionary;
            var baseCache = true;

            // ==========================================
            // STAGE 1: Transport Sensitivity Experiment
            // ==========================================
            Log("═════════════════════════════════════════════════════════════");
            Log("  STAGE 1: Transport Sensitivity (Holding others at baseline)");
            Log("═════════════════════════════════════════════════════════════");
            foreach (var transport in configuration.Transports)
            {
                Log($"Testing Transport: {transport}...");
                var summary = await RunConfigurationTestAsync(client, monitor, transport, baseSerialization, baseMode, baseResolver, baseCache, result.AllRuns, cancellationToken);
                result.TransportSummaries.Add(summary);
                Log($"  -> Total Search Latency: {summary.TotalDurationStats.Mean:F2} ms (P50: {summary.TotalDurationStats.Median:F2} ms) | Avg Provider Call: {summary.AverageProviderDurationMs:F2} ms");
            }

            var bestTransport = result.TransportSummaries.OrderBy(s => s.AverageProviderDurationMs).First().Transport;
            var worstTransport = result.TransportSummaries.OrderByDescending(s => s.AverageProviderDurationMs).First().Transport;
            var bestProvMean = result.TransportSummaries.First(s => s.Transport == bestTransport).AverageProviderDurationMs;
            var worstProvMean = result.TransportSummaries.First(s => s.Transport == worstTransport).AverageProviderDurationMs;
            var provSpeedup = Math.Round(Math.Max(0.01, worstProvMean) / Math.Max(0.01, bestProvMean), 2);
            Log($"[Stage 1 Outcome] Best Transport: {bestTransport} (Avg Provider Call: {bestProvMean:F2}ms) | Worst: {worstTransport} (Avg: {worstProvMean:F2}ms) -> {provSpeedup}x FASTER PER PROVIDER CALL");

            ComputeProviderTransportGains(result, configuration.Queries, result.AllRuns);
            if (result.ProviderTransportGains.Count > 0)
            {
                Log("  -------------------------------------------------------------");
                Log("  STAGE 1 PROVIDER CALL GAIN MATRIX (NamedPipe as Baseline):");
                foreach (var g in result.ProviderTransportGains)
                {
                    var pureHttpText = g.HttpPureTransportMs > 0 ? $"{g.HttpPureTransportMs:F2}ms ({g.PureGainPercentVsHttp:+0.0;-0.0}%)" : "N/A";
                    var overallHttpText = g.HttpMeanMs > 0 ? $"{g.HttpMeanMs:F2}ms ({g.GainPercentVsHttp:+0.0;-0.0}%)" : "N/A";
                    var pureGrpcText = g.GrpcPureTransportMs > 0 ? $"{g.GrpcPureTransportMs:F2}ms ({g.PureGainPercentVsGrpc:+0.0;-0.0}%)" : "N/A";
                    var overallGrpcText = g.GrpcMeanMs > 0 ? $"{g.GrpcMeanMs:F2}ms ({g.GainPercentVsGrpc:+0.0;-0.0}%)" : "N/A";

                    Log($"  * {g.ProviderName,-16} | Exec: {g.ProviderExecutionMeanMs:F2}ms | NP Pure/Tot: {g.NamedPipePureTransportMs:F2}ms / {g.NamedPipeMeanMs:F2}ms");
                    Log($"    - HTTP Pure: {pureHttpText,-18} | Overall (Amdahl): {overallHttpText}");
                    Log($"    - gRPC Pure: {pureGrpcText,-18} | Overall (Amdahl): {overallGrpcText}");
                }
                Log($"  * OVERALL PURE IPC GAIN: vs HTTP: {result.OverallPureTransportGainPercentVsHttp:+0.0;-0.0}% | vs gRPC: {result.OverallPureTransportGainPercentVsGrpc:+0.0;-0.0}%");
                Log($"  * OVERALL END-TO-END GAIN (Amdahl's Law): vs HTTP: {result.OverallProviderGainPercentVsHttp:+0.0;-0.0}% | vs gRPC: {result.OverallProviderGainPercentVsGrpc:+0.0;-0.0}%");
                Log("  -------------------------------------------------------------\n");
            }

            // ==============================================
            // STAGE 2: Serialization Sensitivity Experiment
            // ==============================================
            Log("═════════════════════════════════════════════════════════════");
            Log("  STAGE 2: Serialization Sensitivity (Holding others at baseline)");
            Log("═════════════════════════════════════════════════════════════");
            foreach (var serialization in configuration.Serializations)
            {
                Log($"Testing Serialization: {serialization}...");
                var summary = await RunConfigurationTestAsync(client, monitor, baseTransport, serialization, baseMode, baseResolver, baseCache, result.AllRuns, cancellationToken);
                result.SerializationSummaries.Add(summary);
                Log($"  -> Mean Latency: {summary.TotalDurationStats.Mean} ms | Total Wire Bytes: {summary.TotalWireBytes} B");
            }

            var bestSerialization = result.SerializationSummaries.OrderBy(s => s.TotalWireBytes * s.TotalDurationStats.Mean).First().Serialization;
            var worstSerialization = result.SerializationSummaries.OrderByDescending(s => s.TotalWireBytes).First().Serialization;
            Log($"[Stage 2 Outcome] Best Serialization: {bestSerialization} | Worst Serialization: {worstSerialization}\n");

            // ==============================================
            // STAGE 2B: Encryption Sensitivity Experiment
            // ==============================================
            Log("═════════════════════════════════════════════════════════════");
            Log("  STAGE 2B: Encryption Sensitivity (Holding others at baseline)");
            Log("═════════════════════════════════════════════════════════════");
            foreach (var encryption in configuration.Encryptions)
            {
                Log($"Testing Encryption: {encryption}...");
                var summary = await RunConfigurationTestAsync(client, monitor, baseTransport, baseSerialization, baseMode, baseResolver, baseCache, result.AllRuns, cancellationToken, encryption: encryption);
                result.EncryptionSummaries.Add(summary);
                Log($"  -> Mean Latency: {summary.TotalDurationStats.Mean:F2} ms | CPU Overhead: {summary.EncryptionCpuStats.Mean:F2} ms | Total Wire Bytes: {summary.TotalWireBytes} B");
            }

            var baselineEncryptionSummary = result.EncryptionSummaries.FirstOrDefault(s => s.Encryption == EncryptionKind.None)
                ?? result.EncryptionSummaries.First();
            var bestEncryption = result.EncryptionSummaries.OrderBy(s => s.TotalDurationStats.Mean).First().Encryption;
            var worstEncryption = result.EncryptionSummaries.OrderByDescending(s => s.TotalDurationStats.Mean).First().Encryption;
            Log($"[Stage 2B Outcome] Baseline: None ({baselineEncryptionSummary.TotalDurationStats.Mean:F2} ms) | Best: {bestEncryption} | Worst: {worstEncryption}\n");

            // ==============================================
            // STAGE 3: Search Mode Concurrency Experiment
            // ==============================================
            Log("═════════════════════════════════════════════════════════════");
            Log("  STAGE 3: Search Mode Concurrency (Holding others at baseline)");
            Log("═════════════════════════════════════════════════════════════");
            foreach (var mode in configuration.SearchModes)
            {
                Log($"Testing Search Mode: {mode}...");
                var summary = await RunConfigurationTestAsync(client, monitor, baseTransport, baseSerialization, mode, baseResolver, baseCache, result.AllRuns, cancellationToken);
                result.SearchModeSummaries.Add(summary);
                Log($"  -> Mean Latency: {summary.TotalDurationStats.Mean} ms (P50: {summary.TotalDurationStats.Median} ms)");
            }

            var bestMode = result.SearchModeSummaries.OrderBy(s => s.TotalDurationStats.Mean).First().SearchMode;
            var worstMode = result.SearchModeSummaries.OrderByDescending(s => s.TotalDurationStats.Mean).First().SearchMode;
            Log($"[Stage 3 Outcome] Best Search Mode: {bestMode} | Worst Search Mode: {worstMode}\n");

            // ======================================================
            // STAGE 4: Image Resolver & Cache Sensitivity Experiment
            // ======================================================
            Log("═════════════════════════════════════════════════════════════");
            Log("  STAGE 4: Image Resolver & Cache Sensitivity (Holding others at baseline)");
            Log("═════════════════════════════════════════════════════════════");
            foreach (var resolver in configuration.ImageResolvers)
            {
                foreach (var useCache in configuration.UseCacheOptions)
                {
                    Log($"Testing Image Resolver: {resolver} (Cache: {useCache})...");
                    var summary = await RunResolverStressTestAsync(client, monitor, resolver, useCache, result.AllRuns, cancellationToken);
                    result.ImageResolverSummaries.Add(summary);
                    Log($"  -> Mean Resolution Time: {summary.ImageResolutionStats.Mean} ms | Total Duration: {summary.TotalDurationStats.Mean} ms");
                }
            }

            var bestResolverSummary = result.ImageResolverSummaries.Where(s => s.UseCache).OrderBy(s => s.ImageResolutionStats.Mean).FirstOrDefault()
                ?? result.ImageResolverSummaries.OrderBy(s => s.ImageResolutionStats.Mean).First();
            var worstResolverSummary = result.ImageResolverSummaries.Where(s => !s.UseCache).OrderByDescending(s => s.ImageResolutionStats.Mean).FirstOrDefault()
                ?? result.ImageResolverSummaries.OrderByDescending(s => s.ImageResolutionStats.Mean).First();
            var bestResolver = bestResolverSummary.ImageResolver;
            var worstResolver = worstResolverSummary.ImageResolver;
            Log($"[Stage 4 Outcome] Best Resolver: {bestResolver} (Cache: ON) | Worst Resolver: {worstResolver} (Cache: OFF)\n");

            // ======================================================
            // STAGE 5: Grand Final Comparison (Best vs Worst Settings)
            // ======================================================
            Log("═════════════════════════════════════════════════════════════");
            Log("  STAGE 5: Grand Final Comparison (Best vs Worst Settings)");
            Log("═════════════════════════════════════════════════════════════");

            var finalQueries = configuration.Queries.Concat(new[] { "code", "demo", "idea", "search" }).Distinct().ToList();
            Log($"Running expanded test suite ({finalQueries.Count} queries: {string.Join(", ", finalQueries)})...");

            Log($"Running BEST Configuration: {bestTransport} | {bestSerialization} | {bestMode} | {bestResolver} (Cache: ON)...");
            result.BestSummary = await RunConfigurationTestAsync(client, monitor, bestTransport, bestSerialization, bestMode, bestResolver, true, result.AllRuns, cancellationToken, finalQueries);
            Log($"  [BEST] Mean Latency: {result.BestSummary.TotalDurationStats.Mean} ms | TTFR: {result.BestSummary.TimeToFirstResultStats.Mean} ms | Wire: {result.BestSummary.TotalWireBytes} B");

            Log($"Running WORST Configuration: {worstTransport} | {worstSerialization} | {worstMode} | {worstResolver} (Cache: OFF)...");
            result.WorstSummary = await RunConfigurationTestAsync(client, monitor, worstTransport, worstSerialization, worstMode, worstResolver, false, result.AllRuns, cancellationToken, finalQueries);
            Log($"  [WORST] Mean Latency: {result.WorstSummary.TotalDurationStats.Mean} ms | TTFR: {result.WorstSummary.TimeToFirstResultStats.Mean} ms | Wire: {result.WorstSummary.TotalWireBytes} B");

            var bestMean = Math.Max(0.1, result.BestSummary.TotalDurationStats.Mean);
            var worstMean = Math.Max(0.1, result.WorstSummary.TotalDurationStats.Mean);
            result.SpeedupFactor = Math.Round(worstMean / bestMean, 2);

            var bestTtfr = Math.Max(0.1, result.BestSummary.TimeToFirstResultStats.Mean);
            var worstTtfr = Math.Max(0.1, result.WorstSummary.TimeToFirstResultStats.Mean);
            result.TimeToFirstResultSpeedup = Math.Round(worstTtfr / bestTtfr, 2);

            var bestBytes = Math.Max(1, result.BestSummary.TotalWireBytes);
            var worstBytes = Math.Max(1, result.WorstSummary.TotalWireBytes);
            result.WireReductionPercent = Math.Round(((double)(worstBytes - bestBytes) / worstBytes) * 100.0, 2);

            var bestImg = Math.Max(0.01, result.BestSummary.ImageResolutionStats.Mean);
            var worstImg = Math.Max(0.01, result.WorstSummary.ImageResolutionStats.Mean);
            result.CacheAccelerationRatio = Math.Round(worstImg / bestImg, 2);

            var providerSpeedups = new List<double>();
            foreach (var (provider, worstStats) in result.WorstSummary.ProviderDurationStats)
            {
                if (result.BestSummary.ProviderDurationStats.TryGetValue(provider, out var bestStats))
                {
                    var bMean = Math.Max(0.01, bestStats.Mean);
                    var wMean = Math.Max(0.01, worstStats.Mean);
                    var speedup = Math.Round(wMean / bMean, 2);
                    result.PerProviderSpeedup[provider] = speedup;
                    providerSpeedups.Add(speedup);
                }
            }

            result.AverageProviderSpeedup = providerSpeedups.Count > 0
                ? Math.Round(providerSpeedups.Average(), 2)
                : 1.0;

            Log("═════════════════════════════════════════════════════════════");
            Log("  FINAL GAIN METRICS FOR MASTER THESIS:");
            Log($"  - Speedup Factor (Total Duration):         {result.SpeedupFactor}x FASTER");
            Log($"  - Responsiveness Speedup (First Result):   {result.TimeToFirstResultSpeedup}x FASTER");
            Log($"  - Average Per-Provider Speedup:            {result.AverageProviderSpeedup}x FASTER");
            foreach (var (provider, speedup) in result.PerProviderSpeedup)
            {
                Log($"     * {provider}: {speedup}x faster");
            }
            Log($"  - Wire Payload Reduction:                  {result.WireReductionPercent}% LESS DATA");
            Log($"  - Cache Acceleration Ratio:                {result.CacheAccelerationRatio}x FASTER ICON RESOLUTION");
            Log("═════════════════════════════════════════════════════════════\n");

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

        return result;
    }

    private async Task<ConfigurationBenchmarkSummary> RunConfigurationTestAsync(
        HubBenchmarkClient client,
        ProcessTreeMonitor? monitor,
        ProviderTransportKind transport,
        SerializationKind serialization,
        ProviderSearchMode searchMode,
        ImageResolverKind resolver,
        bool useCache,
        List<SingleRunBenchmarkResult> allRuns,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? customQueries = null,
        EncryptionKind encryption = EncryptionKind.None)
    {
        await client.ResetCachesAsync(cancellationToken);
        await Task.Delay(200, cancellationToken);

        var applyCmd = new BenchmarkControlCommand
        {
            TransportKind = transport,
            SerializationKind = serialization,
            EncryptionKind = encryption,
            SearchMode = searchMode,
            ImageResolverKind = resolver,
            UseCache = useCache
        };
        await client.ApplySettingsAsync(applyCmd, cancellationToken);
        await Task.Delay(200, cancellationToken);

        var measuredRuns = new List<SingleRunBenchmarkResult>();
        var queriesToRun = customQueries ?? configuration.Queries;
        var firstQuery = queriesToRun.FirstOrDefault() ?? "git";

        // Execute 1st query on clean cache as the Cold Start
        var coldResp = await client.SearchAsync(firstQuery, configuration.Limit, configuration.BypassDebounce, $"cold_{transport}_{serialization}_{encryption}", cancellationToken);
        LogRun(firstQuery, $"cold_{transport}_{serialization}_{encryption}", isWarmup: true, coldResp.Report);
        allRuns.Add(new SingleRunBenchmarkResult
        {
            RunId = $"cold_{transport}_{serialization}_{encryption}",
            Iteration = 1,
            IsWarmup = true,
            Transport = transport,
            Serialization = serialization,
            Encryption = encryption,
            SearchMode = searchMode,
            ImageResolver = resolver,
            UseCache = useCache,
            Query = firstQuery + " (Cold)",
            Report = coldResp.Report
        });
        var coldDuration = coldResp.Report?.TotalWallClockMs ?? 0;
        var coldTtfr = coldResp.Report?.TimeToFirstResultMs ?? 0;
        var coldMisses = coldResp.Report?.ImageResolverMetrics?.CacheMisses ?? 0;

        foreach (var query in queriesToRun)
        {
            for (int w = 1; w <= configuration.WarmupRuns; w++)
            {
                var warmupResp = await client.SearchAsync(query, configuration.Limit, configuration.BypassDebounce, $"warmup_{w}", cancellationToken);
                LogRun(query, $"warmup_{w}", isWarmup: true, warmupResp.Report);
                allRuns.Add(new SingleRunBenchmarkResult
                {
                    RunId = $"warmup_{w}",
                    Iteration = w,
                    IsWarmup = true,
                    Transport = transport,
                    Serialization = serialization,
                    Encryption = encryption,
                    SearchMode = searchMode,
                    ImageResolver = resolver,
                    UseCache = useCache,
                    Query = query,
                    Report = warmupResp.Report
                });

            }
            for (int m = 1; m <= configuration.MeasurementRuns; m++)
            {
                var runId = $"run_{transport}_{serialization}_{searchMode}_{m}";
                var resourceBefore = monitor?.Sample();
                var searchResp = await client.SearchAsync(query, configuration.Limit, configuration.BypassDebounce, runId, cancellationToken);
                var resourceAfter = monitor?.Sample() ?? resourceBefore;
                LogRun(query, runId, isWarmup: false, searchResp.Report, resourceAfter);

                var singleRun = new SingleRunBenchmarkResult
                {
                    RunId = runId,
                    Iteration = m,
                    IsWarmup = false,
                    Transport = transport,
                    Serialization = serialization,
                    Encryption = encryption,
                    SearchMode = searchMode,
                    ImageResolver = resolver,
                    UseCache = useCache,
                    Query = query,
                    Report = searchResp.Report,
                    ResourceSample = resourceAfter
                };

                measuredRuns.Add(singleRun);
                allRuns.Add(singleRun);
            }
        }

        return AggregateRuns(transport, serialization, searchMode, resolver, useCache, string.Join(';', queriesToRun), measuredRuns, coldDuration, coldTtfr, coldMisses, encryption);
    }
    private async Task<ConfigurationBenchmarkSummary> RunResolverStressTestAsync(
        HubBenchmarkClient client,
        ProcessTreeMonitor? monitor,
        ImageResolverKind resolver,
        bool useCache,
        List<SingleRunBenchmarkResult> allRuns,
        CancellationToken cancellationToken)
    {
        await client.ResetCachesAsync(cancellationToken);
        await Task.Delay(200, cancellationToken);

        var applyCmd = new BenchmarkControlCommand
        {
            TransportKind = ProviderTransportKind.NamedPipe,
            SerializationKind = SerializationKind.Json,
            SearchMode = ProviderSearchMode.ConcurrentStream,
            ImageResolverKind = resolver,
            UseCache = useCache
        };
        await client.ApplySettingsAsync(applyCmd, cancellationToken);
        await Task.Delay(200, cancellationToken);
        var measuredRuns = new List<SingleRunBenchmarkResult>();

        var resourceBeforeCold = monitor?.Sample();
        var coldResp = await client.StressTestResolverAsync(cancellationToken);
        var resourceAfterCold = monitor?.Sample() ?? resourceBeforeCold;
        LogRun("AppIcons (Cold)", $"cold_{resolver}_{useCache}", isWarmup: true, coldResp.Report, resourceAfterCold);

        var coldRun = new SingleRunBenchmarkResult
        {
            RunId = $"cold_{resolver}_{useCache}",
            Iteration = 1,
            IsWarmup = true,
            Transport = ProviderTransportKind.NamedPipe,
            Serialization = SerializationKind.Json,
            SearchMode = ProviderSearchMode.ConcurrentStream,
            ImageResolver = resolver,
            UseCache = useCache,
            Query = "AppIcons (Cold)",
            Report = coldResp.Report,
            ResourceSample = resourceAfterCold
        };
        allRuns.Add(coldRun);

        for (int m = 1; m <= configuration.MeasurementRuns; m++)
        {
            var runId = $"run_resolver_{resolver}_{useCache}_{m}";
            var resourceBefore = monitor?.Sample();
            var warmResp = await client.StressTestResolverAsync(cancellationToken);
            var resourceAfter = monitor?.Sample() ?? resourceBefore;
            LogRun("AppIcons (Warm)", runId, isWarmup: false, warmResp.Report, resourceAfter);

            var warmRun = new SingleRunBenchmarkResult
            {
                RunId = runId,
                Iteration = m,
                IsWarmup = false,
                Transport = ProviderTransportKind.NamedPipe,
                Serialization = SerializationKind.Json,
                SearchMode = ProviderSearchMode.ConcurrentStream,
                ImageResolver = resolver,
                UseCache = useCache,
                Query = "AppIcons (Warm)",
                Report = warmResp.Report,
                ResourceSample = resourceAfter
            };
            measuredRuns.Add(warmRun);
            allRuns.Add(warmRun);
        }

        var coldDur = coldResp.Report?.TotalImageResolutionMs ?? 0;
        var coldMiss = coldResp.Report?.ImageResolverMetrics?.CacheMisses ?? 0;

        return AggregateRuns(ProviderTransportKind.NamedPipe, SerializationKind.Json, ProviderSearchMode.ConcurrentStream, resolver, useCache, "AppIcons", measuredRuns, coldDur, 0, coldMiss);
    }

    private static ConfigurationBenchmarkSummary AggregateRuns(
        ProviderTransportKind transport,
        SerializationKind serialization,
        ProviderSearchMode searchMode,
        ImageResolverKind imageResolver,
        bool useCache,
        string query,
        List<SingleRunBenchmarkResult> runs,
        double coldDuration = 0,
        double coldTtfr = 0,
        int coldMisses = 0,
        EncryptionKind encryption = EncryptionKind.None)
    {
        var totalDurations = runs.Where(r => r.Report != null).Select(r => r.Report!.TotalWallClockMs).ToList();
        var aggregations = runs.Where(r => r.Report != null).Select(r => r.Report!.TotalAggregationMs).ToList();
        var imageResolutions = runs.Where(r => r.Report != null).Select(r => r.Report!.TotalImageResolutionMs).ToList();
        var cpuSamples = runs.Where(r => r.ResourceSample != null).Select(r => r.ResourceSample!.CpuPercentage).ToList();
        var hubWorkingSets = runs.Where(r => r.ResourceSample != null).Select(r => r.ResourceSample!.HubWorkingSetMb).ToList();
        var totalWorkingSets = runs.Where(r => r.ResourceSample != null).Select(r => r.ResourceSample!.TotalWorkingSetMb).ToList();
        var hubPrivateMemories = runs.Where(r => r.ResourceSample != null).Select(r => r.ResourceSample!.HubPrivateMemoryMb).ToList();
        var totalPrivateMemories = runs.Where(r => r.ResourceSample != null).Select(r => r.ResourceSample!.TotalPrivateMemoryMb).ToList();

        var (rawDur, filteredDur, outliersCount) = StatisticalCalculator.CalculateWithOutlierFilter(totalDurations);
        var summary = new ConfigurationBenchmarkSummary
        {
            Transport = transport,
            Serialization = serialization,
            SearchMode = searchMode,
            ImageResolver = imageResolver,
            UseCache = useCache,
            Query = query,
            TriesCount = runs.Count,
            TotalDurationStats = rawDur,
            FilteredDurationStats = filteredDur,
            OutliersCount = outliersCount,
            TimeToFirstResultStats = StatisticalCalculator.Calculate(
                runs.Where(r => r.Report != null).Select(r => r.Report!.TimeToFirstResultMs).ToList()),
            AggregationStats = StatisticalCalculator.Calculate(aggregations),
            ImageResolutionStats = StatisticalCalculator.Calculate(imageResolutions),
            CpuPercentageStats = StatisticalCalculator.Calculate(cpuSamples),
            HubWorkingSetMbStats = StatisticalCalculator.Calculate(hubWorkingSets),
            TotalWorkingSetMbStats = StatisticalCalculator.Calculate(totalWorkingSets),
            HubPrivateMemoryMbStats = StatisticalCalculator.Calculate(hubPrivateMemories),
            TotalPrivateMemoryMbStats = StatisticalCalculator.Calculate(totalPrivateMemories),
            SerializationCpuStats = StatisticalCalculator.Calculate(
                runs.Where(r => r.Report != null)
                    .Select(r => (double)r.Report!.ProviderMetrics.Sum(p => p.RequestSerializationMs + p.ResponseDeserializationMs))
                    .ToList()),
            Encryption = encryption,
            EncryptionCpuStats = StatisticalCalculator.Calculate(
                runs.Where(r => r.Report != null)
                    .Select(r => (double)r.Report!.ProviderMetrics.Sum(p => p.TotalEncryptionMs))
                    .ToList()),
            RequestEncryptionStats = StatisticalCalculator.Calculate(
                runs.Where(r => r.Report != null)
                    .Select(r => (double)r.Report!.ProviderMetrics.Sum(p => p.RequestEncryptionMs + p.ProviderEncryptionMs))
                    .ToList()),
            ResponseDecryptionStats = StatisticalCalculator.Calculate(
                runs.Where(r => r.Report != null)
                    .Select(r => (double)r.Report!.ProviderMetrics.Sum(p => p.ProviderDecryptionMs + p.ResponseDecryptionMs))
                    .ToList()),
            TotalCacheHits = runs.Where(r => r.Report != null).Sum(r => r.Report!.ImageResolverMetrics.CacheHits),
            TotalCacheMisses = runs.Where(r => r.Report != null).Sum(r => r.Report!.ImageResolverMetrics.CacheMisses),
            CacheHitRatePercent = (runs.Where(r => r.Report != null).Sum(r => r.Report!.ImageResolverMetrics.CacheHits) +
                                   runs.Where(r => r.Report != null).Sum(r => r.Report!.ImageResolverMetrics.CacheMisses)) > 0
                ? Math.Round((double)runs.Where(r => r.Report != null).Sum(r => r.Report!.ImageResolverMetrics.CacheHits) * 100.0 /
                             (runs.Where(r => r.Report != null).Sum(r => r.Report!.ImageResolverMetrics.CacheHits) +
                              runs.Where(r => r.Report != null).Sum(r => r.Report!.ImageResolverMetrics.CacheMisses)), 1)
                : 0.0
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

        var allProviderRoundtrips = runs.Where(r => r.Report != null)
            .SelectMany(r => r.Report!.ProviderMetrics)
            .Select(p => p.TransportRoundtripMs)
            .ToList();
        summary.AverageProviderDurationStats = StatisticalCalculator.Calculate(allProviderRoundtrips);

        summary.TotalWireBytes = runs.Where(r => r.Report != null)
            .SelectMany(r => r.Report!.ProviderMetrics)
            .Sum(p => p.RequestPayloadBytes + p.ResponsePayloadBytes);

        summary.ColdDurationMs = Math.Round(coldDuration, 2);
        summary.ColdTimeToFirstResultMs = Math.Round(coldTtfr, 2);
        summary.ColdCacheMisses = coldMisses;
        return summary;
    }

    private void Log(string message)
    {
        Progress?.Invoke(message);
        AppLogger.Info($"[SensitivityBenchmarkEngine] {message}");
    }

    private void LogRun(string query, string runId, bool isWarmup, SearchBenchmarkReport? report, SystemResourceSample? sample = null)
    {
        var type = isWarmup ? "WARMUP" : "MEASURED";
        var duration = report?.TotalWallClockMs ?? 0;
        var providers = report?.ProviderMetrics.Count ?? 0;
        var wireBytes = report?.ProviderMetrics.Sum(p => p.RequestPayloadBytes + p.ResponsePayloadBytes) ?? 0;
        var iconMs = report?.TotalImageResolutionMs ?? 0;
        var aggMs = report?.TotalAggregationMs ?? 0;
        var cpu = sample?.CpuPercentage ?? 0;
        var wsMb = sample?.TotalWorkingSetMb ?? 0;
        var privMb = sample?.TotalPrivateMemoryMb ?? 0;

        Log($"    [{type} #{runId}] Query: '{query}' -> Duration: {duration:F2}ms | Wire: {wireBytes}B | Agg: {aggMs:F2}ms | Icons: {iconMs:F2}ms | CPU: {cpu:F1}% | TotalWS: {wsMb:F1}MB | TotalPriv: {privMb:F1}MB");
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
                Path.Combine(dir, "Hub", "bin", "Release", "net10.0-windows", "Hub.exe"),
                Path.Combine(dir, "Hub", "Hub", "bin", "Release", "net10.0-windows", "Hub.exe"),
                Path.Combine(dir, "Hub", "bin", "Debug", "net10.0-windows", "Hub.exe"),
                Path.Combine(dir, "Hub", "Hub", "bin", "Debug", "net10.0-windows", "Hub.exe")
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

    public static void ComputeProviderTransportGains(
        SensitivityBenchmarkResult result,
        List<string> queries,
        List<SingleRunBenchmarkResult> allRuns)
    {
        var npRuns = allRuns.Where(r => !r.IsWarmup && r.Transport == ProviderTransportKind.NamedPipe && r.Report != null).ToList();
        var httpRuns = allRuns.Where(r => !r.IsWarmup && r.Transport == ProviderTransportKind.Http && r.Report != null).ToList();
        var grpcRuns = allRuns.Where(r => !r.IsWarmup && r.Transport == ProviderTransportKind.Grpc && r.Report != null).ToList();

        if (npRuns.Count == 0) return;

        var allProviders = npRuns.SelectMany(r => r.Report!.ProviderMetrics)
            .Select(p => p.ProviderName)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        result.ProviderTransportGains.Clear();

        foreach (var provider in allProviders)
        {
            var gainsVsHttp = new List<double>();
            var gainsVsGrpc = new List<double>();
            var pureGainsVsHttp = new List<double>();
            var pureGainsVsGrpc = new List<double>();

            foreach (var q in queries)
            {
                var npMetrics = npRuns.Where(r => r.Query == q)
                    .SelectMany(r => r.Report!.ProviderMetrics)
                    .Where(p => p.ProviderName == provider)
                    .ToList();

                var httpMetrics = httpRuns.Where(r => r.Query == q)
                    .SelectMany(r => r.Report!.ProviderMetrics)
                    .Where(p => p.ProviderName == provider)
                    .ToList();

                var grpcMetrics = grpcRuns.Where(r => r.Query == q)
                    .SelectMany(r => r.Report!.ProviderMetrics)
                    .Where(p => p.ProviderName == provider)
                    .ToList();

                var npTimes = npMetrics.Select(p => p.TransportRoundtripMs).ToList();
                var httpTimes = httpMetrics.Select(p => p.TransportRoundtripMs).ToList();
                var grpcTimes = grpcMetrics.Select(p => p.TransportRoundtripMs).ToList();

                var npPureTimes = npMetrics.Select(p => Math.Max(0.001, p.TransportRoundtripMs - p.ProviderExecutionMs)).ToList();
                var httpPureTimes = httpMetrics.Select(p => Math.Max(0.001, p.TransportRoundtripMs - p.ProviderExecutionMs)).ToList();
                var grpcPureTimes = grpcMetrics.Select(p => Math.Max(0.001, p.TransportRoundtripMs - p.ProviderExecutionMs)).ToList();

                // Overall speedups (Transport + Provider Execution, Amdahl's Law)
                if (npTimes.Count > 0 && httpTimes.Count > 0)
                {
                    var npMean = npTimes.Average();
                    var httpMean = httpTimes.Average();
                    if (npMean > 0.0001)
                    {
                        var speedup = ((httpMean - npMean) / npMean) * 100.0;
                        gainsVsHttp.Add(speedup);
                    }
                }

                if (npTimes.Count > 0 && grpcTimes.Count > 0)
                {
                    var npMean = npTimes.Average();
                    var grpcMean = grpcTimes.Average();
                    if (npMean > 0.0001)
                    {
                        var speedup = ((grpcMean - npMean) / npMean) * 100.0;
                        gainsVsGrpc.Add(speedup);
                    }
                }

                // Pure transport speedups (Isolates pure IPC wire + serialization from Provider internal execution)
                if (npPureTimes.Count > 0 && httpPureTimes.Count > 0)
                {
                    var npPureMean = npPureTimes.Average();
                    var httpPureMean = httpPureTimes.Average();
                    if (npPureMean > 0.0001)
                    {
                        var pureSpeedup = ((httpPureMean - npPureMean) / npPureMean) * 100.0;
                        pureGainsVsHttp.Add(pureSpeedup);
                    }
                }

                if (npPureTimes.Count > 0 && grpcPureTimes.Count > 0)
                {
                    var npPureMean = npPureTimes.Average();
                    var grpcPureMean = grpcPureTimes.Average();
                    if (npPureMean > 0.0001)
                    {
                        var pureSpeedup = ((grpcPureMean - npPureMean) / npPureMean) * 100.0;
                        pureGainsVsGrpc.Add(pureSpeedup);
                    }
                }
            }

            var npAllTimes = npRuns.SelectMany(r => r.Report!.ProviderMetrics)
                .Where(p => p.ProviderName == provider)
                .Select(p => p.TransportRoundtripMs)
                .ToList();
            var httpAllTimes = httpRuns.SelectMany(r => r.Report!.ProviderMetrics)
                .Where(p => p.ProviderName == provider)
                .Select(p => p.TransportRoundtripMs)
                .ToList();
            var grpcAllTimes = grpcRuns.SelectMany(r => r.Report!.ProviderMetrics)
                .Where(p => p.ProviderName == provider)
                .Select(p => p.TransportRoundtripMs)
                .ToList();

            var provExecTimes = allRuns.Where(r => !r.IsWarmup && r.Report != null)
                .SelectMany(r => r.Report!.ProviderMetrics)
                .Where(p => p.ProviderName == provider)
                .Select(p => p.ProviderExecutionMs)
                .ToList();

            var npAllPureTimes = npRuns.SelectMany(r => r.Report!.ProviderMetrics)
                .Where(p => p.ProviderName == provider)
                .Select(p => Math.Max(0.001, p.TransportRoundtripMs - p.ProviderExecutionMs))
                .ToList();
            var httpAllPureTimes = httpRuns.SelectMany(r => r.Report!.ProviderMetrics)
                .Where(p => p.ProviderName == provider)
                .Select(p => Math.Max(0.001, p.TransportRoundtripMs - p.ProviderExecutionMs))
                .ToList();
            var grpcAllPureTimes = grpcRuns.SelectMany(r => r.Report!.ProviderMetrics)
                .Where(p => p.ProviderName == provider)
                .Select(p => Math.Max(0.001, p.TransportRoundtripMs - p.ProviderExecutionMs))
                .ToList();

            var summary = new ProviderTransportGainSummary
            {
                ProviderName = provider,
                ProviderExecutionMeanMs = provExecTimes.Count > 0 ? Math.Round(provExecTimes.Average(), 2) : 0.0,

                NamedPipePureTransportMs = npAllPureTimes.Count > 0 ? Math.Round(npAllPureTimes.Average(), 2) : 0.0,
                HttpPureTransportMs = httpAllPureTimes.Count > 0 ? Math.Round(httpAllPureTimes.Average(), 2) : 0.0,
                GrpcPureTransportMs = grpcAllPureTimes.Count > 0 ? Math.Round(grpcAllPureTimes.Average(), 2) : 0.0,
                PureGainPercentVsHttp = pureGainsVsHttp.Count > 0 ? Math.Round(pureGainsVsHttp.Average(), 1) : 0.0,
                PureGainPercentVsGrpc = pureGainsVsGrpc.Count > 0 ? Math.Round(pureGainsVsGrpc.Average(), 1) : 0.0,

                NamedPipeMeanMs = npAllTimes.Count > 0 ? Math.Round(npAllTimes.Average(), 2) : 0.0,
                HttpMeanMs = httpAllTimes.Count > 0 ? Math.Round(httpAllTimes.Average(), 2) : 0.0,
                GrpcMeanMs = grpcAllTimes.Count > 0 ? Math.Round(grpcAllTimes.Average(), 2) : 0.0,
                GainPercentVsHttp = gainsVsHttp.Count > 0 ? Math.Round(gainsVsHttp.Average(), 1) : 0.0,
                GainPercentVsGrpc = gainsVsGrpc.Count > 0 ? Math.Round(gainsVsGrpc.Average(), 1) : 0.0
            };

            result.ProviderTransportGains.Add(summary);
        }

        // Pure Transport Overalls
        var validPureHttp = result.ProviderTransportGains.Where(g => g.HttpPureTransportMs > 0 && g.NamedPipePureTransportMs > 0).ToList();
        if (validPureHttp.Count > 0)
        {
            var avgNpPure = validPureHttp.Average(g => g.NamedPipePureTransportMs);
            var avgHttpPure = validPureHttp.Average(g => g.HttpPureTransportMs);
            result.OverallPureTransportGainPercentVsHttp = avgNpPure > 0.0001
                ? Math.Round(((avgHttpPure - avgNpPure) / avgNpPure) * 100.0, 1)
                : 0.0;
        }

        var validPureGrpc = result.ProviderTransportGains.Where(g => g.GrpcPureTransportMs > 0 && g.NamedPipePureTransportMs > 0).ToList();
        if (validPureGrpc.Count > 0)
        {
            var avgNpPure = validPureGrpc.Average(g => g.NamedPipePureTransportMs);
            var avgGrpcPure = validPureGrpc.Average(g => g.GrpcPureTransportMs);
            result.OverallPureTransportGainPercentVsGrpc = avgNpPure > 0.0001
                ? Math.Round(((avgGrpcPure - avgNpPure) / avgNpPure) * 100.0, 1)
                : 0.0;
        }

        // Overall Call Overalls (Amdahl's Law)
        var validHttp = result.ProviderTransportGains.Where(g => g.HttpMeanMs > 0 && g.NamedPipeMeanMs > 0).ToList();
        if (validHttp.Count > 0)
        {
            var avgNp = validHttp.Average(g => g.NamedPipeMeanMs);
            var avgHttp = validHttp.Average(g => g.HttpMeanMs);
            result.OverallProviderGainPercentVsHttp = avgNp > 0.0001
                ? Math.Round(((avgHttp - avgNp) / avgNp) * 100.0, 1)
                : 0.0;
        }

        var validGrpc = result.ProviderTransportGains.Where(g => g.GrpcMeanMs > 0 && g.NamedPipeMeanMs > 0).ToList();
        if (validGrpc.Count > 0)
        {
            var avgNp = validGrpc.Average(g => g.NamedPipeMeanMs);
            var avgGrpc = validGrpc.Average(g => g.GrpcMeanMs);
            result.OverallProviderGainPercentVsGrpc = avgNp > 0.0001
                ? Math.Round(((avgGrpc - avgNp) / avgNp) * 100.0, 1)
                : 0.0;
        }
}
}
