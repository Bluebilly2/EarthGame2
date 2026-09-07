# S1a — the audio layer audited: what exists, what it reads, what fakes

**SOUND DEV, 2026-09-01, against `aa390c4`. Deliverable of `CONTRACT_S1_SOUND.md` §"The work".**
Every line reference is to this tree at that commit. Nothing here was measured by ear — nothing
has been launched — so this is an audit of derivation, not of taste.

## 1. The inventory

Eighteen distinct sounds exist: fourteen generators in `Scripts/Audio/ProceduralAudio.cs`, played
through six long-lived `AudioSource`s and one eight-voice one-shot pool. The generators are
genuinely procedural — arithmetic in a float array, no imported asset anywhere — and that half of
the thesis is already true and needs no defending.

**The other half is the question this audit exists to answer.** Not *is the waveform computed?*
but *is its loudness, pitch and timing computed from the model the world runs?* A synthesised
loop played at a constant volume is a stock loop that happens to have been generated at startup.

| # | Sound | Plays from | What sets its level / timing | Derives? |
|---|---|---|---|---|
| 1 | Footfalls (5 surfaces x 4) | `Player/Footsteps.cs:132` | surface from the E-chain; volume from speed | **yes** |
| 2 | Impacts (stone/wood/soft) | `Audio/Impacts.cs:44` | kinetic joules to loudness; mass to pitch | **yes** |
| 3 | Knap crack / dud / shatter | `Game/Knapper.cs:179-228` | the fracture outcome itself (but see F7) | **yes** |
| 4 | Fire drill | `Fire/FireBuilder.cs:211-212` | volume constant; **pitch from `DrillProgress01`** | half |
| 5 | Fire crackle | `Fire/FireSite.cs:441` | **`Fire.FuelKg`** | **wrong fact** |
| 6 | Wind | `Audio/Ambience.cs:113` | `Survival.WindSpeedMs` x **tree _suitability_** | **half-wrong** |
| 7 | Birds (forest, gull) | `Audio/Ambience.cs:158` | **tree suitability**, coast, a private sunrise | **wrong fact** |
| 8 | Crickets | `Audio/Ambience.cs:126` | night + air temperature | thin |
| 9 | Cicadas | `Audio/Ambience.cs:128` | day + air temperature | thin |
| 10 | Surf | `World/Ocean.cs:103` | **the constant `0.55f`** | **no** |
| 11 | Shelter rustle | `Shelter/ShelterBuilder.cs:104` | the constant `0.4f` | no |
| 12 | Gulps, munch, dig | `Drinking.cs:79`, `Foraging.cs:300`, `Actions.cs:29` | fixed | no, and fine |
| — | **Rain** | **nowhere** | — | **absent** |

## 2. The contract's five beta sources, scored

`CONTRACT_S1_SOUND.md` names five: *footsteps by surface, fire by its state, wind by exposure,
rain by the sky, birds by presence.* Against the tree:

**One derives. Three read a plausible fact that is the wrong one. One does not exist.**

### Footsteps — derives, and is the standard for the rest

`Footsteps.SurfaceUnder` (`:159-179`) asks `Region.SandAt`, then `ScatterField.SiteUnderfoot` for
soil depth (under 0.12 m means rock, because that is what a spur is made of), then
`GroundCover.UnderCanopy` through that same call for litter, then `Foraging.SpeciesBeneath` for
grass. Four real models, consulted in the order a foot would meet them. The step *interval* comes
from distance travelled rather than a timer (`:96-104`), so Tobler's slowdown on a hill shortens
the gap between footfalls without anything being told to.

**This was already met, and it is the bar the other four should be held to.**

Extensions available, and they are extensions rather than defects: ground wetness
(`PlantSite.Wetness` is read by every plant and ignored by the ear — wet ground is a different
sound), litter as a depth rather than a boolean, and carried load (`Survival.LoadKg` exists).

### Fire — the ear reads fuel mass where the eye reads radiant power

