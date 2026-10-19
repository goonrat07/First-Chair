# First Chair

A skiing game in Unity with a warm, realistic-leaning look inspired by Grand
Mountain Adventure 2. Carve, jump and thread the trees of a small family-run
mountain, where every run can be replayed exactly.

**Status:** Concept stage. The design brief is written. Next up is the base
skiing system, starting with the riding sim and turning on a test slope.

## Where things are

| Path | What it holds |
|---|---|
| `design/game-brief.md` | The current design: pitch, MVP and build order |
| `design/reference/bluebird-spec.md` | The long-term design from the earlier Bluebird prototype (design intent only) |
| `docs/research/engine-and-pipeline.md` | Why Unity, the Blender pipeline, terrain, snow and the deterministic sim |
| `CLAUDE.md`, `.claude/`, `project.yaml` | The Claude Code Game Studios setup: agents, skills, hooks, rules and config |
| `Packages/com.firstchair.sim/`, `sim/` | The riding sim and its .NET tests (not created yet) |
| `tools/blender/`, `art/source/` | Blender scripts and sources (not created yet) |

## Set up your PC

1. **Install:**
   - Git for Windows, which includes Git Bash and Git LFS.
   - Unity Hub, then Unity **6.3 LTS** (the newest `6000.3.x`) with Windows
     Build Support.
   - Blender **5.2 LTS**.
   - The .NET 8 SDK, for the sim tests.
   - Python 3, which the studio hooks need.
   - jq (optional; the hooks use it if it's there).
   - Claude Code.
2. **Clone, then enable LFS once:** `git lfs install`
3. **Create the Unity project.** Unity Hub won't create a project inside a folder
   that already has files, so:
   1. In Unity Hub, choose **New project**, then **Universal 3D**, with Unity 6.3
      LTS. Name it `FirstChair` and save it **anywhere outside the repo**.
   2. Close Unity. Copy that project's `Assets/`, `Packages/` and
      `ProjectSettings/` folders into the repo root.
   3. In Unity Hub, choose **Add**, then **Add project from disk**, and pick the
      repo folder.
   4. In **Edit > Project Settings > Editor**, check that Asset Serialization
      is **Force Text** and Version Control is **Visible Meta Files**.
   5. Commit the three folders. `Library/`, `Temp/` and `Logs/` are already
      ignored.
4. **Start Claude Code in the repo root.** The hooks and rules only load from
   there. Then:
   - Run `/setup-engine unity 6.3`. It finds your installed editor and fixes the
     paths in `project.yaml`, which were written in a cloud session as a best
     guess.
   - Install Unity's official plugin:
     `/plugin marketplace add Unity-Technologies/unity-agent-plugin`, then
     `/plugin install unity@unity-agent-plugin`.
   - Optional: the [Blender Lab MCP](https://www.blender.org/lab/mcp-server/)
     for live Blender sessions.

## How work runs

The project uses [Claude Code Game Studios](https://github.com/Donchitos/Claude-Code-Game-Studios)
v1.2.0 at **minimal** rigor with **guided** automation:

- Agents ask before big changes (scope, deletions, schema changes) and decide
  small things themselves.
- `/help` shows where you are and what's next. `/settings` changes rigor or
  automation.
- The minimal path is four steps: `/setup-engine` (done), the brief (done),
  `/create-stories`, then `/dev-story`.
- Move to `standard` rigor when Career and story work begins.

First Chair's own rules sit alongside the framework's: `.claude/rules/sim-code.md`
covers the deterministic riding sim and `.claude/rules/blender-assets.md` covers
the Blender pipeline.

## Credits

Claude Code Game Studios is MIT-licensed. See `THIRD_PARTY_NOTICES.md` for the
notice and the pinned version.
