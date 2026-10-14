using Microsoft.Extensions.Time.Testing;
using NoSleep.Models;
using NoSleep.Services;
using NoSleep.Tests.Fakes;
using NoSleep.ViewModels;

namespace NoSleep.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
	private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero));
	private readonly AwakeService _awake;
	private readonly SettingsViewModel _vm;
	private readonly List<string> _errors = [];

	public SettingsViewModelTests()
	{
		_awake = new AwakeService(new FakePowerRequest(), new MemorySettingsStore(), _time);
		_vm = new SettingsViewModel(_awake);
		_vm.ValidationFailed += _errors.Add;
	}

	[Fact]
	public void LoadsCurrentSettings()
	{
		Assert.Equal(("1", "0"), (_vm.Hours, _vm.Minutes));
		Assert.Equal(["30 min", "1 h", "2 h"], _vm.Presets.Select(p => p.Name));
		Assert.Equal(30, _vm.Presets[0].Minutes);
	}

	[Theory]
	[InlineData("x", "0")]
	[InlineData("-1", "30")]
	[InlineData("0", "0")]
	public void StartTimed_InvalidInput_IsRejected(string hours, string minutes)
	{
		_vm.Hours = hours;
		_vm.Minutes = minutes;

		_vm.StartTimedCommand.Execute(null);

		Assert.Single(_errors);
		Assert.Equal(AwakeMode.Indefinite, _awake.Settings.Mode);
	}

	[Fact]
	public void StartTimed_Valid_StartsTimed()
	{
		_vm.Hours = "0";
		_vm.Minutes = "45";

		_vm.StartTimedCommand.Execute(null);

		Assert.Empty(_errors);
		Assert.Equal(_time.GetLocalNow().AddMinutes(45), _awake.Settings.Deadline);
	}

	[Fact]
	public void StartUntil_PastOrMalformed_IsRejected()
	{
		_vm.Date = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Local);
		_vm.StartUntilCommand.Execute(null);
		_vm.Date = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Local);
		_vm.Time = "25:99";
		_vm.StartUntilCommand.Execute(null);

		Assert.Equal(["That time has already passed.", "Pick a date and enter the time as HH:mm."], _errors);
		Assert.Equal(AwakeMode.Indefinite, _awake.Settings.Mode);
	}

	[Fact]
	public void StartUntil_Future_StartsExpirable()
	{
		_vm.Date = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Local);
		_vm.Time = "9:30";

		_vm.StartUntilCommand.Execute(null);

		Assert.Equal(AwakeMode.Expirable, _awake.Settings.Mode);
		Assert.Equal(new DateTimeOffset(new DateTime(2030, 1, 1, 9, 30, 0, DateTimeKind.Local)), _awake.Settings.Deadline);
	}

	[Fact]
	public void SavePresets_SkipsBlankRows_RejectsDuplicates()
	{
		_vm.Presets.Add(new PresetRow { Name = " ", Minutes = 5 });
		_vm.Presets.Add(new PresetRow { Name = "8 h", Minutes = 480 });
		_vm.SavePresetsCommand.Execute(null);
		Assert.Equal(28800, _awake.Settings.CustomTrayTimes["8 h"]);
		Assert.Equal(4, _awake.Settings.CustomTrayTimes.Count);

		_vm.Presets.Add(new PresetRow { Name = "8 h ", Minutes = 10 });
		_vm.SavePresetsCommand.Execute(null);
		Assert.Single(_errors);
	}
}
