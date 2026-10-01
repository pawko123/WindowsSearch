using BaseProvider.Abstractions;
using DemoProvider.Settings;
using WindowsSearch.Common.Models;

namespace DemoProvider.ResultFinders;

public sealed class DemoResultFinder : IResultFinder<DemoProviderSettings>
{
    public Task<ProviderSearchResponse> FindAsync(ProviderSearchRequest request, DemoProviderSettings settings, CancellationToken cancellationToken)
    {
        var limit = Math.Max(1, request.Limit);
        var prefix = settings.EchoPrefix;

        var response = new ProviderSearchResponse
        {
            Categories =
            [
                new ProviderResultCategory
                {
                    Name = "Demo results",
                    IconPath = "Icons/demo-category.png",
                    Items =
                    [
                        new ProviderResultItem
                        {
                            Title = $"{prefix}: {request.Query}",
                            Subtitle = "Echo result from demo provider",
                            ActionPath = settings.ActionPath,
                            ActionArgs = !string.IsNullOrWhiteSpace(settings.ActionArgs)
                                ? [settings.ActionArgs]
                                : [],
                            IconPath = "Icons/echo-result.png",
                        }
                    ]
                }
            ]
        };

        if (limit > 1)
        {
            response.Categories[0].Items.Add(new ProviderResultItem
            {
                Title = "Static example",
                Subtitle = "Useful for testing the settings window",
                ActionPath = "notepad.exe",
                IconPath = "Icons/static-result.png"
            });
        }

        return Task.FromResult(response);
    }
}
