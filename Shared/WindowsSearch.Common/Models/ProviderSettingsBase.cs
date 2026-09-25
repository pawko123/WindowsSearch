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
    [YamlMember(Alias = "is_enabled")]
    [Display(Name = "Is enabled", Description = "When false, Hub will not load or interact with this provider at all.")]
    public bool IsEnabled { get; set; } = true;

    [YamlMember(Alias = "transport")]
    [Display(Name = "Transport", Description = "Which channel Hub uses to reach this provider's process.")]
    public ProviderTransportKind Transport { get; set; } = ProviderTransportKind.NamedPipe;

    [YamlMember(Alias = "endpoint_named_pipe")]
    [NamedPipeEndpoint]
    [Display(Name = "Named pipe endpoint", Description = "Pipe address used when Transport is set to NamedPipe.")]
    public string EndpointNamedPipe { get; set; } = @"\\.\pipe\default_provider";

    [YamlMember(Alias = "endpoint_http")]
    [HttpEndpoint]
    [Display(Name = "HTTP endpoint", Description = "Base URL used when Transport is set to Http.")]
    public string EndpointHttp { get; set; } = "http://localhost:5000";

    [YamlMember(Alias = "endpoint_grpc")]
    [HttpEndpoint]
    [Display(Name = "gRPC endpoint", Description = "Base URL used when Transport is set to Grpc.")]
    public string EndpointGrpc { get; set; } = "http://localhost:5001";

    [YamlMember(Alias = "serialization")]
    [Display(Name = "Serialization", Description = "Wire format used for requests and responses with this provider.")]
    public SerializationKind Serialization { get; set; } = SerializationKind.Json;

    [YamlMember(Alias = "timeout_seconds")]
    [Range(1, int.MaxValue, ErrorMessage = "Timeout seconds must be greater than zero.")]
    [Display(Name = "Timeout (seconds)", Description = "How long Hub waits for this provider to respond before giving up.")]
    public int TimeoutSeconds { get; set; } = 5;

    [YamlMember(Alias = "log_level")]
    [Display(Name = "Log level", Description = "Minimum severity this provider's process writes to its own log file.")]
    public LogLevel LogLevel { get; set; } = LogLevel.Info;

    /// <summary>The endpoint that actually applies, based on <see cref="Transport"/>.</summary>
    [YamlIgnore]
    public string ActiveEndpoint => Transport switch
    {
        ProviderTransportKind.Http => EndpointHttp,
        ProviderTransportKind.Grpc => EndpointGrpc,
        _ => EndpointNamedPipe,
    };
}
