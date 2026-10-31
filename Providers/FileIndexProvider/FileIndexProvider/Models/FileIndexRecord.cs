namespace FileIndexProvider.Models;

public sealed class FileIndexRecord
{
    public int Id { get; set; }
    public int DirectoryId { get; set; }
    public string DirectoryPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public long LastWriteTimeUtc { get; set; }
    public int AccessCount { get; set; }
    public long LastAccessedUtc { get; set; }
    public bool IsDirectory { get; set; }

    public string FullPath => string.IsNullOrEmpty(DirectoryPath)
        ? FileName
        : System.IO.Path.Combine(DirectoryPath, FileName);
}
