# Calibration — the first day, measured against the figures the body model cites

**Status:** measurement record, produced by MAIN DEV 2 for contract B1, 2026-09-01. Requested by
FABLE on the board: *"a founder at core 31.77 °C after one ordinary day outdoors is severe
hypothermia by the published figures the thermal model cites — either late-winter Beach days are
genuinely that lethal (defensible) or a term is running cold; B1's forecast/tick unification is
where that number gets checked against its sources."*

**It was written as a measurement and nothing in `Assets/**` moved for it** — the conclusions were
escalated to FABLE per the rule, because subtle physics is not the dev's call. All four were then
ruled (see *What was ruled*, below), and one of them — the billing — became a change in the same
set as this file. The measurements here are as taken, **before** that fix; where a figure moves
because of it, the ruling section says so.

## The answer in one line

**Neither.** The loss terms are right — each one matches the referent `PHYSICS_NAKED_HUMAN.md`
names for it, and two of them match to the last printed digit. What killed the founder on day one
was a **metabolic** term: the shivering reserve was spent by mid-afternoon, and from that moment the
body produces 80 W against 215–265 W of loss. The day is not too cold. The founder's furnace was
switched off too early — and that is now fixed, which moves an idle death from 19:50 to ≈21:30
without making day one survivable to someone who stands still, because it was never the cold that
was wrong.

## How it was measured

A scratch console harness (session scratchpad; law 2 keeps it out of the tree) linked against
`CI/Sim/EarthGame.Sim.Headless.csproj` and replicated `Survival.Update` for a founder awake and
standing from the wake instant — 08:00, day 237, 34.5°S, 687 m, naked, nothing built.

**The harness is proven faithful before it is believed.** `PHYSICS_NAKED_HUMAN.md` carries a table
measured inside the running game with `-eg-sky-test`. The harness reproduces **all five rows
exactly** — air, sun, sky and breath at 04:00, 08:00, 12:00, 16:00 and 20:00. It is therefore
reading the same world `Survival` reads, and everything below rests on that.

Step size is not a factor: 10 s, 30 s, 60 s and 120 s steps give an identical hour of death, and a
300 s step moves it by five minutes. This is consistent with MAIN DEV's two player runs at 12× and
90× agreeing.

### Running the suite in a fresh worktree — read this before believing a test count

**A clean worktree cannot run the whole suite, and it does not say so loudly.** `.gitignore:96`
excludes `/Assets/StreamingAssets/*.r16`, but the `.meta` files beside it *are* tracked — so a new
worktree looks complete while `Assets/StreamingAssets/EarthElevation.r16` (16 MB, produced by the
Unity menu item `EarthGame/Data/Fetch Global Elevation`) is simply absent. `EarthElevationTests`
and its neighbours then **skip rather than fail**, and `dotnet test` reports a *successful* run with
a total that is fourteen short:

```
Total tests: 353     Passed: 339          <- worktree without the dataset (14 skipped)
Total tests: 353     Passed: 353          <- with it
```

A skipped test reports as green. Anyone comparing counts across trees — which is exactly what a
reviewer does — will read the difference as a regression or, worse, read their own 339 as a pass.

**The fix is one copy**, and the file stays invisible to git because it is ignored:

```
cp "<main tree>/Assets/StreamingAssets/EarthElevation.r16" \
   "<worktree>/Assets/StreamingAssets/EarthElevation.r16"
```

This is recorded here rather than in a session message because the next person to meet it will be
a fresh reviewer on a fresh worktree, and this is the file they will already have open.

## The two flags, checked

Measured against the model **as it stood when the flags were taken** — i.e. before the billing fix
ruled in (2) below, which was itself a consequence of this measurement:

| | board flag | standing still, open ground | what reproduces the flag |
|---|---|---|---|
| core at 20:00 | 31.77 °C | **dead before 20:00** | ≈50 W activity → 32.02 °C |
| death, day one | 21:53 (13.9 h awake) | **19:50 (11.83 h awake)** | ≈50 W activity → 22:03 (14.05 h) |

**And the same two flags after the billing fix**, because the fix moves the thing the flags measure
and a condition quoted against a superseded model is worse than no condition:

