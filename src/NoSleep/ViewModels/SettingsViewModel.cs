using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NoSleep.Services;

namespace NoSleep.ViewModels;

/// <summary>Settings window: Timed interval, Expirable date/time and the tray presets. Validates before changing anything.</summary>
internal sealed partial class SettingsViewModel : ObservableObject
{
	private readonly AwakeService _awake;

	[ObservableProperty]
	private string _hours;

	[ObservableProperty]
	private string _minutes;

	[ObservableProperty]
	private DateTime? _date;

	[ObservableProperty]
	private string _time;

	public SettingsViewModel(AwakeService awake)
	{
		_awake = awake;
		var s = awake.Settings;
		_hours = s.IntervalHours.ToString(CultureInfo.CurrentCulture);
		_minutes = s.IntervalMinutes.ToString(CultureInfo.CurrentCulture);
		var until = s.ExpirationDateTime > awake.Now ? s.ExpirationDateTime.Value.LocalDateTime : awake.Now.LocalDateTime.Date.AddHours(18);
		_date = until.Date;
		_time = until.ToString("HH:mm", CultureInfo.InvariantCulture);
		Presets = new(s.CustomTrayTimes.Select(p => new PresetRow { Name = p.Key, Minutes = p.Value / 60 }));
	}

	/// <summary>Input was rejected; the message says why.</summary>
	public event Action<string>? ValidationFailed;

	public ObservableCollection<PresetRow> Presets { get; }

	[RelayCommand]
	private void StartTimed()
	{
		var hoursOk = int.TryParse(Hours, out var hours) && hours >= 0;
		var minutesOk = int.TryParse(Minutes, out var minutes) && minutes >= 0;
		if (!hoursOk || !minutesOk || hours + minutes == 0)
		{
			ValidationFailed?.Invoke("Enter whole hours and minutes; together more than zero.");
			return;
		}

		_awake.Update((s, now) => s.WithTimed(new TimeSpan(hours, minutes, 0), now));
	}

	[RelayCommand]
	private void StartUntil()
	{
		if (Date is not { } date || !TimeOnly.TryParseExact(Time, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
		{
			ValidationFailed?.Invoke("Pick a date and enter the time as HH:mm.");
			return;
		}

		var until = new DateTimeOffset(date.Date + time.ToTimeSpan());
		if (until <= _awake.Now)
		{
			ValidationFailed?.Invoke("That time has already passed.");
			return;
		}

		_awake.Update((s, now) => s.WithExpirable(until, now));
	}

	[RelayCommand]
	private void SavePresets()
	{
		var presets = Presets.Where(p => !string.IsNullOrWhiteSpace(p.Name)).ToList();
		if (presets.Exists(p => p.Minutes <= 0) || presets.DistinctBy(p => p.Name.Trim()).Count() != presets.Count)
		{
			ValidationFailed?.Invoke("Each preset needs a unique name and more than zero minutes.");
			return;
		}

		_awake.Update((s, _) => s with { CustomTrayTimes = presets.ToDictionary(p => p.Name.Trim(), p => p.Minutes * 60) });
	}
}
