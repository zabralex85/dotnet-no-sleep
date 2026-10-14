using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NoSleep.Models;

namespace NoSleep.Services;

/// <summary>settings.json, written atomically (temp file, then replace).</summary>
internal sealed class JsonSettingsStore(string path) : ISettingsStore
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter() },
	};

	/// <summary><c>%APPDATA%\NoSleep\settings.json</c>; not under <c>%LOCALAPPDATA%\NoSleep</c>, which Velopack deletes on uninstall.</summary>
	public static string DefaultPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NoSleep", "settings.json");

	public AwakeSettings Load()
	{
		try
		{
			return JsonSerializer.Deserialize<AwakeSettings>(File.ReadAllText(path), JsonOptions) ?? new AwakeSettings();
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
		{
			// Missing or corrupt: defaults, rewritten on the next save.
			return new AwakeSettings();
		}
	}

	public void Save(AwakeSettings settings)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		var temp = path + ".tmp";
		File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
		File.Move(temp, path, overwrite: true);
	}
}
