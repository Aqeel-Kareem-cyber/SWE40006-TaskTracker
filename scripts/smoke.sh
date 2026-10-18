#!/usr/bin/env bash
#
# smoke.sh - post-deployment checks run from the GitHub runner (or a laptop)
# against a deployed TaskTracker.
#
# Usage: scripts/smoke.sh <base-url> [expected-commit-sha] [samples]
#   e.g. scripts/smoke.sh http://staging.example.com 3f2c1ab 100
#
# Checks:
#   1. /health returns 200 with body "Healthy"
#   2. /version reports the expected commit (if one is given)
#   3. / and /Tasks return 200
#   4. A labelled SMOKE task can be created and deleted through the form
#      (set SMOKE_WRITE=false to skip)
#   5. Latency sample: N sequential GETs to /Tasks and /health; reports p50/p95/max
#      and fails if p95 > P95_LIMIT_MS (default 500) or any request errors.
#
# Writes a Markdown summary to $GITHUB_STEP_SUMMARY when run in Actions and a
# CSV of raw timings to smoke-results/latency.csv for the report evidence.

set -Eeuo pipefail

BASE_URL="${1:?usage: smoke.sh <base-url> [expected-sha] [samples]}"
BASE_URL="${BASE_URL%/}"
EXPECTED_SHA="${2:-}"
SAMPLES="${3:-100}"
P95_LIMIT_MS="${P95_LIMIT_MS:-500}"
OUT_DIR="${OUT_DIR:-smoke-results}"
SUMMARY="${GITHUB_STEP_SUMMARY:-/dev/null}"

mkdir -p "$OUT_DIR"
failures=0
pass() { echo "PASS  $*"; echo "| :white_check_mark: | $* |" >> "$SUMMARY"; }
bad()  { echo "FAIL  $*"; echo "| :x: | $* |" >> "$SUMMARY"; failures=$((failures + 1)); }

{
  echo "### Smoke test: ${BASE_URL}"
  echo
  echo "| Result | Check |"
  echo "| --- | --- |"
} >> "$SUMMARY"

# Wait up to 60 s for the site to answer at all (DNS, security group, start-up).
for _ in $(seq 1 20); do
  curl -fsS --max-time 5 -o /dev/null "${BASE_URL}/health" && break
  sleep 3
done

# 1. health
health="$(curl -sS --max-time 10 -w '\n%{http_code}' "${BASE_URL}/health" || echo -e '\n000')"
code="${health##*$'\n'}"; body="${health%$'\n'*}"
if [ "$code" = "200" ] && [ "$body" = "Healthy" ]; then pass "/health -> 200 Healthy"
else bad "/health -> ${code} '${body}'"; fi

# 2. version
if [ -n "$EXPECTED_SHA" ]; then
  version="$(curl -sS --max-time 10 "${BASE_URL}/version" || true)"
  if [[ "$version" == *"$EXPECTED_SHA"* ]]; then pass "/version reports ${EXPECTED_SHA:0:7}"
  else bad "/version returned '${version}', expected ${EXPECTED_SHA:0:7}"; fi
fi

# 3. pages
for path in / /Tasks; do
  code="$(curl -sS --max-time 10 -o /dev/null -w '%{http_code}' "${BASE_URL}${path}" || echo 000)"
  if [ "$code" = "200" ]; then pass "GET ${path} -> 200"; else bad "GET ${path} -> ${code}"; fi
done

# 4. write path: create one labelled task through the real form, then delete it.
#    Proves the database accepts writes and deletes without leaving data behind.
if [ "${SMOKE_WRITE:-true}" = "true" ]; then
  jar="$(mktemp)"; page="$(mktemp)"
  title="SMOKE-${EXPECTED_SHA:0:7}-$(date +%s)"
  token() { grep -o 'name="__RequestVerificationToken" type="hidden" value="[^"]*"' "$page" | head -1 | sed 's/.*value="//; s/"$//'; }
  curl -sS --max-time 10 -c "$jar" -b "$jar" -o "$page" "${BASE_URL}/Tasks"
  code="$(curl -sS --max-time 10 -c "$jar" -b "$jar" -o /dev/null -w '%{http_code}' \
    --data-urlencode "__RequestVerificationToken=$(token)" \
    --data-urlencode "NewTask.Title=${title}" \
    --data-urlencode "NewTask.Status=To Do" \
    --data-urlencode "NewTask.Priority=Low" \
    "${BASE_URL}/Tasks" || echo 000)"
  curl -sS --max-time 10 -c "$jar" -b "$jar" -o "$page" "${BASE_URL}/Tasks"
  # Find the ID in the first Edit link after the smoke task's title.
  id="$(python3 - "$page" "$title" <<'PY'
import re, sys
html = open(sys.argv[1], encoding="utf-8").read()
pos = html.find(sys.argv[2])
m = re.search(r'Edit(?:\?id=|/)(\d+)', html[pos:]) if pos >= 0 else None
print(m.group(1) if m else "")
PY
)"
  if [ "$code" = "302" ] && [ -n "$id" ]; then
    pass "created task '${title}' (id ${id})"
    code="$(curl -sS --max-time 10 -c "$jar" -b "$jar" -o /dev/null -w '%{http_code}' \
      --data-urlencode "__RequestVerificationToken=$(token)" \
      "${BASE_URL}/Tasks?handler=Delete&id=${id}" || echo 000)"
    curl -sS --max-time 10 -b "$jar" -o "$page" "${BASE_URL}/Tasks"
    if [ "$code" = "302" ] && ! grep -q "$title" "$page"; then pass "deleted task ${id}; it no longer appears in the list"
    else bad "delete of task ${id} returned ${code} or the task is still listed"; fi
  else
    bad "create task returned ${code}; task id '${id}'"
  fi
  rm -f "$jar" "$page"
fi

# 5. latency sample
echo "path,http_code,seconds" > "${OUT_DIR}/latency.csv"
errors=0
for i in $(seq 1 "$SAMPLES"); do
  path=/Tasks; [ $((i % 2)) -eq 0 ] && path=/health
  line="$(curl -sS --max-time 10 -o /dev/null -w '%{http_code},%{time_total}' "${BASE_URL}${path}" || echo '000,10')"
  echo "${path},${line}" >> "${OUT_DIR}/latency.csv"
  [ "${line%%,*}" = "200" ] || errors=$((errors + 1))
done

read -r p50 p95 pmax < <(tail -n +2 "${OUT_DIR}/latency.csv" | cut -d, -f3 | sort -n | awk '
  { v[NR] = $1 * 1000 }
  END {
    i50 = int(NR * 0.50 + 0.999); i95 = int(NR * 0.95 + 0.999)
    printf "%.0f %.0f %.0f\n", v[i50], v[i95], v[NR]
  }')

msg="latency over ${SAMPLES} requests: p50 ${p50} ms, p95 ${p95} ms, max ${pmax} ms, errors ${errors}"
if [ "$errors" -eq 0 ] && [ "$p95" -le "$P95_LIMIT_MS" ]; then pass "$msg (limit p95 ${P95_LIMIT_MS} ms)"
else bad "$msg (limit p95 ${P95_LIMIT_MS} ms)"; fi

printf '{"base_url":"%s","p50_ms":%s,"p95_ms":%s,"max_ms":%s,"errors":%s,"failures":%s}\n' \
  "$BASE_URL" "$p50" "$p95" "$pmax" "$errors" "$failures" > "${OUT_DIR}/summary.json"

echo
if [ "$failures" -gt 0 ]; then echo "${failures} smoke check(s) failed"; exit 1; fi
echo "All smoke checks passed"
