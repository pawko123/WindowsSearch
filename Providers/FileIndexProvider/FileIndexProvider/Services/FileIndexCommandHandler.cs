using FileIndexProvider.Database.Repositories;
using FileIndexProvider.Settings;
using WindowsSearch.Common.Logging;
using WindowsSearch.Common.Models;

namespace FileIndexProvider.Services;

public sealed class FileIndexCommandHandler
{
    private readonly FileCrawlerService _crawler;
    private readonly IFileRepository _fileRepository;
    private readonly IBuildHistoryRepository _buildHistoryRepository;

    public FileIndexCommandHandler(
        FileCrawlerService crawler,
        IFileRepository fileRepository,
        IBuildHistoryRepository buildHistoryRepository)
    {
        _crawler = crawler;
        _fileRepository = fileRepository;
        _buildHistoryRepository = buildHistoryRepository;
    }

    public async Task<ProviderCommandResult> ExecuteCommandAsync(
        ProviderCommand command,
        FileIndexProviderSettings settings,
        CancellationToken cancellationToken = default)
    {
        AppLogger.Info($"[FileIndexCommandHandler] Received command: '{command.Action}'");

        if (!Enum.TryParse<FileIndexProviderAction>(command.Action, ignoreCase: true, out var actionEnum))
        {
            AppLogger.Warn($"[FileIndexCommandHandler] Unknown action '{command.Action}' received.");
            return new ProviderCommandResult
            {
                Success = false,
                Message = $"Unknown action '{command.Action}'."
            };
        }

        switch (actionEnum)
        {
            case FileIndexProviderAction.Rebuild:
            {
                var started = _crawler.StartRebuild(settings);
                AppLogger.Info($"[FileIndexCommandHandler] Rebuild command triggered (Started={started}).");
                return new ProviderCommandResult
                {
                    Success = true,
                    IsRunning = true,
                    Message = started ? "Index rebuild started in background." : "Rebuild already in progress."
                };
            }

            case FileIndexProviderAction.Cancel:
            {
                AppLogger.Warn("[FileIndexCommandHandler] User requested index crawl cancellation.");
                _crawler.Cancel();
                return new ProviderCommandResult
                {
                    Success = true,
                    IsRunning = false,
                    Message = "Rebuild cancelled by user."
                };
            }

            case FileIndexProviderAction.Sync:
            {
                var stats = await _fileRepository.GetStatsAsync(cancellationToken);
                AppLogger.Info($"[FileIndexCommandHandler] Sync command executed. Indexed files: {stats.TotalFiles}, Size: {stats.FormattedSize}");
                var syncDetails = new FileIndexCommandDetails
                {
                    Files = stats.TotalFiles.ToString(),
                    DbSize = stats.FormattedSize
                };
                return new ProviderCommandResult
                {
                    Success = true,
                    IsRunning = false,
                    Message = $"Index synchronized. {stats.TotalFiles} files indexed.",
                    Details = syncDetails.ToDetails()
                };
            }

            case FileIndexProviderAction.Status:
            {
                var stats = await _fileRepository.GetStatsAsync(cancellationToken);
                var lastBuild = await _buildHistoryRepository.GetLastCompletedBuildAsync(cancellationToken);
                var lastScanText = lastBuild != null ? lastBuild.Summary : (string.IsNullOrEmpty(stats.LastFullScanUtc) ? "Never" : stats.LastFullScanUtc);

                AppLogger.Debug($"[FileIndexCommandHandler] Status query: {stats.TotalFiles} files, Running={_crawler.IsRunning}, LastScan='{lastScanText}'");
                var statusDetails = new FileIndexCommandDetails
                {
                    Files = stats.TotalFiles.ToString(),
                    Directories = stats.TotalDirectories.ToString(),
                    DbSize = stats.FormattedSize,
                    Status = _crawler.IsRunning ? "Crawling" : "Idle",
                    LastScan = lastScanText
                };
                return new ProviderCommandResult
                {
                    Success = true,
                    IsRunning = _crawler.IsRunning,
                    Message = _crawler.IsRunning
                        ? $"Rebuilding index... ({_crawler.FilesIndexed} files indexed)"
                        : $"Index ready. {stats.TotalFiles} files indexed ({stats.FormattedSize})",
                    Details = statusDetails.ToDetails()
                };
            }

            default:
            {
                AppLogger.Warn($"[FileIndexCommandHandler] Unknown action '{command.Action}' received.");
                return new ProviderCommandResult
                {
                    Success = false,
                    Message = $"Unknown action '{command.Action}'."
                };
            }
        }
    }
}
