# Feel review, 2026-09-03 — what the owner's screen showed

**Source:** eight captures of the owner's window at 17:16 (1936×1119, the game paused at Day 1
11:49, 4 minutes played, nothing carried), read by FABLE. The first time anyone but the owner
has looked at the running game since the placeables, the sound layer and the dawn summary
landed. The owner's words that prompted it: *"it still feels roughly thrown together… looking at
the codebase does not paint the picture of why it isn't good."* He is right on both counts, and
this file is the picture.

## What the frame shows

1. **The world reads as a terrain demo, not a place.** Low-poly green hills under a flat
   grey-blue sky with no sun, no cloud, no horizon haze gradient. The distant ridges are a hard
   pale line. Nothing says "late-winter morning on the NSW south coast"; the light has no time
   of day.
2. **The rocks are confetti.** Hundreds of identical grey polyhedra at one scale, spread evenly
   across every slope with no clustering, no bedding, no size variety, no contact shadow. They
   are the first thing the eye lands on and they say "scatter noise", which undoes the geology
   underneath (which is real, and computed).
3. **The trees are dark specks.** Tiny, sparse, no canopy shape, no shadow; the hills look bald.
4. **The tablet is unreadable.** A large black slab tilted at ~30°, its text rotated with it,
   small, dark grey on black, partly off-screen: "5.7 h of light", "A night that does not kill
   you / something between you and the ground", "37.8 °C, well". The one object the whole
   design rests on (the oracle whose every "no" carries its mechanism) looks like a placeholder.
   The content is right; the presentation makes it look like debug text.
5. **No body, no scale.** No hands in view, no crosshair line naming what the button would do,
   nothing at human height to size the world by. The camera feels detached.
6. **The pause menu is fine.** Clean, legible, the one screen that reads as finished.

## Why the code could not show this

Every item above is true of a build whose suites are green. The Sim is correct (693 tests) and
the Scripts compile clean; none of that measures whether light, scale, legibility or rhythm land
on a person. The house rule already says it: *visual changes need recorder frames or the owner's
eyes*. The machine never had eyes; the seats that did are retired; the PC seat cannot yet read
the VPS board. So for three days the tree improved and the game did not visibly.

## The items this frame yields (Scripts track, ordered by how much of the feeling each removes)

