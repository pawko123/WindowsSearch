using System.Globalization;

namespace FileIndexProvider.Models;

public sealed class BuildHistoryEntity
{
    public long Id { get; set; }
    public long StartTimeUtc { get; set; }
    public long? EndTimeUtc { get; set; }
    public long FilesIndexed { get; set; }
    public long ElapsedMs { get; set; }
    public string Status { get; set; } = "Completed";

    public DateTime StartTime => DateTimeOffset.FromUnixTimeSeconds(StartTimeUtc).LocalDateTime;

    public string Summary => $"{StartTime:yyyy-MM-dd HH:mm:ss} ({FilesIndexed.ToString("N0", CultureInfo.InvariantCulture)} files in {(ElapsedMs / 1000.0).ToString("F1", CultureInfo.InvariantCulture)}s)";
}
