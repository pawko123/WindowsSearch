using System.Collections.ObjectModel;
using System.Windows.Input;
using Hub.Common;
using WindowsSearch.Common.Logging;
using WindowsSearch.Common.Models;

namespace Hub.ViewModels.Settings;

public class ActionSettingItem : SettingItemViewModel
{
    private readonly Func<string, Task<ProviderCommandResult>>? _executor;
    private CancellationTokenSource? _pollCts;

    public string Action { get; }
    public string ButtonText { get; }
    public string? PollAction { get; set; }
    public string? CancelAction { get; set; }
    public int PollIntervalMs { get; set; } = 1000;

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            _isRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanCancel));
        }
    }

    public bool CanCancel => !string.IsNullOrEmpty(CancelAction) && IsRunning;

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    private double? _progressPercentage;
    public double? ProgressPercentage
    {
        get => _progressPercentage;
        set
        {
            _progressPercentage = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<CommandDetail> Details { get; } = new();

    public ICommand ExecuteCommand { get; }
    public ICommand CancelCommand { get; }

    public ActionSettingItem(
        string action,
        string buttonText,
        string description,
        Func<string, Task<ProviderCommandResult>>? executor = null,
        string? pollAction = null,
        string? cancelAction = null,
        int pollIntervalMs = 1000)
        : base(null, null, buttonText, description)
    {
        Action = action;
        ButtonText = buttonText;
        _executor = executor;
        PollAction = pollAction;
        CancelAction = cancelAction;
        PollIntervalMs = pollIntervalMs;

        ExecuteCommand = new RelayCommand(async _ => await ExecuteAsync(), _ => !IsRunning);
        CancelCommand = new RelayCommand(async _ => await CancelAsync(), _ => CanCancel);
    }

    public override void Apply()
    {
        // No-op for action buttons
    }

    public async Task ExecuteAsync()
    {
        if (_executor == null || IsRunning)
        {
            return;
        }

        try
        {
            IsRunning = true;
            StatusMessage = "Executing...";
            ProgressPercentage = null;
            Details.Clear();

            var result = await _executor(Action);
            IsRunning = result.IsRunning;
            StatusMessage = result.Message;
            ProgressPercentage = result.ProgressPercentage;

            UpdateDetails(result.Details);

            if (IsRunning && !string.IsNullOrEmpty(PollAction))
            {
                _pollCts?.Dispose();
                _pollCts = new CancellationTokenSource();
                _ = PollStatusLoopAsync(PollAction, Math.Max(250, PollIntervalMs), _pollCts.Token);
            }
        }
        catch (Exception ex)
        {
            IsRunning = false;
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public async Task CancelAsync()
    {
        if (_executor == null || string.IsNullOrEmpty(CancelAction))
        {
            return;
        }

        try
        {
            _pollCts?.Cancel();
            StatusMessage = "Cancelling...";

            var cancelResult = await _executor(CancelAction);
            IsRunning = false;
            StatusMessage = cancelResult.Message;
            ProgressPercentage = null;
            UpdateDetails(cancelResult.Details);
        }
        catch (Exception ex)
        {
            IsRunning = false;
            StatusMessage = $"Cancel error: {ex.Message}";
        }
        finally
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task PollStatusLoopAsync(string pollAction, int intervalMs, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsRunning)
        {
            try
            {
                await Task.Delay(intervalMs, cancellationToken);
                if (cancellationToken.IsCancellationRequested || !IsRunning || _executor == null) break;

                var pollResult = await _executor(pollAction);
                if (cancellationToken.IsCancellationRequested) break;

                IsRunning = pollResult.IsRunning;
                StatusMessage = pollResult.Message;
                ProgressPercentage = pollResult.ProgressPercentage;
                UpdateDetails(pollResult.Details);

                if (!pollResult.IsRunning)
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLogger.Debug($"Poll action error: {ex.Message}");
            }
        }

        IsRunning = false;
        CommandManager.InvalidateRequerySuggested();
    }

    private void UpdateDetails(List<CommandDetail>? details)
    {
        Details.Clear();
        if (details != null)
        {
            foreach (var detail in details)
            {
                Details.Add(detail);
            }
        }
    }
}
