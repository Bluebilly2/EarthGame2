# Contract G1 — readable before beautiful

**Status: binding contract, written before code. Author: FABLE, 2026-09-01, on the owner's
direction ("right now the graphics aren't very pretty, and it makes the game hard for me to
use"). Implementation: GFX session (Opus), worktree `track-gfx`. Review: REVIEWER verifies
mechanism and law-7 compliance only — taste is judged by the owner, in batches, from frames.**

## The thesis

The owner's complaint is usability, and usability is the measurable half of "pretty". G1 spends
nothing on beauty: it makes every piece of text and UI the game already draws **legible,
hierarchical, and quiet**, so the owner can use the game while the world-presentation pass (G2,
later, with a visual-direction doc) decides how it should *look*.

## The currency: frames, or it did not happen

- **Every change ships a before/after recorder-frame pair**, named
  `g1-<change>-before.png` / `g1-<change>-after.png` in the recordings directory, listed by
  filename in the board report. A visual change without its pair does not exist (law 8's
  visual row, made mechanical). *(Amended after GFX's day-one finding: the recorder as found
  wrote only 1280-wide JPEG — this contract asserted PNG/native-res from memory, FABLE's
  concession. Change set zero extends the recorder — PNG + native resolution behind a flag,
  default behaviour untouched — and the currency applies from change set one onward.)*
- Frames are taken at 1920×1080 **and** 1280×720 — a change that only reads at one size is
  half a change.
- The owner judges in batches at checkpoints (their standing preference: the latest build is
  launched for them, old instances killed first). "Easier to use" from the owner is G1's only
  definition of done.

## The work

- **G1a — HUD legibility.** Contrast between text and whatever sits behind it (worst case: the
  bright beach at noon); sizes and spacing off the `UiScale` ramp exclusively; hierarchy — the
  most important line (the transient reply, the wake reason, the danger line) reads FIRST at a
  glance; alignment and margin discipline so panels read as designed, not accreted.
- **G1b — the message channels.** The press-time replies, wake messages, and verb messages:
  placement, hold time, and contrast such that a sentence the game says is a sentence the
  player has actually read.
- **G1c — tablet typography.** The render-target page (fixed 12-pt world): line length,
  leading, paragraph spacing, contrast on the glass — a page of the oracle should read like a
  page.
- **G1d — noise cull.** Anything drawn in normal play that reads as debug output gets designed
  or demoted. The screen carries what the founder needs and nothing else.

## Rules

- **Law 7 is absolute**: IMGUI, `UiScale.BeginFrame()` first, style caches by generation, no
  typed sizes (the source tests enforce this — they are not obstacles, they are the rails).
- **No gameplay logic edits.** A G1 change set touches drawing only. Anything else is claimed
  on the board first and probably refused.
- **Partition:** `Assets/EarthGame/Shaders/**` and draw-side code (`FlatHUD`, `DebugOverlay`
  draw paths, `TabletScreen` paint, `ItemIcons`, `PostGrade`) — with any file DEV 1 has in an
  open change set claimed on the board before touching. FABLE arbitrates contests.
- **Launch discipline:** player launches are announced on the board like test locks — the
  player is single-instance and three sessions want it.
- Commits land on `track-gfx`; REVIEWER passes them there; FABLE merges to main and pushes.

## Explicitly out of scope

World rendering, grading, sky/water cohesion (G2, after the owner marks up a visual-direction
page); any new UI framework or asset pipeline; hotbar/inventory redesign (I1's territory);
anything the founder cannot see.

## Acceptance

A batch of frame pairs the owner reviews and calls easier to use; suites and `CI/Scripts`
green (drawing changes still compile and the UiScale source tests still hold); no scenario
regressions — the six stay green, since G1 must not move anything a probe reads.
