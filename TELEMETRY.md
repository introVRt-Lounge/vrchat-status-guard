# Telemetry (early adopters / guinea pigs)

Status Guard ships **extreme** anonymous usage diagnostics while the first users
dogfood protection. Default is **on** (opt-out). A later release will tone this
way down ([#86](https://github.com/introVRt-Lounge/vrchat-status-guard/issues/86)).

You can turn it off in the first-run wizard, or anytime at **Protection hub → Privacy**.

## What we collect

Batched JSON POSTs to `https://logs.introvrtlounge.com/` (Cloudflare → estate Fluent Bit).

Each batch includes:

| Field | Purpose |
|-------|---------|
| `install_id` | Random UUID per install (not a VRChat id) |
| `app_version` | SemVer from the binary |
| `os` | `Environment.OSVersion` string |
| `events[]` | Allowlisted event name + string props |

Typical event names:

- `app_start` / `app_exit` / `heartbeat`
- `wizard_step` (step, outcome, ms since previous / start)
- `telemetry_consent` (enabled, source = wizard \| hub_privacy)
- `ui_surface` (hub, about, activity_log, whats_new, …)
- `auth_outcome` (ok / ok_2fa / cancel / fail class - **no username**)
- `protection_apply` / `protection_state` / `protection_halt`
- `tray_click` / `unhandled` / `unhandled_ui`

Props are scrubbed: keys containing `user`, `cookie`, `password`, `otp`,
`world_id`, `group_id`, or `instance` are dropped. Values redact bearer-ish
tokens and `C:\Users\<name>\` paths.

## What we do **not** collect

- VRChat username, password, OTP, cookies, or auth tokens
- World / group / instance IDs or names
- Chat, friends lists, or avatar data
- Full file paths with your Windows profile name (scrubbed)

## Opt out

1. Uncheck the guinea-pig box on first run, **or**
2. Open the hub → **Privacy** → uncheck "Send anonymous usage diagnostics"

When disabled, the client drops the queue and does not POST.

## Schema

`schema: 1` today. See `StatusGuard.Core.Telemetry.TelemetryClient`.
