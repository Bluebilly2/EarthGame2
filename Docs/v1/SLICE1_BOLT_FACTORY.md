# Slice 1 — The Bolt Factory (Simulation Architecture Proof)

Implements `Docs/GAME_DESIGN.md` §45: manufacturing process capability → component property
distributions → metrology/inspection → assembly → emergent system reliability → causal failure
propagation → "why did it fail?" explanation. Playable inside the existing planet prototype.

This document is the **binding contract** for all writers, exactly like `ARCHITECTURE.md` was for
the prototype: every file path, namespace, signature, constant, and formula stated here is
mandatory. Read `Docs/GAME_DESIGN.md` first (esp. §7–§30, §45–§49); it governs intent.

## Hard constraints

- **The simulation core is pure C#**: namespace `EarthGame.Sim`, in its own assembly
  `EarthGame.Sim` with **no UnityEngine references** (`"noEngineReferences": true`). Use `System`,
  `System.Math`, `System.Collections.Generic` only. Deterministic, inspectable, unit-testable
  (GAME_DESIGN §34, §44). Doubles throughout; SI units (m, Pa, N, N·m, °C); no `System.Random`,
  no `DateTime`.
- Determinism: all randomness flows from `SimRandom` instances seeded explicitly. Same seed ⇒
  bit-identical results. No statics holding mutable state in the sim assembly.
- §20 discipline: deterministic events never roll dice (overload beyond strength ⇒ certain,
  immediate failure). Stochastic physical variation uses distributions. Epistemic uncertainty is
  represented by keeping **actual** fields separate from **measured/known** fields (§14, §26).
- Every produced artifact (batch, bolt, joint, failure) carries `CausalEvent` provenance so
  failures explain themselves (§12, §23).
- Unity-side code (Assembly-CSharp) may reference `EarthGame.Sim` freely (auto-referenced); the
  sim assembly must never reference Unity code.
- C# 9 compatible; no `async`, no LINQ in hot loops (fine in reporting); one public top-level
  type per file, filename = type name; XML doc comment on every public type.

## File layout

```
Assets/EarthGame/Sim/EarthGame.Sim.asmdef
Assets/EarthGame/Sim/Core/SimRandom.cs
Assets/EarthGame/Sim/Core/NormalDist.cs
Assets/EarthGame/Sim/Core/SampleStats.cs
Assets/EarthGame/Sim/Core/CausalEvent.cs
Assets/EarthGame/Sim/Core/CausalLog.cs
Assets/EarthGame/Sim/Materials/MaterialSpec.cs
Assets/EarthGame/Sim/Materials/MaterialBatch.cs
Assets/EarthGame/Sim/Manufacturing/LatheState.cs
Assets/EarthGame/Sim/Manufacturing/MachiningProcess.cs
Assets/EarthGame/Sim/Manufacturing/HeatTreatProcess.cs
Assets/EarthGame/Sim/Components/Bolt.cs
Assets/EarthGame/Sim/Metrology/Instrument.cs
Assets/EarthGame/Sim/Metrology/InspectionPlan.cs
Assets/EarthGame/Sim/Metrology/InspectionResult.cs
Assets/EarthGame/Sim/Assembly/BoltedJoint.cs
Assets/EarthGame/Sim/Assembly/FlangeAssembly.cs
Assets/EarthGame/Sim/Testing/PressureTestRig.cs
Assets/EarthGame/Sim/Testing/CycleTestResult.cs
Assets/EarthGame/Tests/EarthGame.Sim.Tests.asmdef
Assets/EarthGame/Tests/SimDeterminismTests.cs
Assets/EarthGame/Tests/ProcessCapabilityTests.cs
Assets/EarthGame/Tests/MetrologyTests.cs
Assets/EarthGame/Tests/ReliabilityTests.cs
Assets/EarthGame/Scripts/Workshop/WorkshopController.cs      (Assembly-CSharp, Unity side)
```

Plus three small **edits to existing files** (Workshop writer only):
`GameBootstrap.cs` (create Workshop object, step between HUD and the end),
`FirstPersonPlanetController.cs` (respect `InputSuspended`),
`HUDController.cs` (add "B workshop" to the controls hint string).
No other existing file may be modified by anyone.

## Domain spec (GAME_DESIGN §35 format)

