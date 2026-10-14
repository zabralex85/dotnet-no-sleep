using System.IO;
using System.Windows.Threading;

namespace NoSleep;

/// <summary>Owns the current <see cref="Settings"/>, applies them to the power request and expires Timed/Expirable. UI thread only.</summary>
internal sealed class AwakeController : IDisposable
{
	private readonly KeepAwake _keepAwake = new("NoSleep: keep awake");
	private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
	private readonly string _path;

	public AwakeController(string path)
	{
		_path = path;
		Settings = Settings.Load(path).Expired(DateTimeOffset.Now);
		_timer.Tick += (_, _) => Tick();
		Apply();
	}

	/// <summary>Raised on every change and every second while a deadline runs (tooltip countdown).</summary>
	public event Action? Changed;

	public Settings Settings { get; private set; }

	public void Update(Func<Settings, DateTimeOffset, Settings> change)
	{
		Settings = change(Settings, DateTimeOffset.Now);
		try
		{
			Settings.Save(_path);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			// The new state still applies; it is only not remembered across restarts.
		}

		Apply();
	}

	public void Dispose()
	{
		_timer.Stop();
		_keepAwake.Dispose();
	}

	private void Apply()
	{
		_keepAwake.Apply(system: Settings.IsActive, display: Settings.IsActive && Settings.KeepDisplayOn);
		_timer.IsEnabled = Settings.Deadline is not null;
		Changed?.Invoke();
	}

	private void Tick()
	{
		// Checked every tick, so a deadline that passed during sleep or a clock change still ends the mode.
		if (Settings.Expired(DateTimeOffset.Now) != Settings)
		{
			Update((s, now) => s.Expired(now));
		}
		else
		{
			Changed?.Invoke();
		}
	}
}
