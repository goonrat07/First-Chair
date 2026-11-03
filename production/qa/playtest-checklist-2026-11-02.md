# PC playtest checklist (2026-11-02)

Twelve stories wait on you riding the game on your PC: their code and cloud evidence are in, and only you can judge
the feel and the look, or check 60 frames a second on a real GPU. This list is the quickest way through them. Ride,
then tell me in your own words what works and what doesn't; when you tune something in the Feel Lab, press its
**Copy values** and paste them to me.

## Get set up

1. **The build.** The newest Windows player is CI run 41489906945 (commit b92d0be, the skis riding on top of the
   groomer): https://github.com/goonrat07/First-Chair/actions/runs/41489906945, artifact
   **FirstChair-StandaloneWindows64** (direct download, signed in to GitHub:
   https://github.com/goonrat07/First-Chair/actions/runs/41489906945/artifacts/13163009691). Unzip it and run
   `FirstChair.exe`.
   In the Unity Editor instead: pull, then **First Chair > Build Test Slope Scene** and **First Chair > Build Mountain
   Scenes**.
2. **In the Editor only:** delete `Assets/Data/Riding/LightingSettings.asset` before building the scenes. The scene
   builder keeps an existing one, which still has old values from before stories 007, 014 and 018.
3. **The Feel Lab:** press the backtick key or F1. Its tabs: **Riding** (the sim's numbers), **Camera**, **Effects** (the
   skier's animation and the skis' sink), **Light** (sun, snow look, track depths) and **Controls**, which has
   **Ride the test slope**, **Ride the test mountain** and **Ride the pine bowl**, plus **Go to the lift** and **Go to
   the park**.

## Story by story

| Story | Ride and look for | Knobs | It answers |
|---|---|---|---|
| 018 Deformable snow | Ride into powder: two real trenches with raised snow beside them, the skis sunk to the boots. On the groomer a shallow cut behind the skis, which ride on top of it at full width; on ice only scrape marks. No line where the near snow (about 15 m around you) meets the rest. **Does it hold 60 frames a second?** | Light tab: groove strength (0 switches it all off), the depth per snow type, raise, the floor's tint. Effects tab: the cut the skis ride over (0.02 m), the sink's share and easing | The look sign-off and the 60 fps check (`production/qa/evidence/story-018-deformable-snow/evidence.md`) |
| 007 First art pass | The whole look: skier, trees, snow trails, light | none | Its sign-off (`production/qa/evidence/story-007-first-art-pass-evidence.md`) |
| 009 GMA-style riding | Does the riding feel right: turning, speed, carving, the controls? | Riding tab | The open question in `production/sleep-mode/questions.md` |
| 008 Turn styles and speed | Quick turns, drifts, the hockey stop, race carves; the speed down the fall line | Riding tab | Its feel |
| 011 Chase camera | The camera over your right shoulder, tilting with the snow ahead, closing in with speed | Camera tab | Its feel |
| 013 Procedural animation | The lean into turns, the spring on landings, the legs on uneven snow, pole plants at edge changes | Effects tab | Its feel |
| 012 Skier detail | The skier close up and at speed: the rounder shoulders, sleeves and jacket back (you gave it the triangles) | none | Its look |
| 014 Snow shader | Glints that twinkle as you move, blue shade, the soft edge of the light, ripples in powder | Light tab | Its look |
| 016 Varied forest | Tree lines that mix tall slim firs, broad spruces, young trees and snags. Is the slim fir too sparse? Are the shares right? | none | Its look |
| 010 Test mountain | **Go to the lift**, push W into the station and ride up; the runs and the park | none | Its look and the lift ride |
| 015 Mountain tool | **Ride the pine bowl**, the second mountain made with the tool. In a terminal: `python tools/mountain/mountain.py --help`, and `tools/mountain/README.md` for the format | none | Whether the tool suits you for making mountains |
| 003 Test slope view | The test slope top to bottom in the Windows build **with a gamepad**; save a screenshot from the follow camera | none | Its last criterion |

## Also waiting on you

- **The design drafts, when you want them.** The story (`design/drafts/story-outline.md`) and the season loop
  (`design/drafts/season-loop.md`) hold what you decided on 2026-11-02 and 2026-11-03, with their *Proposed* parts
  open to change. The skill tree (`skill-tree.md`) and the mission system (`mission-system.md`) still end with
  decisions of their own.
