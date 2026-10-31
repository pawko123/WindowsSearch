using BaseProvider.Abstractions;
using WindowsSearch.Common.Models;

namespace BaseProvider.Tests;

public class CommandHandlerTests
{
    private sealed class DummySettings : ProviderSettingsBase
    {
    }

    private sealed class DummyResultFinderWithCommand : IResultFinder<DummySettings>, ICommandHandler<DummySettings>
    {
        public Task<ProviderSearchResponse> FindAsync(ProviderSearchRequest request, DummySettings settings, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ProviderSearchResponse());
        }

        public Task<ProviderCommandResult> ExecuteCommandAsync(ProviderCommand command, DummySettings settings, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ProviderCommandResult
            {
                Success = true,
                Message = $"Executed {command.Action}",
                Details = [new CommandDetail("Action", command.Action)]
            });
        }
    }

    private sealed class DummyResultFinderWithoutCommand : IResultFinder<DummySettings>
    {
        public Task<ProviderSearchResponse> FindAsync(ProviderSearchRequest request, DummySettings settings, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ProviderSearchResponse());
        }
    }

    [Fact]
    public async Task CommandHandler_WhenSupported_ExecutesSuccessfully()
    {
        IResultFinder<DummySettings> finder = new DummyResultFinderWithCommand();
        var handler = finder as ICommandHandler<DummySettings>;
        Assert.NotNull(handler);

        var result = await handler.ExecuteCommandAsync(
            new ProviderCommand { Action = "rebuild" },
            new DummySettings(),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Executed rebuild", result.Message);
        Assert.Equal("rebuild", result.Details[0].Value);
    }

    [Fact]
    public void CommandHandler_WhenNotSupported_TypeCheckFails()
    {
        IResultFinder<DummySettings> finder = new DummyResultFinderWithoutCommand();
        var handler = finder as ICommandHandler<DummySettings>;
        Assert.Null(handler);
    }
}
