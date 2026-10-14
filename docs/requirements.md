# NoSleep — requirements

Windows tray utility that keeps the PC awake like PowerToys Awake, extended for a corporate PC without admin rights.

## Problem

- Corporate policy: after ~30 min idle the PC sleeps / turns the display off / locks the session.
- During a Teams call this does not happen — Teams (WebView2/Chromium) holds "display required" + "system required" power requests (hypothesis, verify per [diagnostics.md](diagnostics.md)).
- PowerToys can't be installed (no admin) — need our own app that installs and runs from the user profile.
- What exactly the policy does (sleep, display off or session lock) is found out via [diagnostics.md](diagnostics.md) before phase 2 is written.

## Constraints

- No admin rights: only HKCU, `%LOCALAPPDATA%` / `%APPDATA%`, user-mode APIs.
- .NET Desktop Runtime may be missing → self-contained build.
- Per-user installer, no UAC (see [release.md](release.md)).
- WPF, .NET 10 (latest LTS; move to .NET 11 after GA, November 2026).
- Single instance per user session.

## Parity with PowerToys Awake

Sources: https://learn.microsoft.com/en-us/windows/powertoys/awake, https://github.com/microsoft/PowerToys/tree/main/src/modules/awake

| Awake | NoSleep | Note |
|---|---|---|
| Passive mode ("Keep using the selected power plan") | ✅ "Off" | No requests, power plan applies |
| Indefinite mode | ✅ | Until switched off / exit |
| Timed mode (interval, hours + minutes) | ✅ | When elapsed → Passive |
| Expirable mode (until date/time) | ✅ | Date in the past → error, mode unchanged |
| Keep screen on | ✅ | `PowerRequestDisplayRequired` |
| Tray presets: 30 min, 1 h, 2 h | ✅ | |
| `customTrayTimes` (own presets: name → seconds) | ✅ | In settings.json and the settings window |
| Tray icon per mode (4 icons) | ✅ | |
| Tooltip with mode and remaining time | ✅ | `NotifyIcon.Text` limit is 127 chars |
| Mode persisted across restarts | ✅ | |
| CLI: `--display-on`, `--time-limit`, `--expire-at` | ⏸ later | Only needed for scripting |
| CLI: `--pid`, `--use-parent-pid` | ❌ | Not needed for this scenario |
| `--use-pt-config`, PowerToys Settings integration | ❌ | Own settings.json |

## Extensions (beyond Awake)

1. **On/Off** — one toggle in the tray: Off = Passive, On = last active mode.
2. **Start with Windows** — `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (no admin).
3. **Installer + auto-update** — Velopack `Setup.exe` built on a git tag, updates from GitHub Releases ([release.md](release.md)).
4. **Teams-style power requests** — `PowerCreateRequest`/`PowerSetRequest` with a readable reason instead of `SetThreadExecutionState` ([architecture.md](architecture.md)).
5. **Simulate activity** (phase 2, off by default) — only if diagnostics show the policy locks/sleeps despite power requests. Periodic `SendInput` (F15 or a 1 px mouse move and back), configurable interval (default 60 s, must be below the policy timeout).
   > ⚠️ Bypassing a screen-lock policy may violate the employer's security rules. The user decides; the feature must be switched on explicitly.
6. **Diagnostics** (phase 2, optional) — menu item showing idle time (`GetLastInputInfo`) and the current `SystemExecutionState` (`CallNtPowerInformation`), to see what holds the system without `powercfg /requests` (needs admin).

## Tray menu

```
NoSleep: Indefinite, screen on        (header, disabled)
☑ Enabled
─────────────
○ Off (power plan)
● Indefinite
○ Timed               ▸ 30 min / 1 h / 2 h / <customTrayTimes>
○ Until date/time…      (opens the settings window)
─────────────
☑ Keep screen on
☐ Simulate activity     (phase 2)
☑ Start with Windows
─────────────
Settings…
Update to vX            (only when a newer release exists)
Check for updates
Exit
```

Double-click on the icon = toggle Enabled. `Custom…` and `Until date/time…` open the settings window.

## Settings window (WPF)

- Interval for Timed: hours + minutes.
- Date/time for Expirable (`DatePicker` + time field).
- `customTrayTimes` list: add/remove (name, minutes in the grid; stored as seconds).
- Simulate activity: on/off, interval in seconds (phase 2).

## Phases

| Phase | Scope | Done when |
|---|---|---|
| 0 | Policy and Teams diagnostics ([diagnostics.md](diagnostics.md)) | Known: what the policy does and after how many minutes |
| 1 (MVP) | Tray, Awake modes, Keep screen on, On/Off, autostart, settings.json, CI, tag → Setup.exe, auto-update | Installed via Setup.exe without admin; without Teams the PC does not sleep/blank for 35+ min under a 30 min policy |
| 2 | Simulate activity, diagnostics — only if phase 1 is not enough | Session does not lock for 35+ min |
| 3 | CLI, README | As needed |

## Non-goals

- Changing the power plan (`powercfg /change`) — needs rights / overwritten by policy, and not needed.
- Working on the lock screen — like Awake, it is a different security context.
- Detecting a Teams call — power requests give the same effect anyway.
- Code signing — no certificate; SmartScreen "More info → Run anyway" is documented instead.
