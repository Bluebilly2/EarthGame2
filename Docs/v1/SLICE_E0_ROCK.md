# Slice E0 — Rock: the link the chain was missing

**Status:** contract, binding. Written before code. Implements `ECOSYSTEM.md` link 1.

## What it is

The first link of the ecosystem chain — *rock → landform → water → soil → plants → animals* — is
the one that was never built. Landform, water, soil and plants are all derived from the ground.
What the ground is *made of* is decided by hashing a direction into a cell 375 km across and
reading a fixed table.

This slice makes surface lithology a consequence of the same landscape everything else already
reads: what the bedrock is, how deeply the land has been cut into it, and what the water has
carried downhill and left behind.

## Why it is urgent rather than merely owed

**The founder cannot make a stone tool at the canonical wake point.** Probed in the running game,
the Highlands world contains Sandstone and Shale and nothing else. Sandstone knaps at 0.071 and
shale at 0.140 against `Knapping.MinimumKnappability = 0.25`. There is no core anywhere in the
9.8 km world.

The Phase 2 gate reads: *"wakes naked at the canonical wake point … and reaches a stone cutting
edge and cordage within 48 in-game hours."* It cannot be passed. Not by a poor player — by any
player, because the stone is not there.

The cause is one number. `SurfaceGeology.ProvinceHash(dir, 17.0)` quantises a unit direction at
17 cells per radian, which is a cell **375 km** across; the dominant-rock hash at 60.0 gives
106 km. A 9.8 km world therefore sits inside a single cell and receives **one frozen draw for its
entire surface**. Sixty-five per cent of draws are knappable. This one was not.

`SurfaceGeology.cs` is the only file in `Assets/EarthGame/Sim` with no test referencing it.

## The external referent (gate rule 1)

**NSW Seamless Geology**, Geological Survey of NSW, 1:100 000, queried by point-identify and
envelope (`spatial.industry.nsw.gov.au/arcgis/rest/services/Minerals/NSW_SeamlessGeology_Zone56_*`).

Within a 10.0 × 9.8 km box centred on the wake point — matched to the playable world:

| | |
|---|---|
| Mapped bodies | **49**, of **10** distinct named units |
| Smallest body of any kind | **0.12 × 0.21 km** |
| Distinct lithologies within 5.6 km | **7** |
| Mean body area | 2.0 km² → equivalent edge **1414 m** |

At the wake point itself: bedrock **Bringelly Shale** (Wianamatta Group), surface **Quaternary
residual regolith**. **Robertson Basalt outcrops 1.60 km away** — 8 polygons in the box, largest
6.98 × 5.90 km. **Mount Gibraltar microsyenite** at 3.75 km. Hawkesbury Sandstone at 3.62 km,
exposed where valleys cut through the Wianamatta cover.

So the game is wrong on the map's own terms before playability is considered: the survey says
there is basalt within a twenty-minute walk and the game says there is none.

A second, independent derivation agrees. The Mittagong Formation is up to 15 m thick at its type
area 8 km away; on a 5–10% hillslope a 15 m unit crops out in a band of 15/0.07 ≈ 210 m. The
mapped minimum and the stratigraphic arithmetic land in the same place.

**Province edge is therefore derived, never typed:**

```
MappedBodiesPerSquareMetre = 49 / 98.0e6
ProvinceEdgeM              = 1 / sqrt(that)        = 1414 m
CellsPerRadian(R)          = R / ProvinceEdgeM     = 4505 at Earth radius
```

This is a **resolution ceiling of one survey at one map scale**, not a fact about Earth's
lithology — a 1:25 000 sheet would lower it. It is recorded as provisional, and it is the finest
scale `FlatTerrain`'s 256 m colour tiles can resolve in any case.

## Flint does not exist here, and that is a citation, not an opinion

Owen, Hise, Player & Ingrey (2019), *Australasian Historical Archaeology* 37:5–17. Flint from
Sydney's first Government House and from RSY1 at Randwick was tested by pXRF against a Thames
Estuary gravel bed at Deptford and found chemically identical: it arrived **as ship's ballast in
convict transports after 1788**, was offloaded, and was picked up and reworked by Aboriginal
knappers. There is no chalk in the Sydney Basin and no natural flint.

