#!/bin/sh

# launchctl submit creates an inferred keepalive job. Run the HUD as a child,
# then remove that job when the HUD exits so a user-requested Exit stays exited.
set +e

if [ "$#" -lt 2 ]; then
    echo 'Usage: hud-runner.sh <launch-label> <executable> [arguments...]' >&2
    exit 2
fi

launch_label=$1
shift

"$@"
hud_exit_code=$?
/bin/launchctl remove "$launch_label" >/dev/null 2>&1 || true
exit "$hud_exit_code"