**Inputs:** steel stock (MaterialSpec), furnace target temperature, lathe state (tool wear,
lubrication), torque wrench target, test pressure.
**Processes:** heat treatment (batch strength), machining (diameter/surface finish), measurement,
inspection, assembly (preload), cyclic pressure testing.
**State variables:** per-batch strength multiplier + furnace deviation; per-bolt actual diameter,
surface roughness Ra, yield/UTS, fatigue life multiplier, latent gross defect; measured diameter
(nullable), accepted flag; per-joint preload, accumulated fatigue damage, failed flag.
**Failure mechanisms:** immediate overload (deterministic), high-cycle fatigue (Basquin + Miner,
Goodman mean-stress correction), gross-defect early failure, cascade via load redistribution.
**Measurements:** caliper (σ 20 µm), micrometer (σ 2 µm), thermocouple (σ 3 °C) for furnace
calibration; all measurements = actual + N(0, σ_instrument).

### Error budget (§37) — intentionally abstracted in this slice
Thread mechanics beyond stress area + Kt; thermal expansion; torsional preload stress; joint
stiffness beyond constant C; corrosion/creep; low-cycle plasticity; gasket behavior (leak =
2 failed joints). Target: correct *directions and orders of magnitude*, Level 3–4 realism —
better process ⇒ measurably better reliability, failures explainable, no conservation
violations. Document any further simplification you make with a code comment stating the
abstraction.

## Fixed engineering model (constants are binding)

- Bolt: nominal M10×1.5, nominal shank/thread diameter `d = 10.0e-3` m, tensile stress area
  `As = 58.0e-6` m². Machining alters actual diameter; effective stress area scales
  `As_eff = As * (d_actual/d)^2`.
- Steel (`MaterialSpec.MediumCarbonSteel()`): density 7850, E 200e9, yield ~ N(640e6, 25e6) Pa,
  UTS ~ N(800e6, 25e6) Pa (per-part, correlated: sample one z for both), fatigue strength coef
  `σf' = 1.5 * UTS_actual`, Basquin exponent `b = -0.09`, thread stress concentration `Kt = 2.6`.
- Heat treat: batch `StrengthMultiplier = clamp(1 + (T_actual − 850°C) * (−0.002)/°C, 0.55, 1.15)`
  where `T_actual = target + CalibrationErrorC + N(0, TempSigmaC)` sampled once per batch.
  Defaults: target 850 °C, `TempSigmaC = 4`, initial `CalibrationErrorC` sampled N(0, 12) at
  process creation (latent!). `Calibrate(Instrument thermocouple, ...)` sets
  `CalibrationErrorC = N(0, instrument σ)` and logs a CausalEvent.
- Machining: `σ_diameter = 8µm + 30µm*ToolWear01 + (Lubricated ? 0 : 6µm)`;
  `bias = +4µm + 45µm*ToolWear01`; `Ra(µm) = 0.8 + 5.2*ToolWear01 + (Lubricated ? 0 : 0.7)`,
  each part's Ra ~ N(that mean, 15% of it). Tool wear += 0.004 per part machined (clamp 0..1).
  `ReplaceTool()` resets wear to 0 and logs a CausalEvent.
  Gross defect probability per part = `0.002 + 0.03*ToolWear01`; a gross defect multiplies
  fatigue life by 0.02. Per-part `FatigueLifeMultiplier = LogNormal(median 1.0, σln 0.35) *
  (1.0/max(Ra,0.3))^0.30 * (gross ? 0.02 : 1)`.
- Preload from torque: `F_preload = T / (K * d_actual)`, nut factor
  `K = (Lubricated at assembly: 0.16, dry: 0.22) * (1 + N(0, WrenchScatter01))`, default
  `WrenchScatter01 = 0.10`. Deterministic yield check at assembly: if
  `F_preload / As_eff > 0.9 * yield` ⇒ joint records over-torque CausalEvent and
  `PreloadN = 0.9*yield*As_eff` (bolt yields to that level) — no dice (§20).
