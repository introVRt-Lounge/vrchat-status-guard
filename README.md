# VRChat Status Guard

[![CI](https://github.com/introVRt-Lounge/vrchat-status-guard/actions/workflows/ci.yml/badge.svg)](https://github.com/introVRt-Lounge/vrchat-status-guard/actions/workflows/ci.yml)

![Status Guard banner](docs/assets/status-guard-banner.jpg)

<p align="center"><img src="docs/assets/status-guard-logo.jpg" alt="VRChat Status Guard logo" width="220" /></p>

## Friends, lend me your ears

**Do you like to party?** Do you *also* like to chill? Want to make sure your chill is not interrupted by surprise guests crashing the velvet rope?

**You need VRChat Status Guard!**

We keep your **public** public and your **private** private - **automatically**. Walk into a world you have marked as protected and Status Guard sets your status to Ask Me, Do Not Disturb, or (when you opt in) Join Me. Leave the guessing to the doorman. You keep the sofa.

Brought to you by the **[introVRt Lounge](https://vrchat.com/home/group/grp_481c4787-5fca-44b2-b6e4-d2d8c585f125)** - movies, games, and good company for fellow introverts. Join the Discord at **[tiny.cc/introvrt](https://tiny.cc/introvrt)**. Website: [introvrtlounge.com](https://www.introvrtlounge.com/).

Unofficial Windows tray helper for PC VRChat. Best-effort automation. A short delay after world entry is expected (see [Limitations](#limitations)). Never invents a blue / Join Me restore unless you configured that world yourself.

## Status

Runtime tray app under [`introVRt-Lounge`](https://github.com/introVRt-Lounge). Default protected world: [VRChat Home](https://vrchat.com/home/world/wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd/info) → **Ask Me (orange)** (configurable to red). Login via tray; cookies in DPAPI.

## Limitations

Status Guard talks to VRChat through the **unofficial** community API. VRChat staff (via **Tupper**) have stated that third-party apps must not query that API more than **once per 60 seconds**, and that abuse can terminate accounts. We treat that as a hard budget for status and group mutations.

**What that means for you:**

- After entering a protected world, status may take **up to ~60 seconds** to change if another mutation just ran. If nothing mutated recently, it can apply almost immediately.
- **Status comes first** (Ask Me / Do Not Disturb / Join Me). Optional group "repping" is deferred and will not block or steal the next status update when you hop worlds.
- Hopping through several protected worlds quickly still respects the same 60s spacing - the app coalesces to your **latest** world and cancels stale applies; it does **not** reset the budget just because the world id changed.
- Tray balloons fire when a status or group change actually lands - silence usually means we are waiting out the budget, not that the app died.

**Citations (Tupper guidance as published by the community API projects):**

- [vrchatapi-csharp README - Disclaimer](https://github.com/vrchatapi/vrchatapi-csharp) ("Do not make queries to the API more than once per 60 seconds.")
- [vrchatapi/specification](https://github.com/vrchatapi/specification) (same Tupper policy text)
- Same wording appears across other official community SDK READMEs (e.g. [vrchatapi-python](https://github.com/vrchatapi/vrchatapi-python)).

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
4. Tray: **left-click** pauses/resumes; **double-click** opens (or foregrounds) the protection hub; right-click for **Check for updates…** / **What's New…** / **About…**.
5. Protection hub: protected worlds first (status + group). Expand **Add from recent** or **Add by ID/URL** for the same guided steps.
6. Enter a protected world - status (and optional group) apply.

Self-contained build - no separate .NET runtime. In-app updates use the same Velopack channel. Session cookies stay in DPAPI on that PC after first login. Releases are **tag-gated** (`v*`).

Local pack (maintainers):

```powershell
.\scripts\build-velopack.ps1 -Version 0.2.0
```

## Quick start (dev)

```powershell
dotnet restore src/VrchatPrivacy.sln
dotnet test tests/VrchatPrivacy.Core.Tests/VrchatPrivacy.Core.Tests.csproj
dotnet run --project src/VrchatPrivacy.App/VrchatPrivacy.App.csproj
```

Requires .NET 8 SDK. VRChat client logs: `%UserProfile%\AppData\LocalLow\VRChat\VRChat\output_log_*.txt`.

## Hard constraints (do not "improve" away)

- Match **world IDs** only (not names / instance IDs).
- Leave status alone when leaving protected worlds (no surprise blue restore). Blue only when explicitly set for a world.
- Store session cookies with Windows DPAPI; never log passwords, OTP codes, or tokens.
- Unofficial VRChat API: identify the app; treat **≤1 mutating request / 60s** as a hard rate budget (see [Limitations](#limitations)).

## License

[MIT](LICENSE). Unofficial - not affiliated with VRChat Inc.
