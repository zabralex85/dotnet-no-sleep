using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NoSleep.Models;
using NoSleep.Services;

namespace NoSleep.ViewModels;

/// <summary>State and commands of the tray icon and its menu.</summary>
internal sealed partial class TrayViewModel : ObservableObject
{
	private readonly AwakeService _awake;
	private readonly IAutoStart _autoStart;
	private readonly IUpdateService _updates;

	public TrayViewModel(AwakeService awake, IAutoStart autoStart, IUpdateService updates)
	{
		_awake = awake;
		_autoStart = autoStart;
		_updates = updates;

		// Every awake property derives from the service's settings.
		_awake.Changed += () => OnPropertyChanged(string.Empty);
	}

	/// <summary>A short notification for the user (tray balloon).</summary>
	public event Action<string>? MessageRequested;

	public AwakeMode Mode => _awake.Settings.Mode;

	public bool IsEnabled => _awake.Settings.IsActive;

	public bool KeepDisplayOn => _awake.Settings.KeepDisplayOn;

	public bool StartWithWindows => _autoStart.IsEnabled;

	/// <summary>Timed presets: name → seconds.</summary>
	public IReadOnlyDictionary<string, int> Presets => _awake.Settings.CustomTrayTimes;

	public string? PendingVersion => _updates.PendingVersion;

	public string Status => Describe(_awake.Settings, _awake.Now);

	public static string Describe(AwakeSettings s, DateTimeOffset now)
	{
		var screen = s.KeepDisplayOn ? "screen on" : "screen off";
		return s.Mode switch
		{
			AwakeMode.Indefinite => $"Indefinite, {screen}",
			AwakeMode.Timed => $"{Remaining(s.Deadline - now)} left, {screen}",
			AwakeMode.Expirable => $"until {s.Deadline?.LocalDateTime:g}, {screen}",
			_ => "Off (power plan)",
		};
	}

	/// <summary>The silent hourly check: only refreshes <see cref="PendingVersion"/>.</summary>
	public async Task CheckForUpdatesSilentlyAsync()
	{
		await _updates.CheckAsync();
		OnPropertyChanged(nameof(PendingVersion));
	}

	private static string Remaining(TimeSpan? left) =>
		left is { } l && l > TimeSpan.Zero ? $"{(int)l.TotalHours}:{l:mm\\:ss}" : "0:00:00";

	[RelayCommand]
	private void ToggleEnabled() => _awake.Update((s, now) => s.IsActive ? s.WithPassive() : s.Enabled(now));

	[RelayCommand]
	private void SetPassive() => _awake.Update((s, _) => s.WithPassive());

	[RelayCommand]
	private void SetIndefinite() => _awake.Update((s, _) => s.WithIndefinite());

	[RelayCommand]
	private void StartTimed(int seconds) => _awake.Update((s, now) => s.WithTimed(TimeSpan.FromSeconds(seconds), now));

	[RelayCommand]
	private void ToggleKeepDisplayOn() => _awake.Update((s, _) => s with { KeepDisplayOn = !s.KeepDisplayOn });

	[RelayCommand]
	private void ToggleStartWithWindows()
	{
		if (_autoStart.IsEnabled)
		{
			_autoStart.Disable();
		}
		else
		{
			_autoStart.Enable();
		}

		OnPropertyChanged(nameof(StartWithWindows));
	}

	[RelayCommand]
	private async Task CheckForUpdatesAsync()
	{
		var reached = await _updates.CheckAsync();
		OnPropertyChanged(nameof(PendingVersion));
		if (!reached)
		{
			MessageRequested?.Invoke("Could not reach GitHub to check for updates.");
		}
		else if (PendingVersion is { } version)
		{
			MessageRequested?.Invoke($"Update to v{version} is available in the menu.");
		}
		else
		{
			MessageRequested?.Invoke($"Up to date (v{_updates.CurrentVersion}).");
		}
	}

	[RelayCommand]
	private async Task ApplyUpdateAsync()
	{
		try
		{
			await _updates.ApplyAsync();
		}
		catch (Exception e)
		{
			MessageRequested?.Invoke($"Update failed: {e.Message}");
		}
	}
}
