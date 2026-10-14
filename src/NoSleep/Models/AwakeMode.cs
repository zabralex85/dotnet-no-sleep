namespace NoSleep.Models;

/// <summary>The PowerToys Awake modes.</summary>
internal enum AwakeMode
{
	/// <summary>No power request: the power plan applies.</summary>
	Passive,

	Indefinite,

	/// <summary>Until <see cref="AwakeSettings.Deadline"/>, started from an interval.</summary>
	Timed,

	/// <summary>Until <see cref="AwakeSettings.Deadline"/>, a date/time picked by the user.</summary>
	Expirable,
}
