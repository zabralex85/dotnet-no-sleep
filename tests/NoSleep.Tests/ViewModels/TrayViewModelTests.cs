using Microsoft.Extensions.Time.Testing;
using NoSleep.Models;
using NoSleep.Services;
using NoSleep.Tests.Fakes;
using NoSleep.ViewModels;

namespace NoSleep.Tests.ViewModels;

public sealed class TrayViewModelTests
{
	private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));
	private readonly FakePowerRequest _power = new();
	private readonly FakeAutoStart _autoStart = new();
	private readonly FakeUpdateService _updates = new();
	private readonly AwakeService _awake;
	private readonly TrayViewModel _vm;
	private readonly List<string> _messages = [];

	public TrayViewModelTests()
	{
		_awake = new AwakeService(_power, new MemorySettingsStore(), _time);
		_vm = new TrayViewModel(_awake, _autoStart, _updates);
		_vm.MessageRequested += _messages.Add;
	}

	[Fact]
	public void ToggleEnabled_SwitchesOffAndBack()
	{
		_vm.ToggleEnabledCommand.Execute(null);
		Assert.False(_vm.IsEnabled);
		Assert.Equal("Off (power plan)", _vm.Status);

		_vm.ToggleEnabledCommand.Execute(null);
		Assert.Equal(AwakeMode.Indefinite, _vm.Mode);
	}

	[Fact]
	public void StartTimed_ShowsCountdown()
	{
		_vm.StartTimedCommand.Execute(5400);
		_time.Advance(TimeSpan.FromSeconds(1));

		Assert.Equal("1:29:59 left, screen on", _vm.Status);
	}

	[Fact]
	public void ServiceChanges_RaisePropertyChanged()
	{
		var raised = 0;
		_vm.PropertyChanged += (_, _) => raised++;

		_awake.Update((s, _) => s with { KeepDisplayOn = false });

		Assert.True(raised > 0);
		Assert.False(_vm.KeepDisplayOn);
	}

	[Fact]
	public void ToggleStartWithWindows_FlipsAutoStart()
	{
		_vm.ToggleStartWithWindowsCommand.Execute(null);
		Assert.True(_vm.StartWithWindows);

		_vm.ToggleStartWithWindowsCommand.Execute(null);
		Assert.False(_vm.StartWithWindows);
	}

	[Theory]
	[InlineData(true, null, "Up to date (v1.0.0).")]
	[InlineData(true, "1.1.0", "Update to v1.1.0 is available in the menu.")]
	[InlineData(false, null, "Could not reach GitHub to check for updates.")]
	public async Task CheckForUpdates_ReportsResult(bool reachable, string? available, string message)
	{
		_updates.Reachable = reachable;
		_updates.Available = available;

		await _vm.CheckForUpdatesCommand.ExecuteAsync(null);

		Assert.Equal([message], _messages);
		Assert.Equal(available, _vm.PendingVersion);
	}

	[Fact]
	public async Task ApplyUpdate_Failure_IsReported()
	{
		await _vm.ApplyUpdateCommand.ExecuteAsync(null);

		Assert.Equal(["Update failed: download failed"], _messages);
	}
}
