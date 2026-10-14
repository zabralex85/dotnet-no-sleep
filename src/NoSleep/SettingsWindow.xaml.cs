using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;

namespace NoSleep;

internal sealed partial class SettingsWindow : Window
{
	private readonly AwakeController _controller;
	private readonly ObservableCollection<Preset> _presets;

	public SettingsWindow(AwakeController controller)
	{
		_controller = controller;
		InitializeComponent();

		var s = controller.Settings;
		Hours.Text = s.IntervalHours.ToString(CultureInfo.CurrentCulture);
		Minutes.Text = s.IntervalMinutes.ToString(CultureInfo.CurrentCulture);
		var until = s.ExpirationDateTime > DateTimeOffset.Now ? s.ExpirationDateTime.Value.LocalDateTime : DateTime.Today.AddHours(18);
		Date.SelectedDate = until.Date;
		Time.Text = until.ToString("HH:mm", CultureInfo.InvariantCulture);
		_presets = new(s.CustomTrayTimes.Select(p => new Preset { Name = p.Key, Minutes = p.Value / 60 }));
		Presets.ItemsSource = _presets;
	}

	private void OnStartTimed(object sender, RoutedEventArgs e)
	{
		if (!int.TryParse(Hours.Text, out var hours) | !int.TryParse(Minutes.Text, out var minutes) || hours < 0 || minutes < 0 || hours + minutes == 0)
		{
			Warn("Enter whole hours and minutes; together more than zero.");
			return;
		}

		var duration = new TimeSpan(hours, minutes, 0);
		_controller.Update((s, now) => s.WithTimed(duration, now));
	}

	private void OnStartExpirable(object sender, RoutedEventArgs e)
	{
		if (Date.SelectedDate is not { } date || !TimeOnly.TryParseExact(Time.Text, "H:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
		{
			Warn("Pick a date and enter the time as HH:mm.");
			return;
		}

		var until = new DateTimeOffset(date.Date + time.ToTimeSpan());
		if (until <= DateTimeOffset.Now)
		{
			Warn("That time has already passed.");
			return;
		}

		_controller.Update((s, now) => s.WithExpirable(until, now));
	}

	private void OnSavePresets(object sender, RoutedEventArgs e)
	{
		Presets.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, exitEditingMode: true);
		var presets = _presets.Where(p => !string.IsNullOrWhiteSpace(p.Name)).ToList();
		if (presets.Any(p => p.Minutes <= 0) || presets.DistinctBy(p => p.Name.Trim()).Count() != presets.Count)
		{
			Warn("Each preset needs a unique name and more than zero minutes.");
			return;
		}

		_controller.Update((s, _) => s with { CustomTrayTimes = presets.ToDictionary(p => p.Name.Trim(), p => p.Minutes * 60) });
	}

	private void OnClose(object sender, RoutedEventArgs e) => Close();

	private void Warn(string text) => MessageBox.Show(this, text, "NoSleep", MessageBoxButton.OK, MessageBoxImage.Warning);

	/// <summary>Editable row of the presets grid.</summary>
	public sealed class Preset
	{
		public string Name { get; set; } = string.Empty;

		public int Minutes { get; set; }
	}
}
