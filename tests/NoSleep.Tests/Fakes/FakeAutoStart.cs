using NoSleep.Services;

namespace NoSleep.Tests.Fakes;

internal sealed class FakeAutoStart : IAutoStart
{
	public bool IsEnabled { get; private set; }

	public void Enable() => IsEnabled = true;

	public void Disable() => IsEnabled = false;
}
