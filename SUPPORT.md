# Getting help

## I have a question / I'm stuck

Open a [GitHub Issue](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/new/choose)
and tag it with `question`. Please include:

- App version (About dialog, installer filename, or git tag, e.g. `v0.2.0`)
- Windows version (`winver`)
- Whether you are logged in (no passwords, please)
- What you expected vs what happened
- Relevant lines from
  `%LOCALAPPDATA%\introVRt Lounge\VRChat Status Guard\guard.log`
  (redact usernames if you want)

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

- Install from the latest GitHub Release **Setup.exe** only (Velopack).
- Use tray → **Check for updates…** after install; do not mix side-by-side copies.
- Self-contained build: you should not need a separate .NET runtime.
- Auth cookies / settings / logs live under
  `%LOCALAPPDATA%\introVRt Lounge\VRChat Status Guard\`
  (migrated from the older Privacy Guard folder when present).
- Local maintainers: after building from source, run
  `.\scripts\deploy-local-install.ps1` so Startup matches the tree you just built
  (see [CONTRIBUTING.md](CONTRIBUTING.md)).

## Status took a while / stayed the same

Two different things get confused:

1. **~60s mutation budget** (Tupper / unofficial API) - intentional spacing between
   status/group writes. See README [Rate limit (Tupper)](README.md#rate-limit-tupper)
   and tray → **About…**. Status is applied before optional group repping; world hops
   do not reset the timer.
2. **API wrote Ask Me but Quick Menu / friends still show Join Me** - known VRChat
   client desync. Status Guard cannot force the live client. Read
   [Does this actually work?](README.md#does-this-actually-work) and upvote
   [VRChat's feedback post](https://feedback.vrchat.com/open-beta/p/vrc-rest-api-websocket-issues-since-years-status-and-avatar-changes-are-extremel).

Use tray → **Activity log…** (or hub → Activity log) for joins, decisions, and
`ApplyStatus` lines. Same data: `guard.log` and `activity.jsonl` in the AppData folder above.
