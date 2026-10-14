using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using NoSleep.Models;
using NoSleep.ViewModels;

namespace NoSleep.Views;

/// <summary>
/// Renders <see cref="TrayViewModel"/> as a WinForms tray icon (WPF has none). The menu is rebuilt on every open,
/// so it always shows the current state; its items only call view-model commands.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
	private const int MaxTooltip = 127;

	private readonly TrayViewModel _vm;
	private readonly Action _showSettings;
	private readonly Action _exit;
	private readonly Dictionary<AwakeMode, Icon> _icons;
	private readonly NotifyIcon _icon;

	public TrayIcon(TrayViewModel vm, Action showSettings, Action exit)
	{
		_vm = vm;
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
				_vm.ToggleEnabledCommand.Execute(null);
			}
		};
		_vm.PropertyChanged += OnViewModelChanged;
		_vm.MessageRequested += ShowMessage;
		Refresh();
		_icon.Visible = true;
	}

	public void Dispose()
	{
		_vm.PropertyChanged -= OnViewModelChanged;
		_vm.MessageRequested -= ShowMessage;

		// Hide first, or a "ghost" icon stays in the tray until the mouse passes over it.
		_icon.Visible = false;
		_icon.ContextMenuStrip?.Dispose();
		_icon.Dispose();
		foreach (var icon in _icons.Values)
		{
			icon.Dispose();
		}
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

	private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

	private void ShowMessage(string text) => _icon.ShowBalloonTip(5000, "NoSleep", text, ToolTipIcon.None);

	private void Refresh()
	{
		var text = $"NoSleep: {_vm.Status}";
		_icon.Text = text.Length > MaxTooltip ? text[..MaxTooltip] : text;
		_icon.Icon = _icons[_vm.Mode];
	}

	private void BuildMenu(ToolStripItemCollection items)
	{
		foreach (var old in items.Cast<ToolStripItem>().ToList())
		{
			old.Dispose();
		}

		items.Add(new ToolStripMenuItem($"NoSleep: {_vm.Status}") { Enabled = false });
		items.Add(Item("Enabled", _vm.IsEnabled, () => _vm.ToggleEnabledCommand.Execute(null)));
		items.Add(new ToolStripSeparator());

		items.Add(Item("Off (power plan)", _vm.Mode == AwakeMode.Passive, () => _vm.SetPassiveCommand.Execute(null)));
		items.Add(Item("Indefinite", _vm.Mode == AwakeMode.Indefinite, () => _vm.SetIndefiniteCommand.Execute(null)));
		var timed = new ToolStripMenuItem("Timed") { Checked = _vm.Mode == AwakeMode.Timed };
		foreach (var (name, seconds) in _vm.Presets.Where(p => p.Value > 0))
		{
			timed.DropDownItems.Add(Item(name, false, () => _vm.StartTimedCommand.Execute(seconds)));
		}

		timed.DropDownItems.Add(new ToolStripSeparator());
		timed.DropDownItems.Add(Item("Custom…", false, _showSettings));
		items.Add(timed);
		items.Add(Item("Until date/time…", _vm.Mode == AwakeMode.Expirable, _showSettings));
		items.Add(new ToolStripSeparator());

		items.Add(Item("Keep screen on", _vm.KeepDisplayOn, () => _vm.ToggleKeepDisplayOnCommand.Execute(null)));
		items.Add(Item("Start with Windows", _vm.StartWithWindows, () => _vm.ToggleStartWithWindowsCommand.Execute(null)));
		items.Add(new ToolStripSeparator());

		items.Add(Item("Settings…", false, _showSettings));
		if (_vm.PendingVersion is { } version)
		{
			items.Add(Item($"Update to v{version}", false, () => _vm.ApplyUpdateCommand.Execute(null)));
		}

		items.Add(Item("Check for updates", false, () => _vm.CheckForUpdatesCommand.Execute(null)));
		items.Add(Item("Exit", false, _exit));
	}
}
