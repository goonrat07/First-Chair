---
name: sleep-mode
description: "Works on First Chair unattended while the user sleeps or is away: runs the story queue that can be done in the cloud, commits and pushes each step, wakes itself after a usage reset, and leaves a morning report. Only when the user types /sleep-mode."
argument-hint: "[hours | status | off]"
user-invocable: true
disable-model-invocation: true
allowed-tools: Read, Glob, Grep, Write, Edit, Agent, Bash(bash .claude/scripts/sleep-mode.sh *), Bash(bash .claude/scripts/story-status.sh), Bash(bash .claude/scripts/md-section.sh *), Bash(dotnet test sim/*), mcp__claude-code-remote__get_session, mcp__claude-code-remote__send_later, mcp__claude-code-remote__list_triggers, mcp__claude-code-remote__delete_trigger
---

# /sleep-mode — keep working while the user is away

The user starts this before sleeping or going to school, then can't answer anything
for hours. The job is to get as much finished, tested, pushed work done as the usage
limits allow, without waiting on them and without guessing on their behalf where a
guess would be wrong. **Typing `/sleep-mode` is the user's instruction to commit and
push on the work branch for the whole run.**

## How it keeps going

These facts set the design. Re-check them if the cloud product changes.

- **No automatic continue in the cloud.** Claude Code's "continue at usage limit"
  runs only in interactive terminal sessions. In this project's first cloud session
  the limit hit at 01:06 UTC on 2026-10-20, and nothing ran until the user wrote at
  10:54.
- **A turn that ends stays ended.** So sleep mode schedules messages to this session
  with `send_later`. The server delivers them, so they arrive even if the machine was
  reclaimed while idle:
  - **keep going**: 1 minute after each turn ends (30 minutes while a background
    agent works);
  - **after usage reset**: 1 minute after the current usage window resets.
    `get_session` reports it in `external_metadata.rate_limit_info.resetsAt` (Unix
    seconds).
- **A reclaimed machine loses everything not pushed.** That includes the gitignored
  state (`production/session-state/`, `project.local.yaml`, the decision log). So:
  commit and push after every finished step, keep the plan and report under
  `production/sleep-mode/` (committed), and every wake-up message carries what
  `sleep-mode.sh ensure` needs to rebuild the state.
- **Names.** Every scheduled message's name starts with `Sleep mode:`, so
  `list_triggers` finds them for cleanup.

Load `send_later`, `list_triggers`, `delete_trigger`, `get_session` and
`PushNotification` with ToolSearch if they aren't loaded. `send_later` asks who wanted
the message. Pass `initiation` = `human_request` on the turn the user typed
`/sleep-mode`, and `human_schedule` on every later turn, because it is the schedule
they set up.

**The wake-up message.** Use this text for every scheduled message. Fill `<until>`
and `<start-head>` from `sleep-mode.sh status`; `<until>` is `none` when there is no
time limit:

```text
Sleep mode wake-up (scheduled by sleep mode, not typed by the user). Run: bash .claude/scripts/sleep-mode.sh ensure --until <until> --start-head <start-head> — then read .claude/skills/sleep-mode/SKILL.md and follow Phase 3.
```

## Arguments

| Typed | Meaning |
|---|---|
| `/sleep-mode` | Start with no time limit. It stops when the queue is empty, stalls, or everything left needs the user. |
| `/sleep-mode 8` | Start, and wrap up after 8 hours. |
| `/sleep-mode status` | Print `bash .claude/scripts/sleep-mode.sh status`, the pending `Sleep mode:` messages (`list_triggers`), and the table in `production/sleep-mode/plan.md`. Change nothing. |
| `/sleep-mode off` | Phase 6 now, with reason `off` and no notification. |

## Phase 1: Preflight

The user is still here, so this phase may ask them things. Nothing after it may.

1. `get_session` with no `session_id`. `permission_mode` must be `auto`. If it isn't,
   stop: tell the user to pick **Auto** in the mode dropdown and run `/sleep-mode`
   again. In any other mode the first permission prompt would sit unanswered all
   night.
2. The branch is the session's work branch, never `main`, `master` or `release/*`.
3. `git status --porcelain` is empty. If it isn't, show what is uncommitted and ask
   whether to commit it first. Don't start on a dirty tree; a reclaimed machine
   would lose it.
4. Read `rate_limit_info`. If `resetsAt` is more than 24 hours away, a weekly limit
   is the one that binds. When it hits, nothing can wake the session. Say so, and
   start only if the user still wants to.

## Phase 2: Build the queue and start

1. `bash .claude/scripts/story-status.sh` lists the unfinished stories in the order
   the minimal route takes them.
2. Decide which items can run here. Read each story's header, `## Acceptance
   Criteria` and `## Dependencies` (`md-section.sh extract`).
   - **Queue:** a story In Review or In Progress whose code is written gets
     `/story-done`. A Ready story whose dependencies are all Done gets `/dev-story`,
     then `/story-done`. A dependency that is itself earlier in this queue counts:
     it will be Done first.
   - **Leave out, as "needs the PC":** Visual/Feel and UI stories, anything that
     needs the Unity Editor, a playtest or a person's sign-off, and the Unity half of
     an Integration story. When a story splits cleanly into a sim half and a Unity
     half, queue the sim half and say so in its row.
   - Nothing can run: tell the user now and don't start.
3. Write `production/sleep-mode/plan.md`, replacing any old one:

   ```markdown
   # Sleep mode plan

   Started: <ISO> · Until: <ISO or none> · Branch: <branch> · Start commit: <sha>

   | # | Item | Skill | Status | Commits |
   |---|------|-------|--------|---------|
   | 1 | story-001-sim-core-replay | story-done | queued | |

   ## Left for the PC
   - story-007-first-art-pass: Visual/Feel, needs the Unity Editor and sign-off
   ```

   Status is one of `queued`, `running`, `done`, `skipped: <reason>`.
4. Start `production/sleep-mode/report.md` (template under Phase 6) and an empty
   `production/sleep-mode/questions.md`, both replacing any old ones.
5. `bash .claude/scripts/sleep-mode.sh on` (add `--hours N` when given). It switches
   `modes.automation` to `autonomous` in `project.local.yaml` until Phase 6.
6. Commit the three files (`chore: start sleep mode`) and push.
7. Phase 4, then tell the user in a few lines: the queue, what was left for the PC,
   the end time, that `/sleep-mode off` stops it, and where the report will be.
8. Go straight into Phase 3 in this same turn.

## Phase 3: Every turn

Do this at the start of every sleep-mode turn: the first one, each wake-up, and each
background-agent notification.

1. **Wake-up message:** run the `ensure` command it carries, exactly as written,
   before anything else.
2. `bash .claude/scripts/sleep-mode.sh tick`. Act on what it prints:
   - `status off`, or anything other than `active` or `waiting`: sleep mode is over.
     Delete any pending `Sleep mode:` messages, schedule nothing, end the turn in one
     line.
   - `minutes_left` 0 or less: Phase 6, reason `time-up`.
   - `turns_without_commit` at its maximum: Phase 6, reason `stalled`. Three turns
     in a row produced nothing worth keeping.
   - `status waiting`: if the background agent you started has reported, run
     `sleep-mode.sh resume` and carry on. If not, do Phase 4, then Phase 5 (the
     30-minute keep going), and end the turn.
   - `tree dirty` at the start of a turn: the last turn was cut off mid-step, usually
     by the usage limit. Finish or undo that step before starting anything new.
3. Phase 4.
4. **CI:** if the last push has a `sim-tests` run (`mcp__github__actions_list`,
   workflow `sim-tests.yml`) that failed, fixing it comes before anything else.
5. **Work the queue.** Take the `running` item in `plan.md`, else the first
   `queued` one, and mark it `running`.
   - **Run the story skills by reading them.** For `/dev-story <path>`, read
     `.claude/skills/dev-story/SKILL.md` and follow it as if the user had typed the
     command. Do the same for `/story-done` with `.claude/skills/story-done/SKILL.md`.
     Both are locked against Claude starting them. The user agreed that this one
     skill may follow them, and only while sleep mode is on. Ignore their
     `model:` and `allowed-tools:` lines.
   - **Autonomous rules** (`.claude/docs/automation-modes.md`): no questions, take the
     recommended option, and log each decision with `log_decision`.
   - **Questions only the user can answer.** These are questions in a
     `modes.automation_always_ask` category (resolve the list; by default
     `scope_changes`, `file_deletions`, `schema_changes`), or anything where a guess
     would be wrong. Don't answer them. Append the question to `questions.md`: the
     item, the question, the options with your recommendation, and what is waiting
     on it. Mark the item `skipped: question` and move to the next item. Also skip
     every later item that depends on it.
   - **Commit after every finished step**, not only at the end of a story.
     - Use Conventional Commits, with `Story: <path>` in the body and the session's
       attribution lines.
     - Push with `git push -u origin <branch>`. Retry network failures up to 4 times
       (2, 4, 8, 16 s).
     - Before every commit that touches the sim, `dotnet test sim/` must pass. Never
       commit red. Never skip or disable a test.
   - **When an item ends:**
     - Set its row in `plan.md`.
     - Add its section to `report.md`: what changed, test results, deviations, and
       what still needs the PC. Copy in the decisions you logged; the decision log
       is gitignored and a reclaimed machine loses it.
     - Commit and push.
   - **Keep going.** Start the next item in the same turn. End turns at item or step
     boundaries, after a push.
6. **Background agents:** when one is working and nothing else can go on, run
   `sleep-mode.sh wait`, then Phase 5, and end the turn. Its notification starts the
   next turn.
7. **Queue empty**, or every remaining item is skipped: Phase 6, reason `done`. If
   nothing was finished and everything waits on the user, use reason `blocked`.

### Never, while asleep

- Ask with `AskUserQuestion`, or end a turn on a question. Nobody will answer it.
- Edit `.claude/settings*.json`, anything under `.claude/hooks/`, `.claude/skills/`,
  `.claude/agents/` or `.claude/rules/`, `CLAUDE.md`, or `project.yaml`.
- Leave the work branch. Open a pull request. Force-push, rebase, `reset --hard` or
  `clean -f`.
- Start PC-only work. Anything Unity-side written here is reported as **not verified
  in the editor**.
- Call paid or asset-generation services.
- Work around a refusal. When auto mode denies an action, don't try another route to
  the same result. Write what was refused, and why it was needed, to `questions.md`,
  then skip the item.

### A message from the user

Answer it like any other message. Sleep mode stays on unless they say `off`. Then
pick the loop back up.

## Phase 4: The after-usage-reset wake-up

1. `get_session` with no `session_id`. Read `rate_limit_info`: `rateLimitType`,
   `resetsAt`, `status`.
2. Wake time = `resetsAt + 60`, as ISO:
   `date -u -d @<seconds> +%Y-%m-%dT%H:%M:%SZ`.
3. Decide:
   - **Already scheduled** at that time (`list_triggers`, name `Sleep mode: after
     usage reset`, same `run_once_at` to the minute): nothing to do.
   - **After `until`:** sleep mode ends first. Schedule nothing.
   - **More than 24 hours away** (a weekly limit binds): schedule nothing, and say in
     `report.md` that a hit on this limit ends the run with no notification.
   - **No `rate_limit_info`:** write `NOT ASSESSED: no rate_limit_info` in
     `report.md`. Use now + 5 hours + 1 minute instead, since a 5-hour window always
     resets within that.
   - **Otherwise:** delete any older `Sleep mode: after usage reset` messages, then
     `send_later` with `at` = the wake time, `name` = `Sleep mode: after usage
     reset` and `message` = the wake-up message.

## Phase 5: Before ending a turn

Every sleep-mode turn ends here, unless it ends in Phase 6.

1. `list_triggers` and delete every pending `Sleep mode: keep going` message.
2. `send_later` with `delay_minutes` 1, or 30 while waiting on a background agent.
   Use `name` = `Sleep mode: keep going` and `message` = the wake-up message.
3. End the turn with one status line: the item and step, and the last commit.

## Phase 6: Wrap up

Reasons: `done`, `time-up`, `stalled`, `blocked`, `off`.

1. **Make the branch safe.** Commit finished work, tests green. Unfinished work that
   doesn't pass its tests is never committed as code. Save it as a patch instead,
   new files included:
   - `git add -A`
   - `git diff --cached --binary > production/sleep-mode/unfinished-<item>.patch`
   - `git stash push`. This takes the staged work off the tree and leaves the
     untracked patch.
   - Commit the patch. The work survives without a red commit;
     `git apply <patch>` brings it back.
2. **Finish the plan and report.** Add `Finished: <ISO> · Reason: <reason>` under the
   Started line in `plan.md`. `sleep-mode.sh ensure` reads it, so a stray wake-up on
   a reclaimed machine can't restart a finished run. Then fill in the report's
   Summary.
3. **Clean up.** Delete every pending `Sleep mode:` message (`list_triggers`, then
   `delete_trigger`).
4. Run `bash .claude/scripts/sleep-mode.sh finish <reason> "<one-line note>"`. This
   restores the automation mode.
5. Commit and push (`chore: sleep mode report`).
6. **Notify**, unless the reason is `off`. Send one `PushNotification`: one line,
   under 200 characters, leading with what the user acts on. For example: `Sleep
   mode done: stories 002 and 004 finished, 1 question waiting. See
   production/sleep-mode/report.md`.
7. End the turn without scheduling anything.

**`report.md` template:**

```markdown
# Sleep mode report

Started <ISO> · Ended <ISO> · Reason <reason> · Commits <n> (`<start>..<end>`)

## Summary
- Finished: <items>
- Skipped: <items and why>
- Waiting on you: <n> questions in production/sleep-mode/questions.md
- Needs the PC: <items>
- Next: <the first thing to do>

## <item>
- Changed: <files, one line each>
- Tests: <command — result>
- Decisions made for you: <one line each, from the decision log>
- Not verified: <Unity-side, feel>
```
