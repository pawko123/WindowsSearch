using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using Hub.Models.Settings;
using Hub.Services.Settings;
using CommonLogging;
using Application = System.Windows.Application;

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
	private NotifyIcon? notifyIcon;

	private void Application_Startup(object sender, StartupEventArgs e)
	{
		AppLogger.Initialize("hub");
		AppLogger.Info("Hub starting up...");

		var (settings, errors, _) = settingsService.Load();
		currentSettings = settings;

		if (errors.Count > 0)
		{
			System.Windows.MessageBox.Show(string.Join(Environment.NewLine, errors), "Hub settings need attention", MessageBoxButton.OK, MessageBoxImage.Warning);
		}

		launcherWindow = new MainWindow(currentSettings);
		MainWindow = launcherWindow;
		launcherWindow.InitializeHotkey();
		CreateTrayIcon();
	}

	private void CreateTrayIcon()
	{
		notifyIcon = new NotifyIcon();
		notifyIcon.Icon = SystemIcons.Application;
		notifyIcon.Text = "Hub Launcher";
		notifyIcon.Visible = true;

		var menu = new ContextMenuStrip();
		var settingsItem = new ToolStripMenuItem("Settings");
		settingsItem.Click += (_, _) =>
		{
			Dispatcher.Invoke(OpenSettingsWindow);
		};
		menu.Items.Add(settingsItem);

		var showItem = new ToolStripMenuItem("Show/Hide");
		showItem.Click += (_, _) =>
		{
			Dispatcher.Invoke(() => launcherWindow?.ToggleLauncher());
		};
		menu.Items.Add(showItem);

		var exitItem = new ToolStripMenuItem("Exit");
		exitItem.Click += (_, _) =>
		{
			Dispatcher.Invoke(() =>
			{
				try
				{
					notifyIcon.Visible = false;
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

		notifyIcon.ContextMenuStrip = menu;
		notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(() => launcherWindow?.ToggleLauncher());
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
			notifyIcon?.Visible = false;
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

