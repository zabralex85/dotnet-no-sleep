namespace NoSleep.Services;

internal interface IUpdateService
{
	string CurrentVersion { get; }

	/// <summary>Newer release found by the last check; null when up to date or not checked.</summary>
	string? PendingVersion { get; }

	/// <summary>False when the check could not reach the release feed.</summary>
	Task<bool> CheckAsync();

	/// <summary>Downloads the pending release, then exits this process and starts the new version.</summary>
	Task ApplyAsync();
}
