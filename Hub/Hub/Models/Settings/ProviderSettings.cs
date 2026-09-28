using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Models;
using WindowsSearch.Common.Serialization;

namespace Hub.Models.Settings;

public sealed class ProviderSettings
{
    [Display(Name = "Provider transport", Description = "How Hub talks to provider processes: named pipe, HTTP, or gRPC.")]
    public ProviderTransportKind ProviderTransportKind { get; set; } = ProviderTransportKind.NamedPipe;

    [Display(Name = "Provider serialization", Description = "Wire format used for provider search requests and responses.")]
    public SerializationKind ProviderSerialization { get; set; } = SerializationKind.Json;

    [Range(1, int.MaxValue, ErrorMessage = "Provider timeout must be greater than zero.")]
    [Display(Name = "Provider timeout (seconds)", Description = "How long Hub waits for a provider to respond before giving up on it.")]
    public int ProviderTimeoutSeconds { get; set; } = 5;
}
