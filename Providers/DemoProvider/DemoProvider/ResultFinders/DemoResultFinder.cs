using BaseProvider.Abstractions;
using BaseProvider.Models;
using WindowsSearch.Common.Models;

namespace DemoProvider.ResultFinders;

public sealed class DemoResultFinder : IResultFinder
{
    public Task<ProviderSearchResponse> FindAsync(ProviderSearchRequest request, CancellationToken cancellationToken)
    {
        var limit = Math.Max(1, request.Limit);
        var prefix = request.Settings.TryGetValue("echo_prefix", out var echoPrefix)
            ? echoPrefix
            : "Demo";

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
                            Score = 1.0,
                            ActionPath = request.Settings.TryGetValue("action_path", out var actionPath) ? actionPath : "notepad.exe",
                            ActionArgs = request.Settings.TryGetValue("action_args", out var actionArgs) && !string.IsNullOrWhiteSpace(actionArgs)
                                ? [actionArgs]
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
                Score = 0.5,
                ActionPath = "notepad.exe",
                IconPath = "Icons/static-result.png"
            });
        }

        return Task.FromResult(response);
    }
}
