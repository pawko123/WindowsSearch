using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using Hub.Models.Settings;
using Hub.Services.Settings;
using WindowsSearch.Common.Logging;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;

namespace Hub;

public partial class App : Application
{
	static App()
	{
		AssemblyLoadContext.Default.Resolving += ResolveSharedAssembly;
	}

	private readonly AppSettingsService settingsService = new();
	private AppSettings currentSettings = new();
	private MainWindow? launcherWindow;
	private SettingsWindow? settingsWindow;
	private TaskbarIcon? notifyIcon;

	private void Application_Startup(object sender, StartupEventArgs e)
	{
		var (settings, errors, _) = settingsService.Load();
		currentSettings = settings;

		AppLogger.Initialize("hub", currentSettings.LogLevel);
		AppLogger.Info("Hub starting up...");

		if (errors.Count > 0)
		{
			MessageBox.Show(string.Join(Environment.NewLine, errors), "Hub settings need attention", MessageBoxButton.OK, MessageBoxImage.Warning);
		}

		launcherWindow = new MainWindow(currentSettings);
		MainWindow = launcherWindow;
		launcherWindow.InitializeHotkey();
		CreateTrayIcon();
	}

	private void CreateTrayIcon()
	{
		notifyIcon = new TaskbarIcon();
		var iconPath = Path.Combine(AppContext.BaseDirectory, "hub.ico");
		notifyIcon.IconSource = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
		try { notifyIcon.ForceCreate(); } catch { } // Ensure handle is created if method exists
		notifyIcon.ToolTipText = "Hub Launcher";
		notifyIcon.Visibility = Visibility.Visible;

		var menu = new ContextMenu();
		
		var settingsItem = new MenuItem { Header = "Settings" };
		settingsItem.Click += (_, _) =>
		{
			Dispatcher.Invoke(OpenSettingsWindow);
		};
		menu.Items.Add(settingsItem);

		var showItem = new MenuItem { Header = "Show/Hide" };
		showItem.Click += (_, _) =>
		{
			Dispatcher.Invoke(() => launcherWindow?.ToggleLauncher());
		};
		menu.Items.Add(showItem);

		var exitItem = new MenuItem { Header = "Exit" };
		exitItem.Click += (_, _) =>
		{
			Dispatcher.Invoke(() =>
			{
				try
				{
					notifyIcon.Visibility = Visibility.Collapsed;
					notifyIcon.Dispose();
				}
				catch { }

				if (launcherWindow is not null)
				{
					launcherWindow.AllowCloseAndExit();
				}
				Shutdown();
			});
		};
		menu.Items.Add(exitItem);

		notifyIcon.ContextMenu = menu;
		notifyIcon.TrayMouseDoubleClick += (_, _) => Dispatcher.Invoke(() => launcherWindow?.ToggleLauncher());
	}

	private void OpenSettingsWindow()
	{
		if (settingsWindow is not null)
		{
			if (settingsWindow.WindowState == WindowState.Minimized)
			{
				settingsWindow.WindowState = WindowState.Normal;
			}

			settingsWindow.Activate();
			return;
		}

		settingsWindow = new SettingsWindow(settingsService, currentSettings, updatedSettings =>
		{
			currentSettings = updatedSettings;
			AppLogger.SetLogLevel(currentSettings.LogLevel);
			launcherWindow?.ApplySettings(updatedSettings);
		});
		settingsWindow.Closed += (_, _) => settingsWindow = null;
		settingsWindow.Show();
		settingsWindow.Activate();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		try
		{
			if (notifyIcon != null) notifyIcon.Visibility = Visibility.Collapsed;
			notifyIcon?.Dispose();
		}
		catch { }

		base.OnExit(e);
	}

	private static Assembly? ResolveSharedAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
	{
		if (string.IsNullOrWhiteSpace(assemblyName.Name) || string.Equals(assemblyName.Name, "Hub", StringComparison.Ordinal))
		{
			return null;
		}

		var sharedAssemblyPath = Path.Combine(AppContext.BaseDirectory, "bin", $"{assemblyName.Name}.dll");
		return File.Exists(sharedAssemblyPath) ? context.LoadFromAssemblyPath(sharedAssemblyPath) : null;
	}
}

