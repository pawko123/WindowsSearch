using WindowsSearch.Common.Models;

namespace BaseProvider.Abstractions;

public interface ICommandHandler<in TSettings>
{
    Task<ProviderCommandResult> ExecuteCommandAsync(ProviderCommand command, TSettings settings, CancellationToken cancellationToken);
}
