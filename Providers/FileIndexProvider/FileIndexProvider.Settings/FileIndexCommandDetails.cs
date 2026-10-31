using System.ComponentModel.DataAnnotations;
using WindowsSearch.Common.Models;

namespace FileIndexProvider.Settings;

public sealed class FileIndexCommandDetails : CommandDetailsBase
{
    [Display(Name = "Files", Order = 1)]
    public string? Files { get; init; }

    [Display(Name = "Directories", Order = 2)]
    public string? Directories { get; init; }

    [Display(Name = "DB Size", Order = 3)]
    public string? DbSize { get; init; }

    [Display(Name = "Status", Order = 4)]
    public string? Status { get; init; }

    [Display(Name = "Last Scan", Order = 5)]
    public string? LastScan { get; init; }
}
