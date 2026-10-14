namespace NoSleep.Services;

/// <summary>"Start with Windows".</summary>
internal interface IAutoStart
{
	bool IsEnabled { get; }

	void Enable();

	void Disable();
}
