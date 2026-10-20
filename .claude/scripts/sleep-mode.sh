#!/bin/bash
# sleep-mode.sh -- state for First Chair's sleep mode (see .claude/skills/sleep-mode/SKILL.md).
#
#   bash .claude/scripts/sleep-mode.sh on [--hours N]
#   bash .claude/scripts/sleep-mode.sh ensure --until <ISO|none> --start-head <sha>
#                                          # after a wake-up: rebuild the state if the machine was reclaimed
#   bash .claude/scripts/sleep-mode.sh tick      # start of every sleep-mode turn
#   bash .claude/scripts/sleep-mode.sh wait      # ending a turn to wait for a background agent
#   bash .claude/scripts/sleep-mode.sh resume    # the agent reported back; keep working
#   bash .claude/scripts/sleep-mode.sh finish <done|stopped|stalled|blocked|time-up|off> [note]
#   bash .claude/scripts/sleep-mode.sh status
#
# State lives in production/session-state/sleep-mode.json (gitignored: it describes this
# session, not the project). `on` switches modes.automation to autonomous in the gitignored
# project.local.yaml; `finish` puts back whatever was there before. A cloud machine reclaimed
# while sleep mode waits for a usage reset loses both files, so every wake-up message carries
# the until time and start commit, and `ensure` rebuilds them from it.
#
# OBSERVATIONS, NEVER VERDICTS (CLAUDE.md, "Helpers in .claude/scripts/"): `tick` reports the
# time left and the turns since the last commit; whether to go on or wrap up is the skill's call.
set -euo pipefail

ROOT="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
cd "$ROOT"
STATE=production/session-state/sleep-mode.json
LOCAL_YAML=project.local.yaml

command -v jq >/dev/null 2>&1 || { echo "sleep-mode: jq is required" >&2; exit 1; }

now() { date -u +%Y-%m-%dT%H:%M:%SZ; }
update() {  # update <jq filter> [jq args...] ; rewrites the state atomically
  local filter="$1" tmp
  shift
  tmp=$(mktemp "$STATE.XXXX")
  jq "$@" "$filter" "$STATE" > "$tmp" && mv -f "$tmp" "$STATE"
}
require_state() { [ -f "$STATE" ] || { echo "status	off (no state file)"; exit 0; }; }

