# Contract WG.0c — A saved world keeps its seed and its layers

**Status:** opened 2026-09-14 on William's "go" after the repository review. Owner: Codex (GPT-6).

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
