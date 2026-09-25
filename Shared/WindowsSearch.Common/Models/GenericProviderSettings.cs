using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace WindowsSearch.Common.Models;

/// <summary>
/// Fallback settings type used when a provider does not ship its own "*.Settings.dll" -
/// keeps unmigrated or third-party providers working with a free-form key/value editor.
/// </summary>
public sealed class GenericProviderSettings : ProviderSettingsBase
{
    [YamlMember(Alias = "settings")]
    [Display(Name = "Provider settings", Description = "Free-form key/value settings for providers without their own typed *.Settings.dll.")]
    public Dictionary<string, string> Extra { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
