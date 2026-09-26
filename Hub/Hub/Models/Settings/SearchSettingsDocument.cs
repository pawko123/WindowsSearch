using YamlDotNet.Serialization;

namespace Hub.Models.Settings;

internal sealed class SearchSettingsDocument
{
    [YamlMember(Alias = "image_resolver")]
    public string? ImageResolver { get; set; }

    [YamlMember(Alias = "limit")]
    public int? Limit { get; set; }

    [YamlMember(Alias = "debounce_delay_ms")]
    public int? DebounceDelayMs { get; set; }
}
