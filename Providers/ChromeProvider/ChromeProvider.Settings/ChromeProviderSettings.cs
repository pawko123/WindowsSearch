using System.ComponentModel.DataAnnotations;
using WebBaseProvider.Settings;

namespace ChromeProvider.Settings;

public class ChromeProviderSettings : WebBaseProviderSettings
{
    public ChromeProviderSettings()
    {
        Endpoints.NamedPipe = @"\\.\pipe\chrome_provider";
        Endpoints.Http = "http://localhost:5042";
        Endpoints.Grpc = "http://localhost:5043";
    }

    [Required(ErrorMessage = "Chrome profile name is required.")]
    [Display(Name = "Profile Name", Description = "The folder name of the Chrome profile in %LOCALAPPDATA%\\Google\\Chrome\\User Data (e.g., Default, Profile 1)")]
    public string ProfileName { get; set; } = "Default";
}