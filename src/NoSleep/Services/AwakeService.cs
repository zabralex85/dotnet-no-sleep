using System.IO;
using NoSleep.Models;

namespace NoSleep.Services;

/// <summary>
/// Owns the current <see cref="AwakeSettings"/>: applies them to the power request, saves them and expires Timed/Expirable.
/// Not thread-safe: used from the UI thread, which calls <see cref="Tick"/> every second.
/// </summary>
internal sealed class AwakeService
{
	private readonly IPowerRequest _power;
	private readonly ISettingsStore _store;
	private readonly TimeProvider _time;

	public AwakeService(IPowerRequest power, ISettingsStore store, TimeProvider time)
	{
		_power = power;
		_store = store;
		_time = time;
		Settings = store.Load().Expired(Now);
		Apply();
	}

	/// <summary>Raised on every change and on every tick while a deadline runs (tooltip countdown).</summary>
	public event Action? Changed;

	public AwakeSettings Settings { get; private set; }

	public DateTimeOffset Now => _time.GetLocalNow();

	public void Update(Func<AwakeSettings, DateTimeOffset, AwakeSettings> change)
	{
		Settings = change(Settings, Now);
		try
		{
			_store.Save(Settings);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			// The new state still applies; it is only not remembered across restarts.
		}

		Apply();
	}

	/// <summary>Checked every second, so a deadline that passed during sleep or a clock change still ends the mode.</summary>
	public void Tick()
	{
		if (Settings.Deadline is null)
		{
			return;
		}

		if (!ReferenceEquals(Settings.Expired(Now), Settings))
		{
			Update((s, now) => s.Expired(now));
		}
		else
		{
			Changed?.Invoke();
		}
	}

	private void Apply()
	{
		_power.Apply(system: Settings.IsActive, display: Settings.IsActive && Settings.KeepDisplayOn);
		Changed?.Invoke();
	}
}
