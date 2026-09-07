# Slice 2.3e — Knapping: the first blade

**Status:** shipped. Contract and measurements together.
**Phase:** 2 — The First Survivor.

## Why

Slice 2.3d ended by naming knapping as out of scope, and the founder has been walking past flint
ever since. Everything ahead of them is downstream of an edge: cordage is torn out by hand at six
minutes a go, a digging stick cannot be pointed, nothing can be skinned, and no haft can be cut.
This is the slice where a person stops using what they find and starts making what they need.

## The transfer test (§3)

**Nothing anywhere labels a stone as toolstone, and nothing labels a stone as a hammer.** Both fall
out of the mineralogy already in `StoneType`. Ask the catalogue which stones will flake and it
answers *obsidian, flint, chert, quartzite* — the four the archaeological record answers with — and
it does so because of their conchoidal fracture and fracture toughness, not because a list says so.

A player who knows that you strike an acute edge, that you swing to control flake size, and that a
big hammerstone cannot do fine work will find all three true here.

## Domain spec (§35)

Two published results carry the mechanic, both used unchanged.

**Auerbach's law.** The load needed to start a Hertzian cone crack rises with the radius of the
indenter — and for indenters of hammerstone size, several centimetres across, it rises as the
*square* of that radius. Implemented as `CriticalEnergyJ = k · toughness · r²`, with `r` derived
from the hammerstone's own mass and density. Below it, nothing happens at all.

**The Hertzian cone angle is a constant**, independent of load. Striking harder changes the size of
the fracture and not its shape. So force sets flake size, and that is *all* it sets — there is no
accuracy stat anywhere in this slice. The founder controls one number, how hard they swing, and the
stone decides everything else.

Three consequences follow without being authored:

- **A heavy hammer has a floor under how small a flake it can take** (`MinimumFlakePerHammerKg`),
  because the cone is as wide as the thing that drove it. Tapping more gently does not get you under
  that floor; it just stops fracturing. This is the whole reason a knapper carries two hammerstones.
- **The platform angle gates everything.** Under 90° a fracture can run and release a flake; over it
  the blow has nowhere to go and crushes the edge. Every flake taken flattens the face and works the
  angle toward 90°, so the core must be turned — which costs stone, because the ruined edge comes
  off with it.
- **A blow too big for the core destroys it.** `ShatterEnergyPerKg` scales with what is left, so the
  swing that was right at 900 g is fatal at 300 g.

Edge quality is `StoneType.EdgeQuality` docked for violence: a heavy blow drives a thick clumsy
flake, which is the first thing a beginner does wrong.

## What it came out as

Critical energy to start a fracture, by stone and hammerstone:

| stone | knap | edge | 1.4 kg hammer | 0.35 kg hammer | |
|---|---|---|---|---|---|
| obsidian | 0.86 | 0.75 | 4.7 J | 1.9 J | |
| flint | 0.77 | **0.95** | 6.0 J | 2.4 J | |
| chert | 0.66 | 0.81 | 6.7 J | 2.7 J | |
| quartzite | 0.29 | 0.25 | 12.0 J | 4.8 J | |
| basalt | 0.18 | 0.13 | — | — | crumbles |
| shale | 0.14 | 0.05 | — | — | crumbles |
| sandstone | 0.07 | 0.03 | — | — | crumbles |
| granite | 0.06 | 0.01 | — | — | crumbles |

**Quartzite needs twice the blow flint does**, because its fracture toughness is twice flint's. That
is exactly why it is the stone people used where there was no flint rather than the stone they
preferred.

A 0.9 kg flint core under the 1.4 kg hammerstone, by how far the founder winds up:

| wind | energy | outcome | flake | edge | cutting fibre |
|---|---|---|---|---|---|
| 0.00 | 1.4 J | nothing | — | — | 6.00 min |
| 0.25 | 5.3 J | nothing | — | — | 6.00 min |
| 0.40 | 8.9 J | flake | 14 g | 0.91 | 1.18 min |
| 0.50 | 11.8 J | flake | 14 g | 0.87 | 1.22 min |
| 0.65 | 16.9 J | flake | 24 g | 0.80 | 1.31 min |
| 0.80 | 22.9 J | flake | 37 g | 0.71 | 1.42 min |
| 1.00 | 32.4 J | flake | 58 g | 0.62 | 1.59 min |

