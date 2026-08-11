using System.Windows;
using Application = System.Windows.Application;

namespace Hub;

public partial class App : Application
{
	private MainWindow? launcherWindow;
	private NotifyIcon? notifyIcon;

	private void Application_Startup(object sender, StartupEventArgs e)
	{
		launcherWindow = new MainWindow();
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
}

