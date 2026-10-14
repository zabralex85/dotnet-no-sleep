using System.Windows;
using System.Windows.Threading;
using NoSleep.Services;
using NoSleep.ViewModels;
using NoSleep.Views;

namespace NoSleep;

/// <summary>Composition root: creates the services, view models and views, and drives the timers.</summary>
internal sealed partial class App : Application
{
	private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
	private readonly DispatcherTimer _updateCheck = new() { Interval = TimeSpan.FromHours(1) };
	private PowerRequest? _power;
	private AwakeService? _awake;
	private TrayIcon? _tray;
	private SettingsWindow? _settingsWindow;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		DispatcherUnhandledException += OnUnhandledException;

		_power = new PowerRequest("NoSleep: keep awake");
		_awake = new AwakeService(_power, new JsonSettingsStore(JsonSettingsStore.DefaultPath), TimeProvider.System);
		var trayVm = new TrayViewModel(_awake, new RegistryAutoStart(), new VelopackUpdateService());
		_tray = new TrayIcon(trayVm, ShowSettings, Shutdown);

		_tick.Tick += (_, _) => _awake.Tick();
		_tick.Start();
		_updateCheck.Tick += async (_, _) => await trayVm.CheckForUpdatesSilentlyAsync();
		_updateCheck.Start();
		_ = trayVm.CheckForUpdatesSilentlyAsync();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_tick.Stop();
		_updateCheck.Stop();
		_tray?.Dispose();
		_power?.Dispose();
		base.OnExit(e);
	}

	private void ShowSettings()
	{
		if (_settingsWindow is null)
		{
			_settingsWindow = new SettingsWindow(new SettingsViewModel(_awake!));
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
