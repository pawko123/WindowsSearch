using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Validation;

namespace WindowsSearch.Common.Models;

public sealed class ProviderEndpoints
{
    [NamedPipeEndpoint]
    [Display(Name = "Named pipe", Description = "Pipe address used when Transport is set to NamedPipe.")]
    public string NamedPipe { get; set; } = @"\\.\pipe\default_provider";

    [HttpEndpoint]
    [Display(Name = "HTTP endpoint", Description = "Base URL used when Transport is set to Http.")]
    public string Http { get; set; } = "http://localhost:5000";

    [HttpEndpoint]
    [Display(Name = "gRPC endpoint", Description = "Base URL used when Transport is set to Grpc.")]
    public string Grpc { get; set; } = "http://localhost:5001";
}
