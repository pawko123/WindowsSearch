using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Validation;
using WindowsSearch.Common.Models;

namespace FileIndexProvider.Settings;

[ProviderAction(FileIndexProviderAction.Rebuild, "Rebuild Index", Description = "Truncates and rebuilds the SQLite file index in the background.", PollAction = FileIndexProviderAction.Status, CancelAction = FileIndexProviderAction.Cancel, PollIntervalMs = 1000)]
[ProviderAction(FileIndexProviderAction.Sync, "Sync Changes", Description = "Reconciles SQLite database against filesystem changes.")]
[ProviderAction(FileIndexProviderAction.Status, "Index Status", Description = "Queries active file count, database size, and indexing status.")]
public sealed class FileIndexProviderSettings : ProviderSettingsBase
{
    [Display(Name = "Indexed Directories", Description = "Drives and folders scanned for files.")]
    public List<string> IndexedDirectories { get; set; } = ["C:\\"];

    [Display(Name = "Excluded Directories", Description = "Folders and patterns skipped during crawl.")]
    public List<string> ExcludedDirectories { get; set; } = [
        ".git",
        "node_modules",
        "bin",
        "obj",
        "$Recycle.Bin",
        "System Volume Information",
        "Windows\\WinSxS",
        "AppData\\Local\\Temp",
        "AppData\\Roaming\\Microsoft\\Windows\\Recent",
        "*.lnk"
    ];

    [Display(Name = "Enable Live Watcher", Description = "Listen to file system changes in real-time.")]
    public bool EnableLiveWatcher { get; set; } = true;

    [Range(5, 600)]
    [Display(Name = "Watcher Sync Interval (s)", Description = "Interval in seconds to batch and flush file system changes to the index.")]
    public int WatcherSyncIntervalSeconds { get; set; } = 60;


    public FileIndexProviderSettings()
    {
        Endpoints.NamedPipe = @"\\.\pipe\file_index_provider";
        Endpoints.Http = "http://localhost:5060";
        Endpoints.Grpc = "http://localhost:5061";
    }
}
