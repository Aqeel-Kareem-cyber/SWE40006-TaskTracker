#!/usr/bin/env bash
#
# deploy.sh - replace the TaskTracker container on an EC2 host with a new image.
#
# Runs ON the host, as root. GitHub Actions sends this file to the host through
# AWS Systems Manager Run Command (default) or SSH and executes it there, so the
# host needs no copy of the repository. It can also be run by hand for recovery.
#
# Usage:
#   sudo IMAGE=ghcr.io/owner/swe40006-tasktracker:sha-abc123 \
#        EXPECTED_SHA=abc123... APP_ENV=Production ./deploy.sh
#
# Environment variables:
#   IMAGE          (required) image reference to run, ideally pinned by tag or digest
#   EXPECTED_SHA   (optional) commit the /version endpoint must report after start
#   APP_ENV        Staging | Production (default Production) -> ASPNETCORE_ENVIRONMENT
#   RESET_DATA     true to start from an empty database (used for staging only)
#   HOST_PORT      host port to publish (default 80)
#   LOG_GROUP      CloudWatch Logs group; if set, container logs use the awslogs driver
#   AWS_REGION     region for awslogs (default ap-southeast-2)
#   GHCR_USER / GHCR_TOKEN  optional; used for a one-off `docker login ghcr.io`
#                  when the image is private. Credentials are removed afterwards.
#
# Steps:
#   1. Pull the new image.
#   2. Stop the running container, keep it as "<name>-previous".
#   3. Back up the SQLite file from the data volume.
#   4. Start the new container on the same volume.
#   5. Wait for /health to return 200 and /version to report EXPECTED_SHA.
#   6. On success remove the previous container. On failure restore the
#      database backup, restart the previous container and exit non-zero.

set -Eeuo pipefail

: "${IMAGE:?IMAGE must be set}"
APP_ENV="${APP_ENV:-Production}"
EXPECTED_SHA="${EXPECTED_SHA:-}"
RESET_DATA="${RESET_DATA:-false}"
HOST_PORT="${HOST_PORT:-80}"
LOG_GROUP="${LOG_GROUP:-}"
AWS_REGION="${AWS_REGION:-ap-southeast-2}"

NAME="tasktracker"
PREVIOUS="${NAME}-previous"
VOLUME="tasktracker-data"
DB_FILE="tasktracker.db"
STATE_DIR="/var/lib/tasktracker-deploy"
BACKUP_DIR="${STATE_DIR}/backups"
HEALTH_TIMEOUT=90

log() { printf '[deploy %s] %s\n' "$(date -u +%H:%M:%S)" "$*"; }
fail() { log "ERROR: $*"; exit 1; }

[ "$(id -u)" -eq 0 ] || fail "run as root (sudo)"
command -v docker >/dev/null || fail "docker is not installed"
command -v curl >/dev/null || fail "curl is not installed"
[ "$RESET_DATA" = "true" ] && [ "$APP_ENV" = "Production" ] && fail "RESET_DATA is not allowed in Production"
mkdir -p "$BACKUP_DIR"

log "environment=${APP_ENV} image=${IMAGE} expected_sha=${EXPECTED_SHA:-<not checked>}"

# ---------- 1. pull ----------
if [ -n "${GHCR_TOKEN:-}" ]; then
  export DOCKER_CONFIG
  DOCKER_CONFIG="$(mktemp -d)"
  trap 'rm -rf "$DOCKER_CONFIG"' EXIT
  echo "$GHCR_TOKEN" | docker login ghcr.io -u "${GHCR_USER:-github}" --password-stdin >/dev/null
fi
docker pull "$IMAGE"
NEW_DIGEST="$(docker image inspect -f '{{index .RepoDigests 0}}' "$IMAGE" 2>/dev/null || echo "$IMAGE")"
log "pulled ${NEW_DIGEST}"

# ---------- 2. keep the running container as the rollback target ----------
docker rm -f "$PREVIOUS" >/dev/null 2>&1 || true
HAD_PREVIOUS=false
if docker container inspect "$NAME" >/dev/null 2>&1; then
  PREV_IMAGE="$(docker container inspect -f '{{.Config.Image}}' "$NAME")"
  log "stopping current container (image ${PREV_IMAGE})"
  docker stop -t 20 "$NAME" >/dev/null
  docker rename "$NAME" "$PREVIOUS"
  HAD_PREVIOUS=true
fi

