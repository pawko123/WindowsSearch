using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Models;
using YamlDotNet.Serialization;

namespace JetBrainsProvider.Settings;

public sealed class JetBrainsProviderSettings : ProviderSettingsBase
{
    [YamlMember(Alias = "cache_ttl_minutes")]
    [Range(1, 1440, ErrorMessage = "Cache TTL must be between 1 and 1440 minutes.")]
    [Display(Name = "Cache TTL (minutes)", Description = "How long recent-project results are cached before JetBrains Toolbox/IDE data is rescanned.")]
    public int CacheTtlMinutes { get; set; } = 5;
}