**There is a dead zone below 6 J and a penalty all the way up.** A quarter-swing does nothing at
all; a full swing takes four times as much stone for an edge a third worse.

The same core with the small 0.35 kg hammerstone reaches down to **3.5 g flakes at edge 0.93** —
work the big stone cannot do at any wind, because its cone floor is 14 g. Two hammerstones is
physics, not spares.

**Worked properly, a 900 g nodule gives 36 usable flakes** over 37 blows and 6 turns of the core,
leaving 202 g — at which point the window between "will fracture" and "will shatter" has closed for
that hammer, and the small one takes over.

## The payoff

Cutting one lot of cordage fibre, which is the founder's most repeated job:

| | minutes |
|---|---|
| bare hands | 6.00 |
| quartzite flake | 2.84 |
| chert flake | 1.32 |
| flint flake | **1.17** |

**Five times faster**, for a minute's work with two stones off the bench. Nothing else the founder
can do to their day pays back like that, and it is why the first blade is the thing every other
technology waits on.

## In the world

One verb, on the button the founder already has. Hold a knappable stone, hold **Work** to wind up,
release to strike — the bar is the swing. The hammerstone picks itself: the *lightest* stone carried
that can still start a fracture, which is what a knapper reaches for and why the small basalt cobble
takes over once it is in the pack.

The core remembers its platform (`ItemState.PlatformAngleDeg`), so putting it down and coming back
tomorrow does not forget six flakes of history. When the edge goes obtuse, **Use** turns it.

It is all audible before it is legible: `KnapCrack` is the highest, sharpest sound in the game,
`KnapDud` is a dead low thud, and `KnapShatter` is a longer unpitched scatter. A knapper works by
ear, and the founder can learn the difference between a blow that worked and one that did not
without ever looking at the core.

## Validation (§36)

16 tests in `Assets/EarthGame/Tests/KnappingTests.cs`; suite at **255 passing**.

| claim | check |
|---|---|
| the knappable set is exactly the toolstones | derived from mineralogy, compared to the archaeological four |
| Auerbach scaling | tripling hammer mass raises critical energy by 3^(2/3), to within 2% |
| tough stone resists | quartzite needs > 1.8× flint's blow |
| the dead zone and the ceiling | under critical does nothing; over the shatter bound destroys the core |
| force sets size | five rising energies give five rising flake masses |
| and costs edge | a 4.5× blow gets an edge < 0.8× the measured one |
| no fine work with a big hammer | swept over 200 blows: the 2 kg hammer's smallest flake > 3× the 0.35 kg one's |
| platforms die | a fresh platform gives 2–12 flakes, then crushes; turning revives it and costs stone |
| a nodule is an afternoon | 900 g yields > 12 usable flakes, ends used up rather than looping |
| quartzite is unforgiving | careful gives a working edge, rough wastes it, and on a small core rough shatters it |
| grain decides the edge | flint's edge > 2.5× quartzite's |
| the swing band is right | rest does nothing, half works, full threatens a 900 g core |
| exhaustion does not stop it | a spent founder still fractures flint, with 30% less behind it |
| the tool pays for itself | 6.00 min by hand against 1.17 min with a flint flake |

Wiring proved in the player by `KnapSelfTest` (`-eg-newgame -eg-knap-test`), which drives the real
inventory and the real component: the workshop's flint and basalt reach a cutting edge, the roles
refuse to swap when the wrong stone is in hand, a full swing measurably underperforms a measured
one, the platform dies and turning revives it, and sandstone is never offered as a core.

## Out of scope

Pressure flaking and soft (antler) hammers, retouch, hafting, ground-edge axes, and edges dulling
with use — `ItemState.Edge01` exists to carry that when it comes.
