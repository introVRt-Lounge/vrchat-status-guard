# VRChat Status Guard

[![CI](https://github.com/introVRt-Lounge/vrchat-status-guard/actions/workflows/ci.yml/badge.svg)](https://github.com/introVRt-Lounge/vrchat-status-guard/actions/workflows/ci.yml)

Windows tray app for PC VRChat: when you enter a designated world, automatically switch status to **Ask Me (orange)**, **Do Not Disturb (red)**, or (if you opt in per world) **Join Me (blue)**.

Best-effort automation. A short delay after world entry is acceptable. Never auto-restores blue.

## Status

**Runtime tray app** under [`introVRt-Lounge`](https://github.com/introVRt-Lounge). Default protected world: [VRChat Home](https://vrchat.com/home/world/wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd/info) (`wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd`, the default user home world) → **Ask Me (orange)** (configurable to red). Login via tray; cookies in DPAPI.

## Docs

- [Build brief](docs/BUILD_BRIEF.md)
- [Research notes (logs + API)](docs/research/vrchat-logs-and-api.md)
- [Milestone 1 plan](docs/plans/2026-11-20-milestone-1.md)
- [CONTRIBUTING](CONTRIBUTING.md) · [CODE_OF_CONDUCT](CODE_OF_CONDUCT.md) · [SUPPORT](SUPPORT.md) · [SECURITY](SECURITY.md) · [REPO_SETTINGS](REPO_SETTINGS.md) · [TODO](TODO.md)
- License: [MIT](LICENSE)

## Architecture (testable slices)

| Project | Role |
|---------|------|
| `VrchatPrivacy.Core` | Log parse, world rules, auth/session interfaces, protection decisions |
| `VrchatPrivacy.App` | WinForms tray + settings (Windows only) |
| `VrchatPrivacy.Core.Tests` | Unit tests (Linux CI-friendly) |

## Install (end users)

1. Download **Setup.exe** from the latest [GitHub Release](https://github.com/introVRt-Lounge/vrchat-status-guard/releases) (Velopack - the only installer).
2. Run it. Tray icon appears → **Login…** with your VRChat username/email, password, and 2FA if prompted.
3. First run: a short **setup wizard** walks you through protecting one world (status required, group optional), then shows tray tips.
4. Tray: **left-click** pauses/resumes; **double-click** opens the protection hub (does not pause); right-click for the full menu (including **Check for updates…** / **What's New…** / **About…**).
5. Protection hub: protected worlds first (status + group). Expand **Add from recent** or **Add by ID/URL** for the same guided steps.
6. Enter a protected world - status (and optional group) apply. Leaving an unprotected world does not invent a status change.

Self-contained build - no separate .NET runtime. In-app updates use the same Velopack channel. Session cookies stay in DPAPI on that PC after first login. Releases are **tag-gated** (`v*`); maintainers push a version tag (or run the Release workflow manually) to publish.

Local pack (maintainers):

```powershell
.\scripts\build-velopack.ps1 -Version 0.2.0
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
- Leave status alone when leaving protected worlds (no surprise blue restore). Blue only when explicitly set for a world.
- Store session cookies with Windows DPAPI; never log passwords, OTP codes, or tokens.
- Unofficial VRChat API: identify the app; treat **≤1 mutating request / 60s** as a hard rate budget.

## License

Private introVRt Lounge software. See [LICENSE](LICENSE).
