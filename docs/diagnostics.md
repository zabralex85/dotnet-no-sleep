# Corporate policy diagnostics (no admin)

Goal: find out **what** happens after 30 minutes (sleep, display off or session lock) and **why** a Teams call prevents it. The answer decides whether phase 1 (power requests) is enough or phase 2 (simulate activity) is needed.

## 1. Which policies apply

```powershell
# User-scope policies (no admin; computer scope needs admin)
gpresult /r /scope user

# Active power scheme timeouts (sleep, display)
powercfg /getactivescheme
powercfg /query SCHEME_CURRENT SUB_SLEEP STANDBYIDLE
powercfg /query SCHEME_CURRENT SUB_VIDEO VIDEOIDLE

# Sleep/display via GPO
reg query "HKLM\SOFTWARE\Policies\Microsoft\Power\PowerSettings" /s

# Lock on idle: "Interactive logon: Machine inactivity limit"
reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System" /v InactivityTimeoutSecs

# Password-protected screen saver via GPO
reg query "HKCU\Software\Policies\Microsoft\Windows\Control Panel\Desktop"
```

What to look for:

| Finding | Meaning |
|---|---|
| `STANDBYIDLE` / `29f6c1db-86da-48c5-9fdb-f2b67b1f44da` = 1800 | Sleep after 30 min → `PowerRequestSystemRequired` is enough |
| `VIDEOIDLE` / `3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e` = 1800 | Display off → `PowerRequestDisplayRequired` is enough |
| `InactivityTimeoutSecs` = 1800 | Session lock on input idle → phase 2 likely needed |
| `ScreenSaveTimeOut` = 1800 + `ScreenSaverIsSecure` = 1 | Password screen saver → a display request usually suppresses it, verify |
| `A4B195F5-8225-47D8-8012-9D41369786E2` = 0 | "Allow applications to prevent automatic sleep" disabled → power requests ignored for sleep (yet Teams works — re-check) |

`powercfg /requests` would show which requests Teams holds, but it needs admin. If a helpdesk admin is available, ask them to run it during a call.

## 2. Experiment

1. No Teams call, don't touch anything for 35 min → note what happened and when (sleep / black screen / lock screen).
2. Teams "Meet now" alone, 35 min → expected: nothing happens.
3. NoSleep phase 1, Indefinite + "Keep screen on", no Teams, 35 min.
   - Nothing happened → phase 2 not needed.
   - Session locked → enable simulate activity (phase 2) and repeat.
4. For phase 2, test both input variants (F15 and mouse move) — which one resets the lock timer.

## 3. Log

Record results here — they are the input for the phase 2 decision.

| Date | Test | Enabled | Result after 35 min |
|---|---|---|---|
| | | | |