| | board flag | standing still, open ground | what reproduces the flag |
|---|---|---|---|
| core at 20:00 | 31.77 °C | **31.06 °C** | ≈10 W activity (25 W gives 32.94) |
| death, day one | 21:53 (13.9 h awake) | **21:30 (13.50 h awake)** | ≈10 W activity (25 W gives 22:39) |

**The fix very nearly retires the discrepancy that started this.** An idle founder now reads 31.06
at 20:00 against the flagged 31.77, and dies at 21:30 against the flagged 21:53 — 0.7 °C and 23
minutes apart, where before the fix that founder was already dead. The activity needed to close the
remaining gap is about **10 W**, not the ≈50 W the pre-fix model implied.

**Both flags are conservative.** A founder who genuinely does nothing but stand outdoors dies
**two hours earlier** than the board records. The flagged figures describe a founder who *moved*
during the day: about 50 W of sustained activity heat reproduces both numbers together, and that
is a modest amount of walking. So the flags are real readings, and they are the gentle end of the
range rather than the harsh one.

### Copies that must carry the condition

**This file owns the day-one figure, and the figure is only meaningful with the model and the
activity that produced it.** Two copies exist, both in the **main tree**:

| copy | what it says |
|---|---|
| `Assets/EarthGame/Scripts/Game/Sleeping.cs:100` | *"core 31.77 … on a founder who had simply lived the day outdoors"* |
| `Assets/EarthGame/Tests/SleepTests.cs:195` | *"core=31.77 shiver=0.000, on a founder who had done nothing but live the day outdoors"* |

**The condition they need is smaller than the ruling assumed, and this is the correction.** FABLE's
part (3) required the copies to name *≈50 W of movement*, which was right against the model the
flags were taken on. The billing fix ruled in part (2) lands in the same change set as this file and
moves that number: an idle founder now reads **31.06 at 20:00** and dies at **21:30**, so the flagged
31.77 corresponds to roughly **10 W**, not 50. Amending the copies to say "≈50 W" would write a
condition into the tree that the tree no longer produces.

So what the copies need is:

- the figure is a **pre-billing-fix observation** — the model it was read from has since changed;
- under the current model *"simply lived the day outdoors"* is very nearly right, giving **≈31.1 °C
  at 20:00**, and the refusal fires either way because both are far below `WakingCoreC` 35.3, which
  is the only thing those comments are actually about.

**Sequencing — the condition is now MET.** Both are DEV 1's to amend, and they were amendable only
once **this file was on `main`**, because a correction citing a document nobody can read is the
asserted-reference disease that ruling was amended to avoid. That happened with `01758b7`, the B1
merge, which is pushed. So the block is lifted and the amendments ride the first DEV 1 commit from
here. Both copies were still unamended as of that merge — checked, not assumed.

Recorded here rather than left in the ruling because obligations live with the fact
(FABLE, 2026-09-01, adopting REVIEWER's 0-for-2 evidence on passengers): a reader who meets the
figure meets its debts, and REVIEWER can check by grep instead of by remembering.

## The rest of the measurement, as taken before the fix

The three standing states `Survival.cs:233-244` can put an awake founder in were all measured.
Standing in the open is the **best** of them:

| state | exposure | skyView | shade | dies |
|---|---|---|---|---|
| open ground | 1.0 | 0.50 | 0.00 | 19:50 |
| under canopy | 0.3 | 0.15 | 0.85 | 16:35 |
| inside a shelter | 0.3 | 0.00 | 1.00 | 15:50 |

Canopy and shelter are worse because losing 85–100% of a 120–154 W sun costs far more than the
wind and sky they save. That is not a defect — it is the model correctly saying a naked founder
should sit in the winter sun — but it is worth knowing that shade is lethal before noon.

## Each loss term against its published referent

**Sky — exact.** At the Doc's own stated reference case (0 °C air, 15 K depression, f_sky 0.5,
A_eff 0.7 × 1.8 m²) the model gives **39.4 W** against the Doc's *"about 39 W"*.

