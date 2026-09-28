using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Models;

namespace WebBaseProvider.Settings;

public class WebBaseProviderSettings : ProviderSettingsBase
{
    [Display(Name = "Enable Web Search", Description = "Query suggestions from the web")]
    public bool EnableSearch { get; set; } = true;

    [Display(Name = "Search Engine", Description = "Select the engine for web queries")]
    public SearchEngine Engine { get; set; } = SearchEngine.Google;
}