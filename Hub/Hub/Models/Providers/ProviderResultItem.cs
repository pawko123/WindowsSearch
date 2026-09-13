namespace Hub.Models.Providers;

public sealed class ProviderResultItem
{
    public string Title { get; set; } = string.Empty;

    public string Subtitle { get; set; } = string.Empty;

    public double Score { get; set; }

    public string ActionPath { get; set; } = string.Empty;

    public List<string> ActionArgs { get; set; } = [];

    public string? IconPath { get; set; }
}