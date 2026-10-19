# Technical Preferences

<!-- project.yaml at the repo root is the machine-readable source of truth for
     engine, specialists, naming, performance, platform, and testing.framework.
     This file is the human-readable LEGACY FALLBACK: agents and skills resolve
     each key from project.yaml first and fall back here only when the
     project.yaml key is absent. /setup-engine dual-writes both.
     Forbidden patterns and allowed libraries are NOT migrated — they live only
     in this file. Populated by /setup-engine; updated as decisions are made. -->

## Engine & Language

- **Engine**: Unity 6.3 LTS
- **Language**: C#
- **Rendering**: URP
- **Physics**: PhysX for engine physics only. Riding runs in First Chair's own deterministic sim (`Packages/com.firstchair.sim/`), never in PhysX.

## Input & Platform

<!-- Written by /setup-engine. Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: PC (Windows)
- **Input Methods**: Keyboard, Gamepad
- **Primary Input**: Keyboard/Mouse
- **Gamepad Support**: Full
- **Touch Support**: None
- **Platform Notes**: Every riding action takes at most two keys on keyboard and also maps to a gamepad. Analog steering feeds the sim as quantized input so replays stay exact.

## Naming Conventions

- **Classes**: PascalCase (e.g., `RiderState`)
- **Variables**: public fields and properties PascalCase (`MoveSpeed`), private fields _camelCase (`_moveSpeed`)
- **Signals/Events**: C# events named `On<Event>` (`OnLanded`)
- **Files**: PascalCase matching the class (`RiderState.cs`)
- **Scenes/Prefabs**: PascalCase
- **Constants**: PascalCase or UPPER_SNAKE_CASE (unverified against the engine reference)

## Performance Budgets

- **Target Framerate**: [TO BE CONFIGURED]
- **Frame Budget**: [TO BE CONFIGURED]
- **Draw Calls**: [TO BE CONFIGURED]
- **Memory Ceiling**: [TO BE CONFIGURED]

## Testing

- **Framework**: Unity Test Framework (NUnit) for Unity-side code; plain NUnit under `dotnet test` for the riding sim in `sim/`
- **Minimum Coverage**: [TO BE CONFIGURED]
- **Required Tests**: Replay determinism for every sim change, riding and scoring formulas, gameplay systems

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->
- `UnityEngine` types, `UnityEngine.Random`, `Time.deltaTime` or PhysX calls inside the riding sim (`Packages/com.firstchair.sim/`) — see `.claude/rules/sim-code.md`
- `float`/`double` math in anything that moves the rider or a pursuer; the sim uses fixed-point
- Riding numbers hard-coded in logic instead of the sim's tuning data

## Allowed Libraries / Addons

<!-- Add approved third-party dependencies here -->
- [None configured yet — add as dependencies are approved]

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- [No ADRs yet — use /architecture-decision to create one]

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: unity-specialist
- **Language/Code Specialist**: unity-specialist (C# review — primary covers it)
- **Shader Specialist**: unity-shader-specialist (Shader Graph, HLSL, URP materials)
- **UI Specialist**: unity-ui-specialist (UI Toolkit UXML/USS, UGUI Canvas, runtime UI)
- **Additional Specialists**: none. unity-dots-specialist is not used (no ECS); unity-addressables-specialist joins when Addressables do.
- **Routing Notes**: The riding sim (`Packages/com.firstchair.sim/`, `sim/`) is plain C# with no UnityEngine references. Review it as engine-independent code against `.claude/rules/sim-code.md`.

### File Extension Routing

<!-- Skills use this table to select the right specialist per file type. -->
<!-- If a row says [TO BE CONFIGURED], fall back to Primary for that file type. -->

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.cs files) | unity-specialist |
| Shader / material files (.shader, .shadergraph, .mat) | unity-shader-specialist |
| UI / screen files (.uxml, .uss, Canvas prefabs) | unity-ui-specialist |
| Scene / prefab / level files (.unity, .prefab) | unity-specialist |
| Native extension / plugin files (.dll, native plugins) | unity-specialist |
| General architecture review | Primary |
