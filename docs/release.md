# NoSleep — installer, updates, releases

Same pipeline as ai-chrome-proxy: Velopack, `Setup.exe` built by GitHub Actions on a `v*` tag, updates from GitHub Releases.

## Install (user)

1. Download `NoSleep-win-Setup.exe` from https://github.com/zabralex85/dotnet-no-sleep/releases and run it.
2. Not code-signed → SmartScreen warns: **More info → Run anyway**.
3. No admin, no UAC, no .NET install needed. Installs per user to `%LOCALAPPDATA%\NoSleep\` (app in `current\`, plus Velopack's `Update.exe`), adds Start menu / desktop shortcuts and an Apps & features entry, then starts NoSleep.

`NoSleep-win-Portable.zip` is the fallback when `Setup.exe` is blocked: unzip anywhere under the profile and run `NoSleep.exe` (no auto-update).

| What | Where |
|---|---|
| App | `%LOCALAPPDATA%\NoSleep\current\NoSleep.exe` |
| Settings | `%APPDATA%\NoSleep\settings.json` (kept on uninstall) |
| Autostart | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `NoSleep` |

## Updates

- The tray checks GitHub Releases at start and then every hour; **Check for updates** checks at once ("Up to date" when nothing newer).
- **Update to vX** appears only when a newer release exists: downloads, applies and restarts NoSleep (`ApplyUpdatesAndRestart`). The power request is dropped for the few seconds of the restart; the new process restores the saved mode.
- Pre-releases (tags with `-suffix`) are never offered.
- No updates under `dotnet run` (`UpdateManager.IsInstalled == false`) or when the build has no repository URL.
- Corporate proxy/firewall blocks GitHub → check fails silently (no error popup; tooltip/menu unchanged).

## Uninstall

**Settings → Apps → Installed apps → NoSleep → Uninstall.** The before-uninstall hook removes the Run value. `%APPDATA%\NoSleep` is kept; delete it by hand for a clean removal.

## Cutting a release

```powershell
git tag v0.1.0
git push origin v0.1.0
```

Tag format: `vMAJOR.MINOR.PATCH[-suffix]`. Anything else fails the workflow. A `-suffix` tag (`v0.1.0-rc1`) becomes a GitHub pre-release that installed apps never offer.

`.github/workflows/release.yml` (tag `v*`, `windows-latest`, `contents: write`):

1. Validate tag → `VERSION` (tag without `v`). Tag goes through `env`, never interpolated into a script.
2. `dotnet restore` → `dotnet build -c Release` (StyleCop errors fail) → `dotnet test`.
3. `dotnet publish src/NoSleep -c Release -r win-x64 --self-contained -p:Version=$VERSION -p:UpdateRepository=<repo url> -o publish`.
4. `dotnet tool install -g vpk --version $VPK_VERSION` (pinned; keep in step with the `Velopack` package version).
5. `vpk pack --packId NoSleep --packVersion $VERSION --runtime win-x64 --packDir publish --mainExe NoSleep.exe --packTitle "NoSleep" --packAuthors "Alexander Zabrodskiy" --icon src/NoSleep/Assets/app.ico --outputDir releases`.
6. `vpk upload github --repoUrl <repo url> --token $GITHUB_TOKEN --publish --releaseName "NoSleep $TAG" --tag $TAG --outputDir releases [--pre]`.

`.github/workflows/ci.yml` (push to master, PRs, `contents: read`): restore → build `-p:Version=0.0.0-ci` → test. No coverage gate (little logic; add one if it grows).

`THIRD-PARTY-NOTICES.md` is copied next to `NoSleep.exe` by the csproj (`CopyToPublishDirectory`), so it ships in every package.

The repository URL reaches the app as `<AssemblyMetadata Include="UpdateRepository" Value="$(UpdateRepository)" />` in the csproj; unset = no update checks.

## Local package

```powershell
dotnet tool install vpk --version <VPK_VERSION> --tool-path .tools
dotnet publish src/NoSleep -c Release -r win-x64 --self-contained -p:Version=0.1.0 -p:UpdateRepository=https://github.com/zabralex85/dotnet-no-sleep -o publish
.\.tools\vpk.exe pack --packId NoSleep --packVersion 0.1.0 --runtime win-x64 --packDir publish --mainExe NoSleep.exe --packTitle "NoSleep" --icon src/NoSleep/Assets/app.ico --outputDir releases
```

`releases\` then holds `NoSleep-win-Setup.exe`, `NoSleep-win-Portable.zip`, `NoSleep-0.1.0-full.nupkg` and `releases.win.json`. `.tools/`, `publish/`, `releases/` are gitignored.

## Manual acceptance checklist

1. `Setup.exe` on a standard (non-admin) account → no UAC, NoSleep in the tray, entry in Apps & features.
2. **Start with Windows** on → sign out/in → NoSleep starts; Run value points at `...\NoSleep\current\NoSleep.exe`.
3. Publish a newer release → **Update to vX** appears → after update the tray shows the new version and the previous mode.
4. `-rc` pre-release → never offered.
5. Under `dotnet run` → no **Update to vX**, **Check for updates** says nothing is available.
6. Uninstall → tray gone, Run value gone, `%APPDATA%\NoSleep\settings.json` still there.
