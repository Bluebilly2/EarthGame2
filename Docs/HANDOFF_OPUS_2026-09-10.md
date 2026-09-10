# Handoff to the next model, 2026-09-10

Written by Claude Fable 5.1 on the morning the owner said he would move the project to Claude Opus 5. It is a
handoff, not canon: `Docs/CANON.md` rules, `Docs/ARCHITECTURE.md` describes, `Docs/STANDARDS.md` binds,
`Docs/DEBTS.md` owes, and the contract in flight promises. This file tells the successor where things stand,
how the work is done here, where the last model's judgement was spent, and how to spend less of it.

## 0. Read in this order, before touching anything

1. `CLAUDE.md` (two minutes; the house rules).
2. `Docs/CANON.md` rulings 8, 11, 12, 16, 17 and 18 (the wake site, the M1.B criteria, feel by playing, the
   verifier lane and its amendment, the mover ruling).
3. `Docs/contracts/M1.3_ENGINE_CORE.md` end to end: a closed contract shows the whole shape of a slice, from the
   promises to the exit record, and it is the shape every slice must have.
4. `Docs/ARCHITECTURE.md` §3, §5, §6, §7, §10 and the decision log at the end (§12). The log is where the reasons
   live; when a rule seems odd, the log says what broke without it.
5. `Docs/DEBTS.md`, so nothing recorded there is rediscovered as a surprise.
6. The memory files under the Claude Code project memory (`MEMORY.md` indexes them). They are loaded for any
   Claude model on this project; they hold what the docs do not: the traps of this machine, the owner's habits,
   what is pending on him.

## 1. Where the project stands

Milestone 1, "First Light", by the plan's slices:

| Slice | State | Where it is written |
|---|---|---|
| M1.0 Foundation, M1.A First frame, M1.1 Region data | closed | their contracts; CANON rulings 17, 18 |
| M1.B Two players | closed on Claude's side 2026-09-08; the owner's ruling (pass / a week / plan B) **pending** | `M1.B_TWO_PLAYERS.md`; the corpus numbers in its exit record |
| M1.2 Engine world layers | closed on Claude's side 2026-09-09; gate red on one row that is the owner's | `M1.2_WORLD_LAYERS.md` (the census as printed) |
| M1.3 Engine core: the entity store | closed on Claude's side 2026-09-09; gate green; the frames sent | `M1.3_ENGINE_CORE.md` |
| M1.4 Terrain, sky and water | **next**; no contract yet | plan §6 item 7 |
| M1.5 to M1.10 | not started | plan §6 |

Numbers as last run (2026-09-09): 255 engine tests; 5 Unity edit-mode tests in a batch editor; gate M1.3 green;
gate M1.2 green but for the stated-wake row; the M1.B corpus's 26 verifier rows green on 2026-09-08.

Pending on the owner, and only him:

- **The M1.B ruling** (CANON ruling 11): the numbers are in the M1.B exit record. Do not proceed as if it were
  made; the engine work of M1.2 and M1.3 was chosen so that it stands either way.
- **The stated wake.** Ruling 8 names 35.159°S 150.6485°E as "the Cave Beach swale". Destination NSW and
  OpenStreetMap put Cave Beach 2.1 km east of that point. `Region.Bherwerre` keeps the ruling's number; the
  census verifier keeps its row red; DEBTS names the owner. If he rules to move it, the corpus walk loop
  (`Routes.WakeLoop`, absolute coordinates) is re-laid and the corpus re-run before M1.B's numbers are quoted
  again.
- **The M1.3 frames** (`Artefacts/frames/m13/frames/`): a cobble and a stick at rest near the wake. Whether they
  land where his eyes expect is his to say; the contract says so.
- **The census** (`Artefacts/worlds/gate/census.txt`, quoted in the M1.2 exit record): he reads it as a document
  about the country and says where it reads wrong. A wrong reading is a debt or a fix, never a reworded census.

## 2. How the work is done here

The loop, every slice, no exceptions: **contract → tests → code → verification → docs in the same commit → dated
commits pushed through the pre-push hook.**

- **The contract** is one page under `Docs/contracts/`: numbered promises with their numbers (metres, seconds,
  counts, tolerances), non-goals, how it is proved (commands, exit codes, counts "as run"), and the exit. Write
  it first, commit it, then build. When a promise cannot be kept as written, keep building the rest, and record
  what changed under "What changed on the way" with the reason: M1.2 has four such entries, all honest.