| # | Item | Verified by |
|---|---|---|
| F1 | **The tablet as a readable page**: text faces the camera, one column, large type, light on dark, a page edge, the slab no longer rotated with its text; the forecast, the body line and the "what you can attempt" list laid out as a page, not a dump | frames at 1920×1080 and 1280×720 |
| F2 | **Sun, sky and time of day**: a sun disc, a sky gradient by hour, warm low light at dawn/dusk, the distant ridges faded into haze | frames at dawn, noon, dusk |
| F3 | **Rocks that belong to the ground**: size variety, clustering along the geology's contacts and creek lines, contact shadow, far fewer in the open | frames, and the geology census unchanged |
| F4 | **Trees with shape**: canopy silhouettes and shadows at the density the plant model already computes | frames |
| F5 | **A body in the frame**: hands at rest and at work, the held item visible, the crosshair line always naming the verb | frames, and the controls scenario |
| F6 | **Feedback on the thing**: flakes fall when knapping, bark comes away in strips, debris lands where placed (FOUNDERS_PATH's feedback row) | frames |

None of these change a rule. All of them need eyes: the owner's, or a PC seat with the recorder.

## Addendum, 18:49 — the First Light baseline, and a correction

`CI/board/tests/firstlight.test.md` recorded the wake beach at 06:19, 12:01 and 17:49 in a
sandbox (eleven frames, kept in `Docs/frames/firstlight-baseline-2026-09-03/`). It corrects
item 1 above in part: **the world has a sky model and a sun disc**, and the dawn and dusk
gradients (orange band on the sea, purple above, the hills silhouetted, the hand in shadow at
dusk) are already good. The paused frame at 11:49 was under 52% cloud, looking down.

What the baseline shows is missing, in the order M1 should take it:

1. **No shadows at all.** The hand, the slab, the trees, the dune: nothing casts one at any
   hour. This, more than the sun, is why the scene reads flat.
2. **No haze with distance.** The far hills are as sharp and saturated as the near sand; the
   horizon is a razor line.
3. **The sea is two flat bands** meeting the sand in a straight edge: no shore break, no foam,
   no wet sand, no wave line.
4. **The sand is a flat cream plane with dark ovals scattered across it** at every distance,
   the same size, reading as shadow decals of nothing. They are `GroundCover`'s litter patches,
   each drawn as one flat faceted disc coloured per renderer: the litter model is real and its
   drawing says "stain". The ground-underfoot frame at noon is a blank surface with those
   ovals and nothing else: no grain, no strands, no wrack line.
5. **Vegetation as specks** on the dune, the same green at noon and dawn.
6. Unchanged from above: the tablet slab, the HUD's developer block, the fist without an arm.

The M1 item is therefore shadows, haze, the shore, the sand surface and the vegetation, in
that order; the sun and sky are not the work.

## Addendum, 19:40 — the first M1 commit, judged by its frames

The PC dev seat landed a644ba3 ("The sun casts, and the far air thickens with the weather
that is actually blowing"): shadow casting turned on for the hand, held item and tablet views,
one owner for fog density, the sky reading Survival's weather instead of a private Climate.
Built from its branch and recorded with the same scenario (kept in
`Docs/frames/firstlight-after-a644ba3/`), the after-frames are indistinguishable from the
baseline: no shadow under the fist or the slab at noon, none from the trees at dawn, the far
ridge as sharp as before. Frame time median 6.0 ms. The code may be right and something
downstream eats it (the sandbox player's quality level, the held-item camera's layer, the
terrain shader's shadow receive, a haze too thin at the Sim's humidity). Item 1b asks the seat
for the numbers. It does not merge until the frames move: that is the slice law working as
written, on the first commit it was written for.

## The loop that fixes the feeling

Play (owner, F9 recording on) → FABLE reads the frames and telemetry → items like the table
above → a PC seat or FABLE lands them → frames again. The Sim track can run unattended; this
track cannot, and pretending otherwise is how a green tree became a game that feels thrown
together. Next machine item: the PC seat reads the VPS board, so Scripts items have a worker.

## Addendum, 20:05 - one sun, and the frames move

The 1b commit (567c027) put the graphics rig into the recorder's log beside every frame, and
that line settled it: `shadows=All dist=300m cascades=4 ... sun=Soft strength=0.80 ...
ambient=Trilight` on a frame with no shadow in it. The configuration was right and the drawing
was wrong (`Docs/frames/firstlight-after-567c027/`: unchanged from the baseline).

The cause was one line in `ItemIcons.EnsureRig`: the hotbar-icon key light is a directional
light at intensity 1.15 with the default culling mask, ten kilometres underground, and a
directional light does not care where it stands. It lit the whole world, brighter than the sun
at any hour (FlatSky peaks at 1.25 clear and is 0.35 at dawn). In the built-in forward path the
base pass, and the shadows, go to the brightest directional light; every other one is an
additive pass, and the terrain's additive pass has no shadow variant. So the sun's shadows
were computed for a pass that could not draw them, and everything was lit from over the
viewer's left shoulder at dawn and at noon alike. That was the flat look.

fc3a86b gives the icon stage its own layer and keeps the suns and the player camera off it.
Built and recorded with the same scenario (`Docs/frames/firstlight-after-fc3a86b-one-sun/`,
sandbox run 2026-09-03-2001-57-Beach; the rig line now reads `mask=0xBFFFFFFF`):

- **Noon, the ground underfoot:** the stick casts a shadow across the sand; the fist is lit
  from the sun's side; the sand has a direction to its light.
- **Dawn, 06:20, looking west:** twilight. Sun intensity 0.18, the hills silhouetted, the
  ground dark, the sky's gradient carrying the hour. The first frame in this file in which
  dawn is not noon.
- **Dusk, 17:49:** just after sunset and nearly black looking east. Honest, and too dark to
  play in without an exposure model: the eye adapts and the renderer does not. That is a
  new item (F7, an exposure/adaptation curve for twilight), not a reason to put the second sun
  back.

Frame time median unchanged. The merge waits on the second key (REVIEW-DEV on the commit) and
on the 567c027 findings being fixed on track-dev, as the gate says; the frames are kept here
so the morning read has the picture whichever way the ledger goes.

## Addendum, 20:25 - the eye adapts (7676584), judged by its frames

The dev seat's F7 commit gives PostGrade an adaptation model: a scene-brightness proxy from
the sun's incidence on level ground plus the ambient equator luminance, a target exposure
0.92..3.26, and a lag of 2 s opening / 6 s closing. Built from track-dev and recorded
(`Docs/frames/firstlight-after-7676584-eye/`, sandbox run 2026-09-03-2021-25-Beach; the rig
line now carries `eye exposure= target= proxy=`):

- **06:20:** legible for the first time with one sun: the hills, the grass, the hand. The sky is
  lifted with everything else and reads pale grey-pink, closer to an overcast morning than to
  the pre-dawn it is; the stars survive faintly. The gain's ceiling (3.26) is a touch high, or
  the sky needs to take less of it than the ground.
- **17:49 looking east:** the dusk gradient kept, the sand dark brown, the hand lit, and the sea
  a bright cyan band, the brightest thing in the frame, which is item B3 exactly.
- **Noon:** logged `exposure=1.09 target=0.92` on the frames shot seconds after the clock was
  wound from 06:20: the lag had not finished closing, so the clock-step snap the commit
  describes did not fire for the scenario's `clock` verb. Noon is 18% brighter than the
  untouched frame it claims to be. Item F7b.

The merge waits on the second key (REVIEW-DEV on 7676584) and on 1f and the rig-layer commit.

## Addendum, 2026-09-04 07:35 - the morning after the night without the PC

track-dev's tip c86ceda (the whole First Light chain, the menu fix S1 and the crosshair
hysteresis C1, the last two landed by the VPS pair overnight) built and recorded
(`Docs/frames/firstlight-after-c86ceda/`, sandbox run 2026-09-04-0731-56-Beach):

- **06:20:** a pre-dawn at last. The sky dark with the glow low in the east behind the camera,
  the hills silhouetted with their green just readable, the ground dark and legible, the hand
  visible. F7b's shoulder took the wash out of the sky that F7 put in.
- **17:49 looking east:** the sea is a dark band under a faint horizon glow. B3 is closed in the
  frame as well as on the ledger.
- **17:49 looking west:** the dusk gradient kept, the ridge silhouetted, the near ground legible.
- **Noon:** every noon shot logs `exposure=1.09 target=0.92 gain=1.19` for many seconds after the
  wind, so this is no longer lag: at proxy 1.019 the gain is 1.19 where it must be 1.00, and
  noon is 19% brighter than the untouched frame the item wants. The clock-moment shot
  (`step=346m`) snapped correctly. Item F7c carries it, with REVIEW-DEV's findings on F7b.

The merge into main waits on F7c's PASS; everything else at the tip carries a PASS or a
superseding PASS.

## Addendum, 2026-09-04 08:50 - the tablet's type (T1, 61c5040 on track-ui), judged by its frames

The PC's visual pair landed T1: the page's type now comes off the same ramp as the HUD, scaled by
the slab's cover of the frame, with the texture stepped to the size the slab is seen at.
Built and recorded (`Docs/frames/tablet-after-61c5040-T1/`, run 2026-09-04-0843-26-Beach): the
glance on the lowered slab reads at HUD size for the first time ("9.5 h of light / A night that
does not kill you / something between you and the ground / 37.0 C, well"), light on dark. Two
things remain, both T2's: the slab is still tilted 30 degrees whether "consulted" or not, so the
words run off its right edge at the lowered pose ("A night that does" / "something between"), and
the raised reading pose that shows the whole page does not exist yet. T1 passes by the frames on
what it claimed; the overflow is the pose, not the type.

## Addendum, 2026-09-04 08:56 - the tablet's pose (T2, 8d50481 on track-ui), judged by its frames

Built and recorded (`Docs/frames/tablet-after-8d50481-T2/`, run 2026-09-04-0851-32-Beach). The
reading pose exists and is right: the slab square to the camera, upright, its top a third down the
frame, the page's type at HUD size ("Light left: 9.5 h", "TONIGHT", the night's arithmetic). Three
faults, all posted as T2b: the page is drawn a third of the glass to the right and cropped at its
edge (blank slab on the left, every line cut on the right: the reviewer's T1 finding about the
page scale cached against the target, or T2's own mapping of the page onto the raised glass); the
lowered glance regressed to twice T1's size, cut on every line; and the recorder's tablet-opened
shot fires mid-ease, so it shows the lowered pose. The pose is the hard part and it is done; the
mapping is arithmetic.

## Addendum, 2026-09-04 09:02 - the HUD's developer blocks (T3, e9291ba on track-ui), judged by its frames

Built and recorded (`Docs/frames/hud-after-e9291ba-T3/`, run 2026-09-04-0859-32-Beach). The
seven-line block is gone. What remains top-left is one quiet line, "Day 2 12:01 - 5.5 h of light
left", with no rectangle behind it, and at 06:19 a second line in the warning colour, "Body
36.9 C (warm) - hypothermia in about 0.6 h", which is the one fact the founder must not glance
past. The controls hint is two lines without a rectangle. The frame reads as a game, not as a
debug view; item 6 of the first list ("the HUD's developer block") is closed. What the scenario
cannot judge: the hint's fade after 90 s and its return after 60 s idle (the scenario is 40 s
long); the owner's eyes or a longer recording. The tablet's faults are T2b's, unchanged here.

## Addendum, 2026-09-04 09:42 - the tablet is a page (T2b, 4160ca8 on track-ui)

The seat found the one bug under four findings: IMGUI drawn into a redirected render texture
keeps the window's pixels, so the scale matrix that T1 introduced drew the page at 1.4x and
shifted it off the glass. With the matrix gone and one rectangle for the page and the glass,
the frames (`Docs/frames/tablet-after-4160ca8-T2b/`, run 2026-09-04-0939-17-Beach) show the
thing the design rests on: the raised page fills the glass edge to edge, two tabs ("what you
can attempt", "what happened, and why"), "Light left: 9.5 h", TONIGHT with the night's whole
arithmetic (621 W short, 9.1 C an hour, 0.4 hours to hypothermia against 13 h of darkness,
shivering 350 W for 3.0 hours, the ground at 20 W/m2K, the sky at 16 K below the air), a
scrollbar, "put it away", light on dark at HUD size. The lowered glance is two lines that fit
the slab. Item 4 of the first list (the tablet unreadable) is closed by the frames; what
remains on the branch is words (T1c) and the hint's key seam (T3b), and the consult shot is
still taken a few frames before the arm arrives.

## Addendum, 2026-09-04 10:08 - the hint through the seam (T3b, 2d6079d on track-ui)

Built and recorded with the controls, firstlight and tablet scenarios (run
2026-09-04-1001-30-Beach; `Docs/frames/hud-after-2d6079d-T3b/`). The controls scenario now
asserts the hint's silence and return through the Keys seam (20/20). By the frames: the quiet
clock line reads on the bright 08:00 sky and the warning line ("Body 36.8 C (warm) shivering 6%
- hypothermia in about 0.8 h") reads on the dark 17:49 sky, no rectangles, one owner of contrast.
The two-line hint is still up at 17:49 because the scenario's real clock is under 90 s; that is
the rule working. Passes by the frames; the reviewer's verdict on the words is pending.

## Addendum, 2026-09-04 17:08 - the menus, photographed by the machine (S2, eb1f5c6 on track-ui)

The shell scenario drives the title, New game, Settings and Controls screens and photographs
each (`Docs/frames/shell-track-ui-eb1f5c6/`, run 2026-09-04-1658-35-Beach, 37/37 on the PC).
The title frame is the first machine witness to S1: New game, Continue, "No saved games yet"
and Settings inside a scrolling panel, Quit pinned below it, nothing cut. The owner's "no back
buttons anywhere" of last night is closed by frames. Two things the same run shows: the first
`screen` moment fires while a world is playing (a beach photographed as "the shell is showing
none"), which S2b fixes, and the reviewer's finding that the shell's Escape now runs through
the Keys seam without the four places that said otherwise being rewritten (also S2b).
