namespace FileIndexProvider.Models;

public sealed class FileChangeEvent
{
    public WatcherChangeTypes ChangeType { get; set; }
    public string FullPath { get; set; } = string.Empty;
    public string? OldFullPath { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
