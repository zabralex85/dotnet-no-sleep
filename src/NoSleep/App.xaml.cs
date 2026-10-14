using System.Windows;
using System.Windows.Threading;

namespace NoSleep;

internal sealed partial class App : Application
{
	private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromHours(1) };
	private AwakeController? _controller;
	private Updates? _updates;
	private TrayIcon? _tray;
	private SettingsWindow? _settingsWindow;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		DispatcherUnhandledException += OnUnhandledException;

		_controller = new AwakeController(Settings.DefaultPath);
		_updates = new Updates();
		_tray = new TrayIcon(_controller, _updates, ShowSettings, Shutdown);

		_updateTimer.Tick += async (_, _) => await _updates.CheckAsync();
		_updateTimer.Start();
		_ = _updates.CheckAsync();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_updateTimer.Stop();
		_tray?.Dispose();
		_controller?.Dispose();
		base.OnExit(e);
	}

	private void ShowSettings()
	{
		if (_settingsWindow is null)
		{
			_settingsWindow = new SettingsWindow(_controller!);
			_settingsWindow.Closed += (_, _) => _settingsWindow = null;
			_settingsWindow.Show();
		}

		_settingsWindow.Activate();
	}

	private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		// A failed power request call is the likely cause: say so instead of vanishing from the tray.
		e.Handled = true;
		MessageBox.Show(e.Exception.Message, "NoSleep error", MessageBoxButton.OK, MessageBoxImage.Error);
		Shutdown(1);
	}
}
