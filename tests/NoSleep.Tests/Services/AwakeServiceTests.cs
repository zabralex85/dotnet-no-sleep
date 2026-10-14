using Microsoft.Extensions.Time.Testing;
using NoSleep.Models;
using NoSleep.Services;
using NoSleep.Tests.Fakes;

namespace NoSleep.Tests.Services;

public sealed class AwakeServiceTests
{
	private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));
	private readonly FakePowerRequest _power = new();
	private readonly MemorySettingsStore _store = new();

	[Fact]
	public void Start_DefaultsToIndefiniteWithScreenOn()
	{
		_ = new AwakeService(_power, _store, _time);

		Assert.Equal((true, true), _power.State);
	}

	[Fact]
	public void Start_WithPassedDeadline_IsPassive()
	{
		var store = new MemorySettingsStore(new AwakeSettings().WithTimed(TimeSpan.FromMinutes(5), _time.GetUtcNow().AddHours(-1)));

		var service = new AwakeService(_power, store, _time);

		Assert.Equal(AwakeMode.Passive, service.Settings.Mode);
		Assert.Equal((false, false), _power.State);
	}

	[Fact]
	public void Update_AppliesAndSaves()
	{
		var service = new AwakeService(_power, _store, _time);

		service.Update((s, _) => s with { KeepDisplayOn = false });

		Assert.Equal((true, false), _power.State);
		Assert.False(_store.Saved!.KeepDisplayOn);
	}

	[Fact]
	public void Update_WhenSaveFails_StillApplies()
	{
		var service = new AwakeService(_power, _store, _time);
		_store.FailSaves = true;

		service.Update((s, _) => s.WithPassive());

		Assert.Equal((false, false), _power.State);
	}

	[Fact]
	public void Tick_EndsTimedAtDeadline_AndRaisesChangedMeanwhile()
	{
		var service = new AwakeService(_power, _store, _time);
		service.Update((s, now) => s.WithTimed(TimeSpan.FromMinutes(1), now));
		var changes = 0;
		service.Changed += () => changes++;

		_time.Advance(TimeSpan.FromSeconds(30));
		service.Tick();
		Assert.Equal(AwakeMode.Timed, service.Settings.Mode);
		Assert.Equal(1, changes);

		_time.Advance(TimeSpan.FromSeconds(30));
		service.Tick();
		Assert.Equal(AwakeMode.Passive, service.Settings.Mode);
		Assert.Equal((false, false), _power.State);
	}

	[Fact]
	public void Tick_WithoutDeadline_DoesNothing()
	{
		var service = new AwakeService(_power, _store, _time);
		var changes = 0;
		service.Changed += () => changes++;

		service.Tick();

		Assert.Equal(0, changes);
	}
}
