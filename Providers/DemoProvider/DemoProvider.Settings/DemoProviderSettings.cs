using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Models;
using YamlDotNet.Serialization;

namespace DemoProvider.Settings;

public sealed class DemoProviderSettings : ProviderSettingsBase
{
    public DemoProviderSettings()
    {
        Endpoints.NamedPipe = @"\\.\pipe\demo_provider";
        Endpoints.Http = "http://localhost:5010";
        Endpoints.Grpc = "http://localhost:5011";
    }

    [Required(ErrorMessage = "Echo prefix is required.")]
    [Display(Name = "Echo prefix", Description = "Text prepended to the query in the demo result title.")]
    public string EchoPrefix { get; set; } = "Demo";

    [Required(ErrorMessage = "Action path is required.")]
    [Display(Name = "Action path", Description = "Executable launched when the demo result is activated.")]
    public string ActionPath { get; set; } = "notepad.exe";

    [Display(Name = "Action args", Description = "Optional arguments passed to the action executable.")]
    public string ActionArgs { get; set; } = string.Empty;
}
