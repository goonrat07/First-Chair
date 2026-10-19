# First Chair -- Claude Code Game Studios

Indie game development managed through 49 coordinated Claude Code subagents.
Each agent owns a specific domain, enforcing separation of concerns and quality.

## First Chair

A skiing game in Unity, with a look inspired by Grand Mountain Adventure 2.
**The base skiing system comes first;** everything else in the design waits until
riding works and feels right. Skis first, snowboard later.

- **Design sources, in order of authority:** what the user says in the session,
  then `design/game-brief.md`, then `design/reference/bluebird-spec.md`. The
  Bluebird spec was written for an earlier browser prototype. Use it for design
  intent only. Its tech (TypeScript, `src/` paths, Vitest, Playwright,
  `window.__game`) does not apply, and where it contradicts the user, the user
  wins.
- **Architecture:** riding runs in a deterministic C# sim
  (`Packages/com.firstchair.sim/`, no UnityEngine references, fixed 120 Hz step,
  fixed-point math). Unity only reads its state and draws it. Rules:
  `.claude/rules/sim-code.md`.
- **Assets:** built in Blender by script, headless. Rules:
  `.claude/rules/blender-assets.md`.
- **Research and rationale:** `docs/research/engine-and-pipeline.md`.
- **Where work happens:** cloud sessions start from a fresh container with no
  Unity Editor. Install the .NET SDK (`dotnet-install.sh`) and Blender 5.2 LTS
  (the Linux build from download.blender.org) when a task needs them, run
  `dotnet test sim/` and Blender scripts there, and report any Unity-side change
  as **not verified in the editor**. On the user's
  PC, Unity's official Claude Code plugin and the Blender Lab MCP give live editor
  access.

## Technology Stack

- **Engine**: Unity 6.3 LTS (URP)
- **Language**: C#
- **Version Control**: Git with trunk-based development, binary assets in Git LFS
- **Build System**: Unity Build Pipeline; the riding sim also builds and tests with the .NET SDK
- **Asset Pipeline**: Blender 5.2 LTS (scripted, headless) → FBX → Unity Asset Import Pipeline

> **Note**: Engine-specialist agents exist for Godot, Unity, and Unreal with
> dedicated sub-specialists. Use the set matching your engine.

## Project Structure

@.claude/docs/directory-structure.md

## Engine Version Reference

<!-- ENGINE-REFERENCE-IMPORT: the line below is engine-specific. /setup-engine
     rewrites it to @docs/engine-reference/<engine>/VERSION.md for the chosen
     engine, so a Unity or Unreal project stops loading the Godot reference every
     session. It defaults to Godot (the template's example engine); skills that
     need the pinned version read docs/engine-reference/<engine>/VERSION.md on
     demand regardless of this import. -->
@docs/engine-reference/unity/VERSION.md


## Technical Preferences

`project.yaml` at the repo root is the primary config store — engine, specialists,
naming, platform, performance, modes. Skills resolve it via `resolve_config`
(see `.claude/docs/config-resolution.md`).

`.claude/docs/technical-preferences.md` is the **legacy fallback**, read on demand
when a key is absent from `project.yaml`. It is not imported here: before
`/setup-engine` runs it is almost entirely `[TO BE CONFIGURED]` placeholders, and
after it runs `project.yaml` holds the real values.

## Coordination Rules

@.claude/docs/coordination-rules.md

## Collaboration Protocol

**User-driven collaboration, not autonomous execution.**
Every task follows: **Question -> Options -> Decision -> Draft -> Approval**

- Agents MUST ask "May I write this to [filepath]?" before using Write/Edit tools
  (exempt: `active.md` appends, agent memory, a subagent's new file at the path
  its orchestrating skill named; see `.claude/rules/agent-memory.md`)
- Agents MUST show drafts or summaries before requesting approval
- Multi-file changes require explicit approval for the full changeset
- No commits without user instruction
- A skipped or dismissed question is not an answer: act on nothing, re-ask in
  plain text and wait
- Started outside this file's folder? Warn first: hooks, deny rules and the
  ask-first mode load only when Claude Code starts here

See `docs/COLLABORATIVE-DESIGN-PRINCIPLE.md` for full protocol and examples.

> **First session?** If the project has no engine configured and no game concept,
> run `/start` to begin the guided onboarding flow.

## Coding Standards

@.claude/docs/coding-standards.md

## Context Management

Read `.claude/docs/context-management.md` on demand — it is a reference, not
session context. Two of its conventions are load-bearing and cited by name
elsewhere in the repo, so they are restated here rather than lost:

- **`production/session-state/active.md` is the session checkpoint.** The file is
  the memory, not the conversation. Read it first after any compaction, crash, or
  `/clear`.
- **Helpers in `.claude/scripts/` emit observations, never verdicts.** A script
  that scores or judges will eventually contradict a mode or override it cannot
  see. (Cited by `artifact-check.sh` and `adr-dep-graph.sh`.)
