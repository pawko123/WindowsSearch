using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Models;
using YamlDotNet.Serialization;

namespace DemoProvider.Settings;

public sealed class DemoProviderSettings : ProviderSettingsBase
{
    [YamlMember(Alias = "echo_prefix")]
    [Required(ErrorMessage = "Echo prefix is required.")]
    [Display(Name = "Echo prefix", Description = "Text prepended to the query in the demo result title.")]
    public string EchoPrefix { get; set; } = "Demo";

    [YamlMember(Alias = "action_path")]
    [Required(ErrorMessage = "Action path is required.")]
    [Display(Name = "Action path", Description = "Executable launched when the demo result is activated.")]
    public string ActionPath { get; set; } = "notepad.exe";

    [YamlMember(Alias = "action_args")]
    [Display(Name = "Action args", Description = "Optional arguments passed to the action executable.")]
    public string ActionArgs { get; set; } = string.Empty;
}
