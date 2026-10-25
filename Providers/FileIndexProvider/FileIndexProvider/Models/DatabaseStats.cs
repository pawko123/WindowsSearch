namespace FileIndexProvider.Models;

public sealed class DatabaseStats
{
    public long TotalFiles { get; set; }
    public long TotalDirectories { get; set; }
    public long DatabaseSizeBytes { get; set; }
    public string LastFullScanUtc { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public double? CurrentProgress { get; set; }

    public string FormattedSize
    {
        get
        {
            if (DatabaseSizeBytes < 1024) return $"{DatabaseSizeBytes} B";
            if (DatabaseSizeBytes < 1024 * 1024) return $"{DatabaseSizeBytes / 1024.0:F1} KB";
            if (DatabaseSizeBytes < 1024 * 1024 * 1024) return $"{DatabaseSizeBytes / (1024.0 * 1024.0):F1} MB";
            return $"{DatabaseSizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }
}
