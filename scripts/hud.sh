#!/bin/sh
set -eu

action=${1:-}
case "$action" in
  Start|Stop|Status) ;;
  *) echo "Usage: scripts/hud.sh Start|Stop|Status" >&2; exit 2 ;;
esac

plugin_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
case "$(uname -m)" in
  arm64) runtime_id=osx-arm64 ;;
  x86_64) runtime_id=osx-x64 ;;
  *) echo "Unsupported macOS architecture: $(uname -m)" >&2; exit 1 ;;
esac

app_bundle="$plugin_root/bin/$runtime_id/Codex Usage.app"
helper="$app_bundle/Contents/MacOS/CodexUsage.Desktop"
find_pids() { pgrep -f "$helper" 2>/dev/null || true; }

case "$action" in
  Status)
    pids=$(find_pids)
    if [ -n "$pids" ]; then echo "Codex Usage is running (PID $(printf '%s\n' "$pids" | head -n 1))."; else echo "Codex Usage is not running."; fi
    ;;
  Stop)
    pids=$(find_pids)
    if [ -z "$pids" ]; then echo "Codex Usage is already stopped."; else kill $pids; echo "Codex Usage stopped."; fi
    ;;
  Start)
    pids=$(find_pids)
    if [ -n "$pids" ]; then echo "Codex Usage is already running (PID $(printf '%s\n' "$pids" | head -n 1))."; exit 0; fi
    if [ ! -d "$app_bundle" ] || [ ! -f "$helper" ]; then echo "Bundled Codex Usage app was not found at $app_bundle." >&2; exit 1; fi
    open "$app_bundle"
    echo "Codex Usage started."
    ;;
esac
