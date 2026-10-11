using System.Globalization;
using System.IO;
using HtmlAgilityPack;
using WindowsSearch.Benchmark.Core.Models;
using WindowsSearch.Common.Models;

namespace WindowsSearch.Benchmark.Reporting.Charts;

public static class HtmlReportGenerator
{
    public static void GenerateReport(
        string outputPath,
        IReadOnlyList<ConfigurationBenchmarkSummary> summaries,
        IReadOnlyList<SingleRunBenchmarkResult> runs,
        IReadOnlyList<(string FileName, string Title)>? chartList = null)
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
        title.InnerHtml = "WindowsSearch Hub Benchmarking Report";
        head.AppendChild(title);

        var style = doc.CreateElement("style");
        style.InnerHtml = @"
            body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; margin: 30px; background: #f8f9fa; color: #212529; }
            h1, h2, h3 { color: #1a1a2e; }
            .card { background: white; border-radius: 8px; padding: 24px; margin-bottom: 28px; box-shadow: 0 2px 8px rgba(0,0,0,0.06); }
            table { width: 100%; border-collapse: collapse; margin-top: 12px; }
            th, td { padding: 10px 14px; text-align: left; border-bottom: 1px solid #dee2e6; font-size: 14px; }
            th { background: #f1f3f5; font-weight: 600; color: #495057; }
            tr:hover { background-color: #f8f9fa; }
            .badge { display: inline-block; padding: 3px 8px; border-radius: 4px; font-size: 12px; font-weight: 600; }
            .badge-np { background: #e3f2fd; color: #0d47a1; }
            .badge-http { background: #e8f5e9; color: #1b5e20; }
            .badge-grpc { background: #fce4ec; color: #880e4f; }
            .stat-val { font-family: monospace; font-weight: 600; }
            .chart-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(460px, 1fr)); gap: 24px; margin-top: 16px; }
            .chart-card { background: #ffffff; border: 1px solid #e9ecef; border-radius: 8px; padding: 18px; text-align: center; box-shadow: 0 1px 4px rgba(0,0,0,0.04); }
            .chart-card h3 { margin-top: 0; margin-bottom: 12px; font-size: 16px; color: #343a40; }
            .chart-card a { display: block; }
            .chart-card img { max-width: 100%; height: auto; border-radius: 4px; transition: transform 0.2s ease; }
            .chart-card img:hover { transform: scale(1.01); }
            .meta-bar { margin-bottom: 20px; font-size: 15px; color: #6c757d; }
        ";
        head.AppendChild(style);

        var body = doc.CreateElement("body");
        html.AppendChild(body);

        var h1 = doc.CreateElement("h1");
        h1.InnerHtml = "WindowsSearch Hub Benchmarking Report";
        body.AppendChild(h1);

        var metaBar = doc.CreateElement("p");
        metaBar.SetAttributeValue("class", "meta-bar");
        metaBar.InnerHtml = $"Generated: <strong>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</strong> | Measured Runs: <strong>{runs.Count(r => !r.IsWarmup)}</strong> | Tested Configurations: <strong>{summaries.Count}</strong>";
        body.AppendChild(metaBar);

        // 1. Charts Section
        var outDir = Path.GetDirectoryName(outputPath) ?? ".";
        var availableCharts = chartList ?? DiscoverCharts(outDir);

        if (availableCharts.Count > 0)
        {
            var chartCard = doc.CreateElement("div");
            chartCard.SetAttributeValue("class", "card");
            body.AppendChild(chartCard);

            var chartH2 = doc.CreateElement("h2");
            chartH2.InnerHtml = "Performance Visualizations";
            chartCard.AppendChild(chartH2);

            var chartGrid = doc.CreateElement("div");
            chartGrid.SetAttributeValue("class", "chart-grid");
            chartCard.AppendChild(chartGrid);

            foreach (var (fileName, chartTitle) in availableCharts)
            {
                var card = doc.CreateElement("div");
                card.SetAttributeValue("class", "chart-card");
                chartGrid.AppendChild(card);

                var h3 = doc.CreateElement("h3");
                h3.InnerHtml = chartTitle;
                card.AppendChild(h3);

                var a = doc.CreateElement("a");
                a.SetAttributeValue("href", fileName);
                a.SetAttributeValue("target", "_blank");
                a.SetAttributeValue("title", "Click to view full image");
                card.AppendChild(a);

                var img = doc.CreateElement("img");
                img.SetAttributeValue("src", fileName);
                img.SetAttributeValue("alt", chartTitle);
                a.AppendChild(img);
            }
        }

        // 2. Data Table Section
        var tableCard = doc.CreateElement("div");
        tableCard.SetAttributeValue("class", "card");
        body.AppendChild(tableCard);

        var tableH2 = doc.CreateElement("h2");
        tableH2.InnerHtml = "Configuration Summary & Latency Statistics";
        tableCard.AppendChild(tableH2);

        var table = doc.CreateElement("table");
        tableCard.AppendChild(table);

        var thead = doc.CreateElement("thead");
        table.AppendChild(thead);

        var headerRow = doc.CreateElement("tr");
        thead.AppendChild(headerRow);

        var headers = new[]
        {
            "Transport", "Serialization", "Search Mode", "Image Resolver",
            "Cache", "Query", "Mean (ms)", "Median (ms)", "P95 (ms)",
            "StdDev", "Aggregation (ms)", "Image Res (ms)", "CPU %", "Total WS (MB)"
        };

        foreach (var headerText in headers)
        {
            var th = doc.CreateElement("th");
            th.InnerHtml = headerText;
            headerRow.AppendChild(th);
        }

        var tbody = doc.CreateElement("tbody");
        table.AppendChild(tbody);

        foreach (var s in summaries)
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

            AddCell(doc, row, $"<span class=\"badge {badgeClass}\">{s.Transport}</span>");
            AddCell(doc, row, $"<strong>{s.Serialization}</strong>");
            AddCell(doc, row, s.SearchMode.ToString());
            AddCell(doc, row, s.ImageResolver.ToString());
            AddCell(doc, row, s.UseCache ? "ON" : "OFF");
            AddCell(doc, row, $"<code>{s.Query}</code>");
            AddCell(doc, row, s.TotalDurationStats.Mean.ToString("F2", CultureInfo.InvariantCulture), "stat-val");
            AddCell(doc, row, s.TotalDurationStats.Median.ToString("F2", CultureInfo.InvariantCulture), "stat-val");
            AddCell(doc, row, s.TotalDurationStats.P95.ToString("F2", CultureInfo.InvariantCulture), "stat-val");
            AddCell(doc, row, s.TotalDurationStats.StandardDeviation.ToString("F2", CultureInfo.InvariantCulture), "stat-val");
            AddCell(doc, row, s.AggregationStats.Mean.ToString("F2", CultureInfo.InvariantCulture), "stat-val");
            AddCell(doc, row, s.ImageResolutionStats.Mean.ToString("F2", CultureInfo.InvariantCulture), "stat-val");
            AddCell(doc, row, $"{s.CpuPercentageStats.Mean.ToString("F2", CultureInfo.InvariantCulture)}%", "stat-val");
            AddCell(doc, row, $"{s.TotalWorkingSetMbStats.Mean.ToString("F2", CultureInfo.InvariantCulture)} MB", "stat-val");
        }

        if (!string.IsNullOrEmpty(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        doc.Save(outputPath, System.Text.Encoding.UTF8);
    }

    private static List<(string FileName, string Title)> DiscoverCharts(string outputDirectory)
    {
        var result = new List<(string FileName, string Title)>();
        var candidates = new (string FileName, string Title)[]
        {
            ("chart_transport_latency.png", "Transport & Serialization Latency"),
            ("chart_search_modes.png", "Search Modes Latency Comparison"),
            ("chart_wire_payloads.png", "Payload Wire Size by Serialization"),
            ("chart_image_resolvers.png", "Image Resolver Strategy & Cache State")
        };

        foreach (var (file, title) in candidates)
        {
            if (File.Exists(Path.Combine(outputDirectory, file)))
            {
                result.Add((file, title));
            }
        }

        return result;
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
