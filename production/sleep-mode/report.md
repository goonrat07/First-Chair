# Sleep mode report

Started 2026-10-27T02:58:57Z · Ended (running) · Reason (running) · Commits 0 (`0d2953f..`)

## Summary
- Finished: (running)
- Skipped: (running)
- Waiting on you: 2 questions in production/sleep-mode/questions.md
- Needs the PC: stories 008 to 011 (In Review), story 003's PC playtest, and the look sign-off of everything below
- Next: (running)

## Usage
- At the start the seven-day limit was at its warning level (`allowed_warning`), resetting 2026-11-01 16:00 UTC. The
  user said not to stop for it and to carry on after resets, so besides the 5-hour resets a wake-up is scheduled for
  just after the weekly reset (the skill's default is to schedule nothing for a weekly limit).

## story-012-skier-detail-pass (In Review)
- Changed:
  - `art/source/CH_Skier/` and `Assets/Art/Characters/CH_Skier/`: the rounder, more detailed skier (0d2953f; the seat
    fix fbca6b6 came just before this run), with an in-repo seat check (`build/seat_only_copy.py`, `build/seat_check.py`)
  - `art/source/PR_Ski/`, `art/source/PR_SkiPole/` and their exports: toe and heel pieces with a lever, brake arms and
    accents; the pole's finger ridge, knob and ringed basket (729473d)
  - `Assets/Scripts/Riding/SkierView.cs`: the player's skier always draws LOD0 (310c30e)
  - `production/qa/evidence/story-012-skier-detail-pass/`: before-and-after rides, evidence and stamp
- Tests: Blender gates (silhouettes front, side and back at every LOD; `validate.py` 0 FAIL; FBX round trips; the seat
  check, 0 seat pixels in all 16 clips and 5,386 on the old model). CI run 40443808319 (bf20bd7): EditMode 224/224,
  Linux and Windows players build. Cloud rides before (a5091fb) and after: at rest, carving, a fast tuck and three
  grabs in the air; the seat shows in every before frame and in no after frame.
- Decisions made for you:
  - Body LOD0 kept at 4,269, under the story's 5k floor (more would smooth the flat facets that are the style); the
    question is in questions.md.
  - LOD2 at 1,262, above about 1k: decimation keeps the stripes' boundaries straight.
  - The player's skier always draws LOD0 (Unity's automatic LOD heights would switch to LOD1 under 25 percent of the
    screen); other skiers, when there are any, keep Unity's LODs.
  - Bindings are generic alpine toe and heel pieces with brakes (no specific product).
  - The in-air grabs were shot from jumps off flat snow: the park ride's line depends on the cloud's frame rate and
    neither build's run left the kicker cleanly.
- Found in passing: in both builds the ski tracks were drawn over the back half of each ski (the ribbon sits 15 mm
  off the snow, the ski's top 17 mm). Fixed as its own change (below).
- Not verified: the Unity editor; the look sign-off is yours, on the PC.

## Fix: ski tracks drawn over the ski tails (found in story 012's rides)
- Changed: `Assets/Scripts/Riding/Core/SkiTrailingEnd.cs` (new), `SkiTrack.cs`, `ViewHelpers.cs`, `SkierView.cs`,
  `RiderView.cs`; `Assets/Tests/EditMode/SkiTrailingEndTests.cs` (new, 7 tests).
- What: tracks start at the ski's trailing end (the tail riding forward, the tip riding switch) instead of under the
  boot, so the translucent ribbon never lies under the ski. The end follows the rider's velocity along the ski's line
  and keeps its end in a skid across the ski or below 0.5 m/s; a change of end breaks the track.
- Tests: local NUnit run (.NET 10) 219 passed; compiles against Unity 6.3's player DLLs. Seen in story 013's build.
- Decisions made for you: a short gap (up to the 0.3 m point spacing) can show between the tail and the track at low
  speed, in place of the ribbon over the ski.
- Not verified: the Unity editor.
