using System.Globalization;
using System.IO;
using HtmlAgilityPack;
using WindowsSearch.Benchmark.Core.Models;
using WindowsSearch.Common.Models;

namespace WindowsSearch.Benchmark.Reporting.Charts;

public static class HtmlReportGenerator
{
    public static void GenerateReport(string outputPath, IReadOnlyList<ConfigurationBenchmarkSummary> summaries, IReadOnlyList<SingleRunBenchmarkResult> runs)
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

        var title = doc.CreateElement("title");
        title.InnerHtml = "WindowsSearch Hub Benchmarking Report";
        head.AppendChild(title);

        var style = doc.CreateElement("style");
        style.InnerHtml = @"
            body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; margin: 30px; background: #f8f9fa; color: #212529; }
            h1, h2 { color: #1a1a2e; }
            .card { background: white; border-radius: 8px; padding: 20px; margin-bottom: 24px; box-shadow: 0 2px 6px rgba(0,0,0,0.06); }
            table { width: 100%; border-collapse: collapse; margin-top: 12px; }
            th, td { padding: 10px 14px; text-align: left; border-bottom: 1px solid #dee2e6; font-size: 14px; }
            th { background: #f1f3f5; font-weight: 600; color: #495057; }
            tr:hover { background-color: #f8f9fa; }
            .badge { display: inline-block; padding: 3px 8px; border-radius: 4px; font-size: 12px; font-weight: 600; }
            .badge-np { background: #e3f2fd; color: #0d47a1; }
            .badge-http { background: #e8f5e9; color: #1b5e20; }
            .badge-grpc { background: #fce4ec; color: #880e4f; }
            .stat-val { font-family: monospace; font-weight: 600; }
        ";
        head.AppendChild(style);

        var body = doc.CreateElement("body");
        html.AppendChild(body);

        var h1 = doc.CreateElement("h1");
        h1.InnerHtml = "WindowsSearch Hub Benchmarking Report";
        body.AppendChild(h1);

        var subtitle = doc.CreateElement("p");
        subtitle.InnerHtml = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Total Measured Runs: {runs.Count(r => !r.IsWarmup)} | Configurations Tested: {summaries.Count}";
        body.AppendChild(subtitle);

        var card = doc.CreateElement("div");
        card.SetAttributeValue("class", "card");
        body.AppendChild(card);

        var h2 = doc.CreateElement("h2");
        h2.InnerHtml = "Configuration Summary & Latency Statistics";
        card.AppendChild(h2);

        var table = doc.CreateElement("table");
        card.AppendChild(table);

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

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        doc.Save(outputPath, System.Text.Encoding.UTF8);
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
