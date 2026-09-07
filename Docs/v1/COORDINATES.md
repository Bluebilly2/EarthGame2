# Coordinate Architecture

Binding for all future work. Every slice that stores, compares or renders a position obeys this.

## The premise

**Space has no centre.** Unity needs numbers, but a position only ever means *position relative
to some chosen reference*. Real astronomy works the same way — ICRF, barycentric and geocentric
coordinates are conventions chosen for convenience, never claims about a centre of the universe.
This project does likewise, and says so out loud rather than letting an origin quietly become a
cosmological assertion.

There is **one Earth**. Any second planet, ghost horizon or duplicate surface is a bug.

## Two problems, two mechanisms

They are independent and must not be conflated.

| Problem | Mechanism |
|---|---|
| float32 resolves only ~0.5 m at Earth's radius | **Floating origin** — Unity's zero follows the player |
| There is no universal centre | **Reference frames** — every position is relative to a named body |

The floating origin is a *rendering* concern. Reference frames are a *world model* concern. A
change to one must never require a change to the other.

## The frame graph

Not a space — a graph. Nothing in it means "the centre of everything".

```
Solar System Barycentre          <- an admitted convention, nothing more
 └── Sun
      ├── Earth                  <- position relative to Sun, plus its own rotation
      │    ├── EarthFixed (ECEF) <- rotates with the surface; where the founder lives
      │    └── EarthInertial (ECI)<- does not rotate; where orbits are computed
      │    └── Moon
      └── Mars
```

An entity is `{ FrameId frame, Double3 position }`. Position is meaningless without the frame,
so the two travel together and the type system should keep them together.

**Frame transitions** happen at defined boundaries — leaving the ground for orbit converts
ECEF → ECI once; leaving Earth's sphere of influence converts ECI → heliocentric once. A
transition rewrites position and velocity into the new frame and changes nothing physical.

## Rules

1. **No absolute positions.** A bare `Double3` is a displacement or a direction, never a place.
   A place is a `WorldPoint`: frame plus offset.
2. **One conversion boundary.** Exactly one component converts world coordinates into Unity
   floats for rendering. No other code reads `transform.position` for meaning — only the renderer
   may treat a Unity transform as authoritative, and only for drawing.
3. **The planet centre is not the Unity origin** and must never be assumed to be.
4. **No absolute distance constants.** Every threshold is expressed relative to planet radius,
   chunk angular size, or another scale-derived quantity. A literal in metres that is not derived
   from something is a defect, because it silently encodes one particular world size.
5. **Frames exist even when there is only one.** Today everything is `EarthFixed`. The seam is
   built now regardless, because we already know it will be needed and retrofitting a coordinate
   assumption is what cost this project its worst week.

## Why rule 5 exists

The prototype baked in "the planet centre is the Unity origin" — an assumption we knew would
break at real scale. Migrating from a 2 km planet to 6,371 km then produced seven distinct bugs,
every one of them that assumption leaking out of a place that had not been updated: a fall-through
teleport mixing world and Unity coordinates, fog calibrated for the old radius, an atmosphere
shell pinned to a stale origin, a HUD reading direction from a position that is always near zero,
a camera clearing a buffer another camera had drawn into, ocean tessellation whose chords sagged
30 km, and a speed cap applied before the multiplier that undid it.

None were logic errors. All were leftovers. The lesson is specific and worth stating plainly:
**do not bake in an assumption you already know you will break.**

## Current state and what changes later

**Now.** All gameplay is on Earth. Everything is `EarthFixed`, the planet does not rotate, and
the sun orbits instead — physically equivalent for a fixed observer, and a stated abstraction
(GAME_DESIGN §47: simplification is fine, contradiction is not).

**When orbits arrive (Phase 5–6).** Earth rotates for real and the ECEF/ECI distinction becomes
load-bearing; the sun stops orbiting us. The Moon, Mars and the rest of the Solar System enter as
siblings in the graph. Nothing above changes — that is the point of writing it down first.