- Flange: bore diameter 150 mm (`A_bore = π*0.075²`), `BoltCount = 8`, joint load factor
  `C = 0.25`. Per pressurization cycle at pressure P: external load per intact joint
  `F_ext = P * A_bore / n_intact`; bolt alternating stress
  `σa = Kt * C * F_ext / (2 * As_eff)`; mean stress `σm = (PreloadN + C*F_ext/2)/As_eff`.
  Goodman: `σar = σa / max(1 − σm/UTS, 0.05)`. Basquin cycles to failure:
  `Nf = 0.5 * (σar / (σf' * batch.StrengthMultiplier))^(1/b)`, then
  `Nf *= FatigueLifeMultiplier`, floor 1. Miner: damage += cycles/Nf; joint fails at damage ≥ 1.
  Immediate overload: if `σm + σa > UTS` ⇒ fail now, deterministic.
  On joint failure: log CausalEvent with causes chaining bolt → batch → process events; load
  redistributes (n_intact shrinks) next block. **Assembly failure (leak)** when ≥ 2 joints failed.
- Simulation of cycles runs in blocks of 100 cycles (recompute loads between blocks).

## API contract (exact signatures)

```csharp
// Core
public sealed class SimRandom {
    public SimRandom(ulong seed);
    public double NextDouble();                      // [0,1)
    public double NextGaussian(double mean, double stdDev);
    public double NextLogNormal(double median, double sigmaLn);
    public bool NextBool(double probabilityTrue);
    public static ulong DeriveSeed(ulong parent, string streamName); // FNV-1a based
}
public readonly struct NormalDist {                  // Mean/StdDev + double Sample(SimRandom)
    public NormalDist(double mean, double stdDev); public double Mean { get; } public double StdDev { get; }
    public double Sample(SimRandom rng);
}
public sealed class SampleStats {                    // Welford
    public void Add(double x);
    public int Count { get; } public double Mean { get; } public double StdDev { get; }
    public double Min { get; } public double Max { get; }
}
public sealed class CausalEvent {
    public string Description { get; }               // human sentence, e.g. "Tool wear reached 62%"
    public IReadOnlyList<CausalEvent> Causes { get; }
}
public sealed class CausalLog {
    public CausalEvent Record(string description, params CausalEvent[] causes);
    public IReadOnlyList<CausalEvent> Events { get; }
    public string ExplainTree(CausalEvent leaf);     // indented multi-line "why" chain, root-causes last
}

// Materials
public sealed class MaterialSpec {
    public string Name { get; } public double DensityKgM3 { get; } public double YoungsModulusPa { get; }
    public NormalDist YieldPa { get; } public NormalDist UltimatePa { get; }
    public double BasquinExponent { get; } public double ThreadKt { get; }
    public static MaterialSpec MediumCarbonSteel();
}
public sealed class MaterialBatch {
    public string Id { get; } public MaterialSpec Spec { get; }
    public double StrengthMultiplier { get; } public double ActualTempC { get; }
    public CausalEvent Origin { get; }
    // samples correlated per-part (yieldPa, utsPa) applying StrengthMultiplier:
    public (double yieldPa, double utsPa) SampleStrength(SimRandom rng);
}

// Manufacturing
public sealed class LatheState {
    public double ToolWear01 { get; set; } public bool Lubricated { get; set; }
    public void ReplaceTool(CausalLog log);          // resets wear, records event
}
public sealed class MachiningProcess {
    public MachiningProcess(LatheState lathe);
    public LatheState Lathe { get; }
    public Bolt ProduceOne(MaterialBatch batch, SimRandom rng, CausalLog log);
    public List<Bolt> ProduceBatch(int count, MaterialBatch batch, SimRandom rng, CausalLog log);
}
public sealed class HeatTreatProcess {
    public HeatTreatProcess(double targetTempC, SimRandom rng); // samples latent CalibrationErrorC
    public double TargetTempC { get; }
    public double CalibrationErrorC { get; }         // LATENT: UI must never display directly
    public MaterialBatch RunBatch(string id, MaterialSpec spec, SimRandom rng, CausalLog log);
    public void Calibrate(Instrument thermocouple, SimRandom rng, CausalLog log);
}

// Components — actual vs known state kept separate (§14/§26)
public sealed class Bolt {
    public string Id { get; }
    public double ActualDiameterM { get; } public double ActualRaMicroM { get; }
    public double YieldPa { get; } public double UltimatePa { get; }
    public double FatigueLifeMultiplier { get; } public bool HasGrossDefect { get; } // latent
    public MaterialBatch Batch { get; } public CausalEvent Origin { get; }
    public double EffectiveStressAreaM2 { get; }
    public double? MeasuredDiameterM { get; set; } public bool Accepted { get; set; }
}

// Metrology
public sealed class Instrument {
    public string Name { get; } public double SigmaM { get; }   // for length; reused as °C sigma for thermocouple
    public double Measure(double actual, SimRandom rng);
    public static Instrument Caliper();      // σ 20e-6
    public static Instrument Micrometer();   // σ 2e-6
    public static Instrument Thermocouple(); // σ 3.0 (°C)
}
public sealed class InspectionPlan {
    public InspectionPlan(double minDiameterM, double maxDiameterM, Instrument gauge);
    public InspectionResult Inspect(List<Bolt> bolts, SimRandom rng, CausalLog log);
}
public sealed class InspectionResult {
    public List<Bolt> Accepted { get; } public List<Bolt> Rejected { get; }
    public SampleStats MeasuredDiameters { get; }     // the civilization's KNOWLEDGE of the population
}

// Assembly
public sealed class BoltedJoint {
    public Bolt Bolt { get; } public double PreloadN { get; }
    public double FatigueDamage01 { get; internal set; }
    public bool Failed { get; internal set; } public CausalEvent FailureEvent { get; internal set; }
}
public sealed class FlangeAssembly {
    public const int BoltCount = 8;
    public IReadOnlyList<BoltedJoint> Joints { get; }
    public CausalEvent Origin { get; }
    public static FlangeAssembly Assemble(List<Bolt> stock, double targetTorqueNm,
        bool lubricatedAssembly, double wrenchScatter01, SimRandom rng, CausalLog log);
        // takes first BoltCount accepted bolts from stock (throws InvalidOperationException if fewer)
}

// Testing
public sealed class PressureTestRig {
    public CycleTestResult Run(FlangeAssembly assembly, double pressurePa, int maxCycles,
        SimRandom rng, CausalLog log);
}
public sealed class CycleTestResult {
    public bool AssemblyFailed { get; } public int CyclesCompleted { get; }
    public IReadOnlyList<BoltedJoint> FailedJoints { get; }
    public string Summary { get; }                    // short human paragraph
    public string Explanation { get; }                // CausalLog.ExplainTree of first failure, "" if none
}
```

