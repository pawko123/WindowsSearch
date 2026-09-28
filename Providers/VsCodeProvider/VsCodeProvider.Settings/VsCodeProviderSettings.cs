using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Models;
using YamlDotNet.Serialization;

namespace VsCodeProvider.Settings;

public sealed class VsCodeProviderSettings : ProviderSettingsBase
{
    public VsCodeProviderSettings()
    {
        Endpoints.NamedPipe = @"\\.\pipe\vscode_provider";
        Endpoints.Http = "http://localhost:5030";
        Endpoints.Grpc = "http://localhost:5031";
    }

    [Required(ErrorMessage = "Jump List ID is required.")]
    [RegularExpression(@"^[0-9a-fA-F]{16}$", ErrorMessage = "Jump List ID must be exactly 16 hexadecimal characters.")]
    [Display(Name = "Jump List ID", Description = "ID of the VS Code Jump List file.")]
    public string JumpListId { get; set; } = "1ced32d74a95c7bc";

    [Range(1, 1440, ErrorMessage = "Cache TTL must be between 1 and 1440 minutes.")]
    [Display(Name = "Cache TTL (minutes)", Description = "How long to keep results in cache.")]
    public int CacheTtlMinutes { get; set; } = 5;
}