- **Tests before code** for engine behaviour: `dotnet test Engine/EarthGame.slnx`, redirected to a file, exit code
  echoed, then the tail; never `| tail` or `| head` (the pipe hides the red). A test names a guarantee; a test that
  cannot fail is not a test.
- **The gate is the verdict**: `python Tools/gate/run_gate.py <slice>`; `Tools/gate/gates.json` lists the steps;
  a verifier it names must exist and be run for its exit code. Green means the steps exited 0, nothing more.
- **Verifiers** live under `Tools/verifiers/checks/`, are listed in `Tools/verifiers/MANIFEST.json`, and must use an
  algorithm or data source independent of what they check, name the source, and print both numbers beside the
  verdict (STANDARDS 19; ARCHITECTURE decision 2026-09-08). `drainage_check.py`, `census_check.py` and
  `save_check.py` are the pattern. A verifier that imports the tool is a stub with extra steps.
- **Docs move in the same commit** as the change that contradicts them (STANDARDS 13): ARCHITECTURE's sections
  and its decision log, DEBTS, THIRD_PARTY_NOTICES for anything vendored or fetched.
- **Commits** say exactly what they contain; a count only when it was run in that tree; the trailer
  `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>` (the model's own name). Push after each slice's commits;
  the hook builds, tests and compiles the Unity layer (about two minutes).
- **The owner's lanes**: he plays the build for feel (ruling 12), reads the census, looks at frames, and enters
  rulings in CANON. He does not write code or checks (ruling 17). He is learning; explain what a step is for in a
  sentence when it is not obvious, never lecture.

## 3. The rules that bite, with the stories behind them

- **One owner per fact.** A number that lives in two places without anything forcing agreement is the named bug
  shape. When a second copy is unavoidable (a verifier restating a coordinate so as to be independent), the copy
  cites its source and says why it is a copy.
- **No TODO comments.** A debt goes into DEBTS.md with an owner and a pay-by, the moment it exists.
- **Date the ruling, never the state.** A comment cites a decision and its date; it never asserts a count, a
  copy-number or a fact that will drift.
- **Frames or it did not happen; hands or it did not happen.** A visual claim carries frames at 1440p and 1080p,
  recorded windowless; a feel claim waits for the owner's hands. Compiling is not running, running is not looking.
- **No visible window, no sound, unless the owner asks.** Automated runs are `-batchmode`, muted, off his screen.
  His own built game may be running; never kill it; build elsewhere (`-buildOut Build/Player-<slice>`).
- **Contracted formats are versioned.** `run.jsonl`, the raster sidecars, `world.json`, the region and player
  files, the wire protocol: a layout change bumps the version in the same commit and amends §10.
- **CANON is the owner's.** Never edit a ruling; propose one in the summary and let him enter it, dated.
- **Never pipe a suite into tail; never claim a count that was not run; never reword a check to pass it.**

## 4. This machine's traps (each cost time once)

- Bash heredocs over about 8 KB fail with "unexpected EOF"; a heredoc that contains `\n` or `\\` in a Python
  string is mangled (the backslash arrives as a newline). Write patch scripts with the Write tool, or build such
  anchors with `chr(92)` and `chr(10)`.
- Bash's working directory persists between calls and `cd` in a compound command can trigger a prompt; use
  absolute paths.
- Git prints CRLF warnings on every add; they are noise, the files are as they were.
- Python for the tools is `Tools/.venv/Scripts/python.exe` (numpy, Pillow, requests); the gate runner picks it up
  when a step starts with `python`. Windows console output needs `PYTHONIOENCODING=utf-8` for degree signs.
- `dotnet test Engine/EarthGame.slnx > test.log 2>&1; echo $?` then grep `Passed!|Failed!|error CS`.
- The Unity-layer compile check `dotnet build Engine/EarthGame.Unity.Compile.csproj` compiles everything under
  `Unity/Assets/EarthGame` against stubs; it catches most Unity-side breakage in seconds.
- Unity batch runs work when the owner's **editor** is closed (`tasklist | findstr Unity`): edit-mode tests with
  `"C:/Program Files/Unity/Hub/Editor/6000.3.22f1/Editor/Unity.exe" -batchmode -nographics -projectPath Unity
  -runTests -testPlatform EditMode -testResults <xml> -logFile <log>`; a player build with
  `-executeMethod EarthGame.Editor.CIBuild.BuildWindows -buildOut Build/Player-<slice> -quit`. About three minutes
  each. Frames: the built exe with `-batchmode -eg-record <dir> -eg-seed 1347 -eg-saves <fresh> -eg-tiles <fresh>`
  (no `-nographics`, or nothing renders); the recorder writes `frames/*_1440p.png` and `*_1080p.png` and
  `run.jsonl`. `-eg-items N` drops N things at the wake.
- A new world is created in the player's Mono in about a minute (18 s in the host); the gate uses the host:
  `python Tools/world/create.py Artefacts/worlds/gate`, `python Tools/world/populate.py Artefacts/worlds/gate`.
- `Data/` is fetched, never committed: `python Tools/data/bake_region.py --zoom 14 --cell-m 4` for the heights,
  the zoom-11 surround as its docstring says, `python Tools/data/bake_water.py` for the OpenStreetMap outlines
  (cached under `Data/cache/osm`). `Artefacts/`, `Saves/`, `Build/`, `*.log` are ignored.
- The corpus (`python Tools/corpus/run.py`, `--quick` for a smoke test) takes about 75 minutes in full and needs
  the host built Release and a player built with `--player`.
- Unity's `.meta` files in this repository are minimal (fileFormatVersion and guid); hand-written YAML prefabs
  and ScriptableObject assets import cleanly; a batch import generates metas for new engine sources, commit them.
- The tests' two-client rig: `Pump(1)` is one server update, one step.

## 5. Where the last model's judgement was spent, and how to need less of it

These are the places where the answer was not in the code and had to be found. Each has a method that a
successor can apply without the same reach.

- **A digest disagreed between the player and the server for identical bits** (M1.B). Cause: Mono and .NET print
  the twelfth significant figure of some doubles differently. Method: never compare formatted doubles across
  runtimes; the digest names whole micrometres and nanohours. When a comparison of "the same state" fails, print
  both strings and diff them before touching the state.
- **A legal sprint was corrected thirty-one times** (M1.B). Cause: jitter bunched two reports into one server tick
  and the validator read their spacing as one tick. Method: reproduce under the harness's shaping, log the
  reason string of every correction, and read what it says; the fix (sequence spacing with a banked-time cap)
  followed from the reason.
- **The lakes** (M1.2). The tiles are noise over the dune lakes (12 to 50 m of "ground" inside Windermere's
  outline). Three rules were tried and each read wrong in the census before the outlines were fetched from
  OpenStreetMap. Method: when a layer reads wrong, look at the raw numbers inside the named place with numpy
  (percentiles, relief, flat fraction) before changing a rule; a rule tuned to make the census read right is a
  reworded census. And when the data cannot show a thing, say so and find a source that can, with its licence.
- **The wake scorer** (M1.2). Threshold factors scored a whole coast at 1.000 and the first cell in row order won;
  then a creek mouth on a platform won; then a swamp's edge. Method: run the real thing after every rule change
  and read the census as a person would; each exclusion carries the date and the failure it answers.
- **Steamers Head is outside the box** and the south-east corner is land (M1.2). Method: convert every published
  point through the sidecar's frame rule and print where it lands before writing a check that assumes it.
- **The made-coast fixture's level plain read as a lake** once the lake rule was right (M1.2). Method: a fixture
  must be physical (a plain drains); when a rule fix breaks a fixture, ask which is wrong before weakening either.

The general method: **measure before reasoning, print both numbers, and let the gate say it.** Where the last
model reached a diagnosis in one step, the successor can reach it in three: reproduce, print, compare.

## 6. What to expect of the change of model, and what to do about it

Fable 5.1 is the more capable tier; Opus 5 is a capable model with the same tools and the same memory. The
project was built so that the difference is survivable: the docs, the contracts, the tests, the verifiers and the
gates carry the state, not the model. Where the difference is likely to show, and the answer to each:

- **Long autonomous stretches.** The last two slices were each built in one sitting with a dozen diagnoses on
  the way. Do not try to match that pace. Cut slices into halves with their own commits (M1.4 below suggests
  the cut), re-read the contract before every commit, and close the day with the memory file updated.
- **Holding every rule at once.** The rules in §3 are enforced by the gate, the hook and the tests only in part;
  the rest (one owner per fact, dated rulings, no rewording) is enforced by attention. Before every commit run
  the checklist in §9. It is short on purpose.
- **Over-claiming.** The strongest habit to keep is the one CANON was written for: a verdict is a comparison the
  owner can read, with both numbers. If a number was not run in this tree, do not state it. If a frame was not
  recorded, the visual claim is not made. If a fix was not verified by the gate, say "not yet verified".
- **Research and geography.** Published coordinates are minute-rounded on Wikidata and metre-precise on
  OpenStreetMap and Destination NSW; the frame rule in every verifier converts them. Cite the source in the
  docstring, as the existing verifiers do, and never reason from memory about where a beach is (the first draft
  of `summit_check` put the bay in the wrong corner from memory; the raster was right).
- **Large edits across four assemblies.** Engine, Protocol, Server, ClientCore and the Unity layer are compiled
  by both Unity and dotnet from the same files. After any cross-cutting change run the suite and the Unity-layer
  compile before anything else; the compile check takes seconds and catches most of it.
- **When unsure between two readings that would make materially different work**, ask the owner in one short
  paragraph with a recommendation; he answers in a line. Do not ask for permission to do ordinary work.

## 7. The next slice: M1.4, terrain, sky and water

The plan (§6 item 7): all tiles with a residency policy, ground textures from the oracle, the far skirt, sea and
creek water, haze, fog and clouds, post, a loading screen, the streaming budget; frames judged.

What already exists to build on: tile streaming of `heights` (M1.B: `TileService`, `TileCodec`, `TileReceiver`,
`DiskTileCache`, the nine tiles around the founder); the world folder's 21 layers including `surface` (the water's
surface where water stands), `water` (classes with the fresh flag), `topology`, `stone`, `overstory`/`understory`
(M1.2); the sea plane drawn at the datum and the coarse region under the tiles (M1.A/M1.B); the census as the
check that the world reads right.

