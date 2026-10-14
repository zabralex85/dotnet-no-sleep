namespace NoSleep.Tests;

public sealed class SettingsTests
{
	private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.FromHours(3));

	[Fact]
	public void Timed_SetsDeadlineAndRemembersInterval()
	{
		var s = new Settings().WithTimed(TimeSpan.FromMinutes(90), Now);

		Assert.Equal(AwakeMode.Timed, s.Mode);
		Assert.Equal(Now.AddMinutes(90), s.Deadline);
		Assert.Equal((1, 30), (s.IntervalHours, s.IntervalMinutes));
	}

	[Fact]
	public void Expirable_InThePast_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new Settings().WithExpirable(Now, Now));
	}

	[Fact]
	public void Expired_PassedDeadline_IsPassive_RunningDeadline_Unchanged()
	{
		var s = new Settings().WithTimed(TimeSpan.FromHours(1), Now);

		Assert.Same(s, s.Expired(Now.AddMinutes(59)));
		var expired = s.Expired(Now.AddHours(1));
		Assert.Equal(AwakeMode.Passive, expired.Mode);
		Assert.Null(expired.Deadline);
		Assert.Equal(AwakeMode.Timed, expired.LastActiveMode);
	}

	[Fact]
	public void Expired_TimedWithoutDeadline_IsPassive()
	{
		Assert.Equal(AwakeMode.Passive, new Settings { Mode = AwakeMode.Timed }.Expired(Now).Mode);
	}

	[Fact]
	public void Enabled_RestartsTimedInterval()
	{
		var s = new Settings().WithTimed(TimeSpan.FromMinutes(30), Now).WithPassive().Enabled(Now.AddHours(5));

		Assert.Equal(AwakeMode.Timed, s.Mode);
		Assert.Equal(Now.AddHours(5).AddMinutes(30), s.Deadline);
	}

	[Fact]
	public void Enabled_ResumesExpirableOnlyWhileAhead()
	{
		var off = new Settings().WithExpirable(Now.AddHours(2), Now).WithPassive();

		Assert.Equal(AwakeMode.Expirable, off.Enabled(Now.AddHours(1)).Mode);
		Assert.Equal(AwakeMode.Indefinite, off.Enabled(Now.AddHours(3)).Mode);
	}

	[Fact]
	public void SaveLoad_RoundTrips()
	{
		var path = Path.Combine(Path.GetTempPath(), $"NoSleep-{Guid.NewGuid():N}", "settings.json");
		try
		{
			var saved = new Settings { KeepDisplayOn = false, CustomTrayTimes = new() { ["4 h"] = 14400 } }.WithExpirable(Now.AddDays(1), Now);
			saved.Save(path);

			var loaded = Settings.Load(path);

			Assert.Equal(saved with { CustomTrayTimes = loaded.CustomTrayTimes }, loaded);
			Assert.Equal(saved.CustomTrayTimes, loaded.CustomTrayTimes);
			Assert.Contains("\"mode\": \"Expirable\"", File.ReadAllText(path), StringComparison.Ordinal);
		}
		finally
		{
			Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
		}
	}

	[Fact]
	public void Load_MissingOrCorrupt_GivesDefaults()
	{
		var path = Path.Combine(Path.GetTempPath(), $"NoSleep-{Guid.NewGuid():N}.json");
		Assert.Equal(AwakeMode.Indefinite, Settings.Load(path).Mode);

		File.WriteAllText(path, "{ not json");
		try
		{
			Assert.Equal(AwakeMode.Indefinite, Settings.Load(path).Mode);
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Fact]
	public void StartupCommand_QuotesPath()
	{
		Assert.Equal("\"C:\\Users\\a b\\NoSleep.exe\"", Startup.Command(@"C:\Users\a b\NoSleep.exe"));
	}
}
