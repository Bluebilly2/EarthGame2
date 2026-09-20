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
- He often has his built game open: never close it, never open a window on his screen (STANDARDS 14). His game is
  `Build/Player` and the runs' is `Build/Harness`; `Tools/build/install.py --into player` refuses to replace his while
  it runs, so a build for him waits until he has closed it.
- When he says "continue", carry on with the work in flight by the plan; state anything pending on him rather
  than assuming it. Finish the job he asked for before proposing the next one, and propose it rather than start
  it.
- **A slice that owes his eyes, his hands or his word adds a row to `DEBTS.md`'s "Waiting on the owner" table**,
  and the slice keeps the detail. Until 2026-09-20 each such item lived only inside the contract that raised it:
  a reading of the whole 7–19 September record, made independently of these documents, found every engineering
  debt in the register and not one of his, twenty contracts saying "his eyes" with nothing listing them together,
  and questions asked once that died unasked again (the gamepad's layout, how swimming feels, whether the beta
  stays a faithful Bherwerre). A row leaves that table by his word, not by age.

## Before every commit

1. `git status` read whole: nothing stray, nothing missing (Unity metas included).
2. The suite run in this tree and the count taken from the run; the Unity-layer compile green; and when the Unity
   layer changed, its edit-mode tests green (`python Tools/unity/editmode.py`); before 2026-09-11 only the loading
   gate ran them, and a red suite went unseen from M1.6a's landing.
3. A changed format's version bumped and ARCHITECTURE §10 amended; a changed rule's decision logged with its date.
4. A new debt in `DEBTS.md` with an owner; no TODO in code.
5. Anything vendored or fetched in `THIRD_PARTY_NOTICES.md`.
6. The message says what the commit contains, no more; the trailer carries the model's own name.
7. Closing a slice: the contract's exit record with the numbers as run, frames if visual, the memory updated,
   the push green.
8. If what the game can do changed, `Docs/THE_GAME_NOW.md` rewritten from the records — the owner's one plain-words
   page, written 2026-09-20 because "continue" had come to mean flying blind for him. It owns no fact and nothing
   cites it; it is derived, and stale is its only failure mode.

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

Codex (GPT-6) committed to this repository until 2026-09-14; William stopped using it on 2026-09-18 ("it kept hitting
usage limits too fast"). What it taught still holds for any other party's work: do not read its exit record and agree: re-run the suite and every
gate, read its verifier's evidence and its frames, and read the code for what a gate cannot see — one owner per
fact, dated comments, no TODOs, versioned formats. Its loading slice held up and fixed a real bug of ours; what it
got wrong was duplication (a copy of `CLAUDE.md` as `AGENTS.md`; the dedicated host left on an old path).

## Two agents, one machine

From CANON ruling 26 (2026-09-13) to its amendment of 2026-09-18, GPT, through Codex, worked on world generation on its
own judgement while Claude carried the beta arc, in the same repository on the same machine; since then world generation
is Claude's and no second agent works here (Claude's own parallel agents work in worktrees of their own, WORKING's
"Two agents" rules below holding for them). Neither handed the other work, and each kept out of the other's way:

- **Main is shared.** Each works on its own branch or worktree and lands on main in small commits, rebased on the
  latest main and green through the pre-push hook. Stage explicit paths, never `git add -A`; never force-push or
  rewrite main; never commit a file the other made and left uncommitted.
  `Unity/Assets/EarthGame/Tests/Editor/ReviewOpusProbe.cs` and its `.meta` stay out of every commit.
- **One Unity batch run at a time.** A batch run locks the project, so `tasklist | findstr Unity` shows nothing
  before one starts: neither the owner's editor nor the other agent's run.
- **A running `EarthGame2.exe` may be the other's timing run.** Frame costs are measured with the machine quiet;
  builds, world creation and suites beside one make its numbers lie, so they wait until it ends.
