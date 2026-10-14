# CLAUDE.md

NoSleep — Windows tray app (WPF, .NET 10) that keeps the PC awake like PowerToys Awake, extended for a locked-down corporate PC without admin rights. Installed per user with a Velopack `Setup.exe` built on git tags.

## Docs (read before changing behavior)

- `docs/requirements.md` — problem, Awake feature parity, extensions, tray menu, phases, non-goals.
- `docs/architecture.md` — layout and layering rules (MVVM/SOLID), power request API, modes/deadline logic, settings, autostart, startup, pitfalls.
- `docs/release.md` — Velopack installer, updates, tag → GitHub Release workflow, manual acceptance checklist.
- `docs/diagnostics.md` — how to check corporate policy without admin; experiment log that decides whether phase 2 (simulate activity) is needed.

All docs are in English. Update them in the same change when behavior or layout differs from them.

## Status

Phase 1 (MVP) implemented: tray, Awake modes, keep screen on, Enabled toggle, autostart, settings window, Velopack updates, CI + tag release. Next: run the `docs/diagnostics.md` experiment on the corporate PC; phase 2 only if it fails.

## Commands

```powershell
dotnet build -c Release
dotnet test --project tests/NoSleep.Tests -c Release --no-build
dotnet run --project src/NoSleep
```

Release: `git tag vX.Y.Z; git push origin vX.Y.Z` → `.github/workflows/release.yml` builds and uploads `NoSleep-win-Setup.exe`. Local packaging: `docs/release.md`.

SDK pinned in `global.json` (10.0.x, rollForward latestFeature). Tests: xunit.v3 on Microsoft.Testing.Platform.

## Hard constraints

- No admin: only HKCU, `%LOCALAPPDATA%`, `%APPDATA%`, user-mode APIs. Never `powercfg /change`, HKLM writes, services or elevated scheduled tasks.
- Self-contained `win-x64` publish: target PC may lack .NET Desktop Runtime. No trimming (WPF).
- Keep-awake via `PowerCreateRequest`/`PowerSetRequest`/`PowerClearRequest` (handle-based, no dedicated thread). Not `SetThreadExecutionState`.
- `VelopackApp.Build()...Run()` is the first line of `Program.Main`; WPF's generated `Main` is replaced via `<StartupObject>`.
- Autostart via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` = `"Environment.ProcessPath"`; registry is the source of truth; the Velopack before-uninstall hook removes it.
- Settings in `%APPDATA%\NoSleep\settings.json` (not `%LOCALAPPDATA%\NoSleep` — Velopack's install root, wiped on uninstall). `System.Text.Json`, atomic write, corrupt file → defaults.
- Update checks: no-op when not installed by Velopack or `UpdateRepository` metadata is empty; network failures are silent.
- Simulate activity (phase 2) stays opt-in and off by default.
- NuGet: `Velopack`, `CommunityToolkit.Mvvm`, analyzers (StyleCop, SonarAnalyzer.CSharp) and test packages; anything else needs a clear reason. Tray = WinForms `NotifyIcon`, no third-party tray lib. Keep `VPK_VERSION` in `release.yml` equal to the `Velopack` package version.
- Workflows: tag/ref names go through `env`, never interpolated into scripts; `release.yml` has `contents: write`, `ci.yml` `contents: read`.

## Structure

- `Models/` pure (no Windows APIs), `Services/` Windows/file/network behind interfaces + `AwakeService`, `ViewModels/` CommunityToolkit.Mvvm (`[ObservableProperty]` fields, `[RelayCommand]`), `Views/` thin. Dependencies point inward; `App` is the composition root (no DI container).
- Add an interface only where a test needs a fake. Time via `TimeProvider`. View models raise events for messages; they never show UI.

## Code style

- StyleCop.Analyzers + SonarAnalyzer.CSharp via `Directory.Build.props` (`StyleCop.ruleset`, `SonarLint.xml`); build must be warning-free.
- `.editorconfig`: tabs (size 4), CRLF, UTF-8 without BOM.
- No file headers (SA1633 off), no required XML docs (SA1600 off), `_camelCase` private fields, no `this.` (SA1101 off).
- Use `[LibraryImport]` (source-generated P/Invoke), not `[DllImport]`. Native declarations live in `Services/NativeMethods.cs`.
- `UseWPF` + `UseWindowsForms`: implicit `System.Windows.Forms`/`System.Drawing` usings are removed in the csproj; import them per file (only `Views/TrayIcon.cs`).
- WPF implicit usings lack `System.IO` — add `using System.IO;`. XAML roots use `x:ClassModifier="internal"`. `using (...) { }` blocks, not `using var` (.editorconfig warning).
- All UI and timer work on the WPF dispatcher thread (`DispatcherTimer`), no locks.
- Tests: models, `AwakeService` and view models with fakes from `tests/NoSleep.Tests/Fakes`; settings store against a temp folder. No UI tests, never write the real registry or `%APPDATA%` in tests.

## Third-party notices

`THIRD-PARTY-NOTICES.md` lists what the package bundles (Velopack, .NET runtime) and ships next to the exe. Adding a NuGet dependency or copying code (e.g. from PowerToys, MIT) → add it there.
