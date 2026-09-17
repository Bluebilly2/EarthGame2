using System;

namespace EarthGame.Engine
{
    /// <summary>What one blow did (FP.3, v1's KnapOutcome).</summary>
    public enum KnapOutcome : byte
    {
        /// <summary>Not enough behind it. The hammer bounced and nothing happened.</summary>
        NoFracture = 0,
        /// <summary>A flake came away. This is the thing the founder is after.</summary>
        Flake = 1,
        /// <summary>Too much. The core broke up instead of releasing a flake.</summary>
        Shatter = 2,
        /// <summary>The edge crumbled rather than fracturing: the wrong stone, or a dead platform.</summary>
        Crushed = 3,
    }

    /// <summary>The outcome of a blow, and what it left behind.</summary>
    public readonly struct KnapResult
    {
        public readonly KnapOutcome Outcome;
        /// <summary>Mass of the flake that came away, kg; zero unless it was a flake.</summary>
        public readonly double FlakeMassKg;
        /// <summary>How good an edge the flake carries, 0 to 1.</summary>
        public readonly double EdgeQuality;
        /// <summary>What the founder would say happened: the answer's words, in one place.</summary>
        public readonly string Note;

        public KnapResult(KnapOutcome outcome, double flakeMassKg, double edgeQuality, string note)
        {
            Outcome = outcome;
            FlakeMassKg = flakeMassKg;
            EdgeQuality = edgeQuality;
            Note = note;
        }
    }

    /// <summary>
    /// A lump of stone being worked, and the platform it is being worked from (FP.3, v1's StoneCore). The platform is the
    /// whole craft: a blow lands on an edge, and whether a flake comes away depends on the angle that edge presents. Under
    /// ninety degrees a fracture can run and release a flake; over it the blow has nowhere to go and crushes the edge
    /// instead. Every flake taken flattens the face a little and works the angle toward ninety, which is why a knapper
    /// turns the core and starts a new platform rather than hammering the same spot.
    /// </summary>
    public sealed class StoneCore
    {
        /// <summary>Below this there is not enough left to hold and strike, kg.</summary>
        public const double SpentMassKg = 0.12;
        /// <summary>Past this the edge is too blunt to fracture from.</summary>
        public const double DeadPlatformDeg = 90.0;
        /// <summary>The angle a fresh cobble's edge presents, and what turning the core finds, degrees.</summary>
        public const double FreshPlatformDeg = 68.0;

        public StoneType Stone { get; }
        public double MassKg { get; private set; }
        /// <summary>The angle the struck edge presents, degrees: acute works, obtuse does not.</summary>
        public double PlatformAngleDeg { get; private set; }
        /// <summary>Flakes taken off this core so far.</summary>
        public int FlakesTaken { get; private set; }

        public StoneCore(StoneType stone, double massKg, double platformAngleDeg = FreshPlatformDeg, int flakesTaken = 0)
        {
            Stone = stone ?? throw new ArgumentNullException(nameof(stone));
            MassKg = Math.Max(0.0, massKg);
            PlatformAngleDeg = platformAngleDeg;
            FlakesTaken = Math.Max(0, flakesTaken);
        }

        /// <summary>Whether this core has anything left to give from where it is being struck.</summary>
        public bool IsWorkable => MassKg > SpentMassKg && PlatformAngleDeg < DeadPlatformDeg;

        /// <summary>Whether it is finished entirely, rather than merely needing turning.</summary>
        public bool IsSpent => MassKg <= SpentMassKg;

        /// <summary>
        /// Turning the core to present a fresh edge: free in effort and not in stone, since finding a new platform means
        /// taking the old ruined edge off, and that is mass gone.
        /// </summary>
        public void Turn()
        {
            MassKg = Math.Max(0.0, MassKg - MassKg * 0.06);
            PlatformAngleDeg = 62.0 + (FlakesTaken % 3) * 3.0;
        }

        internal void Apply(double massLostKg, double angleChangeDeg, bool countFlake)
        {
            MassKg = Math.Max(0.0, MassKg - massLostKg);
            PlatformAngleDeg = Math.Min(140.0, PlatformAngleDeg + angleChangeDeg);
            if (countFlake) FlakesTaken++;
        }
    }

