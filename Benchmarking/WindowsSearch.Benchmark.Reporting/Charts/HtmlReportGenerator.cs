using System.Globalization;
using System.IO;
using HtmlAgilityPack;
using WindowsSearch.Benchmark.Core.Models;
using WindowsSearch.Common.Models;

namespace WindowsSearch.Benchmark.Reporting.Charts;

public static class HtmlReportGenerator
{
    public static void GenerateSensitivityReport(string outputPath, SensitivityBenchmarkResult result)
    {
        var doc = new HtmlDocument();
        var html = doc.CreateElement("html");
        html.SetAttributeValue("lang", "en");
        doc.DocumentNode.AppendChild(html);

        var head = doc.CreateElement("head");
        html.AppendChild(head);

        var metaUtf8 = doc.CreateElement("meta");
        metaUtf8.SetAttributeValue("charset", "UTF-8");
        head.AppendChild(metaUtf8);

        var metaViewport = doc.CreateElement("meta");
        metaViewport.SetAttributeValue("name", "viewport");
        metaViewport.SetAttributeValue("content", "width=device-width, initial-scale=1.0");
        head.AppendChild(metaViewport);

        var title = doc.CreateElement("title");
        title.InnerHtml = "WindowsSearch Multi-Factor Benchmarking Report";
        head.AppendChild(title);

        var style = doc.CreateElement("style");
        style.InnerHtml = @"
            body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; margin: 30px; background: #f8f9fa; color: #212529; }
            h1, h2, h3 { color: #1a1a2e; }
            .card { background: white; border-radius: 8px; padding: 24px; margin-bottom: 28px; box-shadow: 0 2px 8px rgba(0,0,0,0.06); }
            .kpi-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 20px; margin-bottom: 28px; }
            .kpi-card { background: linear-gradient(135deg, #1e3c72 0%, #2a5298 100%); color: white; border-radius: 8px; padding: 20px; text-align: center; box-shadow: 0 4px 12px rgba(30,60,114,0.15); }
            .kpi-val { font-size: 36px; font-weight: 700; margin: 8px 0; }
            .kpi-label { font-size: 13px; opacity: 0.9; text-transform: uppercase; letter-spacing: 0.5px; }
            table { width: 100%; border-collapse: collapse; margin-top: 14px; overflow-x: auto; display: block; }
            th, td { padding: 9px 12px; text-align: left; border-bottom: 1px solid #dee2e6; font-size: 13px; white-space: nowrap; }
            th { background: #f1f3f5; font-weight: 600; color: #495057; }
            tr:hover { background-color: #f8f9fa; }
            .badge { display: inline-block; padding: 3px 8px; border-radius: 4px; font-size: 12px; font-weight: 600; }
            .badge-np { background: #e3f2fd; color: #0d47a1; }
            .badge-http { background: #e8f5e9; color: #1b5e20; }
            .badge-grpc { background: #fce4ec; color: #880e4f; }
            .badge-best { background: #d4edda; color: #155724; font-size: 12px; }
            .badge-worst { background: #f8d7da; color: #721c24; font-size: 12px; }
            .stat-val { font-family: monospace; font-weight: 600; }
            .stage-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; margin-top: 16px; margin-bottom: 20px; }
            .stage-col { background: #fdfdfd; border: 1px solid #e9ecef; border-radius: 8px; padding: 18px; text-align: center; }
            .stage-col h3 { margin-top: 0; margin-bottom: 12px; font-size: 15px; color: #495057; }
            .stage-col img { max-width: 100%; height: auto; border-radius: 4px; box-shadow: 0 1px 4px rgba(0,0,0,0.06); margin-bottom: 12px; }
            @media (max-width: 1100px) { .stage-grid { grid-template-columns: 1fr; } }
        ";
        head.AppendChild(style);

        var body = doc.CreateElement("body");
        html.AppendChild(body);

        var h1 = doc.CreateElement("h1");
        h1.InnerHtml = "WindowsSearch Optimization & Multi-Factor Sensitivity Report";
        body.AppendChild(h1);

        var subtitle = doc.CreateElement("p");
        subtitle.InnerHtml = $"Master's Thesis Experimental Verification | Generated: <strong>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</strong>";
        body.AppendChild(subtitle);

        // 1. KPI Grid Banner
        var kpiGrid = doc.CreateElement("div");
        kpiGrid.SetAttributeValue("class", "kpi-grid");
        body.AppendChild(kpiGrid);
        double avgProvSpeedup = result.AverageProviderSpeedup;
        if (avgProvSpeedup <= 0 && result.WorstSummary.ProviderDurationStats.Count > 0)
        {
            var list = new List<double>();
            foreach (var (p, wStats) in result.WorstSummary.ProviderDurationStats)
            {
                if (result.BestSummary.ProviderDurationStats.TryGetValue(p, out var bStats))
                {
                    list.Add(Math.Max(0.01, wStats.Mean) / Math.Max(0.01, bStats.Mean));
                }
            }
            if (list.Count > 0)
            {
                avgProvSpeedup = Math.Round(list.Average(), 2);
            }
        }
        if (avgProvSpeedup <= 0) avgProvSpeedup = 1.0;

        AddKpiCard(doc, kpiGrid, $"{result.SpeedupFactor}x", "Total Duration Speedup (Worst -> Best)");
        AddKpiCard(doc, kpiGrid, $"{result.TimeToFirstResultSpeedup}x", "TTFR Responsiveness Speedup");
        AddKpiCard(doc, kpiGrid, $"{avgProvSpeedup}x", "Average Provider Speedup");
        AddKpiCard(doc, kpiGrid, $"{result.WireReductionPercent}%", "Wire Payload Reduction");
        AddKpiCard(doc, kpiGrid, $"{result.CacheAccelerationRatio}x", "Icon Cache Acceleration");
        // 2. Stage 5: Final Comparison (Best vs Worst)
        var stage5Card = doc.CreateElement("div");
        stage5Card.SetAttributeValue("class", "card");
        body.AppendChild(stage5Card);

        var s5H2 = doc.CreateElement("h2");
        s5H2.InnerHtml = "Stage 5: Final Comparison (BEST vs WORST Settings across Diverse Queries)";
        stage5Card.AppendChild(s5H2);

        var s5Grid = doc.CreateElement("div");
        s5Grid.SetAttributeValue("class", "stage-grid");
        stage5Card.AppendChild(s5Grid);

        // Left Column: Performance
        var s5Left = doc.CreateElement("div");
        s5Left.SetAttributeValue("class", "stage-col");
        s5Grid.AppendChild(s5Left);
        var s5LeftH3 = doc.CreateElement("h3");
        s5LeftH3.InnerHtml = "Performance & Latency (Best vs Worst)";
        s5Left.AppendChild(s5LeftH3);
        AddImageLink(doc, s5Left, "chart_stage5_best_vs_worst.png", "Best vs Worst Performance Comparison");

        // Right Column: System Resources
        var s5Right = doc.CreateElement("div");
        s5Right.SetAttributeValue("class", "stage-col");
        s5Grid.AppendChild(s5Right);
        var s5RightH3 = doc.CreateElement("h3");
        s5RightH3.InnerHtml = "System Resources (Best vs Worst CPU & RAM)";
        s5Right.AppendChild(s5RightH3);
        AddImageLink(doc, s5Right, "chart_stage5_resources.png", "Best vs Worst System Resources");

        var s5Table = doc.CreateElement("table");
        stage5Card.AppendChild(s5Table);
        AppendSummaryHeader(doc, s5Table);
        var s5Tbody = doc.CreateElement("tbody");
        s5Table.AppendChild(s5Tbody);

        AppendSummaryRow(doc, s5Tbody, result.BestSummary, "badge-best", "BEST");
        AppendSummaryRow(doc, s5Tbody, result.WorstSummary, "badge-worst", "WORST");

        // 3. Stage 1: Transport Sensitivity
        var stage1Card = AppendStageSection(doc, body, "Stage 1: Transport Roundtrip Latency Sensitivity",
            [
                ("chart_stage1_provider_transport_gains.png", "NamedPipe IPC Gain vs HTTP & gRPC per Provider (% Faster)"),
                ("chart_stage1_transports_provider_calls.png", "Pure Provider IPC Call Latency Zoom (Demonstrating Real Transport Gain)")
            ],
            [
                ("chart_stage1_transports_filtered.png", "Provider Call Avg vs Total Search Latency (Amdahl's Law Comparison)"),
                ("chart_stage1_resources.png", "Stage 1 System Resources (CPU % & Total Working Set MB)")
            ],
            result.TransportSummaries);
        AppendProviderGainMatrixTable(doc, stage1Card, result);
        AppendStageSection(doc, body, "Stage 2: Serialization Payload Size & CPU Parsing Time",
            [
                ("chart_stage2_wire_bytes.png", "Total Wire Payload Size (Bytes)"),
                ("chart_stage2_serialization_cpu.png", "Serialization & Deserialization CPU Time (ms)")
            ],
            [
                ("chart_stage2_resources.png", "Stage 2 System Resources (CPU % & Total Working Set MB)")
            ],
            result.SerializationSummaries);

        // 5. Stage 3: Search Mode Concurrency
        AppendStageSection(doc, body, "Stage 3: Hub Search Mode Concurrency Latency",
            [
                ("chart_stage3_search_modes.png", "Search Mode Concurrency (TTFR vs Total Latency)"),
                ("chart_stage3_provider_breakdown.png", "Per-Provider Latency Breakdown (Demonstrating Amdahl's Law)")
            ],
            [
                ("chart_stage3_resources.png", "Stage 3 System Resources (CPU % & Total Working Set MB)")
            ],
            result.SearchModeSummaries);

        // 6. Stage 4: Image Resolvers & Cache Impact
        AppendStageSection(doc, body, "Stage 4: Image Resolver Strategy & Caching Analysis (App Icons Stress Test)",
            [
                ("chart_stage4_cache_penalty.png", "Cache Macro Impact (Cache ON vs Cache OFF)"),
                ("chart_stage4_resolvers_zoom.png", "In-Memory Resolver Scaling under 100+ App Icons")
            ],
            [
                ("chart_stage4_cache_hits_misses.png", "Cache Hit / Miss Counts Across Configurations"),
                ("chart_stage4_resources.png", "Stage 4 System Resources (CPU % & Total Working Set MB)")
            ],
            result.ImageResolverSummaries);

        var outDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        doc.Save(outputPath, System.Text.Encoding.UTF8);
    }

    public static void GenerateReport(
        string outputPath,
        IReadOnlyList<ConfigurationBenchmarkSummary> summaries,
        IReadOnlyList<SingleRunBenchmarkResult> runs,
        IReadOnlyList<(string FileName, string Title)>? chartList = null)
    {
        var pseudoResult = new SensitivityBenchmarkResult
        {
            TransportSummaries = summaries.ToList(),
            AllRuns = runs.ToList()
        };
        GenerateSensitivityReport(outputPath, pseudoResult);
    }

    private static HtmlNode AppendStageSection(
        HtmlDocument doc,
        HtmlNode body,
        string title,
        (string FileName, string Title)[] leftCharts,
        (string FileName, string Title)[] rightCharts,
        List<ConfigurationBenchmarkSummary> summaries)
    {
        var card = doc.CreateElement("div");
        card.SetAttributeValue("class", "card");
        body.AppendChild(card);

        var h2 = doc.CreateElement("h2");
        h2.InnerHtml = title;
        card.AppendChild(h2);

        var stageGrid = doc.CreateElement("div");
        stageGrid.SetAttributeValue("class", "stage-grid");
        card.AppendChild(stageGrid);

        // Left Column: Performance Metrics
        var leftCol = doc.CreateElement("div");
        leftCol.SetAttributeValue("class", "stage-col");
        stageGrid.AppendChild(leftCol);
        var leftH3 = doc.CreateElement("h3");
        leftH3.InnerHtml = "Performance & Latency Metrics";
        leftCol.AppendChild(leftH3);
        foreach (var (fileName, chartTitle) in leftCharts)
        {
            AddImageLink(doc, leftCol, fileName, chartTitle);
        }

        // Right Column: System Resources
        var rightCol = doc.CreateElement("div");
        rightCol.SetAttributeValue("class", "stage-col");
        stageGrid.AppendChild(rightCol);
        var rightH3 = doc.CreateElement("h3");
        rightH3.InnerHtml = "System Resources (CPU & RAM)";
        rightCol.AppendChild(rightH3);
        foreach (var (fileName, chartTitle) in rightCharts)
        {
            AddImageLink(doc, rightCol, fileName, chartTitle);
        }

        var table = doc.CreateElement("table");
        card.AppendChild(table);
        AppendSummaryHeader(doc, table);

        var tbody = doc.CreateElement("tbody");
        table.AppendChild(tbody);

        foreach (var s in summaries)
        {
            AppendSummaryRow(doc, tbody, s);
        }
        return card;
    }

    private static void AppendProviderGainMatrixTable(HtmlDocument doc, HtmlNode card, SensitivityBenchmarkResult result)
    {
        var gains = result.ProviderTransportGains;
        if (gains.Count == 0 && result.AllRuns.Count > 0)
        {
            // Compute on the fly for legacy results
            var npRuns = result.AllRuns.Where(r => !r.IsWarmup && r.Transport == ProviderTransportKind.NamedPipe && r.Report != null).ToList();
            var httpRuns = result.AllRuns.Where(r => !r.IsWarmup && r.Transport == ProviderTransportKind.Http && r.Report != null).ToList();
            var grpcRuns = result.AllRuns.Where(r => !r.IsWarmup && r.Transport == ProviderTransportKind.Grpc && r.Report != null).ToList();
            var allProviders = npRuns.SelectMany(r => r.Report!.ProviderMetrics).Select(p => p.ProviderName).Distinct().OrderBy(p => p).ToList();
            var queries = result.AllRuns.Select(r => r.Query).Distinct().ToList();

            foreach (var prov in allProviders)
            {
                var gHttpList = new List<double>();
                var gGrpcList = new List<double>();
                foreach (var q in queries)
                {
                    var npT = npRuns.Where(r => r.Query == q).SelectMany(r => r.Report!.ProviderMetrics).Where(p => p.ProviderName == prov).Select(p => p.TransportRoundtripMs).ToList();
                    var httpT = httpRuns.Where(r => r.Query == q).SelectMany(r => r.Report!.ProviderMetrics).Where(p => p.ProviderName == prov).Select(p => p.TransportRoundtripMs).ToList();
                    var grpcT = grpcRuns.Where(r => r.Query == q).SelectMany(r => r.Report!.ProviderMetrics).Where(p => p.ProviderName == prov).Select(p => p.TransportRoundtripMs).ToList();
                    if (npT.Count > 0 && httpT.Count > 0 && npT.Average() > 0.0001)
                        gHttpList.Add(((httpT.Average() - npT.Average()) / npT.Average()) * 100.0);
                    if (npT.Count > 0 && grpcT.Count > 0 && npT.Average() > 0.0001)
                        gGrpcList.Add(((grpcT.Average() - npT.Average()) / npT.Average()) * 100.0);
                }
                var npAll = npRuns.SelectMany(r => r.Report!.ProviderMetrics).Where(p => p.ProviderName == prov).Select(p => p.TransportRoundtripMs).ToList();
                var httpAll = httpRuns.SelectMany(r => r.Report!.ProviderMetrics).Where(p => p.ProviderName == prov).Select(p => p.TransportRoundtripMs).ToList();
                var grpcAll = grpcRuns.SelectMany(r => r.Report!.ProviderMetrics).Where(p => p.ProviderName == prov).Select(p => p.TransportRoundtripMs).ToList();
                gains.Add(new ProviderTransportGainSummary
                {
                    ProviderName = prov,
                    NamedPipeMeanMs = npAll.Count > 0 ? Math.Round(npAll.Average(), 2) : 0,
                    HttpMeanMs = httpAll.Count > 0 ? Math.Round(httpAll.Average(), 2) : 0,
                    GrpcMeanMs = grpcAll.Count > 0 ? Math.Round(grpcAll.Average(), 2) : 0,
                    GainPercentVsHttp = gHttpList.Count > 0 ? Math.Round(gHttpList.Average(), 1) : 0,
                    GainPercentVsGrpc = gGrpcList.Count > 0 ? Math.Round(gGrpcList.Average(), 1) : 0
                });
            }
            if (gains.Any(g => g.HttpMeanMs > 0))
            {
                var npMeanForHttp = gains.Where(g => g.HttpMeanMs > 0).Select(g => g.NamedPipeMeanMs).DefaultIfEmpty(0).Average();
                var httpMean = gains.Where(g => g.HttpMeanMs > 0).Select(g => g.HttpMeanMs).DefaultIfEmpty(0).Average();
                result.OverallProviderGainPercentVsHttp = npMeanForHttp > 0.0001
                    ? Math.Round(((httpMean - npMeanForHttp) / npMeanForHttp) * 100.0, 1)
                    : 0.0;
            }
            if (gains.Any(g => g.GrpcMeanMs > 0))
            {
                var npMeanForGrpc = gains.Where(g => g.GrpcMeanMs > 0).Select(g => g.NamedPipeMeanMs).DefaultIfEmpty(0).Average();
                var grpcMean = gains.Where(g => g.GrpcMeanMs > 0).Select(g => g.GrpcMeanMs).DefaultIfEmpty(0).Average();
                result.OverallProviderGainPercentVsGrpc = npMeanForGrpc > 0.0001
                    ? Math.Round(((grpcMean - npMeanForGrpc) / npMeanForGrpc) * 100.0, 1)
                    : 0.0;
            }
        }
        if (gains.Count == 0) return;

        var h3 = doc.CreateElement("h3");
        h3.InnerHtml = "NamedPipe IPC Speedup Matrix vs HTTP & gRPC (Per-Provider & Query Average)";
        h3.SetAttributeValue("style", "margin-top: 24px; margin-bottom: 6px; font-size: 15px; color: #343a40;");
        card.AppendChild(h3);

        var p = doc.CreateElement("p");
        p.InnerHtml = "Percentage speedup of NamedPipe relative to alternative transports per individual provider call: " +
                      $"<code>Speedup % = ((t_other - t_np) / t_np) * 100%</code>. Shows how much faster NamedPipe is relative to HTTP and gRPC (e.g., +200% means 3x as fast). Isolates pure IPC transport performance from Amdahl's Law masking.";
        p.SetAttributeValue("style", "font-size: 13px; color: #6c757d; margin-top: 0; margin-bottom: 12px;");
        card.AppendChild(p);

        var table = doc.CreateElement("table");
        card.AppendChild(table);

        var thead = doc.CreateElement("thead");
        table.AppendChild(thead);
        var headRow = doc.CreateElement("tr");
        thead.AppendChild(headRow);

        string[] headers = [ "Provider", "NamedPipe (ms)", "HTTP (ms)", "gRPC (ms)", "NamedPipe Speedup vs HTTP (%)", "NamedPipe Speedup vs gRPC (%)" ];
        foreach (var h in headers)
        {
            var th = doc.CreateElement("th");
            th.InnerHtml = h;
            headRow.AppendChild(th);
        }

        var tbody = doc.CreateElement("tbody");
        table.AppendChild(tbody);

        foreach (var g in gains)
        {
            var row = doc.CreateElement("tr");
            tbody.AppendChild(row);

            AddCell(doc, row, $"<strong>{g.ProviderName}</strong>");
            AddCell(doc, row, $"{g.NamedPipeMeanMs:F2} ms", "stat-val");
            AddCell(doc, row, g.HttpMeanMs > 0 ? $"{g.HttpMeanMs:F2} ms" : "-", "stat-val");
            AddCell(doc, row, g.GrpcMeanMs > 0 ? $"{g.GrpcMeanMs:F2} ms" : "-", "stat-val");

            var httpGainBadge = g.GainPercentVsHttp >= 0
                ? $"<span class=\"badge badge-best\">+{g.GainPercentVsHttp:F1}%</span>"
                : $"<span class=\"badge badge-worst\">{g.GainPercentVsHttp:F1}%</span>";
            AddCell(doc, row, g.HttpMeanMs > 0 ? httpGainBadge : "-", "stat-val");

            var grpcGainBadge = g.GainPercentVsGrpc >= 0
                ? $"<span class=\"badge badge-best\">+{g.GainPercentVsGrpc:F1}%</span>"
                : $"<span class=\"badge badge-worst\">{g.GainPercentVsGrpc:F1}%</span>";
            AddCell(doc, row, g.GrpcMeanMs > 0 ? grpcGainBadge : "-", "stat-val");
        }

        // Overall summary row
        var sumRow = doc.CreateElement("tr");
        sumRow.SetAttributeValue("style", "background-color: #e8f4fd; font-weight: bold;");
        tbody.AppendChild(sumRow);

        var avgNp = gains.Average(g => g.NamedPipeMeanMs);
        var avgHttp = gains.Where(g => g.HttpMeanMs > 0).Select(g => g.HttpMeanMs).DefaultIfEmpty(0).Average();
        var avgGrpc = gains.Where(g => g.GrpcMeanMs > 0).Select(g => g.GrpcMeanMs).DefaultIfEmpty(0).Average();

        AddCell(doc, sumRow, "OVERALL AVERAGE GAIN");
        AddCell(doc, sumRow, $"{avgNp:F2} ms", "stat-val");
        AddCell(doc, sumRow, avgHttp > 0 ? $"{avgHttp:F2} ms" : "-", "stat-val");
        AddCell(doc, sumRow, avgGrpc > 0 ? $"{avgGrpc:F2} ms" : "-", "stat-val");

        double overallHttpGain = (avgNp > 0.0001 && avgHttp > 0)
            ? Math.Round(((avgHttp - avgNp) / avgNp) * 100.0, 1)
            : result.OverallProviderGainPercentVsHttp;

        double overallGrpcGain = (avgNp > 0.0001 && avgGrpc > 0)
            ? Math.Round(((avgGrpc - avgNp) / avgNp) * 100.0, 1)
            : result.OverallProviderGainPercentVsGrpc;

        var overallHttpBadge = overallHttpGain >= 0
            ? $"<span class=\"badge badge-best\" style=\"font-size: 13px;\">+{overallHttpGain:F1}%</span>"
            : $"<span class=\"badge badge-worst\" style=\"font-size: 13px;\">{overallHttpGain:F1}%</span>";
        AddCell(doc, sumRow, avgHttp > 0 ? overallHttpBadge : "-", "stat-val");

        var overallGrpcBadge = overallGrpcGain >= 0
            ? $"<span class=\"badge badge-best\" style=\"font-size: 13px;\">+{overallGrpcGain:F1}%</span>"
            : $"<span class=\"badge badge-worst\" style=\"font-size: 13px;\">{overallGrpcGain:F1}%</span>";
        AddCell(doc, sumRow, avgGrpc > 0 ? overallGrpcBadge : "-", "stat-val");
    }

    private static void AddKpiCard(HtmlDocument doc, HtmlNode grid, string value, string label)
    {
        var card = doc.CreateElement("div");
        card.SetAttributeValue("class", "kpi-card");
        grid.AppendChild(card);

        var valNode = doc.CreateElement("div");
        valNode.SetAttributeValue("class", "kpi-val");
        valNode.InnerHtml = value;
        card.AppendChild(valNode);

        var labelNode = doc.CreateElement("div");
        labelNode.SetAttributeValue("class", "kpi-label");
        labelNode.InnerHtml = label;
        card.AppendChild(labelNode);
    }

    private static void AddImageLink(HtmlDocument doc, HtmlNode container, string fileName, string title)
    {
        var a = doc.CreateElement("a");
        a.SetAttributeValue("href", fileName);
        a.SetAttributeValue("target", "_blank");
        a.SetAttributeValue("title", $"Click to view {title}");
        container.AppendChild(a);

        var img = doc.CreateElement("img");
        img.SetAttributeValue("src", fileName);
        img.SetAttributeValue("alt", title);
        a.AppendChild(img);
    }

    private static void AppendSummaryHeader(HtmlDocument doc, HtmlNode table)
    {
        var thead = doc.CreateElement("thead");
        table.AppendChild(thead);

        var row = doc.CreateElement("tr");
        thead.AppendChild(row);

        var headers = new[]
        {
            "Tag", "Transport", "Serialization", "Search Mode", "Image Resolver",
            "Cache", "Tries", "Cold (ms)", "Mean (ms)", "Filtered (ms)", "Outliers", "TTFR (ms)", "Median (ms)", "P95 (ms)", "StdDev",
            "Avg Prov (ms)", "Ser CPU (ms)", "Agg (ms)", "Img Res (ms)",
            "Total Wire", "CPU %", "Hub WS (MB)", "Total WS (MB)", "Cache Hits", "Cache Misses", "Hit Rate %"
        };

        foreach (var h in headers)
        {
            var th = doc.CreateElement("th");
            th.InnerHtml = h;
            row.AppendChild(th);
        }
    }

    private static void AppendSummaryRow(HtmlDocument doc, HtmlNode tbody, ConfigurationBenchmarkSummary s, string? tagClass = null, string? tagText = null)
    {
        var row = doc.CreateElement("tr");
        tbody.AppendChild(row);

        var badgeClass = s.Transport switch
        {
            ProviderTransportKind.NamedPipe => "badge-np",
            ProviderTransportKind.Http => "badge-http",
            ProviderTransportKind.Grpc => "badge-grpc",
            _ => "badge-np"
        };

        var tagHtml = !string.IsNullOrEmpty(tagText)
            ? $"<span class=\"badge {tagClass ?? "badge-best"}\">{tagText}</span>"
            : "-";

        AddCell(doc, row, tagHtml);
        AddCell(doc, row, $"<span class=\"badge {badgeClass}\">{s.Transport}</span>");
        AddCell(doc, row, $"<strong>{s.Serialization}</strong>");
        AddCell(doc, row, s.SearchMode.ToString());
        AddCell(doc, row, s.ImageResolver.ToString());
        AddCell(doc, row, s.UseCache ? "ON" : "OFF");
        AddCell(doc, row, s.TriesCount.ToString(), "stat-val");
        AddCell(doc, row, s.ColdDurationMs > 0 ? s.ColdDurationMs.ToString("F2", CultureInfo.InvariantCulture) : "-", "stat-val");
        AddCell(doc, row, s.TotalDurationStats.Mean.ToString("F2", CultureInfo.InvariantCulture), "stat-val");
        AddCell(doc, row, s.FilteredDurationStats.Mean.ToString("F2", CultureInfo.InvariantCulture), "stat-val");
        AddCell(doc, row, s.OutliersCount.ToString(), "stat-val");
        AddCell(doc, row, s.TimeToFirstResultStats.Mean.ToString("F2", CultureInfo.InvariantCulture), "stat-val");
        AddCell(doc, row, s.TotalDurationStats.Median.ToString("F2", CultureInfo.InvariantCulture), "stat-val");
        AddCell(doc, row, s.TotalDurationStats.P95.ToString("F2", CultureInfo.InvariantCulture), "stat-val");
        AddCell(doc, row, s.TotalDurationStats.StandardDeviation.ToString("F2", CultureInfo.InvariantCulture), "stat-val");
        AddCell(doc, row, s.AverageProviderDurationMs.ToString("F2", CultureInfo.InvariantCulture), "stat-val");
        AddCell(doc, row, s.SerializationCpuStats.Mean.ToString("F2", CultureInfo.InvariantCulture), "stat-val");
        AddCell(doc, row, s.AggregationStats.Mean.ToString("F2", CultureInfo.InvariantCulture), "stat-val");
        AddCell(doc, row, s.ImageResolutionStats.Mean.ToString("F2", CultureInfo.InvariantCulture), "stat-val");
        AddCell(doc, row, $"{s.TotalWireBytes:N0} B", "stat-val");
        AddCell(doc, row, $"{s.CpuPercentageStats.Mean.ToString("F2", CultureInfo.InvariantCulture)}%", "stat-val");
        AddCell(doc, row, $"{s.HubWorkingSetMbStats.Mean.ToString("F2", CultureInfo.InvariantCulture)} MB", "stat-val");
        AddCell(doc, row, $"{s.TotalWorkingSetMbStats.Mean.ToString("F2", CultureInfo.InvariantCulture)} MB", "stat-val");
        AddCell(doc, row, s.TotalCacheHits.ToString("N0"), "stat-val");
        AddCell(doc, row, s.TotalCacheMisses.ToString("N0"), "stat-val");
        AddCell(doc, row, $"{s.CacheHitRatePercent:F1}%", "stat-val");
    }

    private static void AddCell(HtmlDocument doc, HtmlNode row, string innerHtml, string? className = null)
    {
        var td = doc.CreateElement("td");
        td.InnerHtml = innerHtml;
        if (!string.IsNullOrEmpty(className))
        {
            td.SetAttributeValue("class", className);
        }
        row.AppendChild(td);
    }
}
