#!/bin/bash
# sleep-mode.sh -- state for First Chair's sleep mode (see .claude/skills/sleep-mode/SKILL.md).
#
#   bash .claude/scripts/sleep-mode.sh on [--hours N] [--max-continues N]
#   bash .claude/scripts/sleep-mode.sh wait      # ending a turn to wait for a background agent
#   bash .claude/scripts/sleep-mode.sh resume    # the agent reported back; keep working
#   bash .claude/scripts/sleep-mode.sh finish <done|stopped|stalled|blocked|time-up|off> [note]
#   bash .claude/scripts/sleep-mode.sh status
#
# State lives in production/session-state/sleep-mode.json (gitignored: it describes this
# session, not the project). `on` switches modes.automation to autonomous in the gitignored
# project.local.yaml; `finish` puts back whatever was there before.
# Prints observations only; the Stop hook (.claude/hooks/sleep-mode-stop.sh) reads the state.
set -euo pipefail

ROOT="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
cd "$ROOT"
STATE=production/session-state/sleep-mode.json
LOCAL_YAML=project.local.yaml

command -v jq >/dev/null 2>&1 || { echo "sleep-mode: jq is required" >&2; exit 1; }

now() { date -u +%Y-%m-%dT%H:%M:%SZ; }
set_field() {  # set_field <jq filter> ; rewrites the state atomically
  local tmp
  tmp=$(mktemp "$STATE.XXXX")
  jq "$1" "$STATE" > "$tmp" && mv -f "$tmp" "$STATE"
}
require_state() { [ -f "$STATE" ] || { echo "sleep-mode: off (no state file)"; exit 0; }; }

cmd="${1:-status}"; shift || true
case "$cmd" in
  on)
    hours=0; max=60
    while [ $# -gt 0 ]; do
      case "$1" in
        --hours) hours="$2"; shift 2 ;;
        --max-continues) max="$2"; shift 2 ;;
        *) echo "sleep-mode: unknown option $1" >&2; exit 2 ;;
      esac
    done
    branch=$(git rev-parse --abbrev-ref HEAD)
    case "$branch" in
      main|master|release/*) echo "sleep-mode: refusing to run on $branch; use a claude/* work branch" >&2; exit 1 ;;
    esac
    until_ts=null
    if [ "$hours" != "0" ]; then
      until_ts="\"$(date -u -d "+$hours hours" +%Y-%m-%dT%H:%M:%SZ)\""
    fi
    # remember the local automation override so `finish` can restore it exactly
    had_local=false; prev_auto=""
    if [ -f "$LOCAL_YAML" ]; then
      had_local=true
      prev_auto=$(bash .claude/hooks/yaml-helper.sh get_yaml_key "$LOCAL_YAML" modes.automation 2>/dev/null || true)
    fi
    mkdir -p "$(dirname "$STATE")"
    jq -n --arg started "$(now)" --arg branch "$branch" --arg head "$(git rev-parse HEAD)" \
          --argjson until "$until_ts" --argjson max "$max" \
          --argjson had_local "$had_local" --arg prev_auto "$prev_auto" \
      '{status:"active", started:$started, until:$until, branch:$branch, start_head:$head,
        last_head:$head, continues:0, max_continues:$max, no_progress:0, max_no_progress:3,
        had_local_yaml:$had_local, prev_local_automation:$prev_auto, reason:null}' > "$STATE"
    bash .claude/scripts/config-set.sh --local modes.automation autonomous >/dev/null
    echo "sleep-mode: on"
    echo "branch: $branch"
    echo "until: $(jq -r '.until // "no time limit"' "$STATE")"
    echo "automation: autonomous (project.local.yaml) until sleep mode ends"
    ;;
  wait)
    require_state; set_field '.status = "waiting"'; echo "sleep-mode: waiting for a background agent" ;;
  resume)
    require_state; set_field '.status = "active" | .no_progress = 0'; echo "sleep-mode: active" ;;
  finish)
    require_state
    why="${1:-done}"; note="${2:-}"
    set_field ".status = \"$why\" | .finished = \"$(now)\" | .reason = \"${note//\"/\\\"}\""
    # restore the automation setting sleep mode replaced
    had_local=$(jq -r .had_local_yaml "$STATE"); prev=$(jq -r .prev_local_automation "$STATE")
    if [ "$had_local" = "false" ]; then
      rm -f "$LOCAL_YAML"
    elif [ -n "$prev" ]; then
      bash .claude/scripts/config-set.sh --local modes.automation "$prev" >/dev/null
    else
      bash .claude/scripts/config-set.sh --local modes.automation guided >/dev/null
      echo "note: project.local.yaml had no automation override before; set to guided" >&2
    fi
    echo "sleep-mode: finished ($why). Automation restored."
    echo "commits since start: $(git rev-list --count "$(jq -r .start_head "$STATE")"..HEAD)"
    ;;
  status)
    require_state
    jq -r '"status: \(.status)\nstarted: \(.started)\nuntil: \(.until // "no time limit")\nbranch: \(.branch)\ncontinues: \(.continues)/\(.max_continues)\nno-progress stops: \(.no_progress)/\(.max_no_progress)\nreason: \(.reason // "-")"' "$STATE"
    echo "commits since start: $(git rev-list --count "$(jq -r .start_head "$STATE")"..HEAD)"
    ;;
  *) echo "usage: sleep-mode.sh on [--hours N] [--max-continues N] | wait | resume | finish <why> [note] | status" >&2; exit 2 ;;
esac
