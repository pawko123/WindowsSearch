using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;
using WindowsSearch.Common.Logging;

namespace Hub.Models.Settings;

public sealed class AppSettings
{
    [Display(Name = "Image resolver", Description = "Strategy used to cache and resolve app icons in memory.")]
    public ImageResolverKind ImageResolverKind { get; set; } = ImageResolverKind.ConcurrentDictionary;

    [Display(Name = "Provider transport", Description = "How Hub talks to provider processes: named pipe, HTTP, or gRPC.")]
    public ProviderTransportKind ProviderTransportKind { get; set; } = ProviderTransportKind.NamedPipe;

    [Display(Name = "Provider serialization", Description = "Wire format used for provider search requests and responses.")]
    public SerializationKind ProviderSerialization { get; set; } = SerializationKind.Json;

    [Display(Name = "Log level (Hub)", Description = "Minimum severity written to Hub's own log file.")]
    public LogLevel LogLevel { get; set; } = LogLevel.Info;

    [Range(1, int.MaxValue, ErrorMessage = "Provider timeout must be greater than zero.")]
    [Display(Name = "Provider timeout (seconds)", Description = "How long Hub waits for a provider to respond before giving up on it.")]
    public int ProviderTimeoutSeconds { get; set; } = 5;

    [Range(1, int.MaxValue, ErrorMessage = "Search limit must be greater than zero.")]
    [Display(Name = "Search limit", Description = "Maximum number of results Hub requests from each provider per search.")]
    public int SearchLimit { get; set; } = 50;
}
