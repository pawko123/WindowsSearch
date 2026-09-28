using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Models;

namespace Hub.Models.Settings;

public sealed class SearchSettings
{
    [Display(Name = "Image resolver", Description = "Strategy used to cache and resolve app icons in memory.")]
    public ImageResolverKind ImageResolverKind { get; set; } = ImageResolverKind.ConcurrentDictionary;

    [Range(1, int.MaxValue, ErrorMessage = "Search limit must be greater than zero.")]
    [Display(Name = "Search limit", Description = "Maximum number of results Hub requests from each provider per search.")]
    public int SearchLimit { get; set; } = 50;

    [Range(0, 5000, ErrorMessage = "Debounce delay must be between 0 and 5000 ms.")]
    [Display(Name = "Provider debounce delay (ms)", Description = "How long to wait after user stops typing before sending calls to providers.")]
    public int ProviderDebounceDelayMs { get; set; } = 200;
}