A suggested cut into two contracts, each with its own gate:

- **M1.4a, the layers reach the client.** The tile protocol carries layers other than `heights` by name (a
  `TileRequest` names the layer; the codec gains integer dtypes; the CRC and the cache key include the layer),
  the client holds `surface`, `water` and `topology` for its tiles, wading reads the surface (so a lake wades; the
  DEBTS row "water is known only where a tile is held" is paid), and a residency policy keeps the nine tiles
  around the founder and drops the rest. Verifier: a Python reader of the cached tiles against the world folder's
  layers by checksum. Frames: the founder wading into Windermere, to the knee.
- **M1.4b, the look.** Ground textures from the ground-colour oracle (v1's `GroundColourAt`, re-implemented,
  reading `stone`, `wetness`, `topology`), water surfaces for the lakes and creeks from `surface`, the far skirt
  and the haze, a loading screen with the creation moved off the main thread and progress by layer name (DEBTS:
  "new world blocks the game for a minute"), the streaming budget measured in `run.jsonl`. Frames at the wake, at
  Windermere and at the bay, both resolutions; the owner's eyes decide; STANDARDS 15.

Risks known in advance, from M1.A: Unity Terrain in a built player drew nothing with instancing and unlit
without the sun (decision log 2026-09-08); the URP terrain material's default smoothness reads as water; a
batch-mode player renders only on an explicit render request. Read that log entry and the client's diagnostics
(`-eg-hide`, `-eg-probe`, `-eg-plain`) before the first frame is blamed on new code.

## 8. The owner

William. He owns the game's design (`Docs/GAME_DESIGN.md`) and every ruling in CANON. He is learning to program
and sometimes wants to code a part himself; when he does, hand him one shape and its test, not a lecture. He
asks short questions ("update?", "how are we going?", "continue work") and wants short, plain answers: the
outcome first, then what he decides, then what is next. He plays the build to judge feel and reads frames and
the census to judge the world. He has a built game open most evenings; never close it, never open a window on
his screen. When he says "continue work", the next slice by the plan is the work, and the pending rulings are
stated, not assumed.

## 9. The checklist before every commit

1. `git status` read whole; nothing stray, nothing missing (Unity metas included).
2. The suite run in this tree and the count taken from the run; the Unity-layer compile green.
3. Any changed format's version bumped and §10 amended; any changed rule's decision logged with the date.
4. Any new debt in DEBTS.md with an owner; no TODO in code.
5. Anything vendored or fetched in THIRD_PARTY_NOTICES.md.
6. The commit message says what the commit contains, no more; the trailer carries the model's own name.
7. Before closing a slice: the contract's exit record with the numbers as run, the frames if visual, the memory
   file updated, the push green.

Fable 5.1, 2026-09-10.
