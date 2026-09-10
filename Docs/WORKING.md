# Working here — how the work is done, and the traps

The house rules are `CLAUDE.md` and the standards `Docs/STANDARDS.md`; this file does not repeat them. It holds
what those do not: the shape of a slice, working with the owner, the checklist before a commit, the methods that
saved time, and this machine's traps. It replaced the handoff written for the change of model on 2026-09-10,
keeping what lasts and dropping what was a snapshot of the day.

## The loop

Every slice, no exceptions: **contract → tests → code → verification → docs in the same commit → dated commits
pushed through the pre-push hook.**

- **The contract** is one page under `Docs/contracts/`: numbered promises with their numbers (metres, seconds,
  counts, tolerances), non-goals, how it is proved (commands, exit codes, counts as run) and the exit. Write it
  first, commit it, then build. When a promise cannot be kept as written, keep building the rest and record what
  changed under "What changed on the way", with the reason. When the contract's own premise turns out wrong,
  amend it in writing before proving anything against it (M1.4e did).
- **Tests before code** for engine behaviour: `dotnet test Engine/EarthGame.slnx > test.log 2>&1; echo $?`, then
  the tail. A test names a guarantee; a test that cannot fail is not a test; an enforcement test is proved by
  sabotage before it is trusted.
- **The gate is the verdict**: `python Tools/gate/run_gate.py <slice>` runs `Tools/gate/gates.json`'s steps; a
  verifier it names must exist and is run for its exit code. Green means the steps exited 0, nothing more.
