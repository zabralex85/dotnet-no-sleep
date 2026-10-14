# NoSleep — architecture

## Stack

- .NET 10 (`net10.0-windows`), WPF (`UseWPF`) + WinForms only for `NotifyIcon` (`UseWindowsForms`).
- NuGet: `Velopack` (installer + updates) only. Plus StyleCop (from `Directory.Build.props`) and test packages.
- Settings: `System.Text.Json` → `%APPDATA%\NoSleep\settings.json`.
- Tests: xunit.v3 on Microsoft.Testing.Platform (see `global.json`), same packages as ai-chrome-proxy.

## Layout

```
NoSleep.slnx
.github/workflows/ci.yml       build + test on master / PR
.github/workflows/release.yml  tag v* → Setup.exe on GitHub Releases (see release.md)
src/NoSleep/
  NoSleep.csproj
  app.manifest             asInvoker, Windows 10/11, PerMonitorV2 DPI
  Program.cs               Main: VelopackApp.Build()...Run() first, then mutex, then App
  App.xaml(.cs)            load settings, create tray; ShutdownMode=OnExplicitShutdown, no StartupUri
  TrayIcon.cs              NotifyIcon + ContextMenuStrip, icon and tooltip per mode
  AwakeController.cs       current mode, deadline, 1 s DispatcherTimer, applies KeepAwake
  KeepAwake.cs             power request wrapper (Set/Clear), IDisposable
  Native.cs                [LibraryImport] power request functions (kernel32)
  Settings.cs              settings record + Load/Save
  Startup.cs               HKCU Run: IsEnabled/Enable/Disable
  Updates.cs               Velopack UpdateManager + GithubSource
  SettingsWindow.xaml(.cs) settings window
  Assets/                  app.ico, passive.ico, indefinite.ico, timed.ico, expirable.ico
tests/NoSleep.Tests/       pure logic: mode transitions, deadlines, settings round-trip, Run-key command string
```

Icons were generated once with a System.Drawing script (coloured circle per mode: grey ring + z, green ∞, blue clock, orange clock + dot); `app.ico` = `indefinite.ico`. They are WPF `Resource`s loaded via `pack://application:,,,/Assets/<name>.ico`.

Add files only when the existing ones get in the way. No interfaces/DI while there is a single implementation.

## Keeping the PC awake

PowerToys Awake calls `SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED [| ES_DISPLAY_REQUIRED])`. The state is bound to the calling thread, hence Awake's dedicated thread + queue.

NoSleep uses power requests — the same mechanism Chromium/WebView2 (new Teams) uses during a call:

```csharp
// REASON_CONTEXT { Version = POWER_REQUEST_CONTEXT_VERSION (0), Flags = POWER_REQUEST_CONTEXT_SIMPLE_STRING (0x1), SimpleReasonString }
SafeFileHandle h = PowerCreateRequest(ref context);          // once at startup, reason: "NoSleep: keep awake"
PowerSetRequest(h, PowerRequestSystemRequired);              // = 1, no sleep
PowerSetRequest(h, PowerRequestDisplayRequired);             // = 0, when "Keep screen on"
PowerClearRequest(h, ...);                                   // on Passive / unchecking
```

- The request is bound to the handle, not a thread → no dedicated thread, everything on the UI thread.
- Process dies → handle closed → Windows drops the request. No leaked state.
- `PowerRequestExecutionRequired` / `AwayModeRequired` are not used.
- The reason is visible to an admin in `powercfg /requests` — honest and diagnosable.

## Modes and time

```
enum AwakeMode { Passive, Indefinite, Timed, Expirable }
```

- Timed turns into an absolute deadline `now + interval` when started; Expirable already is a deadline. From there both are the same.
- The deadline is saved in settings.json → after a restart: deadline passed → Passive, otherwise continue.
- `DispatcherTimer` (1 s) on the UI thread: updates the tooltip countdown and checks the deadline. No locks.
- Transitions are pure methods on the `Settings` record (`WithPassive`, `WithIndefinite`, `WithTimed`, `WithExpirable`, `Enabled`, `Expired`), each taking `now`; that is what tests cover. `AwakeController.Update(Func<Settings, DateTimeOffset, Settings>)` applies one, saves and re-applies the power request.
- Tray presets start Timed and remember the duration as `intervalHours`/`intervalMinutes`; "Enabled" then restarts that interval. Expirable resumes on "Enabled" only while `expirationDateTime` is ahead; otherwise Indefinite.
- Clock changes / sleep: `now >= deadline` is checked every tick, so a mode expires correctly after resume.