**Respiration — exact against measurement, and the Doc contradicts itself.** The model reproduces
the Doc's measured table to the digit: **11.0 W** resting at 2 °C and **55.1 W** working at 400 W,
both 13.8% of metabolic rate. The Doc's *prose* claims something different — *"at rest at 0 °C that
is around 20 W"* and *"60–80 W"* working — where the model gives 11.3 W and 55.1 W. The formula is
Fanger's, applied correctly, and the areas do cancel as the code comment says: `C_res` and `E_res`
are both per unit area against a metabolic rate per unit area. The prose figures are ~1 met
(58 W/m² ≈ 105 W whole-body, a *seated* person) while `BasalHeatW` is 80 W, a true basal rate. The
Doc's own results section already recorded 11.0 W and called it *"exactly as it should"*, so the
disagreement is inside the Doc rather than between the Doc and the code. **Minor, and it is not
what kills anyone — the whole term is 9–11 W.**

**Boundary layer — exact where the nights are, conservative where the days are.** Against the
published standing-person coefficients (`h_c = 8.3·v^0.6`, `h_r ≈ 4.7`, which does *not* rise with
wind):

| wind m/s | model W/m²K | published W/m²K | |
|---|---|---|---|
| 0.0 | 9.22 | 8.10 | model 14% high |
| 1.5 | 15.42 | 15.29 | **within 1%** |
| 2.75 | 17.62 | 19.93 | model 12% low |
| 4.0 | 19.35 | 23.77 | model 19% low |
| 6.0 | 21.63 | 29.02 | model 25% low |

1.5 m/s is the night wind, where the model is essentially exact. The day runs 2.75–4.0 m/s, where
the model **under**-predicts loss by 12–19%, because it thins the whole boundary layer with wind
including the radiative half, which physically does not thin. So the real day would be *worse*
than the model says, not better. **The loss side is not overstated anywhere it matters.**

## What actually kills the founder

Hold the shivering reserve at 1.0 and change nothing else, and the same founder on the same day
survives the day **and the entire following night**, sitting between 33.4 °C and 36.6 °C, still
alive at dawn and warming again as the sun comes up. The loss terms are survivable. The reserve is
what is not.

Two things about how that reserve is spent, both in `BodyState.Tick:531-548`:

**1. A reserve documented as *maximal* endurance is spent at *any* intensity.**
`ShiverEnduranceHours = 3.0` carries the comment *"maximal shivering exhausts in about three
hours"*. The drain is `demand * seconds / (ShiverEnduranceHours * 3600)`. Across the whole modelled
day `demand` never exceeds **0.32** — the founder is holding 36.4 °C, shivering gently, never in
distress — and the entire reserve is nevertheless gone by **15:48**, having never once delivered
more than a third of a shiver.

**2. The reserve is billed for shivering demanded, not shivering delivered.** Delivered shivering
is `Shivering01 = demand × ShiverStamina01 × energy`, but the charge is `demand` alone — it ignores
`ShiverStamina01`. So as the reserve empties the body delivers less and less while paying the same
price:

| core | demand | stamina | delivered | billed /h | delivered /h |
|---|---|---|---|---|---|
| 36.5 | 0.292 | 1.00 | 0.292 | 0.097 | 0.097 |
| 36.5 | 0.292 | 0.50 | 0.146 | 0.097 | 0.049 |
| 36.5 | 0.292 | 0.20 | 0.058 | 0.097 | **0.019** |

At a fifth of a reserve the founder is charged **five times** what they receive. Billing the
delivered shivering instead — the only change — moves death from 19:50 to **21:30** and leaves the
reserve never fully spent.

This is the named bug shape wearing physiological clothes: *how much shivering you have done* is
computed from something other than the shivering that was done. Glycogen is burned by the muscle
work actually performed.

**3. And none of it is pinned.** `ShiverEnduranceHours` is referenced by **no test in the
repository** — the same shape REVIEWER found for `WakingShiver` on 2026-09-01. Four tests read
`ShiverStamina01`, and all four assert only that it went *down*. The constant that decides whether
day one is survivable is unenforced, and the demand-versus-delivered billing is untested in both
directions.

## What this does and does not mean for B1

**It does not disturb B1's tests.** `ForecastMatchesTheNightTests` builds a fresh `BodyState` for
every night — 37 °C, full reserve — so the day-one drain never reaches them. The grid tests T2–T4
are unaffected.

