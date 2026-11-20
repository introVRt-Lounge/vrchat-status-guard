# VRChat Privacy Guard

[![CI](https://github.com/introVRt-Lounge/vrchat-privacy/actions/workflows/ci.yml/badge.svg)](https://github.com/introVRt-Lounge/vrchat-privacy/actions/workflows/ci.yml)

Windows tray app for PC VRChat: when you enter a designated sleep world, automatically switch status to **Ask Me (orange)** or **Do Not Disturb (red)** so you do not stay on blue by accident.

Best-effort automation. A short delay after world entry is acceptable. Never auto-restores blue.

## Status

**Repo bootstrap + Core milestone foundation.** Private under [`introVRt-Lounge`](https://github.com/introVRt-Lounge). Full tray UI, live login, and in-game confirmation are tracked in GitHub Issues.

## Docs

- [Build brief](docs/BUILD_BRIEF.md)
- [Research notes (logs + API)](docs/research/vrchat-logs-and-api.md)
- [Milestone 1 plan](docs/plans/2026-11-20-milestone-1.md)
- [CONTRIBUTING](CONTRIBUTING.md) · [SECURITY](SECURITY.md) · [REPO_SETTINGS](REPO_SETTINGS.md) · [TODO](TODO.md)

## Architecture (testable slices)

| Project | Role |
|---------|------|
| `VrchatPrivacy.Core` | Log parse, world rules, auth/session interfaces, protection decisions |
| `VrchatPrivacy.App` | WinForms tray + settings (Windows only) |
| `VrchatPrivacy.Core.Tests` | Unit tests (Linux CI-friendly) |

## Install (end users)

Run the wizard: `VrchatPrivacyGuard-Setup-*.exe` (CI artifact / Releases).

It installs a **self-contained** build - no separate .NET runtime install. Optional Start Menu, desktop icon, and Start-with-Windows.

Local build:

```powershell
# Needs Inno Setup 6+ (winget install JRSoftware.InnoSetup)
.\scripts\build-installer.ps1
```

## Quick start (dev)

```powershell
dotnet restore src/VrchatPrivacy.sln
dotnet test tests/VrchatPrivacy.Core.Tests/VrchatPrivacy.Core.Tests.csproj
# Windows tray scaffold:
dotnet run --project src/VrchatPrivacy.App/VrchatPrivacy.App.csproj
```

Requires .NET 8 SDK for development. VRChat client logs: `%UserProfile%\AppData\LocalLow\VRChat\VRChat\output_log_*.txt`.

## Hard constraints (do not "improve" away)

- Match **world IDs** only (not names / instance IDs).
- Leave status alone when leaving protected worlds.
- Store session cookies with Windows DPAPI; never log passwords, OTP codes, or tokens.
- Unofficial VRChat API: identify the app; treat **≤1 mutating request / 60s** as a hard rate budget.

## License

Private introVRt Lounge software. See [LICENSE](LICENSE).
