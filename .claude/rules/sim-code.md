---
paths:
  - "Packages/com.firstchair.sim/**"
  - "sim/**"
---

# Riding Sim Rules (First Chair)

The riding sim is the only code that moves the rider or a pursuer. Unity reads
its state and draws it; nothing in Unity writes back. Every replay, ghost, clip
and leaderboard check depends on one property: **the same RunSetup and the same
input stream produce the same ride, bit for bit.** Every rule below protects it.

## Where it lives

- Source: `Packages/com.firstchair.sim/`. Its asmdef sets
  `"noEngineReferences": true`, so Unity compiles it with no access to
  `UnityEngine` or `UnityEditor`.
- The same source files also build outside Unity through `sim/*.csproj`, so the
  tests run with the .NET SDK alone: `dotnet test sim/`. This works in cloud
  sessions with no Unity installed and in CI.

## Determinism

- Fixed step of 1/120 s. Never read wall-clock time, `Time.deltaTime` or a frame
  rate. Unity runs as many steps as a frame needs and interpolates between the
  last two states.
- Fixed-point math (Q32.32) for everything that moves the rider or a pursuer.
  No `float` or `double`, and no `System.Math` or `Mathf`: use the fixed-point
  library's sqrt and trig.
- One seeded random generator, seeded from `RunSetup.Seed`. Never use
  `System.Random`, `UnityEngine.Random`, `Guid`, `DateTime` or hash codes as a
  source of randomness or ordering.
- No iteration over `Dictionary`/`HashSet` where order changes a result. No
  threads or tasks inside a step.
- Input enters as one packed integer per step. Quantize analog values (gamepad
  steering) into that integer before they reach the sim, never after.
- Anything that changes a ride lives in `RunSetup`: sim version, sport, assist
  level, ability ranks, gear stats, seed. It is copied once at run start and
  stored in the recording header.
- Bump the sim version whenever riding behaviour changes. Old recordings must
  then report a version mismatch, never replay into a different ride.

## Boundaries

- No rendering, audio, camera, animation, UI or save code in the sim. Snow
  trails and VFX are render-only and never feed back.
- The sim reports what happened as events (takeoff, land, crash, nearMiss and so
  on). The game layer listens to events and does not poll or write rider state.
- Every riding number lives in the sim's tuning data, never inline in logic.

## Tests

- Every sim change runs `dotnet test sim/` before it is called done.
- The determinism test is mandatory and never skipped: record a ride, replay it
  from its header, and compare position, velocity and the event list at every
  step.
- New riding behaviour gets a scripted-input test: a fixed input sequence from
  a fixed start, asserting the outcome (speed after a carve, landing grade,
  crash cause).
- Watch a new test fail before trusting it (`test-standards.md`).