`FireSite.RefreshSound` (`:441-442`):

    _crackle.volume = Impacts.Master * Clamp01(0.25f + (float)Fire.FuelKg * 0.03f) * 0.8f

Eighteen lines below, `Refresh` (`:459`) sizes the flame from `Fire.RadiantPowerW / 3000f`.

**The eye reads the fire's power; the ear reads the weight of wood sitting in it** — and they
disagree exactly when it matters. A freshly stacked heap barely alight is `0.25 + 0.30 = 0.55`
and *loud*; the same fire an hour later, roaring on 2 kg, is `0.31` and *quieter*. A founder
listening for whether the fire has caught is being told the opposite of the truth.

This is the named bug shape — one fact, *how big is this fire*, derived twice with nothing forcing
agreement — and the fix is one line, because the right number is already computed and already on
screen.

**The file states both halves of its own contradiction** (REVIEWER, verified here). `FireSite`'s
class doc at `:9-11` says *"Everything visible here is driven by `Fire.RadiantPowerW` — the flame
height, the light range, the colour. A fire that is dying looks like a fire that is dying because
it is the same number doing both jobs."* It is the right principle, written down, in the same file
whose sound method reaches for a different number. The doc comment was never wrong; the ear simply
was not counted as one of the jobs.

### Wind — right speed, wrong trees, and an exposure the founder never feels

`Ambience.Update:112-116` scales the loop by `Clamp01(survival.WindSpeedMs / 9f)`, the same number
the body loses heat to. That is correct, and it is the good half. It then scales by
`(0.30f + 0.70f * _canopy01)`, because trees are what make wind audible — right reasoning, wrong
number.

`_canopy01` (`Reassess:139-141`) is `PlantCommunity.TotalSuitability(site, PlantForm.Tree)`:
**how well trees would grow here, not whether any are standing.** It is habitat potential.
`GroundCover.UnderCanopy` already answers the realised question, and `Footsteps` already uses it;
`Ambience` calls the neighbouring `SiteAt`, which leaves `Shaded` at its default `false` and never
asks.

