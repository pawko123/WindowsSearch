namespace BaseProvider.Models;

public sealed class ProviderResultCategory
{
    public string Name { get; set; } = string.Empty;

    public string? IconPath { get; set; }

    public List<ProviderResultItem> Items { get; set; } = [];
}