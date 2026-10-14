namespace NoSleep.Services;

/// <summary>Keeps the system and/or the display awake while set.</summary>
internal interface IPowerRequest : IDisposable
{
	void Apply(bool system, bool display);
}
