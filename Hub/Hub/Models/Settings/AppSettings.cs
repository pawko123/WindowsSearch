using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;
using WindowsSearch.Common.Logging;

namespace Hub.Models.Settings;

public sealed class AppSettings
{
    [Display(Name = "Search")]
    public SearchSettings Search { get; set; } = new();

    [Display(Name = "Provider communication")]
    public ProviderSettings Provider { get; set; } = new();

    [Range(1, 1440, ErrorMessage = "App cache TTL must be between 1 and 1440 minutes.")]
    [Display(Name = "App cache TTL (minutes)", Description = "How long installed applications are cached in memory.")]
    public int AppCacheTtlMinutes { get; set; } = 60;

    [Display(Name = "Log level (Hub)", Description = "Minimum severity written to Hub's own log file.")]
    public LogLevel LogLevel { get; set; } = LogLevel.Info;
}