refuse_protected_branch() {
  case "$1" in
    main|master|release/*) echo "sleep-mode: refusing to run on $1; use a claude/* work branch" >&2; exit 1 ;;
  esac
}

# start <until-json> <start-head> ; writes a fresh state and switches automation to autonomous
start() {
  local until_json="$1" start_head="$2" branch had_local=false prev_auto=""
  branch=$(git rev-parse --abbrev-ref HEAD)
  refuse_protected_branch "$branch"
  # remember the local automation override so `finish` can restore it exactly
  if [ -f "$LOCAL_YAML" ]; then
    had_local=true
    prev_auto=$(bash .claude/hooks/yaml-helper.sh get_yaml_key "$LOCAL_YAML" modes.automation 2>/dev/null || true)
  fi
  mkdir -p "$(dirname "$STATE")"
  jq -n --arg started "$(now)" --arg branch "$branch" --arg start_head "$start_head" \
        --arg head "$(git rev-parse HEAD)" --argjson until "$until_json" \
        --argjson had_local "$had_local" --arg prev_auto "$prev_auto" \
    '{status:"active", started:$started, until:$until, branch:$branch, start_head:$start_head,
      last_head:$head, turns:0, turns_without_commit:0, max_turns_without_commit:3,
      had_local_yaml:$had_local, prev_local_automation:$prev_auto, reason:null}' > "$STATE"
  bash .claude/scripts/config-set.sh --local modes.automation autonomous >/dev/null
}

print_status() {
  jq -r '"status\t\(.status)\nstarted\t\(.started)\nuntil\t\(.until // "none")\nbranch\t\(.branch)\nstart_head\t\(.start_head)\nturns\t\(.turns)\nturns_without_commit\t\(.turns_without_commit)/\(.max_turns_without_commit)\nreason\t\(.reason // "-")"' "$STATE"
  echo "commits_since_start	$(git rev-list --count "$(jq -r .start_head "$STATE")"..HEAD 2>/dev/null || echo unknown)"
}

cmd="${1:-status}"; shift || true
case "$cmd" in
  on)
    hours=0
    while [ $# -gt 0 ]; do
      case "$1" in
        --hours) hours="$2"; shift 2 ;;
        *) echo "sleep-mode: unknown option $1" >&2; exit 2 ;;
      esac
    done
    case "$hours" in ''|*[!0-9]*) echo "sleep-mode: --hours takes a whole number" >&2; exit 2 ;; esac
    until_json=null
    if [ "$hours" != "0" ]; then
      until_json="\"$(date -u -d "+$hours hours" +%Y-%m-%dT%H:%M:%SZ)\""
    fi
    start "$until_json" "$(git rev-parse HEAD)"
    print_status
    echo "automation	autonomous (project.local.yaml) until sleep mode ends"
    ;;
  ensure)
    until_arg=""; head_arg=""
    while [ $# -gt 0 ]; do
      case "$1" in
        --until) until_arg="$2"; shift 2 ;;
        --start-head) head_arg="$2"; shift 2 ;;
        *) echo "sleep-mode: unknown option $1" >&2; exit 2 ;;
      esac
    done
    [ -n "$until_arg" ] && [ -n "$head_arg" ] || { echo "sleep-mode: ensure needs --until and --start-head" >&2; exit 2; }
    finished=$(grep -m1 '^Finished:' production/sleep-mode/plan.md 2>/dev/null || true)
    if [ -f "$STATE" ]; then
      echo "rebuilt	no (state file present)"
    elif [ -n "$finished" ]; then
      echo "rebuilt	no (production/sleep-mode/plan.md says this run already ended: $finished)"
      echo "status	off (no state file)"
      exit 0
    else
      git cat-file -e "$head_arg^{commit}" 2>/dev/null || { echo "sleep-mode: start commit $head_arg not found" >&2; exit 1; }
      if [ "$until_arg" = "none" ]; then until_json=null; else until_json="\"$until_arg\""; fi
      start "$until_json" "$head_arg"
      echo "rebuilt	yes (the machine was reclaimed; automation set to autonomous again)"
    fi
    print_status
    ;;
  tick)
    require_state
    status=$(jq -r .status "$STATE")
    head=$(git rev-parse HEAD)
    if [ "$status" = "active" ]; then
      if [ "$head" = "$(jq -r .last_head "$STATE")" ]; then
        update '.turns_without_commit += 1'
      else
        update '.turns_without_commit = 0'
      fi
    fi
    update '.turns += 1 | .last_head = $head' --arg head "$head"
    print_status
    until_ts=$(jq -r '.until // empty' "$STATE")
    if [ -n "$until_ts" ]; then
      left=$(( ($(date -u -d "$until_ts" +%s) - $(date -u +%s)) / 60 ))
      echo "minutes_left	$left"
    else
      echo "minutes_left	none (no time limit)"
    fi
    echo "tree	$([ -z "$(git status --porcelain)" ] && echo clean || echo dirty)"
    ;;
  wait)
    require_state; update '.status = "waiting"'; echo "status	waiting (for a background agent)" ;;
  resume)
    require_state; update '.status = "active" | .turns_without_commit = 0'; echo "status	active" ;;
  finish)
    require_state
    why="${1:-done}"; note="${2:-}"
    update '.status = $why | .finished = $at | .reason = $note' --arg why "$why" --arg at "$(now)" --arg note "$note"
    # restore the automation setting sleep mode replaced
    had_local=$(jq -r .had_local_yaml "$STATE"); prev=$(jq -r .prev_local_automation "$STATE")
    if [ "$had_local" = "false" ]; then
      rm -f "$LOCAL_YAML"
    elif [ -n "$prev" ]; then
      bash .claude/scripts/config-set.sh --local modes.automation "$prev" >/dev/null
    else
      bash .claude/scripts/config-set.sh --local modes.automation guided >/dev/null
      echo "note	project.local.yaml had no automation override before; set to guided" >&2
    fi
    print_status
    echo "automation	restored"
    ;;
  status)
    require_state; print_status ;;
  *) echo "usage: sleep-mode.sh on [--hours N] | ensure --until <ISO|none> --start-head <sha> | tick | wait | resume | finish <why> [note] | status" >&2; exit 2 ;;
esac
