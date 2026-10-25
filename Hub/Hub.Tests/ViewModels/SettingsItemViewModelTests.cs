using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using Hub.Converters;
using Hub.ViewModels.Settings;
using WindowsSearch.Common.Models;

namespace Hub.Tests.ViewModels;

public class SettingsItemViewModelTests
{
    private sealed class DummyTarget
    {
        public string Text { get; set; } = string.Empty;
        public List<string> Paths { get; set; } = [];
    }

    private sealed class TestSettingItem : SettingItemViewModel
    {
        public TestSettingItem(object target, PropertyInfo prop) : base(target, prop, "Label", "Desc") { }
        public override void Apply() { }
    }

    [Fact]
    public void SettingItemViewModel_ErrorsManagement_UpdatesHasErrorsAndErrorText()
    {
        var target = new DummyTarget();
        var prop = typeof(DummyTarget).GetProperty(nameof(DummyTarget.Text))!;
        var item = new TestSettingItem(target, prop);

        Assert.False(item.HasErrors);
        Assert.Null(item.ErrorText);

        item.Errors.Add("Error 1");
        Assert.True(item.HasErrors);
        Assert.Equal("Error 1", item.ErrorText);

        item.Errors.Add("Error 2");
        Assert.True(item.HasErrors);
        Assert.Equal(2, item.Errors.Count);
        Assert.Equal("Error 1", item.ErrorText); // Preserves first error for halo/triggers

        item.Errors.Clear();
        Assert.False(item.HasErrors);
        Assert.Null(item.ErrorText);
    }

    [Fact]
    public void PathListSettingItem_AddRemoveApply_WorksCorrectly()
    {
        var target = new DummyTarget { Paths = ["C:\\Existing"] };
        var prop = typeof(DummyTarget).GetProperty(nameof(DummyTarget.Paths))!;
        var item = new PathListSettingItem(target, prop, "Paths", "Description", target.Paths);

        Assert.Single(item.Paths);
        Assert.Equal("C:\\Existing", item.Paths[0]);

        // Add duplicate should be ignored
        item.AddPath("C:\\Existing");
        Assert.Single(item.Paths);

        // Add new path
        item.AddPath("D:\\NewFolder");
        Assert.Equal(2, item.Paths.Count);

        // Remove path via command
        item.RemovePathCommand.Execute("C:\\Existing");
        Assert.Single(item.Paths);
        Assert.Equal("D:\\NewFolder", item.Paths[0]);

        // Apply
        item.Apply();
        Assert.Single(target.Paths);
        Assert.Equal("D:\\NewFolder", target.Paths[0]);
    }

    [Fact]
    public async Task ActionSettingItem_ExecuteAsync_InvokesExecutorAndUpdatesState()
    {
        bool executed = false;
        var item = new ActionSettingItem("rebuild", "Rebuild", "Description", async action =>
        {
            executed = true;
            await Task.Yield();
            return new ProviderCommandResult
            {
                Success = true,
                Message = "Done indexing",
                IsRunning = false,
                ProgressPercentage = 100.0,
                Details = [new CommandDetail("Files", "42")]
            };
        });

        Assert.Equal("rebuild", item.Action);
        Assert.Equal("Rebuild", item.ButtonText);
        Assert.False(item.IsRunning);

        await item.ExecuteAsync();

        Assert.True(executed);
        Assert.False(item.IsRunning);
        Assert.Equal("Done indexing", item.StatusMessage);
        Assert.Equal(100.0, item.ProgressPercentage);
        Assert.Single(item.Details);
        Assert.Equal("Files", item.Details[0].Key);
        Assert.Equal("42", item.Details[0].Value);
    }

    [Fact]
    public async Task ActionSettingItem_PollingAndCancellation_WorksCorrectly()
    {
        var actionsCalled = new List<string>();
        var item = new ActionSettingItem(
            action: "rebuild",
            buttonText: "Rebuild",
            description: "Desc",
            executor: action =>
            {
                actionsCalled.Add(action);
                if (action == "rebuild")
                {
                    return Task.FromResult(new ProviderCommandResult
                    {
                        Success = true,
                        IsRunning = true,
                        Message = "Rebuilding..."
                    });
                }
                if (action == "cancel")
                {
                    return Task.FromResult(new ProviderCommandResult
                    {
                        Success = true,
                        IsRunning = false,
                        Message = "Cancelled"
                    });
                }
                return Task.FromResult(new ProviderCommandResult { Success = false });
            },
            pollAction: "status",
            cancelAction: "cancel",
            pollIntervalMs: 50);

        Assert.False(item.CanCancel);

        var execTask = item.ExecuteAsync();
        Assert.True(item.IsRunning);
        Assert.True(item.CanCancel);

        // Cancel
        await item.CancelAsync();

        Assert.False(item.IsRunning);
        Assert.False(item.CanCancel);
        Assert.Equal("Cancelled", item.StatusMessage);
        Assert.Contains("cancel", actionsCalled);
    }
}
