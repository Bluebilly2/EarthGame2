# Development

Practical commands. For *what* is being built and *why*, see `ROADMAP.md`, `GAME_DESIGN.md`, and
`THE_CIVILISATION_OF_ONE.md`.

## Requirements

- **Unity 6000.3.22f1** (Built-in Render Pipeline), free Personal licence — to play or build.
- **.NET SDK** — to run the simulation validation suite without Unity. Any modern SDK works.
- Nothing else. All game content is generated at runtime; the project ships no scene assets.

## Play

Open the project in Unity Hub and press **Play**. You spawn on the planet.

`WASD` move · mouse look · `Space` jump · `Shift` sprint/boost · `F` fly · `Ctrl` descend ·
`R` respawn · `Esc` cursor · **`B` workshop**

## Fetch the world

The planet's shape comes from real elevation data, which is **fetched, never committed**
(see `Docs/DATA_SOURCES.md` for sources and attribution). A fresh clone has no dataset and runs
on procedural terrain instead, logging one warning that says exactly that.

In the editor: **EarthGame -> Data -> Fetch Global Elevation**. Headless:

```
Unity.exe -batchmode -quit -projectPath . ^
  -executeMethod EarthGame.Editor.ElevationDataTool.FetchAndBake -logFile bake.log
```

It downloads 1024 tiles (~64 MB) into `../EarthGameData/tiles/` - outside the repo - and bakes
`Assets/StreamingAssets/EarthElevation.r16` (16 MB). Re-running is cheap: cached tiles are
reused, so a re-bake needs no network. Delete the cache to force a clean fetch.

## Run the validation suite

The fast path — no Unity, ~5 seconds, this is what CI runs:

```bash
dotnet test CI/Tests/EarthGame.Sim.Tests.Headless.csproj
```

`CI/` compiles the *same* source files as the Unity assemblies (it globs `Assets/EarthGame/Sim`
and `Assets/EarthGame/Tests`), so the two can never drift apart. This works because
`EarthGame.Sim` has `noEngineReferences: true` and the tests import only NUnit.

### The Scripts layer, without opening the editor

The suite above covers `Assets/EarthGame/Sim`. It does not compile the other two thirds of the
project — the Unity layer — because that needs Unity's own assemblies. This does, in about two
seconds, by reading them out of the local editor install:

```bash
dotnet build CI/Scripts/EarthGame.Scripts.Compile.csproj
```

Warnings are errors there, so it is also the dead-code check. It compiles and nothing more: it
catches a typo, a missing using, a changed signature — and nothing about behaviour. A Scripts
change still needs a scenario, a self-test, or the owner's eyes before it is believed
(`Docs/STANDARDS.md` §8).

Point `<UnityManaged>` in that csproj at your own editor install if it is not in the default place,
or pass `-p:UnityManaged=...` on the command line.

### The pre-push gate

Both checks run automatically on `git push`. **Activate it once per clone:**

```bash
git config core.hooksPath CI/hooks
```

This exists because hosted CI cannot do the job. GitHub's own runners have no Unity, compiling
the Scripts layer needs assemblies that are neither redistributable nor available without a
licence, and `.github/workflows/validate.yml` deliberately refuses a licence secret. Since
2026-09-01 the self-hosted runner on this machine closes half of that gap — the workflow's
`scripts-compile` job re-runs this compile on every push to main — and the local pre-push gate
remains the mandatory half that runs *before* publication. It fails loudly if the Unity assemblies
are missing rather than skipping quietly, because a gate that goes green when it could not look is
worse than none. To push past it deliberately: `git push --no-verify`.

The Unity-side path (same tests through the Test Runner; **the editor must be closed**):

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml -logFile test.log
```

## Build a standalone player

Editor closed, then:

```bash
"C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe" -batchmode -quit -projectPath . -executeMethod EarthGame.CIBuild.BuildWindows -logFile build.log
```

Output: `Build/Player/EarthGame.exe` (untracked).

## Layout

| Path | What |
|---|---|
| `Assets/EarthGame/Sim/` | **Simulation core.** Pure C#, deterministic, no UnityEngine. The real game logic. |
| `Assets/EarthGame/Tests/` | Physics validation suite (§36). Runs headless and in-editor. |
| `Assets/EarthGame/Scripts/` | Unity presentation layer — planet meshing, player, environment, UI, workshop. |
| `Assets/EarthGame/Shaders/` | Built-in RP shaders (terrain, water, atmosphere, skybox). |
| `Assets/EarthGame/Editor/` | Editor/CI tooling. Not shipped in players. |
| `CI/` | Hand-written csproj mirrors for licence-free CI. Tracked. |
| `Docs/` | The governing documents and per-slice binding contracts. |

## Rules that outrank convenience

1. **Nothing in `Assets/EarthGame/Sim/` may reference UnityEngine.** The simulation must stay
   engine-free, deterministic, and testable (GAME_DESIGN §34).
2. **Every slice gets a binding contract before code** — `Docs/SLICEn_*.md`, following
   `SLICE1_BOLT_FACTORY.md`: domain spec, error budget, exact API, validation tests.
3. **The validation suite only grows.** Loosening or deleting a test to pass a gate is a gate
   failure (ROADMAP gate rule 6).
4. **Determinism is a test, not an aspiration.** Same seed, same result, bit-identical.