### asmdefs (exact content)

`EarthGame.Sim.asmdef`:
```json
{ "name": "EarthGame.Sim", "rootNamespace": "EarthGame.Sim", "autoReferenced": true, "noEngineReferences": true }
```
`EarthGame.Sim.Tests.asmdef`:
```json
{ "name": "EarthGame.Sim.Tests", "rootNamespace": "EarthGame.Sim.Tests",
  "references": [ "UnityEngine.TestRunner", "UnityEditor.TestRunner", "EarthGame.Sim" ],
  "includePlatforms": [ "Editor" ], "precompiledReferences": [ "nunit.framework.dll" ],
  "overrideReferences": true, "defineConstraints": [ "UNITY_INCLUDE_TESTS" ] }
```
Test writer also adds `"com.unity.test-framework": "1.4.5"` to `Packages/manifest.json`
dependencies (edit that one line only).

## Tests (all seeds fixed; no flakiness tolerated)

`SimDeterminismTests`: same seed ⇒ identical batch diameters/strengths; DeriveSeed streams differ.
`ProcessCapabilityTests`: worn tool (0.8) batch of 400 has ≥2× the diameter σ and worse Ra than
fresh-tool batch; bias drifts positive with wear; deterministic overload: pressure such that
σm+σa > UTS fails at cycle ≤ 100 with AssemblyFailed on every seed tried (3 seeds).
`MetrologyTests`: micrometer + tight limits (±25 µm) yields outgoing (true) diameter σ strictly
smaller than incoming σ on a 400-bolt worn-tool batch; caliper misclassifies more bolts than
micrometer vs true limits (count actual out-of-limit bolts accepted); calibrated furnace ⇒
|batch temp − target| smaller on average over 20 batches than uncalibrated with error 12+.
`ReliabilityTests`: a disciplined shop's bolts at 6 MPa survive ≥ 3× the cycles of a sloppy
shop's at the same pressure (median of 5 rigs each). Disciplined shop: fresh lubricated tool,
on-temperature batch (StrengthMultiplier 1.0), micrometer inspection to tight ±25 µm limits.
Sloppy shop: tool worn to 0.8 cutting dry, batch from an uncalibrated furnace that overshot to
890 °C (StrengthMultiplier 0.92 by the clamp formula), no inspection (everything ships). Both
shops use wrench scatter 0.03 so torque variation does not mask the contrast. *(Amended: tool
wear + inspection alone yields only ~1.7–2× under the binding formulas above — the good rigs
themselves cascade-fail well before the cycle cap — so the furnace deviation, already part of
the feel checklist's causal chain below, is included in the mandated contrast.)* Higher
pressure ⇒ fewer median cycles (6 vs 9 MPa); a failed rig's `Explanation` string is non-empty
and mentions its bolt's batch id; Miner sanity: constant-amplitude single-joint life within
±35% of closed-form Basquin prediction for a mid-wear bolt with multiplier forced to 1
(construct directly).

## Unity side — WorkshopController (Assembly-CSharp)

`Assets/EarthGame/Scripts/Workshop/WorkshopController.cs`, namespace `EarthGame`. OnGUI window
(560×540, translucent black style consistent with HUDController), toggled with **B**. While open:
`Cursor.lockState = None`, `Cursor.visible = true`, and `FirstPersonPlanetController.InputSuspended = true`
(restore all three on close). Holds: master `SimRandom` seeded `(ulong)PlanetConfig.Seed`,
`CausalLog`, one `HeatTreatProcess` (850), one `LatheState`+`MachiningProcess`, current
`MaterialBatch`, `List<Bolt>` stock, latest `InspectionResult`, latest `FlangeAssembly`, latest
`CycleTestResult`. Panel sections (vertical, scroll view):
1. **FURNACE** — "Run heat-treat batch" button (makes new batch, names B-001…); "Calibrate
   furnace (thermocouple)" button. Show batch id + *measured* temp only (target ± thermocouple
   reading), never CalibrationErrorC or StrengthMultiplier.
