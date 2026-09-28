using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Logging;
using WindowsSearch.Common.Serialization;
using WindowsSearch.Common.Validation;
using YamlDotNet.Serialization;

namespace WindowsSearch.Common.Models;

/// <summary>
/// Fields every provider shares (transport/endpoint/serialization/logging). Provider-specific
/// settings live on subclasses defined in each provider's own "*.Settings" project, discovered
/// by Hub via reflection - see WindowsSearch.Common.Reflection.ProviderSettingsTypeLocator.
/// </summary>
public abstract class ProviderSettingsBase
{
    [Display(Name = "Is enabled", Description = "When false, Hub will not load or interact with this provider at all.")]
    public bool IsEnabled { get; set; } = true;

    [Display(Name = "Log level", Description = "Minimum severity this provider's process writes to its own log file.")]
    public LogLevel LogLevel { get; set; } = LogLevel.Info;

    [Display(Name = "Endpoints")]
    public ProviderEndpoints Endpoints { get; set; } = new();
}