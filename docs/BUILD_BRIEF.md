# VRChat Status Guard - build brief

## Purpose

Prevent someone accidentally remaining on blue when entering designated protected worlds. Best-effort automation; a short delay after entry is acceptable.

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

- Enter a protected world while blue: automatically become orange/red.
- Start the tool while already in a protected world: protection applies.
- Move between protected worlds: remain protected.
- Leave a protected world: remain orange/red.
- Restart VRChat: detection resumes.
- Lose connectivity or authentication: display the failure and retry with bounded backoff.
- Pause protection: stop automatic changes.

## Scope limits

Windows PC VRChat only for the first version. No guarantee against brief location exposure during transitions, and no control over information revealed by other people in the instance.

World IDs and the final choice of orange versus red can be supplied later; they don’t block starting the build.