The world emits no flint. `StoneType.Flint` stays in the material list — seventeen sites construct
it by name, and it is a real rock elsewhere — but nothing in this region may produce it.

**What was actually knapped here** (Koettig 1981 and 1985, Hume Highway survey at Berrima and
Mittagong, 8–15 km from the wake point, 24 sites including a quarry; Austral Archaeology ACHDDA
for New Berrima): *"primarily silcrete and quartz, however, quartzite and chert were present in
lower percentages"*. The same assessment confirms the game's own instinct about the bedrock:
*"Raw materials from both units [Ashfield and Bringelly Shale] would not have had the integrity to
be used for the manufacturing of Aboriginal tools."*

**Silcrete is added** — a silica-cemented duricrust, ≥85% SiO₂ (Summerfield's definition), the
dominant flaked material of the Cumberland Plain through the mid–late Holocene (Doelman et al.
2015, *Geoarchaeology*).

## Where silcrete comes from, so that it is derived and not placed

Silcrete is not a bedrock formation and cannot be put on a stratigraphic table. It forms near the
surface, and in the eastern Australian highlands by one published mechanism (Webb et al. 2013,
*Archaeology in Oceania* 48; same process described for Armidale–Uralla):

```
Tertiary lava flows bury palaeovalleys and their stream gravels
  → groundwater perched by the impermeable basalt silicifies the buried gravel
    → silcrete forms at the base and margin of the basalt sheet
      → erosion strips the basalt back
        → silcrete crops out at the basalt edge and on ridge crests that were
          the old valley floors — relief inversion
          → cobbles shed downslope into colluvium and alluvium
```

**The honest gap, stated rather than papered over:** no silcrete is mapped inside the playable
world at 1:100 000; the nearest mapped body is 38.1 km. Three things reconcile that, and none of
them is a fudge. Mapped silcrete bodies are 50–660 m across (median 190 m), below reliable
1:100 000 resolution. The local archaeology 8–15 km away is silcrete-dominant *with a recorded
quarry*, so the material was demonstrably in this landscape. And the **alluvium 0.57 km from the
wake point is mapped explicitly as "polymictic pebble to cobble"**, the colluvial talus at 5.59 km
likewise — transported clasts of everything upstream, which is how Australian knappers mostly got
stone (Flenniken & White 1985, *Records of the Australian Museum* 36:131–151: collected *"from a
secondary deposit such as a river point bar"*).

## The governing rule

> The model may emit a rock whose controls are **geomorphic**. It may not emit one whose controls
> are **stratigraphic**.

Weathering, transport, deposition and induration are functions of the landscape the game already
simulates. Which Triassic bed lies under a given hill is not, and inventing one would be authored
content wearing a geological hat (`GAME_DESIGN.md` §2). Flint is excluded under the same rule from
the other direction: its control here is anthropogenic and dated 1788.

## The transfer test (§3, §48 Level 4)

1. **Stone is where the water put it.** A creek bed carries a different, harder population of
   cobbles than the interfluve 200 m away, because the soft clasts did not survive the journey.
2. **Reading country finds stone.** A player who walks downhill to running water finds knappable
   material sooner than one who searches a ridge at random.
3. **The wake point is workable, and not by exception.** A cutting edge is reachable within the
   visible horizon at the canonical wake point — because the survey says basalt is at 1.6 km and
   the creeks carry silicified clasts, not because anything was placed there.
4. **Poor country stays poor.** The Nullarbor, the Riverine Plain, the North German Plain and the
   Ganges delta come out stone-poor. A model that makes everywhere workable has proved nothing.
5. **Nothing is stratigraphic.** No emitted rock requires knowing a named formation.

## Scope

**In:** the derived province scale; `Silcrete`; bedrock from terrain class and incision; a cover
layer (residuum, colluvium, and a hardness-and-toughness–sorted lag above the trickle threshold);
deletion of the `ReferenceEquals(pick, dominant) ? Shale : pick` fallback, which returns Shale on
16.6% of land points against a 0% table share; first tests for `SurfaceGeology`.

**Out, deliberately:** quartz as a separate material; heat treatment and induration as a property;
a basalt-proximity query; any relocation of the wake point; any special case for it.

## The parts

Five, in order. Each lands on its own, carries its own tests, and leaves the game playable — so
each can be looked at before the next is started. The seams are chosen where the *claim* changes,
not where the code happens to divide.

### E0a — Provinces the size of real map bodies

The derived scale and the hash that uses it. `GeologyScale.cs` with `ProvinceEdgeM` from the
mapped-body density; `Province()` rewritten equal-area, keyed on `d.Y` so rows are exact, with a
domain warp so contacts are not a graticule. The table it feeds is untouched.

**Done when:** province count in the visible disc is within ±15% of `πr²/a² + 4r/a + 1`; the same
site samples identically twice; a world contains tens of provinces rather than one. No new rock
appears that was not already in the table — this part changes *how many* draws a world gets, not
*what* it can draw.

### E0b — The rocks that are actually there

`Silcrete` added with its properties and its sensitivity bands. Flint excluded from emission.
The `ReferenceEquals(pick, dominant) ? Shale : pick` fallback deleted — it returns Shale on 16.6%
of land points against a 0% table share. The emitted set declared beside the model and asserted
**equal** to it, so a rock cannot appear or vanish unnoticed.

**Done when:** the emitted-set test pins the list; the negative controls come out stone-poor and
silcrete-free; `KnappingTests` knows about silcrete. **The gate blocker should clear here** — the
survey puts basalt 1.6 km from the wake point, and at 1414 m cells that is now several provinces
away rather than the same one.

### E0c — Bedrock that follows the land

`GeologySite`; `Bedrock()` blending the class table across soft terrain-class weights and choosing
the exposed bed from incision; `Landform.DenudationAt`; `FlatTerrain.RockColour` reading the same
function the cobbles do.

**Done when:** composition is continuous across a class boundary with the province held fixed
(bound 0.34/100 m); the ground's colour and the stone in the hand agree because they are one call.

### E0d — Transport, which is the real claim

`Cover()`: residuum, colluvium, and above `DrainageNetwork.TrickleM2` a lag admitting only clasts
that pass **both** a hardness and a toughness threshold, its weight rising with `log10(catchment)`.
Wired into `ScatterField`.

**Done when:** a creek bed carries a measurably different and harder population than the
interfluve 200 m away, *and still contains something above `MinimumPoundingQuality`* — a lag that
sorts on hardness alone strips out the basalt, which is the only hammer.

### E0e — Proof that it is playable, not just correct

The composed test that builds the best core and hammer the model emits and drives the real
`Knapping.Strike`. Reachability at the wake point and at the Beach, with distances in the message.
The in-game `GeologySelfTest`. The scenario transect. And a look at province contacts on screen,
because the warp is the thing screenshots catch and tests do not.

**Done when:** a flake comes off stone the world produced, at the wake point, with no test handing
the founder anything.

## Verification

- Headless `GeologyTests.cs`, every stochastic case seeded through `SimRandom.DeriveSeed`. Each
  test states whether it fails against today's code; any that does not is decoration.
- Province count in the visible disc within ±15% of `πr²/a² + 4r/a + 1` — the perimeter term is
  why, and a test that "simplifies" to the area ratio is wrong.
- A composed test that builds the best core and hammer the model emits and drives the real
  `Knapping.Strike`, asserting a usable edge. Nothing else makes the grain size load-bearing.
- Negative controls, which are the ungameable half.
- In-game `GeologySelfTest` building sites out to the valid radius, reporting distances.
- A scenario script: at the Highlands origin, `nearest:Silcrete` and `nearest:Basalt` inside the
  visible horizon; a 2 km transect whose cobble population changes at least three times; a creek
  cell nearer to silcrete than an interfluve 200 m away.
- **Eyeball province contacts in game.** The domain warp is the thing screenshots catch and tests
  do not: unwarped cells align to a graticule and read as a grid.

## Recorded as provisional (§37)

The 1414 m province edge is one survey at one map scale in one region. The silcrete fracture
toughness and conchoidal-fracture figures are estimates; the sensitivity band is stated in
`StoneType` beside them, and the grain-size band is one the conclusion does **not** survive.
`DATA_SOURCES.md` carries all three as gaps.