- **A scenario that leaves things behind runs on a copy of its world.** The scenario tools share `Artefacts/worlds/gate`;
  the knap scenario leaves a struck core and its flakes at the wake and the founder holding the hammer, and its second
  run on the same world (2026-09-18) found the first run's core under the crosshair where it had just set its own down.
  `knap.py` now copies the world under the run's folder and never writes the one named; a tool that changes what lies
  at the wake must do the same. The dirtied save of that day is set aside as `Artefacts/worlds/gate-dirty-20260918`
  (its regions and player), and the gate's fixtures were placed again by `populate.py` on the untouched layers.
- **No host build while the corpus runs.** The corpus's dedicated server runs from
  `Engine/.build/bin/EarthGame.ServerHost/Release/`, and a Release build of the host then fails on the locked
  files (2026-09-16, MSB3021) without harming the run; a Debug suite run beside a soak is a burst the `ticks` rows
  may show. Build the host before, and keep the suite to the corpus's walks, never its soak.
- **Output under a name of one's own:** fresh folders under `Artefacts/`, and no deletes in an automated run. Players
  are not output under a name of one's own: they go through `Tools/build/install.py` into `Build/Harness` (the runs')
  or `Build/Player` (the owner's), replaced in place and told apart by their `version.json` and the log's first line.
  Every new folder an exe runs from is a permission prompt in the harness and, if it opens a socket, a firewall box on
  the owner's screen; by 2026-09-16 there were forty-one such folders (M1.Bb).
- **The suite binds no socket on every address.** M1.Ba's two contrast tests did, to read the system's table, and
  every new path the suite ran from (a temporary checkout, a scratch harness) was a firewall box on the owner's
  screen: three in ten minutes on 2026-09-16. The transport's choice of address is read (`BindAddressFor`,
  `BindsLocalOnlyFor`), never exercised, and every socket is closed unless opened.
- **Main can move under you.** It lives in one checkout, and either agent may merge into it and push at any time:
  `git log --oneline -5` before building on main shows what the other has landed, to be reviewed first, and
  `git log origin/main..main` before a push names every commit that push carries. On 2026-09-13 Codex merged and
  pushed WG.0a from this checkout a minute before Claude's next commit; Claude saw it only in its own push's report,
  reviewed it after, and at first misread that report as its push having carried Codex's commits (the remote's
  reflog shows Codex's own push at 12:14 and Claude's at 12:17).
- **Each reviews what the other lands**, as the section above says, before building on it.

## This machine's traps

- Bash heredocs over about 8 KB fail with "unexpected EOF", and a heredoc containing `\n` or `\\` in a Python
  string arrives mangled. Write patch scripts with the Write tool into the session's scratchpad and run them.
