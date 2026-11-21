#!/usr/bin/env bash
# Local OWASP-aligned Semgrep gate (Tier I). Prefer Docker; fail closed if unavailable.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
IMAGE="${SEMGREP_IMAGE:-semgrep/semgrep:1.122.0}"
RULES="${ROOT}/security/semgrep/vrchat-status-guard.yml"

if command -v docker >/dev/null 2>&1; then
  docker run --rm -v "${ROOT}:/src" -w /src "${IMAGE}" \
    semgrep scan --config /src/security/semgrep/vrchat-status-guard.yml --error \
    /src/src /src/tests
elif command -v semgrep >/dev/null 2>&1; then
  semgrep scan --config "${RULES}" --error "${ROOT}/src" "${ROOT}/tests"
else
  echo "Neither docker nor semgrep available; refuse to skip OWASP gate." >&2
  exit 1
fi
