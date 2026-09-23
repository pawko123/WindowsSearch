using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using Hub.Models.App;
using Hub.Models.Settings;
using Hub.Services;
using Hub.Services.Results;
using Hub.Services.Windowing;
using Hub.ViewModels;

namespace Hub;

public partial class MainWindow : Window
{
    private const int HotkeyId = 1;
    private const int WmHotkey = 0x0312;
    private const uint ModControl = 0x0002;
    private const uint ModAlt = 0x0001;
    private const uint VkSpace = 0x20;

    private readonly MainViewModel viewModel;
    private readonly Dictionary<AppEntry, System.Windows.Controls.Button> resultButtons = new(ReferenceEqualityComparer.Instance);
    private HwndSource? hwndSource;
    private bool hotkeyRegistered;
    private DateTime lastToggle = DateTime.MinValue;

    public MainWindow(AppSettings settings)
    {
        InitializeComponent();
        viewModel = new MainViewModel(Dispatcher, settings);
        DataContext = viewModel;
        viewModel.SetImageResolver(CreateImageResolver(settings.ImageResolverKind));
        viewModel.SetProviderSearchLimit(settings.SearchLimit);
        Loaded += (_, _) => SearchBox.Focus();
    }

    public void ApplySettings(AppSettings settings)
    {
        viewModel.ApplySettings(settings);
        viewModel.SetProviderSearchLimit(settings.SearchLimit);
        viewModel.SetImageResolver(CreateImageResolver(settings.ImageResolverKind));
    }

    private static IImageResolver CreateImageResolver(ImageResolverKind kind)
    {
        return kind switch
        {
            ImageResolverKind.List => new ListImageResolver(),
            ImageResolverKind.Dictionary => new DictionaryImageResolver(),
            ImageResolverKind.ConcurrentDictionary => new ConcurrentDictionaryImageResolver(),
            _ => new ConcurrentDictionaryImageResolver(),
        };
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _ = viewModel.UpdateVisibleAndResolveAsync(Math.Max(200, ActualWidth));
    }

    public void InitializeHotkey()
    {
        if (hwndSource is not null)
        {
            return;
        }

        var helper = new WindowInteropHelper(this);
        helper.EnsureHandle();

        hwndSource = HwndSource.FromHwnd(helper.Handle);
        hwndSource?.AddHook(WndProc);

        hotkeyRegistered = RegisterHotKey(helper.Handle, HotkeyId, ModControl | ModAlt, VkSpace);
    }

    public void ToggleLauncher()
    {
        var now = DateTime.Now;
        if ((now - lastToggle).TotalMilliseconds < 250)
            return;
        lastToggle = now;

        if (IsVisible)
        {
            viewModel.SetLauncherVisible(false);
            Hide();
            return;
        }

        viewModel.SetLauncherVisible(true);
        Show();
        Activate();
        Topmost = true;
        WindowPositionService.CenterOnCurrentMonitor(this);
        SearchBox.Focus();
        SearchBox.CaretIndex = SearchBox.Text.Length;
    }

    protected override void OnClosed(EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (hotkeyRegistered && handle != IntPtr.Zero)
        {
            UnregisterHotKey(handle, HotkeyId);
        }

        hwndSource?.RemoveHook(WndProc);
        viewModel.Dispose();
        base.OnClosed(e);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            ToggleLauncher();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            viewModel.SetLauncherVisible(false);
            Hide();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            viewModel.LaunchSelected();
            viewModel.ClearSearch();
            viewModel.SetLauncherVisible(false);
            Hide();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Back)
        {
            if (!SearchBox.IsKeyboardFocusWithin)
            {
                SearchBox.Focus();
            }

            if (SearchBox.Text.Length > 0)
            {
                SearchBox.Text = SearchBox.Text[..^1];
                SearchBox.CaretIndex = SearchBox.Text.Length;
            }

            e.Handled = true;
            return;
        }

        if (e.Key is Key.Left or Key.Right)
        {
            if (viewModel.IsSelectedInApps() && MoveAppSelection(e.Key == Key.Right ? 1 : -1))
            {
                e.Handled = true;
            }

            return;
        }

        if (e.Key is Key.Up or Key.Down)
        {
            var moved = viewModel.IsSelectedInApps()
                ? viewModel.MoveFromAppsToProviders()
                : e.Key == Key.Down
                    ? viewModel.MoveProviderSelection(1)
                    : viewModel.MoveProviderUpOrBackToApps();

            if (moved)
            {
                FocusSelectedResult();
                e.Handled = true;
            }

            return;
        }
    }

    private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Allow Shift (for capitals and symbols like _) but ignore Ctrl, Alt, Windows
        if (string.IsNullOrEmpty(e.Text) || (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != ModifierKeys.None)
        {
            return;
        }

        if (!SearchBox.IsKeyboardFocusWithin)
        {
            SearchBox.Focus();
        }

        SearchBox.Text += e.Text;
        SearchBox.CaretIndex = SearchBox.Text.Length;
        e.Handled = true;
    }

    private void ResultButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement frameworkElement && frameworkElement.Tag is AppEntry app)
        {
            viewModel.Launch(app);
        }

        viewModel.SetLauncherVisible(false);
        Hide();
        e.Handled = true;
    }

    private void ResultButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not AppEntry app)
        {
            return;
        }

        resultButtons[app] = button;
        if (ReferenceEquals(viewModel.SelectedApp, app))
        {
            button.Focus();
            button.BringIntoView();
        }
    }

    private void ResultButton_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is AppEntry app)
        {
            resultButtons.Remove(app);
        }
    }

    private bool MoveAppSelection(int offset)
    {
        var moved = viewModel.MoveAppSelection(offset);
        if (moved)
        {
            FocusSelectedResult();
        }

        return moved;
    }

    private void FocusSelectedResult()
    {
        if (viewModel.SelectedApp is null)
        {
            return;
        }

        if (resultButtons.TryGetValue(viewModel.SelectedApp, out var button))
        {
            button.Focus();
            button.BringIntoView();
        }
    }

    private bool allowClose = false;

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!allowClose)
        {
            e.Cancel = true;
            viewModel.SetLauncherVisible(false);
            Hide();
            return;
        }
    }

    public void AllowCloseAndExit()
    {
        allowClose = true;
        Close();
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}