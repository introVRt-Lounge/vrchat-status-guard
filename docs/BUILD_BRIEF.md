# VRChat Status Guard - build brief

## Purpose

Prevent someone accidentally remaining on blue when entering designated protected worlds. Best-effort automation; a short delay after entry is acceptable.

**Platform reality:** writing status via the unofficial API does **not** reliably update the live VRChat client or friends' Join vs Request Invite. See README [Does this actually work?](../README.md#does-this-actually-work) and [feedback.vrchat.com](https://feedback.vrchat.com/open-beta/p/vrc-rest-api-websocket-issues-since-years-status-and-avatar-changes-are-extremel). Acceptance checks below mean "API + best-effort client sync," not a hard privacy guarantee.

## Platform and access

- Windows tray application, running alongside PC VRChat.
- Read access to the current Windows user’s VRChat output logs.
- Internet access and the account owner’s VRChat login, including any required two-factor authentication.
- Store reusable authentication securely using Windows protection. Never log passwords, codes, or session tokens.

## Required behaviour

1. Let the user add protected worlds by world ID or VRChat world URL.
2. Allow orange or red as the protection status; default to orange.
3. Detect entry into a protected world and apply the configured status.
4. When starting during an active VRChat session, determine the current world and apply protection if appropriate.
5. Leave the status unchanged when leaving protected worlds. Never automatically restore blue.
6. Offer automatic startup with Windows and a visible pause control.
7. Show whether protection is working, paused, disconnected, or awaiting login. Report failures without claiming success.

## Implementation proposal

Use C#/.NET with a small settings window and tray icon. Separate log detection, world rules, authentication, and status updates so each can be tested independently.

Match world IDs, rather than names or instance IDs. Follow newly created log files when VRChat restarts. Avoid collecting player names, companions, or world history.

## First technical milestone

Before building the full interface, demonstrate:

- Reliable world ID detection from current VRChat logs.
- Login and two-factor authentication.
- A successful orange/red update while VRChat is running.
- Confirmation that the change persists and is reflected in-game.

The exact log events and API behaviour remain to be verified against a live session (log join pattern verified 2026-11-20; live status PUT still pending).

## Acceptance checks

- Enter a protected world while blue: Status Guard writes orange/red to the API (verified GET).
- Start the tool while already in a protected world: catch-up apply runs.
- Move between protected worlds: coalesce to latest; status beats deferred repping.
- Leave a protected world: leave status unchanged (no invented blue).
- Restart VRChat: log watcher follows the newest file from EOF (no full-history replay).
- VRChat process not running: halt pending status/group mutations.
- Lose connectivity or authentication: display the failure; bounded backoff still TODO.
- Pause protection: stop automatic changes.
- In-game Quick Menu colour matching the API is **not** guaranteed (VRChat client bug).

## Scope limits

Windows PC VRChat only for the first version. No guarantee against brief location exposure during transitions, and no control over information revealed by other people in the instance.

World IDs and the final choice of orange versus red can be supplied later; they don’t block starting the build.
