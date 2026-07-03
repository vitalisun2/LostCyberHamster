#!/bin/sh
set -eu

log() {
    echo "[ngrok-watchdog] $(date -u '+%Y-%m-%dT%H:%M:%SZ') $*"
}

if [ "$#" -eq 0 ]; then
    set -- http http://collector:8765 --url="https://${NGROK_DOMAIN}"
fi

: "${PUBLIC_HEALTH_URL:=https://${NGROK_DOMAIN}/health}"
: "${HEALTHCHECK_INTERVAL_SECONDS:=15}"
: "${HEALTHCHECK_TIMEOUT_SECONDS:=5}"
: "${HEALTHCHECK_FAILURE_THRESHOLD:=4}"
: "${HEALTHCHECK_START_PERIOD_SECONDS:=25}"

ngrok "$@" &
ngrok_pid="$!"
log "started ngrok pid=${ngrok_pid}"

shutdown() {
    log "received shutdown signal"
    if kill -0 "$ngrok_pid" 2>/dev/null; then
        kill "$ngrok_pid" 2>/dev/null || true
        wait "$ngrok_pid" 2>/dev/null || true
    fi
    exit 0
}

trap shutdown INT TERM

sleep "$HEALTHCHECK_START_PERIOD_SECONDS"
failures=0

while kill -0 "$ngrok_pid" 2>/dev/null; do
    if wget -q -T "$HEALTHCHECK_TIMEOUT_SECONDS" -O - \
        --header='ngrok-skip-browser-warning: true' \
        "$PUBLIC_HEALTH_URL" | grep -q '"ok"[[:space:]]*:[[:space:]]*true'; then
        failures=0
    else
        failures=$((failures + 1))
        log "public health failed ${failures}/${HEALTHCHECK_FAILURE_THRESHOLD}"
    fi

    if [ "$failures" -ge "$HEALTHCHECK_FAILURE_THRESHOLD" ]; then
        log "public health did not recover; restarting ngrok container"
        kill "$ngrok_pid" 2>/dev/null || true
        wait "$ngrok_pid" 2>/dev/null || true
        exit 1
    fi

    sleep "$HEALTHCHECK_INTERVAL_SECONDS"
done

set +e
wait "$ngrok_pid"
exit_code="$?"
set -e

log "ngrok exited with code ${exit_code}"
exit "$exit_code"
