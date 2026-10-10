#!/usr/bin/env bash
# Rebuilds the MatDesign container and redeploys the dev stack (the project's live-reload / test workflow).
#   ./scripts/redeploy.sh [--port 8060]
set -euo pipefail

port=8060
while [[ $# -gt 0 ]]; do
  case "$1" in
    --port) port="$2"; shift ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
  shift
done

cd "$(dirname "$0")/.."
export MATDESIGN_CHANNEL=local
export MATDESIGN_BUILD_DATE="$(date -u +%Y%m%d)"
export MATDESIGN_PORT="$port"

docker compose -f docker-compose.dev.yml up -d --build --remove-orphans

echo
echo "MatDesign local-${MATDESIGN_BUILD_DATE} is running on http://localhost:${port}"
