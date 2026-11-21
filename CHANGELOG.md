# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Private repo bootstrap under `introVRt-Lounge`.
- `VrchatPrivacy.Core` world-id and log join parsing (pattern verified on live PC logs 2026-11-20).
- Protection rules/decision engine (default orange / `ask me`; never auto blue).
- WinForms tray scaffold.
- Tier I CI (tests, gitleaks, Semgrep, nuget audit).
- Self-contained Inno Setup wizard (`scripts/build-installer.ps1` → `VrchatStatusGuard-Setup-*.exe`).
- Log watcher, DPAPI session, protection orchestrator, tray login/pause/settings.
- Default protected world `wrld_4432ea9b-729c-46e3-8eaf-846aa0a37fdd` (VRChat Home) → orange / ask me.
