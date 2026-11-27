# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- Release ship train: push to `main` auto-publishes Velopack again (tag-only gate after #37/#43 left users on stale v0.1.6 while main moved).
- Auto-release is **major/minor only** (conventional `feat` / breaking); **patch** via `workflow_dispatch` on demand (#77).
- Tray icons: branded Status Guard glyphs (SystemIcons.Shield/Application blanked after install; paused Warning still worked) (#80).
- What's New: GitHub Release body filled after `vpk upload` (was empty) (#81).

### Changed

- Test project renamed to `StatusGuard.Core.Tests` (#82).

### Changed

- Single installer path: Velopack `Setup.exe` only (`scripts/build-velopack.ps1`). Inno Setup removed from CI/release/docs.
- README honesty: API status ≠ guaranteed in-game joinability; cite VRChat feedback and ask for upvotes.
- Docs sweep: CONTRIBUTING/SUPPORT/BUILD_BRIEF/research paths and scripts (`deploy-local-install.ps1`, correct AppData folder).

### Added

- Join coalesce + tray balloons; status before deferred repping; activity log (Basic/Debug).
- EOF-only log watcher; halt mutations when VRChat process is not running.
- `scripts/deploy-local-install.ps1` for Startup-aligned local installs.
- Private repo bootstrap under `introVRt-Lounge` (now public).
- `StatusGuard.Core` world-id and log join parsing (pattern verified on live PC logs 2026-11-20).
- Protection rules/decision engine (default orange / `ask me`; never auto blue).
- WinForms tray, DPAPI session, protection orchestrator, hub UI, Velopack updates.
- Default protected world `wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd` (VRChat Home) → orange / ask me.
- Tier A/B/C CI (tests, gitleaks, Semgrep, nuget audit, installer pack).
