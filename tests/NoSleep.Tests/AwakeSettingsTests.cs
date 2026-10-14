using NoSleep.Models;

namespace NoSleep.Tests;

public sealed class AwakeSettingsTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.FromHours(3));

	[Fact]
	public void Timed_SetsDeadlineAndRemembersInterval()
	{
		var s = new AwakeSettings().WithTimed(TimeSpan.FromMinutes(90), Now);

		Assert.Equal(AwakeMode.Timed, s.Mode);
		Assert.Equal(Now.AddMinutes(90), s.Deadline);
		Assert.Equal((1, 30), (s.IntervalHours, s.IntervalMinutes));
	}

	[Fact]
	public void Expirable_InThePast_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new AwakeSettings().WithExpirable(Now, Now));
	}

	[Fact]
	public void Expired_PassedDeadline_IsPassive_RunningDeadline_Unchanged()
	{
		var s = new AwakeSettings().WithTimed(TimeSpan.FromHours(1), Now);

		Assert.Same(s, s.Expired(Now.AddMinutes(59)));
		var expired = s.Expired(Now.AddHours(1));
		Assert.Equal(AwakeMode.Passive, expired.Mode);
		Assert.Null(expired.Deadline);
		Assert.Equal(AwakeMode.Timed, expired.LastActiveMode);
	}

	[Fact]
	public void Expired_TimedWithoutDeadline_IsPassive()
	{
		Assert.Equal(AwakeMode.Passive, new AwakeSettings { Mode = AwakeMode.Timed }.Expired(Now).Mode);
	}

	[Fact]
	public void Enabled_RestartsTimedInterval()
	{
		var s = new AwakeSettings().WithTimed(TimeSpan.FromMinutes(30), Now).WithPassive().Enabled(Now.AddHours(5));

		Assert.Equal(AwakeMode.Timed, s.Mode);
		Assert.Equal(Now.AddHours(5).AddMinutes(30), s.Deadline);
	}

	[Fact]
	public void Enabled_ResumesExpirableOnlyWhileAhead()
	{
		var off = new AwakeSettings().WithExpirable(Now.AddHours(2), Now).WithPassive();

		Assert.Equal(AwakeMode.Expirable, off.Enabled(Now.AddHours(1)).Mode);
		Assert.Equal(AwakeMode.Indefinite, off.Enabled(Now.AddHours(3)).Mode);
	}
}