- **Verifiers** are independent of what they check (STANDARDS 19) and print both numbers; `drainage_check.py`,
  `census_check.py`, `cover_check.py` and `build_check.py` are the pattern. When a check disagrees with the tool,
  find out which is wrong before either moves; when it was the check, its docstring says why (cover_check's posts
  the stored layers cannot decide, tile_check's quantisation slack).
- **Docs move in the same commit** as the change that contradicts them (STANDARDS 13). Push after each slice; the
  hook builds, tests and compiles the Unity layer in about two minutes.

## Working with the owner

William owns the design (`Docs/GAME_DESIGN.md`), and his decisions are in `Docs/CANON.md`. He is learning to
program, and since 2026-09-08 the agent writes everything, checks included (ruling 17).

- **Sort what he says before acting on it.** A question gets an answer. Talk about the game gets engagement and
  a plain reflection of what he meant. Only an instruction is a job. One message often holds all three: do the
  job, answer the question, and do not let the talk set new work going (his words, 2026-09-10: "you need to
  know when i am asking about something, talking about the game, and when i want something actually done").
- **Only his decisions are rulings.** A question or a remark is not recorded in CANON, however it could be
  phrased; when he decides, record his words.
- **Plain words.** Short answers, the outcome first, then anything he decides, then what is next. No jargon
  without a plain reading beside it.
- **What is his:** design, scope and taste; how the build feels, by playing it (ruling 12); how it looks, in
  frames, once there is enough on screen to judge (he declined to judge the ground's colour on a bare plane).
  **What is not:** the code and the checks (ruling 17), and whether the world matches the real peninsula
  (ruling 21) — he does not know the place well, so that is researched from published sources.
- He often has his built game open: never close it, never open a window on his screen (STANDARDS 14), and build
  elsewhere with `-buildOut Build/Player-<slice>`.
- When he says "continue", carry on with the work in flight by the plan; state anything pending on him rather
  than assuming it. Finish the job he asked for before proposing the next one, and propose it rather than start
  it.

## Before every commit

1. `git status` read whole: nothing stray, nothing missing (Unity metas included).
2. The suite run in this tree and the count taken from the run; the Unity-layer compile green.
3. A changed format's version bumped and ARCHITECTURE §10 amended; a changed rule's decision logged with its date.
4. A new debt in `DEBTS.md` with an owner; no TODO in code.
5. Anything vendored or fetched in `THIRD_PARTY_NOTICES.md`.
6. The message says what the commit contains, no more; the trailer carries the model's own name.
7. Closing a slice: the contract's exit record with the numbers as run, frames if visual, the memory updated,
   the push green.

## Methods that saved time

- **Measure before reasoning, print both numbers, let the gate say it.** Price a design before building it (the
  cover byte against the fields it comes from; the water as depth over the ground); time work before budgeting it
  (M1.4e found 65 ms a tile that nobody had measured).
- **When "the same state" compares unequal**, print both strings and diff them before touching the state: Mono
  and .NET print the twelfth significant figure of some doubles differently, which is why digests are integers.
- **When a check goes red**, reproduce under the real conditions, log the reason string and read it. A legal
  sprint corrected thirty-one times was jitter bunching two reports into one tick, and the reason said so.
- **When a layer reads wrong**, look at the raw numbers inside the named place (percentiles, relief) before
  changing a rule. A rule tuned until the census reads right is a reworded census.
- **Geography from sources, never from memory:** convert every published point through the sidecar's frame rule
  and print where it lands before a check assumes it. Wikidata rounds to the minute; OpenStreetMap and
  Destination NSW are metre-precise.
- **Bisect what is drawn** with `-eg-hide <object names>` before blaming new code: the stepped rectangles on the
  sea were the streamed tiles' own terrain at the waterline, not the sea plane or the coarse ring.
- **"Nothing moved" is proved on the data, not on frames:** every host run advances the world's clock, so two
  recordings differ in the sun even when the ground is identical.
- **A fact in two places will bite.** It has four times in one area alone: a layer checked by its number rather
  than against the set, which layers carry codes, a post count held in three files, a tile's edge read through a
  field that did not hold its neighbour. When the same thing is decided in two places, give it one owner first.

## Reviewing another model's work

Codex (GPT-6) has committed to this repository. Do not read its exit record and agree: re-run the suite and every
gate, read its verifier's evidence and its frames, and read the code for what a gate cannot see — one owner per
fact, dated comments, no TODOs, versioned formats. Its loading slice held up and fixed a real bug of ours; what it
got wrong was duplication (a copy of `CLAUDE.md` as `AGENTS.md`; the dedicated host left on an old path).

## This machine's traps

- Bash heredocs over about 8 KB fail with "unexpected EOF", and a heredoc containing `\n` or `\\` in a Python
  string arrives mangled. Write patch scripts with the Write tool into the session's scratchpad and run them.
- `$TEMP` in Git Bash is the system temp folder, not the session's scratchpad; use the scratchpad's full path.
- A scratch script named like a standard module (`bisect.py`) shadows it for every script in that folder.
- Git prints CRLF warnings on every add; they are noise.
- Python for the tools is `Tools/.venv/Scripts/python.exe` (numpy, Pillow, requests); the gate runner uses it for
  steps that start with `python`. Console output needs `PYTHONIOENCODING=utf-8` for degree signs.
- `dotnet build Engine/EarthGame.Unity.Compile.csproj` compiles the Unity layer against stubs in seconds and
  catches most Unity-side breakage.
- Unity batch runs need the owner's editor closed (`tasklist | findstr Unity`). Edit-mode tests:
  `"C:/Program Files/Unity/Hub/Editor/6000.3.22f1/Editor/Unity.exe" -batchmode -nographics -projectPath Unity
  -runTests -testPlatform EditMode -testResults <xml> -logFile <log>`. A player build:
  `-executeMethod EarthGame.Editor.CIBuild.BuildWindows -buildOut Build/Player-<slice> -quit`. About three minutes.
- Frames: the built exe with `-batchmode -eg-record <dir> -eg-saves <fresh> -eg-tiles <fresh>` and no
  `-nographics`, or nothing renders; it writes `frames/*_1440p.png`, `*_1080p.png` and `run.jsonl`. The host's
  `stand <name> <east> <north>` then `save` puts the founder where the thing being judged is. `-eg-items N` drops
  N things at the wake; without it nothing is in the world.
- A world: `python Tools/world/create.py <folder>` (about 18 s in the host). A shaped join: `python
  Tools/world/stream.py --build`. The corpus: `python Tools/corpus/run.py` (`--quick` for a smoke test; about 75
  minutes in full, with the host built Release and a player built with `--player`); it creates its own world
  first and gives every scenario a copy; when the wake moves, `python Tools/corpus/lay_loop.py --write` lays its
  loop again, and the player is rebuilt.
- `Data/` is fetched, never committed: `python Tools/data/bake_region.py --zoom 14 --cell-m 4` for the heights,
  `python Tools/data/bake_water.py` for the OpenStreetMap outlines, `python Tools/data/fetch_ala.py` for the plants' records. `Artefacts/`, `Saves/`, `Build/` and logs are
  ignored by git.
- Unity `.meta` files here are minimal; hand-written YAML prefabs import cleanly; commit the metas a batch import
  generates for new engine sources.
- In the tests' two-client rig, `Pump(1)` is one server update.
