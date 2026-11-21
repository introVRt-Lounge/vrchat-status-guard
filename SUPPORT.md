# Getting help

## I have a question / I'm stuck

Open a [GitHub Issue](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/new/choose)
and tag it with `question`. Please include:

- App version (installer filename or git tag, e.g. `v0.1.5`)
- Windows version (`winver`)
- Whether you are logged in (no passwords, please)
- What you expected vs what happened
- Relevant lines from `%LocalAppData%\VRChat Status Guard\logs\` (redact usernames if you want)

## I found a bug

Use the [bug report template](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/new?template=bug_report.yml).

## I have a feature idea

Use the [feature request template](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/new?template=feature_request.yml).
Scope is intentionally narrow: Windows PC VRChat status/group automation for
protected world IDs. Quest, overlay UI, and "control other players" ideas are
usually out of scope.

## I think there's a security issue

**Do not open a public issue.** See [SECURITY.md](SECURITY.md).

## Install / update problems

- Prefer the latest GitHub Release installer.
- Self-contained build: you should not need a separate .NET runtime.
- Auth cookies live under `%LocalAppData%\VRChat Status Guard\` (migrated from
  the older Privacy Guard folder when present).
