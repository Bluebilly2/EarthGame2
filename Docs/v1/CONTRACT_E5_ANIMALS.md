# Contract E5 — animals: what the plants support

**Status: binding contract, written before code. Author: FABLE, 2026-09-01, closing the chain
`ECOSYSTEM.md` opened ("E5 is the direction, and gets its own contract when it is reached" —
it is reached: E1–E4 done, weather merged, nights honest). Implementation: MAIN DEV 2, on
`track-sim`, Sim-only. Review: REVIEWER, against this document. The owner has ruled animals
into the beta ("both in", FOUNDERS_PATH answers, 2026-09-01), so this work is on the path
under either queue.**

## The thesis

The sixth link obeys the same law as the other five: **nothing is placed.** An animal put down
by a spawn table is vegetation placed by a biome lookup — the same mistake at the last level.
Animals are what the plant communities can support, where they can reach, when their kind is
awake. And like the weather (`Synoptic`'s law): **presence is a stateless seeded function, not
simulation state** — what you see at a place and hour is *derivable*, so the tablet can know
it, a test can replay it, and the save file never carries it.

## E5a — the population layer (capacity from the plant chain)

**(Amended 2026-09-01, DEV 2's layering dispute upheld — verified at source by FABLE:
`PlantSite` is a Sim struct, but only Scripts assembles it from world coordinates
(`ScatterField.cs`, via `Landform`), and `SoilModel` is cell-indexed with no world origin. So
"capacity at a region coordinate, Sim-only" was not jointly satisfiable, and the fix is the
E4 seam itself, not a second sampler.)**

Per species, a **carrying capacity as a pure function of `PlantSite`** —
`AnimalCapacity.PerKm2(species, in PlantSite site)` — grass and forb-bearing sites feed
grazers, browse-bearing sites feed browsers, wetness and cover modulate. `PlantSite` is
already "everything about a square metre that decides what will grow on it"; capacity is the
next sentence — what grows there decides what it feeds. Deterministic from the site; never
saved. **The world→site binding stays where it lives** (the Scripts side owns it and supplies
sites, exactly as E4's scatter does); binding capacity to coordinates later is purely
additive and happens at that existing seam, not by a copy of the sampling.

**The species are real, and the time is pre-human (the climate ruling, `CONTRACT_W1` §pre-human
baseline).** Slice one carries four, chosen to span the niches at 34.5°S 150.4°E:

| species | niche | rhythm | grouping |
|---|---|---|---|
| Eastern Grey Kangaroo | grazer, open woodland and flats | crepuscular | mobs |
| Common Wombat | grazer, gully margins; burrows | nocturnal | solitary |
| *Diprotodon optatum* | megafauna browser, woodland | diurnal | small herds |
| *Genyornis newtoni* | large ground bird, mixed feeder | diurnal | pairs/small groups |

Slice one computes **equilibrium capacity, not dynamics** — the population the vegetation
would hold, not the year-by-year eating and dying that holds it there. That simplification is
the slice boundary stated in the open, not a fake: dynamics (growth, drought response,
predation — *Thylacoleo* waits here) are E5c, later, and they replace the equilibrium with the
thing that produces it.

**Calibration anchor, in the Moss Vale manner:** Eastern Grey density in good SE-Australian
woodland is reported roughly **10–30 / km²**; capacity must land inside that band on a site
shaped like the wake region's best habitat and fall toward zero on a site shaped like a bare
crest. Megafauna land at elephant-order sparseness (**< 1 / km²**). The test asserts the
bands; the contract owns the numbers. **(Flanks cited 2026-09-01, DEV 2's upgrade, partial
and honest: NSW inland plains aerial survey 3.18 / km² at the poor end; Coffs Harbour
periurban 20–490 / km² at the artificially-fed high end, *Australian Mammalogy* AM17010. No
direct woodland figure found and none claimed — the band stays the contract's, with its
flanks cited and the gap named.)**

## E5b — ambient presence (the Synoptic pattern, on legs)

Given (capacity, hour, seed, position-as-seed): **which individuals are near, where they
stand, and what they are doing** — a pure function in which position enters only to vary the
draw, never as a terrain query (amended 2026-09-01 with E5a: terrain reaches presence only
through the capacity a supplied site produced), consistent with E5a (expected sightings
integrate to the density) and with each species' rhythm on the same clock the nights already
run (wombats
after dark, roo mobs at dawn and dusk on the flats — the same hours the founder is coldest,
which is the kind of coincidence this game is made of). Movement in slice one is unhurried
seeded drift along the presence function — enough for a mob to graze across a flat over an
hour — not steering, not pathfinding, not reaction to the player.

The Scripts side (rendering, LODs, sound — `Ambience.cs` will want them) is **not in this
change set**; the Sim exposes queries and dev-1 draws on them later. The oracle reads the same
functions when the tablet grows a fauna page — never a second derivation.

## Rules

- Sim-only; no UnityEngine; deterministic from (seed, region, time). No new save fields —
  presence is derivable, and that is the point.
- Law 6/8 as always: every capacity input and rhythm window sabotaged once (a wombat abroad at
  noon must redden a test, not raise an eyebrow).
- Tests to hold it to: capacity tracks E4 cover (she-oak crests near zero, grassy flats in
  band); determinism (same inputs, same mob, twice); rhythm (activity mass inside each
  species' window); integration (sightings over a long walk ≈ density).
- The `ECOSYSTEM.md` test governs: someone who can read country should be able to say "roos
  will be on that flat at dusk" — and be right, because it is a consequence, not a rule.

## Explicitly out of scope

Population dynamics and predation (E5c); hunting, threat, or any player–animal interaction;
individual animal state or persistence; rendering, animation, sound; the tablet's fauna page.

## Acceptance

Headless suite green with new tests in the floor count; the four species' capacities on
representative sites match the bands and the vegetation the sites describe (built the way
`FoodTests` builds sites); presence queries replay byte-identically under a fixed seed;
rhythm sabotages red; REVIEWER passes on `track-sim`. **Rider, deferred to the binding:**
when the Scripts side supplies real sites, the capacity field on the actual wake region gets
the E4-style measurement (the she-oak-98% manner) as its own later verification — owed, not
waived.
