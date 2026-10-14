using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NoSleep;

internal enum AwakeMode
{
	Passive,
	Indefinite,
	Timed,
	Expirable,
}

/// <summary>
/// Persisted state plus the pure mode transitions (tested without Windows).
/// Timed and Expirable both run until <see cref="Deadline"/>; a passed deadline means Passive.
/// </summary>
internal sealed record Settings
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter() },
	};

	public static string DefaultPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NoSleep", "settings.json");

	public AwakeMode Mode { get; init; } = AwakeMode.Indefinite;

	/// <summary>Mode that "Enabled" switches back to.</summary>
	public AwakeMode LastActiveMode { get; init; } = AwakeMode.Indefinite;

	public bool KeepDisplayOn { get; init; } = true;

	public int IntervalHours { get; init; } = 1;

	public int IntervalMinutes { get; init; }

	public DateTimeOffset? ExpirationDateTime { get; init; }

	/// <summary>End of the running Timed/Expirable mode; null otherwise.</summary>
	public DateTimeOffset? Deadline { get; init; }

	/// <summary>Tray presets for Timed: name → seconds (same shape as PowerToys Awake).</summary>
	public Dictionary<string, int> CustomTrayTimes { get; init; } = new() { ["30 min"] = 1800, ["1 h"] = 3600, ["2 h"] = 7200 };

	[JsonIgnore]
	public bool IsActive => Mode != AwakeMode.Passive;

	[JsonIgnore]
	public TimeSpan Interval => new(IntervalHours, IntervalMinutes, 0);

	public static Settings Load(string path)
	{
		try
		{
			return JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), JsonOptions) ?? new Settings();
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
		{
			// Missing or corrupt: defaults, rewritten on the next save.
			return new Settings();
		}
	}

	public void Save(string path)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		var temp = path + ".tmp";
		File.WriteAllText(temp, JsonSerializer.Serialize(this, JsonOptions));
		File.Move(temp, path, overwrite: true);
	}

	public Settings WithPassive() => this with { Mode = AwakeMode.Passive, Deadline = null };

	public Settings WithIndefinite() => this with { Mode = AwakeMode.Indefinite, LastActiveMode = AwakeMode.Indefinite, Deadline = null };

	public Settings WithTimed(TimeSpan duration, DateTimeOffset now)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
		return this with
		{
			Mode = AwakeMode.Timed,
			LastActiveMode = AwakeMode.Timed,
			IntervalHours = (int)duration.TotalHours,
			IntervalMinutes = duration.Minutes,
			Deadline = now + duration,
		};
	}

	public Settings WithExpirable(DateTimeOffset expiration, DateTimeOffset now)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiration, now);
		return this with { Mode = AwakeMode.Expirable, LastActiveMode = AwakeMode.Expirable, ExpirationDateTime = expiration, Deadline = expiration };
	}

	/// <summary>"Enabled" turned on: Timed restarts its interval, Expirable resumes while its time is ahead, anything else is Indefinite.</summary>
	public Settings Enabled(DateTimeOffset now) => LastActiveMode switch
	{
		AwakeMode.Timed when Interval > TimeSpan.Zero => WithTimed(Interval, now),
		AwakeMode.Expirable when ExpirationDateTime > now => WithExpirable(ExpirationDateTime.Value, now),
		_ => WithIndefinite(),
	};

	/// <summary>Timed/Expirable whose deadline passed (or is missing) become Passive.</summary>
	public Settings Expired(DateTimeOffset now) =>
		Mode is AwakeMode.Timed or AwakeMode.Expirable && !(Deadline > now) ? WithPassive() : this;
}
