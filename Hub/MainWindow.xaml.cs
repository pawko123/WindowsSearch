using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using Hub.Services;
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
    private HwndSource? hwndSource;
    private bool hotkeyRegistered;
    private DateTime lastToggle = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        viewModel = new MainViewModel(Dispatcher);
        DataContext = viewModel;
        viewModel.SetImageResolver(new DictionaryImageResolver());
        Loaded += (_, _) => SearchBox.Focus();
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
        if (e.Key == Key.Right || e.Key == Key.Down)
        {
            viewModel.MoveSelection(1);
            ResultsList.ScrollIntoView(viewModel.SelectedApp);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Left || e.Key == Key.Up)
        {
            viewModel.MoveSelection(-1);
            ResultsList.ScrollIntoView(viewModel.SelectedApp);
            e.Handled = true;
            return;
        }
    }

    private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Text) || Keyboard.Modifiers != ModifierKeys.None)
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

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        viewModel.LaunchSelected();
        viewModel.SetLauncherVisible(false);
        Hide();
        e.Handled = true;
    }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (viewModel.SelectedApp is not null)
        {
            ResultsList.ScrollIntoView(viewModel.SelectedApp);
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