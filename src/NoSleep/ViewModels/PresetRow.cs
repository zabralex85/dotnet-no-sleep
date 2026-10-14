namespace NoSleep.ViewModels;

/// <summary>Editable row of the presets grid (minutes; stored as seconds).</summary>
internal sealed class PresetRow
{
	public string Name { get; set; } = string.Empty;

	public int Minutes { get; set; }
}
