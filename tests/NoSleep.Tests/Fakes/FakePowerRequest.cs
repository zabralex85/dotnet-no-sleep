using NoSleep.Services;

namespace NoSleep.Tests.Fakes;

internal sealed class FakePowerRequest : IPowerRequest
{
	public (bool System, bool Display) State { get; private set; }

	public void Apply(bool system, bool display) => State = (system, display);

	public void Dispose()
	{
	}
}
