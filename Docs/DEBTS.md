# Debts

Known, owned, dated. A debt is recorded here the moment it exists; there are no TODO comments in code. An entry
leaves this file only by being paid (with the commit that paid it) or by an owner ruling that retires it.

| Since | Debt | Owner | Pay by |
|---|---|---|---|
| 2026-09-07 | **Collision divergence at cliff edges, rock platforms and the water's edge.** The client's PhysX Terrain collider interpolates between height posts while the server's heightfield clamp samples the posts, so the two disagree where the ground is steepest or where wading begins; Steamers Head, the platforms and the lake approach are all in the Bherwerre box. False-positive movement corrections there are a named defect class, never noise, budgeted by N2 (≤ 2 per minute on the divergence segment, no displacement over 1.0 m). | Claude | M1.5's walk scenario carries the segment; the budget is measured at M1.B and again at every gate after it. |
| 2026-09-07 | **Bherwerre's iron is thin.** No bedded ironstone, coal or limestone in the box; the in-world iron path is bog iron at the swamp margins, ferruginous sandstone bands, lateritic gravel or ilmenite-rich black sand (a poor bloomery feed). | Claude | Decide at M2's end: a bog-iron mechanic, or the bloomery as a second-world goal (Kiama–Saddleback). |
| 2026-09-07 | **No middens on a pre-human Earth.** Every "abundant shell" claim about this coast rests on 6,000 years of Aboriginal midden accumulation; shell for lime must be gathered live off the platforms. | Claude | Budget the lime step of the ascent accordingly when it is contracted; never paint middens into the terrain. |
| 2026-09-07 | **Station normals are hand-pulled.** The Bureau of Meteorology blocks scripted fetches; the 068034 and 068072 monthly normals were not retrieved by the site research. | William (verifier lane) | Before the climate step is calibrated (M2): download the station CSVs by hand; `station_table.py` parses them. |
| 2026-09-08 | **The UI Toolkit tablet spike was not run in M1.0.** The one-day timebox that proves a scrolling page renders under UI Toolkit was not spent; the tablet's page design (a screen-space page with a diegetic prop) still rests on the plan's reading of the docs, not on a rendered page. | Claude | Before the tablet's first page is contracted; the timebox stays one day, and a failed spike reopens the page-versus-world-space decision in `ARCHITECTURE.md`, not in code. |

## Paid

| Since | Debt | Paid |
|---|---|---|
| 2026-09-07 | The template's sample content in `Unity/Assets` (TutorialInfo, Readme, SampleScene, the mobile render assets). | 2026-09-08, removed in M1.0 before the first commit. |
