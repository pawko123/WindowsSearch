using System.ComponentModel.DataAnnotations;
using WebBaseProvider.Settings;

namespace FirefoxProvider.Settings;

public class FirefoxProviderSettings : WebBaseProviderSettings
{
    public FirefoxProviderSettings()
    {
        Endpoints.NamedPipe = @"\\.\pipe\firefox_provider";
        Endpoints.Http = "http://localhost:5040";
        Endpoints.Grpc = "http://localhost:5041";
    }

    [Required(ErrorMessage = "Firefox profile name is required.")]
    [Display(Name = "Profile Name", Description = "The folder name of the Firefox profile in %APPDATA%\\Mozilla\\Firefox\\Profiles (e.g., xvi9uw4p.default-release)")]
    public string ProfileName { get; set; } = "xvi9uw4p.default-release";
}