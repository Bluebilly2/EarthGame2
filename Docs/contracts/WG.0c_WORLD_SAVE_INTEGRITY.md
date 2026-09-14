# Contract WG.0c — A saved world keeps its seed and its layers

**Status:** completed 2026-09-14, opened on William's "go" after the repository review. Owner: Codex (GPT-6).

## What was found

The host accepts an unsigned 64-bit seed, but the JSON reader converts every number through a double: the seed
9007199254740993 comes back as 9007199254740992. The same boundary affects saved ticks and the next entity id.
Continuing checks only the heights against the world's layer manifest. Other missing layers can silently take
the legacy fallback, and a replacement with its own valid checksum can belong to a different world.
Claude's M1.3d owns player-name collisions and exclusive world access; this contract leaves those with him.

## Promises

1. All unsigned 64-bit seeds survive write, read and restore exactly, including zero, both sides of 2^53 and the
   maximum. Saved integer ticks and entity allocation counters use exact integer access too. Invalid integer
   values are refused rather than rounded. Existing numeric JSON fields and format versions stay unchanged.
2. Before restoration, every layer named in the saved manifest exists and its raw bytes agree with that world's
   fingerprint, as well as its own sidecar. A missing pair of water files or missing capacity layer is an error
   when promised. Optional layers genuinely absent from an old manifest retain their legacy behaviour.
3. Loaded layers belong to the terrain's region and geographic frame. Different sampling pitches are allowed;
   region, origin and extent must agree. A primary bake must match the requested region before generation writes.
4. Tests fail on the old implementation. An independent Python checker writes its own synthetic files, drives the
   real host, and compares exact integer values with Python's JSON reader and raw hashes with hashlib. It prints
   actual and required values. Refused input leaves saved world content unchanged.
5. The stale approval footer in FOUNDERS_PATH is corrected to its already recorded binding status. Architecture
   and debts describe the resulting boundaries and the compatibility limit: an already rounded and resaved seed
   cannot be reconstructed from that save alone.

## Non-goals

New data, changes to valid generated terrain, selectable regions, drawing, animals, save transaction/locking work,
or recovering a seed lost by an older build. No new on-disk fields or protocol changes.

## Proof and exit

Baseline on e4be2dc: `dotnet test Engine/EarthGame.slnx -c Debug --nologo`, output redirected to
`Artefacts/codex-world-integrity-20260914/baseline-main.log`: exit 0; 468 passed, 0 failed, 0 skipped.
Close with the failing regression run, full suite, Unity-layer compile, independent checker and gate, each with
exit and actual/required counts. No valid visual rule changes, so no new frames are required.

## Exit record (2026-09-14)

The implementation was rebased onto Claude's `48ac225` (M1.3d). His name-collision and world-lock work remains
intact. `Json` now retains integer tokens, and `WorldSave` uses exact accessors for the seed, tick, next entity id
and legacy player tick. A fractional or exponent spelling is refused for these integer fields, including a
fraction small enough to disappear when converted to double; the game's existing writer uses integer tokens.
`SavedWorldLayers` checks runtime layers as they load and verifies the other manifest entries before restoration.
`WorldCreation` checks the primary bake's requested place. No generated layer values, file fields or versions changed.

Evidence is under `C:/Users/willi/projects/EarthGame2-world-generation-design/Artefacts/codex-world-integrity-20260914/`.

| Check | Actual | Required |
|---|---|---|
| Baseline `dotnet test` on e4be2dc, `baseline-main.log` | 468 passed, 0 failed, 0 skipped; exit 0 | 0 failed/skipped; exit 0 |
| Initial regression run before implementation, `regression-red.log` | 16 failed, 6 passed, 22 total; exit 1 | Failing tests demonstrating accepted bad input / rounded state; exit nonzero |
| Final full suite through WG.0c, `gate-final.log` | 516 passed, 0 failed, 0 skipped; exit 0 | 0 failed/skipped; exit 0 |
| Dedicated-host build through WG.0c | 0 warnings, 0 errors; exit 0 | 0 warnings/errors; exit 0 |
| Unity-layer compile through WG.0c | 0 warnings, 0 errors; exit 0 | 0 warnings/errors; exit 0 |
| Independent host checker through WG.0c | 82 passing rows, 0 failures; exit 0 | 82 passing rows, 0 failures; exit 0 |
| Same checker against the pre-fix host built on main, `verifier-old-host.log` | 58 failed assertions plus the failed aggregate row (59 FAIL rows), 23 PASS rows; exit 1 | Detect the old behaviour; failed assertions greater than 0; exit nonzero |
| Previous WG.0b gate, `previous-gate.log` | 16 input cases (2 valid, 14 invalid), 0 unexpected failures; exit 0 | 16 cases, 0 unexpected failures; exit 0 |
| Existing Bherwerre save continued without a bake, `existing-world-check.log` | 48 of 48 layer files unchanged (24 raw grids plus sidecars); 0 differences; exit 0 | All 48 unchanged; 0 differences; exit 0 |
| Existing world identity after continue | seed 1347, region bherwerre, wake east/up/north -1392/0/2804 | seed 1347, region bherwerre, wake east/up/north -1392/0/2804 |

The independent checker uses Python's own integer JSON parsing and raw SHA-256 snapshots, rather than the C#
reader or its reported hashes. Its host cases include every one of the synthetic world's 24 promised layers
missing in turn, replacements with internally valid sidecars, wrong-place headers, and a wrong new-world bake.
Valid legacy worlds without a manifest load; the engine suite also accepts a coarser correctly aligned layer.
The real-world check copies `codex-wg0b-before-20260913-161245/world` into a fresh evidence folder and runs only
that copy. It neither changes the original save nor downloads data.

**What changed on the way.** M1.3d landed during this work, so the branch was rebased and the legacy-world test
releases the new world lock. WG.0b's old directory-absence assertion was narrowed to zero world files: opening
and releasing M1.3d's lock can leave an empty directory. An initial new checker run timed out because it supplied
fractional seconds to the host's integer seconds option; the harness now supplies 1 second. A new NUnit message
constraint was corrected after it inspected the exception rather than its Message. Both were check defects,
and the final gate was run after their correction. No production rule was weakened to satisfy them.

**Limitations.** Previously rounded and resaved seeds need their original creation record to repair; this is in
DEBTS. Verifying previously unchecked layers adds load-time reading, on the existing preparation worker; it does
not retain all those grids or run during gameplay. This is data integrity work, not proof that Bherwerre's
geography is accurate. No visible or feel rule changed, so no frames or owner play session are claimed.
