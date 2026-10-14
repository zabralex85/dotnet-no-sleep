using System.Drawing;
using System.Windows.Forms;

namespace NoSleep;

/// <summary>Tray icon and menu. The menu is rebuilt on every open, so it always shows the current state.</summary>
internal sealed class TrayIcon : IDisposable
{
	private const int MaxTooltip = 127;

	private readonly AwakeController _controller;
	private readonly Updates _updates;
	private readonly Action _showSettings;
	private readonly Action _exit;
	private readonly Dictionary<AwakeMode, Icon> _icons;
	private readonly NotifyIcon _icon;

	public TrayIcon(AwakeController controller, Updates updates, Action showSettings, Action exit)
	{
		_controller = controller;
		_updates = updates;
		_showSettings = showSettings;
		_exit = exit;
		_icons = new()
		{
			[AwakeMode.Passive] = LoadIcon("passive"),
			[AwakeMode.Indefinite] = LoadIcon("indefinite"),
			[AwakeMode.Timed] = LoadIcon("timed"),
			[AwakeMode.Expirable] = LoadIcon("expirable"),
		};
		_icon = new NotifyIcon { ContextMenuStrip = new ContextMenuStrip() };
		_icon.ContextMenuStrip.Opening += (_, _) => BuildMenu(_icon.ContextMenuStrip.Items);
		_icon.MouseDoubleClick += (_, e) =>
		{
			if (e.Button == MouseButtons.Left)
			{
				ToggleEnabled();
			}
		};
		_controller.Changed += Refresh;
		Refresh();
		_icon.Visible = true;
	}

	public static string Describe(Settings s, DateTimeOffset now)
	{
		var screen = s.KeepDisplayOn ? "screen on" : "screen off";
		return s.Mode switch
		{
			AwakeMode.Indefinite => $"Indefinite, {screen}",
			AwakeMode.Timed => $"{Remaining(s, now)} left, {screen}",
			AwakeMode.Expirable => $"until {s.Deadline:g}, {screen}",
			_ => "Off (power plan)",
		};
	}

	public void ShowMessage(string text) => _icon.ShowBalloonTip(5000, "NoSleep", text, ToolTipIcon.None);

	public void Dispose()
	{
		_controller.Changed -= Refresh;

		// Hide first, or a "ghost" icon stays in the tray until the mouse passes over it.
		_icon.Visible = false;
		_icon.ContextMenuStrip?.Dispose();
		_icon.Dispose();
		foreach (var icon in _icons.Values)
		{
			icon.Dispose();
		}
	}

	private static string Remaining(Settings s, DateTimeOffset now)
	{
		var left = s.Deadline - now ?? TimeSpan.Zero;
		return left < TimeSpan.Zero ? "0:00:00" : $"{(int)left.TotalHours}:{left:mm\\:ss}";
	}

	private static Icon LoadIcon(string name)
	{
		using (var stream = System.Windows.Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/{name}.ico")).Stream)
		{
			return new Icon(stream, SystemInformation.SmallIconSize);
		}
	}

	private static ToolStripMenuItem Item(string text, bool isChecked, Action onClick)
	{
		var item = new ToolStripMenuItem(text) { Checked = isChecked };
		item.Click += (_, _) => onClick();
		return item;
	}

	private void Refresh()
	{
		var s = _controller.Settings;
		var text = $"NoSleep: {Describe(s, DateTimeOffset.Now)}";
		_icon.Text = text.Length > MaxTooltip ? text[..MaxTooltip] : text;
		_icon.Icon = _icons[s.Mode];
	}

	private void ToggleEnabled() =>
		_controller.Update((s, now) => s.IsActive ? s.WithPassive() : s.Enabled(now));

	private void BuildMenu(ToolStripItemCollection items)
	{
		var s = _controller.Settings;
		foreach (var old in items.Cast<ToolStripItem>().ToList())
		{
			old.Dispose();
		}

		items.Add(new ToolStripMenuItem($"NoSleep: {Describe(s, DateTimeOffset.Now)}") { Enabled = false });
		items.Add(Item("Enabled", s.IsActive, ToggleEnabled));
		items.Add(new ToolStripSeparator());

		items.Add(Item("Off (power plan)", s.Mode == AwakeMode.Passive, () => _controller.Update((x, _) => x.WithPassive())));
		items.Add(Item("Indefinite", s.Mode == AwakeMode.Indefinite, () => _controller.Update((x, _) => x.WithIndefinite())));
		var timed = new ToolStripMenuItem("Timed") { Checked = s.Mode == AwakeMode.Timed };
		foreach (var (name, seconds) in s.CustomTrayTimes.Where(p => p.Value > 0))
		{
			timed.DropDownItems.Add(Item(name, false, () => _controller.Update((x, now) => x.WithTimed(TimeSpan.FromSeconds(seconds), now))));
		}

		timed.DropDownItems.Add(new ToolStripSeparator());
		timed.DropDownItems.Add(Item("Custom…", false, _showSettings));
		items.Add(timed);
		items.Add(Item("Until date/time…", s.Mode == AwakeMode.Expirable, _showSettings));
		items.Add(new ToolStripSeparator());

		items.Add(Item("Keep screen on", s.KeepDisplayOn, () => _controller.Update((x, _) => x with { KeepDisplayOn = !x.KeepDisplayOn })));
		items.Add(Item("Start with Windows", Startup.IsEnabled, () =>
		{
			if (Startup.IsEnabled)
			{
				Startup.Disable();
			}
			else
			{
				Startup.Enable();
			}
		}));
		items.Add(new ToolStripSeparator());

		items.Add(Item("Settings…", false, _showSettings));
		if (_updates.PendingVersion is { } version)
		{
			items.Add(Item($"Update to v{version}", false, () => _ = ApplyUpdateAsync()));
		}

		items.Add(Item("Check for updates", false, () => _ = CheckForUpdatesAsync()));
		items.Add(Item("Exit", false, _exit));
	}

	private async Task CheckForUpdatesAsync()
	{
		if (!await _updates.CheckAsync())
		{
			ShowMessage("Could not reach GitHub to check for updates.");
		}
		else
		{
			ShowMessage(_updates.PendingVersion is { } version ? $"Update to v{version} is available in the menu." : $"Up to date (v{_updates.CurrentVersion}).");
		}
	}

	private async Task ApplyUpdateAsync()
	{
		try
		{
			await _updates.ApplyAsync();
		}
		catch (Exception e)
		{
			ShowMessage($"Update failed: {e.Message}");
		}
	}
}
