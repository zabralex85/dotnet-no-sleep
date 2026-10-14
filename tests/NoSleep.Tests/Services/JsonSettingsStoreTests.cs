using NoSleep.Models;
using NoSleep.Services;

namespace NoSleep.Tests.Services;

public sealed class JsonSettingsStoreTests : IDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.FromHours(3));
	private readonly string _dir = Path.Combine(Path.GetTempPath(), $"NoSleep-{Guid.NewGuid():N}");

	public void Dispose()
	{
		if (Directory.Exists(_dir))
		{
			Directory.Delete(_dir, recursive: true);
		}
	}

	[Fact]
	public void SaveLoad_RoundTrips()
	{
		var path = Path.Combine(_dir, "settings.json");
		var store = new JsonSettingsStore(path);
		var saved = new AwakeSettings { KeepDisplayOn = false, CustomTrayTimes = new() { ["4 h"] = 14400 } }.WithExpirable(Now.AddDays(1), Now);

		store.Save(saved);
		var loaded = store.Load();

		Assert.Equal(saved with { CustomTrayTimes = loaded.CustomTrayTimes }, loaded);
		Assert.Equal(saved.CustomTrayTimes, loaded.CustomTrayTimes);
		Assert.Contains("\"mode\": \"Expirable\"", File.ReadAllText(path), StringComparison.Ordinal);
	}

	[Fact]
	public void Load_MissingOrCorrupt_GivesDefaults()
	{
		var path = Path.Combine(_dir, "settings.json");
		Assert.Equal(new AwakeSettings().Mode, new JsonSettingsStore(path).Load().Mode);

		Directory.CreateDirectory(_dir);
		File.WriteAllText(path, "{ not json");
		Assert.Equal(AwakeMode.Indefinite, new JsonSettingsStore(path).Load().Mode);
	}

	[Fact]
	public void AutoStartCommand_QuotesPath()
	{
		Assert.Equal("\"C:\\Users\\a b\\NoSleep.exe\"", RegistryAutoStart.Command(@"C:\Users\a b\NoSleep.exe"));
	}
}
