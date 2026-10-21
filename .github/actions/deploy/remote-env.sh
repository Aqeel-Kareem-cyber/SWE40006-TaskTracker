# Sourced by action.yml. Builds REMOTE_ENV, the "NAME=value ..." list passed to
# `env` on the host. Each value is shell-quoted so it cannot break the command.
REMOTE_ENV=""
for var in IMAGE EXPECTED_SHA APP_ENV RESET_DATA LOG_GROUP AWS_REGION GHCR_USER GHCR_TOKEN; do
  REMOTE_ENV+="${var}=$(printf %q "${!var:-}") "
done
export REMOTE_ENV