**Why this is live everywhere rather than an edge case** — REVIEWER's correction to this audit's
first argument, verified here at source. The first draft reached for a bare clearing, and a
clearing is not reachable: nothing fells a tree (`Interactor.cs:345`, *"Felling it needs an
edge"*), so a checker could have downgraded the finding on that example. The real mechanism is
stronger. `ScatterField.cs:357` keeps a tree by

    if (keepRoll > 0.25f + 0.75f * support) return;

so **suitability sets the *probability* that a tree stands, not whether one does.** On good ground
at `support = 0.8`, about one square metre in seven is empty by design — and `_canopy01` is
*identical* in that gap and under the tree beside it. The audio layer is reading the probability
field, which is the average over a stand, where it needs the realised fact at a point. It
under-reads under a tree and over-reads in every gap, in every stand, from the first frame.

Second, and not mine to fix: `Survival:194` takes wind as
`_climate.WindSpeedMs(hourOfDay, IsSheltered ? 0.3 : 1.0)` — **exposure as a two-valued canopy
flag.** `PlantSite.Exposure` (0 in the lee of a hill, 1 on an open crest, from
`Landform.ExposureAt`) is carved by the erosion model and read by every plant in the world, and
the founder's own wind ignores it. `Weather.At` already takes an `exposure01` argument for exactly
this, defaulted to 1.0.

So the contract's *"wind reads the exposure the erosion carved"* is **not currently possible for
the ear, because it is not yet true for the body** — and the ear must not be where it becomes
true, or that is a second derivation of the wind in a presentation path. Filed to FABLE below,
not built here.

### Rain — no code exists

Nothing in the tree plays a sound for rain. `ProceduralAudio` has no rain generator and no
`AudioSource` references one. The world rains and is silent.

The fact it needs is `RainRateMmPerHour`, and **it reaches the Scripts layer only on DEV 2's
unmerged `957fe06`** (`Survival:87`, assembled once per tick from `Weather.At`). In this tree
`Survival` still calls `_climate` directly and carries no rain rate at all.

I will not assemble a second `Weather` in the audio layer to get around that. It is precisely the
shape DEV 2 removed this evening, and a rain sound that disagreed with the rain would be worse
than silence. **Rain waits on the merge**; the rest of S1b proceeds meanwhile.

### Birds — the one place `AnimalPresence` should be read, and is not

`Ambience.CallABird` (`:148-186`) chooses gull against forest bird from `_nearCoast` and
`_canopy01`, and calls on a `Random` interval: 2.5-5.5 s at dawn, 7-16 s otherwise.

- **It reads plant suitability, not `AnimalPresence`.** `Sim/World/AnimalPresence.cs` exists, is
  E5-complete, and computes `Activity01(species, hourOfDay, ...)` with a twilight width and a
  resting floor, plus `Near(...)` returning sightings with group sizes. It is the model that knows
  whether an animal is here and awake. The birds ignore it entirely.
- **The dawn chorus derives sunrise privately.** `:151` computes
  `sunrise = 12.0 - clock.DaylightHours * 0.5`. `SolarClock:149` computes
  `sunset = 12.0 + DaylightHours * 0.5` inside `HoursOfLightLeft`. **Sunrise and sunset are the
  same fact and neither has an owner** — one is a local in the audio layer, the other a local in
  the clock, each holding half a formula.
- The abstraction should survive: `ForestBirds` are deliberately not species, and the contract
  agrees ("a forest that sounds inhabited, not an ornithology lesson"). But *whether* and *when*
  they call is a world fact, and the world has the model.

## 3. Findings outside the five

**F1 — `Impacts.Forget()` is dead, and would leak if anyone called it** (`Impacts.cs:117-121`).
`FlatWorldBootstrap.DestroyWorld` (`:412-427`) forgets shelters, item icons, fires and the terrain
surface. It does not forget the voice pool, and that is *correct*: the pool root is
`DontDestroyOnLoad` (`:79`) and `EnsureVoices` null-checks before reuse, so it survives a reload on
purpose. But `Forget()` nulls the array **without destroying the root**, so calling it would strand
one root and eight sources every time. It is a trap sitting beside a teardown list that is
otherwise complete: the next reader to notice the missing line will add it, and leak.

**F2 — the impact gate is global, not per-body** (`Impacts.cs:47-48`).
`if (Time.unscaledTime - _lastAt < 0.02f) return;` drops *any* impact within 20 ms of *any* other.
Two stones landing together being one sound is the stated intent and is right; but a footfall
landing in the same 20 ms as a dropped flake also silently loses one of them, with eight voices
free. The gate does coincidence-merging through a single global.

**F3 — the one-shot pool has no priority** (`Impacts.cs:64-79`). Eight voices, round-robin, so a
bird 35 m away can cut off a footfall at the founder's feet. Cheapest fix: do not steal a voice
that is currently louder than the incoming sound.

**F4 — three constant volumes** (`Ocean.cs:90,103`; `ShelterBuilder.cs:104`;
`FireBuilder.cs:211`). The surf is the loudest of them: `SurfVolume = 0.55f` never changes, so a
still morning and a gale sound identical at the waterline, on a coast whose wind the model knows.
`Ocean.Update` already runs every frame and already slides the source along the waterline to keep
it abreast — the loop is there, only the number is dead.

**F5 — `Ambience` and `Footsteps` ask one question two ways.** `SiteAt` against `SiteUnderfoot`
(`ScatterField.cs:520-525`); see the wind finding. Not a second model, but a second reading of
one, and the reading that skips `Shaded` is the one that most needs it.

**F6 — not one runtime volume is verified anywhere.** `SoundSelfTest` measures every clip's
length, RMS and peak, and checks that the wind source and the fire crackle are *playing*. It never
asserts that a level tracks anything: the wind at 0 m/s and at 15 m/s both pass, and the fire
crackle passes while reading the wrong fact. **Every derivation defect above is invisible to the
suite**, which is how they survived.

This is the gap S1b's headless tests exist to close, and it is the argument for putting the
derivations in `Sim/World/Soundscape.cs` as pure functions where NUnit can reach them, rather than
inline in an `Update` where nothing can.

**F7 — the mute is two switches for one fact, and one source has already fallen out of
agreement.** Found by REVIEWER in its review of this contract, and verified here at source before
being written down: `Knapper.Awake` (`:53-56`) builds its own `AudioSource` at
`volume = 0.55f` and plays through `PlayOneShot` (`:305-308`), and the file contains **zero**
references to `Impacts.Master`. Every other source in the tree is master-scaled — `Ambience`
`:96,104,106`, `Impacts:73`, `FireBuilder:211`, `FireSite:441`, `ShelterBuilder:104`,
`Ocean:90,103`. Knapping is silenced today by `AudioListener.volume` alone.

It is latent while the two switches are always written together (`FlatWorldBootstrap:256-260`).
**S1c's entire purpose is to make them conditional**, and at that moment it becomes live in both
directions: lift `Master` alone and everything stays silent while this seat debugs a failure that
is not its own; lift `AudioListener.volume` alone and **every master-scaled source stays at zero
while knapping plays at 0.55** — a world whose only audible thing is stone on stone, on the
owner's speakers, arriving through the very mechanism built to let sound out safely.

The fix is one line in `Knapper` and it is pre-existing and DEV 1's, so it is filed rather than
folded in. But it is a precondition: **S1c must not land before it is fixed, or the escape ships
holed.** The escape itself must lift the mute in one place through one predicate — two switches
for one fact is the named bug shape, and this is what it costs.

## 4. What S1b builds, and what it needs from other seats

`Sim/World/Soundscape.cs` — pure, headless, no UnityEngine — takes the world's own numbers and
returns loudness, rate and pitch: surf from wind, rain from mm/h, wind from speed by realised
canopy by exposure, fire from radiant power, bird call rate from `AnimalPresence.Activity01`.
Each sabotaged once in the tests, per house law.

Files outside `Scripts/Audio/**` this needs, each a claim on the board:

| File | Change | Clear with |
|---|---|---|
| `Scripts/Fire/FireSite.cs` | one line: crackle from `RadiantPowerW` | DEV 1 |
| `Scripts/World/Ocean.cs` | surf volume from `Soundscape` | DEV 1 |
| `Scripts/Player/Footsteps.cs` | surface classification moves to `Soundscape` | DEV 1 |
| `Scripts/Game/SoundSelfTest.cs` | assert levels track, not merely play | DEV 1 |
| `Sim/World/SolarClock.cs` | `SunriseHour` / `SunsetHour`, one owner for the pair | DEV 2 |
| `Scripts/Flat/FlatWorldBootstrap.cs` | S1c's `sound: on` escape | DEV 1 (hot file) |
| `Assets/EarthGame/Tests/SoundscapeTests.cs` | new file | — |

Blocked on the merge: rain, which needs `957fe06`'s `Survival.RainRateMmPerHour`.
Filed and not built: `Survival`'s binary exposure (section 2, wind).

## 5. Baseline measured in this worktree

`dotnet test CI/Tests/EarthGame.Sim.Tests.Headless.csproj` at `aa390c4`: **410 tests, 396 passed,
0 failed, 14 inconclusive.** `CI/Scripts` compiles clean.

The fourteen are not a problem and are worth recording so nobody re-investigates them: a fresh
worktree has no downloaded global elevation dataset, so `EarthElevationTests:60` and
`TerrainClassifierTests:49` call `Assert.Inconclusive` rather than assert against data that is not
there. Sessions quoting "N/N" are quoting passed-against-passed; the honest pair for this worktree
is 396/410 with the reason named.