## Settings

```json
{
  "mode": "Indefinite",
  "lastActiveMode": "Indefinite",
  "keepDisplayOn": true,
  "intervalHours": 1,
  "intervalMinutes": 0,
  "expirationDateTime": null,
  "deadline": null,
  "customTrayTimes": { "30 min": 1800, "1 h": 3600, "2 h": 7200 }
}
```

- Path `%APPDATA%\NoSleep\settings.json` — **not** `%LOCALAPPDATA%\NoSleep`, which is Velopack's install root and is deleted on uninstall.
- First run (no file): Indefinite + keep screen on — the reason the app was installed.
- Phase 2 adds `simulateInput` and `simulateInputIntervalSeconds`.
- Enums as strings (`JsonStringEnumConverter`).
- Corrupt/missing file → defaults; the file is rewritten on the next save.
- Atomic write: temp file + `File.Move(overwrite: true)`.
- "Start with Windows" is **not** stored in settings.json — the registry value is the source of truth.

## Autostart

`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `NoSleep` = `"<Environment.ProcessPath>"` (quoted). Under Velopack that is the stable `%LOCALAPPDATA%\NoSleep\current\NoSleep.exe`, so it survives updates. Velopack's before-uninstall hook deletes the value.

## Tray

- `System.Windows.Forms.NotifyIcon` + `ContextMenuStrip`, menu in [requirements.md](requirements.md#tray-menu).
- Icon and tooltip change on every state change; tooltip ≤ 127 chars.
- Exit: `PowerClearRequest`, `NotifyIcon.Visible = false; Dispose()` (otherwise a "ghost" icon), `Application.Current.Shutdown()`.

## Startup and single instance

```csharp
[STAThread]
static void Main()
{
	// Must run first: handles Velopack's --veloapp-* hook invocations and exits.
	VelopackApp.Build()
		.OnBeforeUninstallFastCallback(_ => Startup.Disable())
		.Run();

	using (var single = new Mutex(initiallyOwned: true, @"Local\NoSleep", out var isFirst))
	{
		if (isFirst)
		{
			var app = new App();
			app.InitializeComponent();
			app.Run();
		}
	}
}
```

- csproj: `<StartupObject>NoSleep.Program</StartupObject>` so the XAML-generated `Main` is not used.
- Second instance exits silently (phase 3: could signal the first one to show the menu).
- An unhandled exception (e.g. a failed power request call) shows a message box and exits instead of vanishing from the tray.

## Updates

See [release.md](release.md#updates). `Updates.cs` wraps `UpdateManager(new GithubSource(repo, accessToken: null, prerelease: false))`; does nothing when `!IsInstalled` (dotnet run) or the build has no repository URL.

## Simulate activity (phase 2)

- `SendInput` of key F15 (not on keyboards, apps ignore it) or `MOUSEEVENTF_MOVE` +1/−1 px.
- Timer with `simulateInputIntervalSeconds`; send only when `GetLastInputInfo` shows idle ≥ interval — never interfere while the user works.
- Which variant resets the lock timer on this PC — verify by experiment ([diagnostics.md](diagnostics.md)).

## Build

```powershell
dotnet build -c Release
dotnet test --project tests/NoSleep.Tests -c Release --no-build
dotnet run --project src/NoSleep
```

`powercfg /requests` needs admin; without it, check the request by its effect (diagnostics experiment) or ask an admin.

Packaging and release: [release.md](release.md).

## Pitfalls

- `UseWPF` + `UseWindowsForms` → ambiguous `Application`, `MessageBox`, `Button`, etc. Remove the implicit using: `<Using Remove="System.Windows.Forms" />` in the csproj; write WinForms types fully qualified or via alias.
- WPF's implicit usings leave out `System.IO` (clash with `System.Windows.Shapes.Path`): add `using System.IO;` where needed.
- XAML-generated partial classes are public: internal windows need `x:ClassModifier="internal"`.
- `.editorconfig` warns on `using var`: use `using (...) { }` blocks.
- `NotifyIcon` needs a message loop — the WPF Dispatcher provides it. The menu is rebuilt (old items disposed) on every `Opening`, so it always shows the current state.
- No trimming — WPF doesn't support it.
- AppLocker/WDAC on the corporate PC may block unsigned exes from the profile. If NoSleep doesn't start, check `Event Viewer → Applications and Services → Microsoft → Windows → AppLocker` — not a program bug.
- If code is ever copied from PowerToys (MIT), add it to `THIRD-PARTY-NOTICES.md`. Currently nothing is copied; the APIs are called directly.
