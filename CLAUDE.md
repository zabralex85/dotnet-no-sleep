# CLAUDE.md

NoSleep — Windows tray app (WPF, .NET 10) that keeps the PC awake like PowerToys Awake, extended for a locked-down corporate PC without admin rights. Installed per user with a Velopack `Setup.exe` built on git tags.

## Docs (read before changing behavior)

- `docs/requirements.md` — problem, Awake feature parity, extensions, tray menu, phases, non-goals.
- `docs/architecture.md` — project layout, power request API, modes/deadline logic, settings, autostart, startup, pitfalls.
- `docs/release.md` — Velopack installer, updates, tag → GitHub Release workflow, manual acceptance checklist.
- `docs/diagnostics.md` — how to check corporate policy without admin; experiment log that decides whether phase 2 (simulate activity) is needed.

All docs are in English. Update them in the same change when behavior or layout differs from them.

## Status

Docs only, no code yet. Next: phase 1 (MVP) per `docs/requirements.md`, including `.github/workflows/ci.yml` and `release.yml`.

## Commands

```powershell
dotnet build -c Release
dotnet test --project tests/NoSleep.Tests -c Release --no-build
dotnet run --project src/NoSleep
```

Release: `git tag vX.Y.Z; git push origin vX.Y.Z` → `.github/workflows/release.yml` builds and uploads `NoSleep-win-Setup.exe`. Local packaging: `docs/release.md`.

SDK pinned in `global.json` (10.0.x, rollForward latestFeature). Tests use Microsoft.Testing.Platform (MSTest).

## Hard constraints

- No admin: only HKCU, `%LOCALAPPDATA%`, `%APPDATA%`, user-mode APIs. Never `powercfg /change`, HKLM writes, services or elevated scheduled tasks.
- Self-contained `win-x64` publish: target PC may lack .NET Desktop Runtime. No trimming (WPF).
- Keep-awake via `PowerCreateRequest`/`PowerSetRequest`/`PowerClearRequest` (handle-based, no dedicated thread). Not `SetThreadExecutionState`.
- `VelopackApp.Build()...Run()` is the first line of `Program.Main`; WPF's generated `Main` is replaced via `<StartupObject>`.
- Autostart via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` = `"Environment.ProcessPath"`; registry is the source of truth; the Velopack before-uninstall hook removes it.
- Settings in `%APPDATA%\NoSleep\settings.json` (not `%LOCALAPPDATA%\NoSleep` — Velopack's install root, wiped on uninstall). `System.Text.Json`, atomic write, corrupt file → defaults.
- Update checks: no-op when not installed by Velopack or `UpdateRepository` metadata is empty; network failures are silent.
- Simulate activity (phase 2) stays opt-in and off by default.
- NuGet: only `Velopack`, StyleCop and test packages without a clear reason. Tray = WinForms `NotifyIcon`, no third-party tray lib. Keep `VPK_VERSION` in `release.yml` equal to the `Velopack` package version.
- Workflows: tag/ref names go through `env`, never interpolated into scripts; `release.yml` has `contents: write`, `ci.yml` `contents: read`.

## Code style

- StyleCop.Analyzers via `Directory.Build.props` + `StyleCop.ruleset`; build must be warning-free.
- `.editorconfig`: tabs (size 4), CRLF, UTF-8 without BOM.
- No file headers (SA1633 off), no required XML docs (SA1600 off), `_camelCase` private fields, no `this.` (SA1101 off).
- `[LibraryImport]` (source-generated P/Invoke), not `[DllImport]`. Native declarations live in `Native.cs`.
- `UseWPF` + `UseWindowsForms`: `<Using Remove="System.Windows.Forms" />` in the csproj; WinForms types fully qualified or via alias.
- All UI and timer work on the WPF dispatcher thread (`DispatcherTimer`), no locks.
- Tests only for pure logic (deadline/mode computation, settings round-trip, Run-key command string). No UI tests, never write the real registry in tests.

## Repo housekeeping (inherited from ai-chrome-proxy)

`NOTICE`, `THIRD-PARTY-NOTICES.md`, `licenses/cloudflared-LICENSE.txt` and parts of `.gitignore` still reference AiChromeProxy — fix when touching them. If code is ever copied from PowerToys (MIT), add it to `THIRD-PARTY-NOTICES.md`.
