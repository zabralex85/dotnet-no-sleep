using NoSleep.Models;

namespace NoSleep.Services;

internal interface ISettingsStore
{
	/// <summary>Never throws: a missing or corrupt store gives defaults.</summary>
	AwakeSettings Load();

	void Save(AwakeSettings settings);
}
