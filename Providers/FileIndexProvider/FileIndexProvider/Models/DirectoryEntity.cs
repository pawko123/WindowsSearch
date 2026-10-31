namespace FileIndexProvider.Models;

public sealed class DirectoryEntity
{
    public int Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public string DirectoryName { get; set; } = string.Empty;
    public List<FileEntity> Files { get; set; } = [];
}