    /// <summary>
    /// Striking flakes off stone (FP.3, the Founder's Path's first stone), ported from v1's Knapping. Two pieces of real
    /// fracture mechanics carry it. Auerbach's law: the load needed to start a cone crack rises with the radius of what is
    /// hitting it, and for hammerstone-sized indenters as the square of that radius, so a big hammer is not a better hammer
    /// but one that needs a great deal more behind it, which is why a knapper reaches for a small one to do fine work. And
    /// the Hertzian cone's angle is a constant: hitting harder does not change the shape of the fracture, only its size,
    /// so the force behind a blow sets how big a flake comes away and nothing else. There is no accuracy stat anywhere in
    /// this: the founder controls how hard they swing, and the stone decides the rest.
    /// </summary>
    public static class Knapping
    {
        /// <summary>Scales the critical energy: chosen so a 1.4 kg hammerstone starts a fracture in flint at about 6 J, a gentle tap, and needs twice that in quartzite.</summary>
        public const double CriticalEnergyConstant = 2825.0;
        /// <summary>Below this knappability a stone does not fracture conchoidally at all.</summary>
        public const double MinimumKnappability = 0.25;
        /// <summary>Flake mass per joule of energy above the critical, kg.</summary>
        public const double FlakeMassPerJoule = 0.0022;
        /// <summary>
        /// The smallest flake a hammer can take, per kilogram of hammer. The cone a blow drives is as wide as the thing that
        /// drove it, so a heavy hammer has a floor under how small a piece it can remove; tapping more gently does not get
        /// under it, it just fails to fracture at all. This is why a knapper carries more than one hammerstone.
        /// </summary>
        public const double MinimumFlakePerHammerKg = 0.010;
        /// <summary>How much of a core one blow can ever take, as a share of its mass.</summary>
        public const double MaxFlakeShare = 0.22;
        /// <summary>Energy a core can absorb before it breaks up instead, J per kg of core.</summary>
        public const double ShatterEnergyPerKg = 40.0;
        /// <summary>Below this a stone is too light to drive a cone at all, kg.</summary>
        public const double MinimumHammerKg = 0.15;
        /// <summary>Below this a stone spends the blow breaking itself instead of the core.</summary>
        public const double MinimumPoundingQuality = 0.3;
        /// <summary>
        /// The most a person can put behind one swing, J. Without this ceiling a heavier hammer would always be a better
        /// one, swing energy going as the mass while Auerbach's critical load goes as its two-thirds power; an arm can
        /// accelerate a light stone to full speed and cannot do the same with a heavy one, which is what makes a
        /// hammerstone's size a real choice rather than a number to maximise.
        /// </summary>
        public const double ArmEnergyCeilingJ = 40.0;
        /// <summary>
        /// Whether a flake is sharp enough to be a tool rather than a scrap: low enough that quartzite, which derives to
        /// a 0.25 edge, crude and real, counts, but only from a controlled blow; the penalty for swinging too hard drops a
        /// quartzite flake below it on its own.
        /// </summary>
        public const double UsableEdge = 0.20;
        /// <summary>Smallest flake worth keeping, kg: two and a half grams of flint is a plate four centimetres by two and a millimetre thick, a blade.</summary>
        public const double UsableFlakeKg = 0.0025;
        /// <summary>How much faster a cutting job goes per unit of edge.</summary>
        public const double EdgeSpeedup = 4.5;

        /// <summary>Radius of a roughly spherical hammerstone of a given mass, m.</summary>
        public static double HammerRadiusM(StoneType hammer, double massKg)
        {
            double density = hammer != null ? hammer.DensityKgM3 : DefinitionCatalogue.CobbleDensityKgM3;
            double volume = Math.Max(1e-6, massKg) / density;
            return Math.Pow(3.0 * volume / (4.0 * Math.PI), 1.0 / 3.0);
        }

        /// <summary>The energy a blow must carry to start a cone crack in the core, J: Auerbach's law over the hammer's radius.</summary>
        public static double CriticalEnergyJ(StoneType core, StoneType hammer, double hammerMassKg)
        {
            if (core == null) return double.PositiveInfinity;
            double r = HammerRadiusM(hammer, hammerMassKg);
            return CriticalEnergyConstant * core.FractureToughness * r * r;
        }

