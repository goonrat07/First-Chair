# Epic: First Chair MVP (base skiing system)

> **Tier**: minimal (built from the brief; no GDDs or ADRs at this tier)
> **Source**: `design/game-brief.md`

**Goal:** A warm, sunlit skiing game where you carve, jump and thread the trees of a small
family-run mountain, and every run you ride can be replayed exactly.

**Scope:** the brief's MVP list: deterministic riding sim with exact replay; skiing on snow
with five snow types; air, landings, crashes and reset; trees and Flow; rails, boxes and
single-trick scoring; one test slope shared by the sim and Unity Terrain, with a GMA2-style
follow camera and keyboard plus gamepad; the first art pass.

**Order:** the brief's Build order: sim skeleton and replay test, glide and carve, Unity view,
snow types and air, trees then rails and scoring, art pass.

**Out of scope:** snowboard; combos, presses, rewind, Stoke and signature tricks; Career,
story and Mountain Ops; lifts and the full mountain; online play (see the brief). The user asked for a small test
mountain with one chairlift on 2026-10-26 (story 010); the full mountain stays out.

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | [Deterministic sim core and replay](story-001-sim-core-replay.md) | Logic | Ready | N/A (minimal) |
| 002 | [Skiing on snow: carve, skid, tuck, brake, pole, snow types](story-002-skiing-on-snow.md) | Logic | Ready | N/A (minimal) |
| 003 | [Test slope, Unity view, follow camera and input](story-003-test-slope-unity-view.md) | Integration | Ready | N/A (minimal) |
| 004 | [Air: jumps, spins, flips, grabs, landings, crashes, reset](story-004-air-landings-crashes.md) | Logic | Ready | N/A (minimal) |
| 005 | [Trees and Flow](story-005-trees-and-flow.md) | Logic | Ready | N/A (minimal) |
| 006 | [Rails, boxes and single-trick scoring](story-006-rails-and-trick-scoring.md) | Logic | Ready | N/A (minimal) |
| 007 | [First art pass toward the target look](story-007-first-art-pass.md) | Visual/Feel | Ready | N/A (minimal) |
| 008 | [Turn styles and speed: carve, quick turns, powder, faster slopes](story-008-turn-styles-and-speed.md) | Logic | In Review | N/A (minimal) |
| 009 | [GMA-style riding, the Feel Lab and speed cues](story-009-gma-style-riding-and-feel-lab.md) | Logic + Visual/Feel | In Review | N/A (minimal) |
| 010 | [A small test mountain with a chairlift, runs, trees and a park](story-010-test-mountain.md) | Logic + Visual/Feel | In Review | N/A (minimal) |
| 011 | [A Steep-style chase camera](story-011-steep-chase-camera.md) | Logic + Visual/Feel | In Review | N/A (minimal) |
| 012 | [A skier detail pass for the chase camera](story-012-skier-detail-pass.md) | Visual/Feel | In Progress | N/A (minimal) |