**One piece is B1-adjacent and should be watched while writing T3.** The forecast prices phase one
as shivering *flat out* for `ShiverStamina01 × ShiverEnduranceHours` hours
(`BodyState.Forecast:708-710`, `HeatBalance.HoursToHypothermia`), while the lived night bills at
`demand` and produces `demand × stamina × energy`. That is a forecast/night disagreement about
**time**, which is precisely the one thing the contract says they should still be allowed to
disagree about after B1 — but T3's 1.5 °C budget is where it will show up if it is larger than
expected.

## A second finding, turned up by B1's own tests: the durable balance pays for breathing it
## has just stopped doing

Found while measuring why two `OracleTests` moved. It is a **forecast-side** defect, so unlike the
shivering reserve above it sits squarely in B1's territory — but it changes `HeatBalance`'s
meaning, so it is reported rather than fixed.

`HeatBalance.DurableNetW` answers "what is the balance once the shivering stops", and it answers it
as `NetW - ShiveringW`. But `NetW` was computed with a respiratory term priced at the **shivering**
metabolic rate:

```
respiration = RespiratoryW(resting + shiver, air)     // BodyState.Forecast
NetW        = resting + shiver + radiant - loss - sky - respiration
DurableNetW = NetW - ShiveringW
```

Removing `ShiveringW` removes the 350 W but leaves the breathing that 350 W was paying for.
Respiration scales with metabolic rate — that is the whole point of the term, and
`PHYSICS_NAKED_HUMAN.md` says so: *"it scales with ventilation, so it scales with exertion"* — so a
body that has stopped shivering breathes far less.

Measured at 4 °C, 1.5 m/s, 40 kg of bedding:

| walls | LossW | of which breath | breath if resting | DurableNetW | corrected |
|---|---|---|---|---|---|
| 45 kg | 101.7 W | 56.4 W @ 422 W metabolic | 9.6 W | **−29.7 W** | **+17.1 W** |
| 60 kg | 87.6 W | 56.4 W | 9.6 W | **−15.6 W** | **+31.2 W** |
| 80 kg | 78.0 W | 56.4 W | 9.6 W | **−6.0 W** | **+40.8 W** |

**46.8 W of phantom loss in every durable balance**, at every shelter, which is more than the
deficit that condemns the 45 kg night in the first place.

**It shows up as a real forecast/night disagreement.** At 40 kg bedding and 45 kg walls the
forecast gives 7.6 hours to hypothermia against a ten-hour night and refuses it. Ticked through
`BodyState.Tick`, the same night ends at a dawn core of **37.00 °C** — the founder never cooled at
all, because at that insulation the demand for shivering never rose, so the reserve the forecast
spent was never touched. That is a 2 °C error in the direction the contract cares least about
being wrong in, but it is large.

**Why T3 still passed before the fix.** The contract's grid is walls {0, 30, 80} × bedding
{0, 12, 40}, which does not contain the marginal band. Of its 27 nights, 24 were refused, and the
five nearest the bound woke the founder at 36.42, 36.39, 36.38, 36.37 and 36.32 °C against a bound
of 36.50 — inside a tenth of a degree. So T3 was tight rather than vacuous, and it passed honestly.

### Ruled a defect and fixed — and what it actually moved

FABLE ruled it, 2026-09-01, with the principle stated: **every term in a balance prices the same
metabolic state.** `DurableNetW` removes shivering's heat, so it must price respiration at the
durable rate. Breathing for muscle work that is not happening is not pessimism; it is arithmetic
contradicting its own premise, and an oracle lying in the safe direction is still an oracle lying —
it costs the founder the last hour of daylight, every time.

The fix is structural rather than a correction applied at the point of subtraction:
`HeatBalance` now carries **`DurableLossW`** beside `LossW`, and `DurableNetW` is
`RestingW + FireW − DurableLossW` instead of `NetW − ShiveringW`. Two metabolic states, two losses,
each net built from its own. `Forecast` prices respiration twice, once at `resting + shiver` and
once at `resting`.

**Measured after the fix, against the UNCHANGED 1.5 °C budget** (the contract's instruction was to
escalate rather than widen, so the budget stands until the corrected grid argues — it does not):

