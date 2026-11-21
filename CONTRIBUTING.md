# Contributing

Public Windows tray app for VRChat status/group automation. Please read
[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) and [SUPPORT.md](SUPPORT.md).

## Issues first

Features and bugs need a GitHub Issue before implementation work. See [AGENTS.md](AGENTS.md). Exceptions: operator says "skip issue", one-line doc typo, or declared hotfix mode.

## Setup

1. Windows 10/11 for the tray app and installer; Core unit tests also run in CI.
2. Install [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
3. For the wizard installer: `winget install JRSoftware.InnoSetup` (ISCC).
4. Clone this repo; restore and test:

```powershell
dotnet restore src/VrchatPrivacy.sln
dotnet test tests/VrchatPrivacy.Core.Tests/VrchatPrivacy.Core.Tests.csproj
.\scripts\build-installer.ps1
```

## Git hygiene

- Default path: issue → branch (`feat/123-short-name`) → PR with `Fixes #N` → squash merge.
- Solo direct push to `main` is allowed during early bootstrap; run a wrap-up pass (CI green, docs current) before calling work done.
- Dependabot PRs auto-merge when aggregate job `ci` is green.
- Human PRs opt into auto-merge with the `automerge` label.

## Secrets

- Never commit `.env`, auth cookies, passwords, or live `output_log_*.txt` files.
- Use sanitized fixtures in tests (redact `usr_` / `grp_` ids).

## PR checklist

- [ ] Linked issue (`Fixes #N`) unless exception applies
- [ ] `dotnet test` green locally for Core
- [ ] No secrets in the diff
- [ ] Docs/TODO updated if behaviour or milestone status changed
