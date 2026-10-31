using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows.Input;
using Hub.Common;
using Microsoft.Win32;

namespace Hub.ViewModels.Settings;

public class PathListSettingItem : SettingItemViewModel
{
    public ObservableCollection<string> Paths { get; } = new();
    public ObservableCollection<string> AvailableDrives { get; } = new();

    private string? _selectedDrive;
    public string? SelectedDrive
    {
        get => _selectedDrive;
        set
        {
            _selectedDrive = value;
            OnPropertyChanged();
        }
    }

    private string _newPathText = string.Empty;
    public string NewPathText
    {
        get => _newPathText;
        set
        {
            _newPathText = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddNewPathCommand { get; }
    public ICommand BrowseFolderCommand { get; }
    public ICommand AddDriveCommand { get; }
    public ICommand RemovePathCommand { get; }
    public PathListSettingItem(object targetObject, PropertyInfo property, string label, string description, IEnumerable<string> initialPaths)
        : base(targetObject, property, label, description)
    {
        foreach (var path in initialPaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                Paths.Add(path);
            }
        }

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    AvailableDrives.Add(drive.RootDirectory.FullName);
                }
            }
        }
        catch
        {
            // Ignore drive enumeration errors
        }

        if (AvailableDrives.Count > 0)
        {
            SelectedDrive = AvailableDrives[0];
        }

        AddNewPathCommand = new RelayCommand(AddNewPath);
        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        AddDriveCommand = new RelayCommand(AddDrive);
        RemovePathCommand = new RelayCommand(RemovePath);
    }

    public override void Apply()
    {
        Property?.SetValue(TargetObject, Paths.ToList());
    }

    public void AddPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var trimmed = path.Trim();
        if (!Paths.Any(p => string.Equals(p, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            Paths.Add(trimmed);
        }
    }

    private void BrowseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = $"Select {Label}",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var folder in dialog.FolderNames)
            {
                AddPath(folder);
            }
        }
    }

    private void AddDrive(object? parameter)
    {
        var driveToAdd = (parameter as string) ?? SelectedDrive;
        if (!string.IsNullOrWhiteSpace(driveToAdd))
        {
            AddPath(driveToAdd);
        }
    }

    private void RemovePath(object? parameter)
    {
        if (parameter is string path)
        {
            Paths.Remove(path);
        }
    }

    private void AddNewPath()
    {
        if (!string.IsNullOrWhiteSpace(NewPathText))
        {
            AddPath(NewPathText);
            NewPathText = string.Empty;
        }
    }
}
