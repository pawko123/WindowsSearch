namespace Hub.Models;

public sealed class AppEntry
{
    public required string Name { get; init; }
    public required string ExecutablePath { get; init; }
    public string? Arguments { get; init; }
    public string? Source { get; init; }
    public string? IconPath { get; set; }
    public System.Windows.Media.ImageSource? IconImage { get; set; }
}
