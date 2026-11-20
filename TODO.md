# TODO

## Done

- [x] Private GitHub repo `introVRt-Lounge/vrchat-privacy`
- [x] Tier I baseline (README, CONTRIBUTING, SECURITY, CI with test/secret-scan/owasp-sast/audit, Dependabot)
- [x] Core library: world id parse, log join parse (live-verified pattern), protection rules/decisions
- [x] Unit tests for parse + decision invariants (no blue restore)
- [x] Research notes + milestone 1 plan

## Next

- [ ] Issue + implement log directory watcher (follow new `output_log_*.txt`)
- [ ] Issue + implement DPAPI credential store + VRChat.API session (login/2FA)
- [ ] Issue + milestone-1 live demo: apply `ask me`/`busy`, confirm in-game
- [ ] Tray settings UI (world list, orange/red, pause, startup-with-Windows)
- [ ] Bounded backoff on auth/network failure
- [ ] Operator supplies final protected world IDs + orange vs red preference

## Out of scope (v1)

- Quest / non-Windows
- Guaranteeing zero blue flash during transitions
- Controlling what others disclose about the instance