        /// <summary>
        /// The energy a founder puts behind a swing, J, from how far they wound up (0 to 1), how heavy the hammer is and
        /// what thirst has left of their work. A full swing of a 1.4 kg stone is a real blow.
        /// </summary>
        public static double SwingEnergyJ(double windUp01, double hammerMassKg, double workCapacity01)
        {
            double speed = 1.4 + 5.4 * SimMath.Clamp01(windUp01);
            double capacity = 0.55 + 0.45 * SimMath.Clamp01(workCapacity01);
            double swung = 0.5 * Math.Max(0.05, hammerMassKg) * speed * speed;
            return Math.Min(swung, ArmEnergyCeilingJ * SimMath.Clamp01(windUp01)) * capacity;
        }

        /// <summary>One blow. <paramref name="energyJ"/> is what the founder put behind the swing, the only thing they control.</summary>
        public static KnapResult Strike(StoneCore core, StoneType hammer, double hammerMassKg, double energyJ)
        {
            if (core == null || core.IsSpent)
                return new KnapResult(KnapOutcome.Crushed, 0.0, 0.0, "There is nothing left to hold.");

            // A pebble or a flake swung at a core drives no cone: what it has is not mass enough to put behind the blow.
            if (hammerMassKg < MinimumHammerKg)
                return new KnapResult(KnapOutcome.NoFracture, 0.0, 0.0, "The stone in hand is too light to start a fracture.");

            // Some stone simply does not do this: sandstone crumbles, granite powders; neither has ever made a blade.
            if (core.Stone.Knappability < MinimumKnappability)
            {
                core.Apply(core.MassKg * 0.02, 3.0, false);
                return new KnapResult(KnapOutcome.Crushed, 0.0, 0.0, core.Stone.Name + " crumbles instead of flaking. It will never take an edge.");
            }

            // A blow on an edge that has gone obtuse has nowhere to run.
            if (core.PlatformAngleDeg >= StoneCore.DeadPlatformDeg)
            {
                core.Apply(core.MassKg * 0.01, 1.0, false);
                return new KnapResult(KnapOutcome.Crushed, 0.0, 0.0, "The edge is too blunt to strike from. Turn the core and find a new one.");
            }

            double critical = CriticalEnergyJ(core.Stone, hammer, hammerMassKg);
            if (energyJ < critical)
                return new KnapResult(KnapOutcome.NoFracture, 0.0, 0.0, "The hammer bounced. Not enough behind it.");

            if (energyJ > core.MassKg * ShatterEnergyPerKg)
            {
                core.Apply(core.MassKg * 0.55, 14.0, false);
                return new KnapResult(KnapOutcome.Shatter, 0.0, 0.0, "Too hard. It broke up rather than giving a flake.");
            }

            // Above the critical the cone grows with the energy behind it, and the cone's angle is fixed, so all the extra
            // force buys is size: a bigger flake is not a better flake.
            double excess = energyJ - critical;
            double ceiling = core.MassKg * MaxFlakeShare;
            double floor = MinimumFlakePerHammerKg * hammerMassKg;

            // A hammer whose smallest possible bite is more than the core can spare does not reduce that core, it destroys it:
            // what happens when you go at a nearly spent core with the big stone rather than reaching for the small one.
            if (floor > ceiling)
            {
                core.Apply(core.MassKg * 0.5, 14.0, false);
                return new KnapResult(KnapOutcome.Shatter, 0.0, 0.0, "Too heavy a hammer for what is left of it. The core broke up.");
            }

            double flake = Math.Min(Math.Max(FlakeMassPerJoule * excess, floor), ceiling);

            // How fine an edge it carries is the stone's business, docked for violence: a heavy blow drives a thick clumsy
            // flake, which is the first thing a beginner does wrong.
            double overshoot = SimMath.Clamp01((energyJ / critical - 1.0) / 4.0);
            double edge = core.Stone.EdgeQuality * (1.0 - 0.35 * overshoot);

            // Every flake flattens the face a little and works the platform toward ninety.
            core.Apply(flake, 4.5 + overshoot * 5.0, true);
            return new KnapResult(KnapOutcome.Flake, flake, edge, edge > 0.6 ? "A clean flake, and it is sharp." : "A flake, but a coarse one.");
        }

