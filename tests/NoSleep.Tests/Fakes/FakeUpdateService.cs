using NoSleep.Services;

namespace NoSleep.Tests.Fakes;

internal sealed class FakeUpdateService : IUpdateService
{
	public bool Reachable { get; set; } = true;

	public string? Available { get; set; }

	public string CurrentVersion => "1.0.0";

	public string? PendingVersion { get; private set; }

	public Task<bool> CheckAsync()
	{
		if (Reachable)
		{
			PendingVersion = Available;
		}

		return Task.FromResult(Reachable);
	}

	public Task ApplyAsync() => throw new InvalidOperationException("download failed");
}
