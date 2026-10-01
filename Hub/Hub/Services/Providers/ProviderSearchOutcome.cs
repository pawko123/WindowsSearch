using System.Collections.Generic;
using Hub.Models.Results;

namespace Hub.Services.Providers;

public sealed class ProviderSearchOutcome
{
    public string ProviderName { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public System.Windows.Media.ImageSource? IconImage { get; set; }
    public IReadOnlyList<ProviderCategoryResultUi> Categories { get; set; } = [];
}