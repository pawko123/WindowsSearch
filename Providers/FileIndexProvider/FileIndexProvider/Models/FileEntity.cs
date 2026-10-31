namespace FileIndexProvider.Models;

public sealed class FileEntity
{
    public int Id { get; set; }
    public int DirectoryId { get; set; }
    public DirectoryEntity? Directory { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public long LastWriteTimeUtc { get; set; }
    public int AccessCount { get; set; }
    public long LastAccessedUtc { get; set; }

    public string FullPath => Directory != null && !string.IsNullOrEmpty(Directory.Path)
        ? System.IO.Path.Combine(Directory.Path, FileName)
        : FileName;
}