- `$TEMP` in Git Bash is the system temp folder, not the session's scratchpad; use the scratchpad's full path.
- A scratch script named like a standard module (`bisect.py`) shadows it for every script in that folder.
- Git prints CRLF warnings on every add; they are noise.
- A command run in the background is cut off at the timeout it was given, and a chain of runs cut off mid-way leaves what
  it was doing: on 2026-09-14 a sabotage script stopped between its change and its restore left a sabotaged line in
  `GameServer.cs` (found by `grep` for the sabotage's own text, and put back by hand). Give a chain the time it needs,
  a corpus among it more than an hour, or run its pieces one at a time; and after any stop, `git diff` before building on.
  A sabotage script must not run while a Unity build is compiling the same sources: on 2026-09-14 a player built
  alongside one carried a sabotaged transport (its client bound every address, and raised the very firewall box the
  build was to prove gone), and the run on it proved nothing. Build with the sources at rest; a build folder Windows
  has already been asked about proves nothing either, so a firewall check needs a folder of a new name.
- Something on this machine holds a file for a moment after it is touched: on 2026-09-13 the built game's closing save
  could not replace a region file its autosave had written, and on 2026-09-14 a sabotage script's restore of
  `GameServer.cs` was refused on the open itself ("invalid argument") and left the sabotaged file in place. A save
  tries again (M1.3c); a script that writes and restores a project file must too, and must verify the restore.
- Python for the tools is `Tools/.venv/Scripts/python.exe` (numpy, Pillow, requests); the gate runner uses it for
  steps that start with `python`. Console output needs `PYTHONIOENCODING=utf-8` for degree signs.
- `dotnet build Engine/EarthGame.Unity.Compile.csproj` compiles the Unity layer against the editor's own assemblies
  (no editor, no licence) in seconds and catches most Unity-side breakage; it needs `Unity/Library/ScriptAssemblies`
  from one batch import.
- Unity batch runs need the owner's editor closed (`tasklist | findstr Unity`). Edit-mode tests:
  `"C:/Program Files/Unity/Hub/Editor/6000.3.22f1/Editor/Unity.exe" -batchmode -nographics -projectPath Unity
  -runTests -testPlatform EditMode -testResults <xml> -logFile <log>`. A player build: `python
  Tools/build/install.py --into harness --label <slice>` (Unity into `Build/.staging`, then installed with its
  `version.json`; `--into player` for the owner's copy, refused while it runs; `--show` names what is installed
  where). About three minutes.
- Frames: the built exe with `-batchmode -eg-record <dir> -eg-saves <fresh> -eg-tiles <fresh>` and no
  `-nographics`, or nothing renders; it writes `frames/*_1440p.png`, `*_1080p.png` and `run.jsonl`. The host's
  `stand <name> <east> <north>` then `save` puts the founder where the thing being judged is. `-eg-items N` drops
  N things at the wake; without it nothing is in the world.
- A world: `python Tools/world/create.py <folder>` (about 18 s in the host). A shaped join: `python
  Tools/world/stream.py --build`. The corpus: `python Tools/corpus/run.py` (`--quick` for a smoke test; about 75
  minutes in full, with the host built Release and a player built with `--player`); it creates its own world
  first and gives every scenario a copy; when the wake moves, `python Tools/corpus/lay_loop.py --write` lays its
  loop again, and the player is rebuilt. It points `Artefacts/corpus/latest`, which the gates read, at whichever run it
  wrote, a quick one included: a smoke run after a full one goes in a folder of its own (`--out
  Artefacts/corpus-postfix/<name>`), or the gate reads the smoke run (2026-09-13).
- Windows' firewall asks about a program the moment it binds a socket on every address, once for each program path,
  with a box on the owner's main screen; by 2026-09-14 he had answered it for eight test builds and the test suite
  twice. Every end for this machine binds the loopback address alone (`UdpOptions.LocalOnly`, M1.Ba): the host takes
  `+server.local 1`, the game `-eg-local`, and a client joining 127.0.0.1 does it by itself. A new tool that starts
  the host passes `+server.local 1`; only a game hosted for friends should ever raise the box, and then once.
- `Data/` is fetched, never committed: `python Tools/data/bake_region.py --zoom 14 --cell-m 4` for the heights,
  `python Tools/data/bake_water.py` for the OpenStreetMap outlines, `python Tools/data/fetch_ala.py` for the plants' records. `Artefacts/`, `Saves/`, `Build/` and logs are
  ignored by git.
- Unity `.meta` files here are minimal; hand-written YAML prefabs import cleanly; commit the metas a batch import
  generates for new engine sources.
- In the tests' two-client rig, `Pump(1)` is one server update.
- A world's clock moves with every host and player run, so a reused world drifts from morning into night. Frames or
  timings that are to be compared are each taken on a freshly created world, which wakes at about 08:00 (2026-09-11:
  a timing round that ran into the world's nightfall read the near trees at under half their cost, the sun being
  down).
- A frame's cost: the recorder with `-eg-hold 20` (a full turn, the GPU waited for every frame; a `timing` record)
  and `-eg-hide trees|near|far|shadows|loose` to part the costs. Timing wobbles with whatever else the machine is
  doing, so a round is run twice, each pass on its own fresh world, and each configuration keeps the faster of its two
  medians (2026-09-11: three runs in a row came out 2 ms slow).
- A shader drawn only from materials made at runtime loses its instanced variants in a build: give it a material
  asset with instancing on (`ProjectSetup`; ARCHITECTURE §12, 2026-09-08 and 2026-09-11). A `-nographics` player has
  no instancing at all, so code that draws instanced checks `SystemInfo.supportsInstancing` first.
