# Contributing

Public Windows tray app for VRChat status/group automation. Please read
[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md), [SUPPORT.md](SUPPORT.md), and the honesty
block at the top of [README.md](README.md) (API status ≠ guaranteed in-game joinability).

## Issues first

Features and bugs need a GitHub Issue before implementation work. See [AGENTS.md](AGENTS.md).
Exceptions: operator says "skip issue", one-line doc typo, or declared hotfix mode.

## Setup

1. Windows 10/11 for the tray app and installer; Core unit tests also run in CI (Linux).
2. Install [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
3. Clone this repo; restore and test:

```powershell
dotnet restore src/VrchatPrivacy.sln
dotnet test tests/VrchatStatusGuard.Core.Tests/VrchatStatusGuard.Core.Tests.csproj
dotnet run --project src/VrchatPrivacy.App/VrchatPrivacy.App.csproj
```

### Local install (matches Startup)

Windows **Start with Windows** (`HKCU\...\Run\VrchatStatusGuard`) points at
`%LOCALAPPDATA%\Programs\introVRt Lounge\VRChat Status Guard` - not `bin\Release`.
After local fixes, deploy into that folder:

```powershell
.\scripts\deploy-local-install.ps1
```

### Release pack (maintainers)

Velopack `Setup.exe` is the only installer. Merges to `main` auto-publish only for
conventional **major/minor** (`feat` / breaking). **Patch** releases are on demand:

```powershell
.\scripts\build-velopack.ps1 -Version 0.2.0
gh workflow run release.yml --ref main                 # patch bump (on demand)
gh workflow run release.yml --ref main -f version=0.2.0
```

Default script `-Version` is `0.1.0` for CI smoke only. Use `feat:` in the squash
subject when the merge should cut a GitHub Release.

## Git hygiene

- Default path: issue → branch (`feat/123-short-name`) → PR with `Fixes #N` → squash merge.
- Resting state is **happy git place**: clean `main` only - no open finished issues, no
  unmerged session PRs, no stray worktrees. See [AGENTS.md](AGENTS.md).
- Dependabot PRs auto-merge when aggregate job `ci` is green.
- Human PRs opt into auto-merge with the `automerge` label.

## Secrets

- Never commit `.env`, auth cookies, passwords, or live `output_log_*.txt` files.
- Use sanitized fixtures in tests (redact `usr_` / `grp_` ids).
- App data (cookies, settings, `guard.log`, `activity.jsonl`) lives under
  `%LOCALAPPDATA%\introVRt Lounge\VRChat Status Guard\` - keep it off git.

## PR checklist

- [ ] Linked issue (`Fixes #N`) unless exception applies
- [ ] `dotnet test tests/VrchatStatusGuard.Core.Tests/VrchatStatusGuard.Core.Tests.csproj` green
- [ ] No secrets in the diff
- [ ] Docs/TODO updated if behaviour or milestone status changed