| | before | after |
|---|---|---|
| grid nights approved / refused | 3 / 24 | **8 / 19** |
| T2 breaches (approved but not survived) | 0 | **0** — lowest approved dawn core **36.37** against a floor of 35.00 |
| T3 breaches (refused but comfortably alive) | 0 | **0** — warmest refused dawn core **36.32** against a bound of 36.50 |

The direction that mattered was T2's: the forecast became **less** pessimistic, so the risk was a
night newly approved and not actually survived. There are none, with 1.37 °C of margin. And the
five nights that used to sit a tenth of a degree under T3's bound are now **approved** rather than
refused, which is the correct answer for them — so the fix made T3 less strained, not more.

**The two `OracleTests` masses came back, as this file predicted they would.**
`AGoodShelterFlipsTheNightToSurvivable` returns to **45 kg** of walls — durable net **+17.1 W**, a
lived dawn core of 37.00 — after B1's continuous sky had pushed it to 60. Measured on the way back:
30 kg survives on shivering alone (13.9 h, durable net −12.5 W), 40 kg is the first durably covered
(+9.8 W), 45 kg has margin. `AndItChangesItsMindWhenTheShelterIsBuilt` moves **down to 30 kg**
instead, because 45 kg is now *covered* and reads "The night is covered" — that test exists to
check the middle verdict, "survivable, and cold", and 30 kg is the state its own comment describes.
Two shelters now, each saying what its test is named for.

The round trip is the point worth keeping: a threshold moved twice in one day, both times because a
term got more honest, and the two moves pointed opposite ways.

## What was ruled (FABLE, 2026-09-01) — and what this file now records

The four questions above were escalated rather than answered here. All four are ruled, and this
section is the outcome so the next reader meets the decision, not the open question.

1. **Day one IS lethal to a founder who stands still, and stays so — canon.** The cold is this
   game's first enemy from the first minute, not from dusk. The numbers above are the argument for
   it rather than against it: every counterplay is real and measurable. The sun is worth standing
   in (shade kills by noon — the model correctly orders a naked founder into winter sun), ≈50 W of
   movement buys two hours, and a fire ends the question. With the billing fixed (2), an idle
   founder reaches ≈21:30 — late enough that dark arrives first and the tablet's warning has
   meaning, harsh enough that idleness is never a strategy.
2. **The billing is ruled a defect and is FIXED.** `BodyState.Tick` now charges the reserve for
   `Shivering01` — the shivering **delivered** — instead of `demand`. Glycogen burns with the muscle
   work performed. Two consequences worth recording: an idle founder's death moves 19:50 → ≈21:30,
   and `ShiverEnduranceHours` starts meaning exactly what it has always said. The decay is now
   exponential rather than linear, and the integral of delivered shivering over the whole life of
   the reserve is **exactly** the endurance at any intensity — three hours of maximal shivering in
   total, spent fast or slow. The old linear charge delivered less than that whenever demand was
   below full, which was almost always.
3. **`ShiverEnduranceHours` is pinned at last**, in both directions, in `BodyTests`:
   `TheShiveringReserveIsChargedForWhatItDeliversNotForWhatIsWanted` holds the billing (a drained
   body must be charged proportionally less at equal demand; sabotage is swapping the term back),
   and `TheShiveringReserveIsThreeHoursOfMaximalShiveringHoweverItIsSpent` holds the constant
   against a **literal** three hours, so moving the constant reddens it rather than moving with it.
4. **The durable balance's respiratory term: accepted and watched, not acted on.** The
   forecast/night time-disagreement is the one disagreement contract B1 permits, and **T3's budget
   is where it shows** if it is larger than believed. The endurance curve's linearity is partially
   self-corrected by (2) and is re-examined against literature **only if T3 argues**. The
   respiration prose contradiction inside `PHYSICS_NAKED_HUMAN.md` is corrected there as a
   one-liner.

## Sources

- `Docs/PHYSICS_NAKED_HUMAN.md` and the sources it lists, which are the referents used above.
- Convective coefficients for a standing person, `h_c = 8.3·v^0.6`; radiative `h_r ≈ 4.7 W/m²K`
  (PubMed 9195861, as cited in `PHYSICS_NAKED_HUMAN.md`).
- Fanger / ISO 7933 respiratory terms, as implemented in `BodyState.RespiratoryW`.
