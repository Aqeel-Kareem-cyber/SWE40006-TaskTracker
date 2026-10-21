#!/usr/bin/env bash
# Runs every automated test suite against the deployed staging app (pipeline job 4).
#   Required: E2E_BASE_URL         e.g. http://localhost (the staging app on this host)
#   Optional: E2E_EXPECTED_COMMIT  commit being released (checked against /version)
#             E2E_HEALTH_PATH      /health (default) or /healthz
# Results (TRX test reports, Playwright traces/screenshots on failure, latency.json) go to /results,
# which the pipeline mounts from the host and collects before allowing production promotion.
set -uo pipefail

if [[ -z "${E2E_BASE_URL:-}" ]]; then
  echo "ERROR: E2E_BASE_URL must be set to the deployed staging app URL." >&2
  exit 2
fi

mkdir -p /results
echo "Running all test suites against ${E2E_BASE_URL} (commit: ${E2E_EXPECTED_COMMIT:-not set})"

dotnet test /src/TaskTracker.slnx -c Release --no-build \
  --results-directory /results \
  --logger "trx" \
  --logger "console;verbosity=normal"
status=$?

echo "Test run finished with exit code ${status}. Results are in /results."
exit "${status}"