2. **LATHE** — tool wear % bar, lubrication toggle, "Replace tool", "Machine 50 bolts" (needs a
   batch). Shows stock count.
3. **MEASURE & INSPECT** — gauge choice (Caliper/Micrometer) as two toggle buttons, limit choice
   (Loose ±60 µm / Tight ±25 µm), "Inspect stock" → shows measured mean/σ (mm, 4 decimals),
   accepted/rejected counts.
4. **ASSEMBLE** — torque slider 20–70 N·m (default 45), assembly lube toggle, "Assemble flange
   (8 bolts)" (needs ≥8 accepted).
5. **TEST** — pressure slider 2–12 MPa (default 6), "Run 20,000 cycles" → Summary text.
6. **WHY DID IT FAIL?** — scrollable monospace label with `Explanation` (or "No failure.").
All state persists while panel closed. Guard every button with its precondition; show the reason
inline when disabled (e.g. "Need a material batch first").

Integration edits (Workshop writer ONLY):
- `GameBootstrap.Boot()`: after the HUD creation step, create child `"Workshop"` with
  `WorkshopController`.
- `FirstPersonPlanetController`: add `public static bool InputSuspended;` — when true, skip mouse
  look, movement input, jump/fly toggles, and all cursor re-locking (gravity/alignment still run).
  Touch nothing else in the file.
- `HUDController`: append " · B workshop" to the controls-hint string. Touch nothing else.

## Feel checklist (what "done" means, GAME_DESIGN §45 steps 1–11)
Press Play → B opens the workshop. Run a batch, machine 50 bolts, inspect with caliper+loose,
assemble at 45 N·m, test at 6 MPa: usually survives. Now wear the tool to ~80% (machine ~200
bolts), skip inspection discipline (caliper/loose), test at 9 MPa: failures within 20k cycles —
and the WHY panel traces: joint → fatigue → bolt → rough surface/undersize → tool wear → (maybe)
furnace deviation. Replace tool, calibrate furnace, micrometer+tight, retest: measurably better.
The player's *process* decisions — not a stat — changed reliability, and the game explained why.
