using CommonValidation;
using YamlDotNet.Serialization;

namespace Hub.Models.Settings;

public sealed class ProviderFileSettingsDocument
{
    [YamlMember(Alias = "transport")]
    public string Transport { get; set; } = "NamedPipe";

    [YamlMember(Alias = "endpoint_named_pipe")]
    [NamedPipeEndpoint]
    public string EndpointNamedPipe { get; set; } = @"\\.\pipe\default_provider";

    [YamlMember(Alias = "endpoint_http")]
    [HttpEndpoint]
    public string EndpointHttp { get; set; } = "http://localhost:5000";

    [YamlMember(Alias = "endpoint_grpc")]
    [HttpEndpoint]
    public string EndpointGrpc { get; set; } = "http://localhost:5001";

    [YamlMember(Alias = "serialization")]
    public string Serialization { get; set; } = "json";

    [YamlMember(Alias = "timeout_seconds")]
    public int TimeoutSeconds { get; set; } = 5;

    [YamlMember(Alias = "settings")]
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}