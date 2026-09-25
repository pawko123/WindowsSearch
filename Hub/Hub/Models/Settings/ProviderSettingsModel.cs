using WindowsSearch.Common.Models;

namespace Hub.Models.Settings;

public sealed class ProviderSettingsModel
{
    public string ProviderName { get; set; } = string.Empty;

    public string SettingsPath { get; set; } = string.Empty;

    public ProviderSettingsBase Settings { get; set; } = new GenericProviderSettings();
}
