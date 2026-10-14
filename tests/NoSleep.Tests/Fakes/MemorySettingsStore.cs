using NoSleep.Models;
using NoSleep.Services;

namespace NoSleep.Tests.Fakes;

internal sealed class MemorySettingsStore(AwakeSettings? initial = null) : ISettingsStore
{
	public AwakeSettings? Saved { get; private set; } = initial;

	/// <summary>Simulates a read-only or locked settings file.</summary>
	public bool FailSaves { get; set; }

	public AwakeSettings Load() => Saved ?? new AwakeSettings();

	public void Save(AwakeSettings settings)
	{
		if (FailSaves)
		{
			throw new IOException("locked");
		}

		Saved = settings;
	}
}