# ---------- 3. back up the database (container stopped, so the file is consistent) ----------
docker volume create "$VOLUME" >/dev/null
VOLUME_PATH="$(docker volume inspect -f '{{.Mountpoint}}' "$VOLUME")"
BACKUP_FILE=""
if [ -f "${VOLUME_PATH}/${DB_FILE}" ]; then
  BACKUP_FILE="${BACKUP_DIR}/${DB_FILE}.$(date -u +%Y%m%dT%H%M%SZ)"
  cp -p "${VOLUME_PATH}/${DB_FILE}" "$BACKUP_FILE"
  log "database backed up to ${BACKUP_FILE}"
  # keep the 10 most recent backups
  ls -1t "${BACKUP_DIR}/${DB_FILE}".* 2>/dev/null | tail -n +11 | xargs -r rm -f
fi

if [ "$RESET_DATA" = "true" ]; then
  log "RESET_DATA=true: removing ${DB_FILE} so migrations rebuild an empty database"
  rm -f "${VOLUME_PATH}/${DB_FILE}" "${VOLUME_PATH}/${DB_FILE}-shm" "${VOLUME_PATH}/${DB_FILE}-wal"
fi

# ---------- rollback helper ----------
rollback() {
  log "ROLLBACK: $1"
  docker logs --tail 50 "$NAME" 2>&1 | sed 's/^/[new container] /' || true
  docker rm -f "$NAME" >/dev/null 2>&1 || true
  if [ -n "$BACKUP_FILE" ] && [ "$RESET_DATA" != "true" ]; then
    cp -p "$BACKUP_FILE" "${VOLUME_PATH}/${DB_FILE}"
    rm -f "${VOLUME_PATH}/${DB_FILE}-shm" "${VOLUME_PATH}/${DB_FILE}-wal"
    log "database restored from ${BACKUP_FILE}"
  fi
  if [ "$HAD_PREVIOUS" = true ]; then
    docker rename "$PREVIOUS" "$NAME"
    docker start "$NAME" >/dev/null
    log "previous release restarted"
  fi
  exit 1
}

# ---------- 4. start the new container ----------
RUN_ARGS=(
  --detach
  --name "$NAME"
  --restart unless-stopped
  --publish "${HOST_PORT}:8080"
  --volume "${VOLUME}:/app/data"
  --env "ASPNETCORE_ENVIRONMENT=${APP_ENV}"
  --env "ConnectionStrings__DefaultConnection=Data Source=/app/data/${DB_FILE}"
  --label "tasktracker.environment=${APP_ENV}"
  --label "tasktracker.deployed-at=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
)
if [ -n "$LOG_GROUP" ]; then
  RUN_ARGS+=(
    --log-driver awslogs
    --log-opt "awslogs-region=${AWS_REGION}"
    --log-opt "awslogs-group=${LOG_GROUP}"
    --log-opt "awslogs-stream=${NAME}-$(date -u +%Y%m%d%H%M%S)"
    --log-opt awslogs-create-group=true
  )
fi

docker run "${RUN_ARGS[@]}" "$IMAGE" >/dev/null || rollback "container failed to start"
log "new container started"

# ---------- 5. health and version gate ----------
BASE="http://127.0.0.1:${HOST_PORT}"
deadline=$(( $(date +%s) + HEALTH_TIMEOUT ))
healthy=false
while [ "$(date +%s)" -lt "$deadline" ]; do
  if [ "$(docker container inspect -f '{{.State.Running}}' "$NAME" 2>/dev/null)" != "true" ]; then
    rollback "new container exited during start-up"
  fi
  body="$(curl -fsS --max-time 5 "${BASE}/health" 2>/dev/null || true)"
  if [ "$body" = "Healthy" ]; then healthy=true; break; fi
  sleep 3
done
[ "$healthy" = true ] || rollback "/health did not report Healthy within ${HEALTH_TIMEOUT}s"
log "/health: Healthy"

if [ -n "$EXPECTED_SHA" ]; then
  version="$(curl -fsS --max-time 5 "${BASE}/version" 2>/dev/null || true)"
  case "$version" in
    *"$EXPECTED_SHA"*) log "/version reports ${EXPECTED_SHA}" ;;
    *) rollback "/version returned '${version}', expected ${EXPECTED_SHA}" ;;
  esac
fi

curl -fsS --max-time 10 -o /dev/null "${BASE}/" || rollback "home page did not return 2xx"
curl -fsS --max-time 10 -o /dev/null "${BASE}/Tasks" || rollback "/Tasks did not return 2xx"
log "home page and /Tasks return 2xx"

# ---------- 6. success ----------
docker rm -f "$PREVIOUS" >/dev/null 2>&1 || true
# keep the three most recent TaskTracker images for manual rollback
REPO="${IMAGE%%[:@]*}"
docker images "$REPO" --format '{{.ID}}' | awk '!seen[$0]++' | tail -n +4 | xargs -r docker rmi >/dev/null 2>&1 || true

log "deployment succeeded: ${NEW_DIGEST}"