        /// <summary>Whether a result produced something the founder should keep.</summary>
        public static bool IsUsableTool(in KnapResult result) =>
            result.Outcome == KnapOutcome.Flake && result.EdgeQuality >= UsableEdge && result.FlakeMassKg >= UsableFlakeKg;

        /// <summary>
        /// A cutting job that takes <paramref name="bareHandedMinutes"/> without a tool, done with an edge: the return on
        /// the whole beat. Tearing cordage fibre out of a plant with bare hands is six minutes of work; a flint flake makes
        /// it a minute. Nothing else the founder can do to their day pays back like that, which is why the first blade is
        /// the thing every other technology waits on.
        /// </summary>
        public static double CuttingMinutes(double bareHandedMinutes, double edge01) =>
            bareHandedMinutes / (1.0 + EdgeSpeedup * SimMath.Clamp01(edge01));
    }

    /// <summary>
    /// The stones as items (FP.3): what the knapping physics reads off a thing's definition and the state a blow left on it,
    /// and what it writes back. A thing's mass is its definition's until a blow gives it one of its own
    /// (<see cref="ItemComponent.MassKg"/>), and a stone never struck presents a fresh cobble's platform; so a cobble taken
    /// from the litter, or one written before this state existed, knaps as a fresh cobble of its stone. One owner for the
    /// reading and the writing, so the server, the save and the wire cannot disagree about what a struck stone is.
    /// </summary>
    public static class KnappingItems
    {
        /// <summary>Whether a thing can be struck or struck with: an item of a stone the country names (a cobble of a stone, or a flake).</summary>
        public static bool IsStone(Definition definition) => DefinitionCatalogue.StoneOf(definition) != null;

        /// <summary>
        /// Whether a thing can be swung as a hammer: any cobble, the plain cobble included (a hammer's part in the physics is its
        /// mass and its radius, and the plain cobble has both), or a flake; not a stick.
        /// </summary>
        public static bool IsHammer(Definition definition) =>
            IsStone(definition) || ReferenceEquals(definition, DefinitionCatalogue.Cobble);

        /// <summary>What a thing weighs, kg: its own mass once a blow gave it one, else its definition's.</summary>
        public static double MassOf(Definition definition, in ItemComponent item) => item.MassKg > 0f ? item.MassKg : definition.MassKg;

        /// <summary>The core a stone item is, to be struck: its stone, its mass, the platform it presents and the flakes taken so far.</summary>
        public static StoneCore CoreOf(Definition definition, in ItemComponent item)
        {
            StoneType stone = DefinitionCatalogue.StoneOf(definition);
            if (stone == null) throw new ArgumentException("'" + definition + "' is of no stone the country names, and cannot be a core", nameof(definition));
            return new StoneCore(stone, MassOf(definition, item), item.PlatformDeg > 0f ? item.PlatformDeg : StoneCore.FreshPlatformDeg, item.FlakesTaken);
        }

        /// <summary>The item's state after a blow on it, as the core now stands: its rest and fall as they were, the rest the core's.</summary>
        public static ItemComponent Struck(in ItemComponent before, StoneCore core)
        {
            ItemComponent after = before;
            after.MassKg = (float)core.MassKg;
            after.PlatformDeg = (float)core.PlatformAngleDeg;
            after.FlakesTaken = (ushort)Math.Min(core.FlakesTaken, ushort.MaxValue);
            return after;
        }

        /// <summary>The state a flake is born with (FP.3): the mass the blow took and the edge it made, let go to fall where it came away.</summary>
        public static ItemComponent FlakeOf(in KnapResult result)
        {
            ItemComponent flake = default;
            flake.Resting = false;
            flake.FallSpeed = 0f;
            flake.MassKg = (float)result.FlakeMassKg;
            flake.Edge01 = (float)result.EdgeQuality;
            return flake;
        }

        /// <summary>The answer a blow's outcome is sent as (<see cref="VerbOutcome"/>): one place maps the physics' word to the wire's.</summary>
        public static VerbOutcome OutcomeOf(KnapOutcome outcome)
        {
            switch (outcome)
            {
                case KnapOutcome.Flake: return VerbOutcome.Flaked;
                case KnapOutcome.Shatter: return VerbOutcome.Shattered;
                case KnapOutcome.Crushed: return VerbOutcome.Crushed;
                default: return VerbOutcome.Bounced;
            }
        }
    }
}
