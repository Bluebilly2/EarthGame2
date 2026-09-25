using System;
using System.Collections.Generic;
using System.Globalization;

namespace EarthGame.Engine
{
    /// <summary>A kind of work a founder does on a thing (BF.2) or on the world (BF.3). Wire-visible and never renumbered.</summary>
    public enum WorkKind : byte
    {
        None = 0,
        /// <summary>Break a stick over the knee into two.</summary>
        Break = 1,
        /// <summary>Strip the bark off a stick of a tree whose bark comes away.</summary>
        Strip = 2,
        /// <summary>Carve a point on a stick with an edge in hand.</summary>
        Point = 3,
        /// <summary>Lay a bark strip into cord, with a strip or a cord in hand.</summary>
        Twist = 4,
        /// <summary>Strip the bark off a standing trunk (BF.3).</summary>
        StripTrunk = 5,
        /// <summary>Cut fibre from a tuft with an edge in hand (BF.3).</summary>
        CutFibre = 6,
        /// <summary>Pull a tuft up whole (BF.3).</summary>
        PullTuft = 7,
        /// <summary>Clear a cell of the ground of its tufts (BF.3).</summary>
        ClearGround = 8,
        /// <summary>Dig a cell of the ground a decimetre deeper with a pointed stick (BF.3).</summary>
        Dig = 9,
        /// <summary>Cut a standing trunk through with a heavy edge, and fell it (BF.3).</summary>
        CutTrunk = 10,
    }

    /// <summary>What work has left on a thing: the bits of <see cref="ThingState.Marks"/> (BF.2). Never renumbered.</summary>
    public static class ThingMarks
    {
        public const ushort Pointed = 1;
        public const ushort Split = 2;
        public const ushort Stripped = 4;
        public const ushort Notched = 8;
        public const ushort Charred = 16;
    }

    /// <summary>What one kind of work would do to a thing, judged from properties: whether, why not, how long, and the words for it.</summary>
    public struct WorkOffer
    {
        public WorkKind Kind;
        public VerbOutcome Outcome;
        /// <summary>Seconds at a body's full capacity.</summary>
        public double Seconds;
        public string Words;
        public bool Possible => Outcome == VerbOutcome.Done;
    }

    /// <summary>A thing a finished work made, and where it lies from the work's own point.</summary>
    public struct MadeThing
    {
        public Definition Definition;
        public ThingState State;
        /// <summary>
        /// Metres along the work's own line and across it, from the target's point (BF.3): a felled tree's wood lies down the
        /// fall's line, a cleared cell's bundles where their tufts stood (north along, east across). Both zero for a thing let
        /// go beside the target, as BF.2's are.
        /// </summary>
        public double AlongM;
        public double AcrossM;
    }

    /// <summary>What a finished work leaves: things made, the target and the tool changed or spent, and the words.</summary>
    public sealed class WorkResult
    {
        public readonly List<MadeThing> Made = new List<MadeThing>();
        public bool TargetSpent;
        public bool TargetChanged;
        public ThingState TargetAfter;
        public bool ToolSpent;
        public bool ToolChanged;
        public ThingState ToolAfter;
        /// <summary>The bits of <see cref="TrunkChange"/> a work on a standing trunk sets (BF.3): its bark taken, or felled with its limbs.</summary>
        public byte TrunkFlags;
        public string Words = string.Empty;
    }

    /// <summary>What a finished work on the ground leaves (BF.3): things made, the tufts taken, the cell cleared, how deep it is dug, and the words.</summary>
    public sealed class GroundResult
    {
        public readonly List<MadeThing> Made = new List<MadeThing>();
        /// <summary>The tufts taken by this work, bits by index.</summary>
        public ushort TuftsTaken;
        public bool Cleared;
        /// <summary>How deep the cell is dug after this work, cm.</summary>
        public byte DugCm;
        public string Words = string.Empty;
    }

    /// <summary>
    /// One model of work (BF.2, 2026-09-22): a kind of work on a thing looked at, with what is in hand, judged from the
    /// things' properties and never from their keys (GAME_DESIGN §7B: a transformation has requirements, a rate, results and
    /// failure modes; §9: it makes a distribution). <see cref="Judge"/> says whether and how long; <see cref="Apply"/> says
    /// what a finished work leaves. Both are pure, so the client offers what the server will accept and the tests read the
    /// same numbers the server commits. The knap stays a blow of its own (FP.3); everything that takes time comes here.
    ///
    /// <para>Since BF.3 (2026-09-23) the standing world is a target too: a trunk and a tuft come as their plant's definition
    /// with a state of what they have of their own (<see cref="StandingThings"/>), and the ground of a cell comes as a
    /// <see cref="GroundSite"/> to <see cref="JudgeGround"/> and <see cref="ApplyGround"/>. What a work on the world leaves
    /// behind is a change (<see cref="WorldChanges"/>), never a changed layer.</para>
    /// </summary>
    public static class Work
    {
        /// <summary>The bending moment a person puts through a stick over the knee, N·m: 150 N at 0.4 m (a stated model).</summary>
        public const double KneeMomentNm = 60.0;
        /// <summary>The modulus of rupture of a stick of no tree, MPa: a middling hardwood.</summary>
        public const double PlainRuptureMPa = 60.0;
        public const double BreakSeconds = 2.0;
        /// <summary>The least share of a broken stick either piece is: no slivers.</summary>
        public const double LeastBreakShare = 0.35;

        public const double StripSecondsPerMetre = 6.0;
        /// <summary>A bark strip's length, m: what comes away in one pull.</summary>
        public const double StripLengthM = 0.3;
        /// <summary>The bark of a stick is at most this share of its diameter thick: a tree's slab bark does not grow on a branch.</summary>
        public const double BarkShareOfDiameter = 0.06;
        /// <summary>Bark's density, kg/m³, the order of eucalypt bark.</summary>
        public const double BarkDensityKgM3 = 600.0;

        /// <summary>A stick thicker than this is not pointed with a flake: it is a log, and wants splitting first (BF.5).</summary>
        public const double PointableDiameterM = 0.030;
        /// <summary>Wood carved away at a full edge in a fully soft wood, cm³ a second (a stated rate, DEBTS: to be measured).</summary>
        public const double CarveCm3PerSecond = 0.5;
        /// <summary>A point is a cone this many diameters long.</summary>
        public const double PointLengthDiameters = 3.0;
        /// <summary>What an edge loses for every 10 cm³ carved in a wood of full softness; harder wood wears it more.</summary>
        public const double EdgeWearPer10Cm3 = 0.05;

        public const double TwistSecondsPerJoin = 15.0;
        /// <summary>Metres of two-ply cord laid from a metre of strip: the twist takes up the rest.</summary>
        public const double CordPerStripLength = 0.45;
        public const double CordDiameterM = 0.004;

        // ---- the standing world (BF.3) ----

        /// <summary>Stripping a trunk's bark below head height, s: the slabs come away in the hands, the fibrous ones tear (a stated model).</summary>
        public const double StripTrunkSeconds = 30.0;
        /// <summary>A trunk gives one strip for every this much of its girth at breast height, m.</summary>
        public const double TrunkStripPerGirthM = 0.1;
        /// <summary>A trunk's strip: this long and this wide, m.</summary>
        public const double TrunkStripLengthM = 0.3;
        public const double TrunkStripWidthM = 0.1;

        /// <summary>Cutting the leaves off a fibre tuft with an edge, s.</summary>
        public const double CutFibreSeconds = 12.0;
        /// <summary>A tuft gives this many fibre strips, each this long, m, and this heavy, kg, dry.</summary>
        public const int FibreStrips = 3;
        public const double FibreLengthM = 0.6;
        public const double FibreMassKg = 0.015;

        /// <summary>Pulling a tuft up whole, s, and the bundle it makes, kg.</summary>
        public const double PullSeconds = 4.0;
        public const double BundleKg = 0.3;

        /// <summary>Clearing a cell of the ground, s for each tuft on it.</summary>
        public const double ClearSecondsPerTuft = 2.0;

        /// <summary>A dig takes the ground down by this much, cm; in sand and in soil it takes this long, s.</summary>
        public const int DigStepCm = 10;
        public const double DigSecondsSand = 10.0;
        public const double DigSecondsSoil = 20.0;
        /// <summary>Soil thinner than this is not dug: the stick meets stone, m.</summary>
        public const double LeastSoilM = 0.1;
        /// <summary>A tuber-bearing plant's tuber lies in the top this much of the ground, cm: a dig from deeper turns up nothing.</summary>
        public const int TuberDepthCm = 30;

        /// <summary>Cutting a tree wants an edge at least this keen, in a thing at least this heavy, kg: a heavy flake cuts slowly, a hafted axe (later) faster.</summary>
        public const double CutLeastEdge = 0.4;
        public const double CutLeastMassKg = 0.3;
        /// <summary>
        /// Wood chopped out at a full edge with a thing of the least mass in a fully soft wood, cm³ a second: a stated model
        /// (DEBTS) pinned at one end by an axeman, who fells a 60 cm hardwood with a 2 kg steel axe in about an hour, and
        /// leaving a stone chopper of half a kilogram days against a blackbutt of the stand's stoutness, which is the truth
        /// of it: the peoples who lived with these trees ring-barked and burned them rather than cut them down.
        /// </summary>
        public const double CutCm3PerSecond = 4.0;
        /// <summary>The wood a felling cut takes out, as a share of the cube of the trunk's diameter at the cut: a notch a third of the width deep.</summary>
        public const double CutVolumeShare = 0.15;
        /// <summary>How high above the foot a tree is cut, m.</summary>
        public const double CutHeightM = 0.3;
        /// <summary>A felled trunk lies in logs this long, m, and its limbs come off as sticks this long and thick, m, one for every this many metres of height.</summary>
        public const double LogLengthM = 2.0;
        public const double LimbLengthM = 1.2;
        public const double LimbDiameterM = 0.035;
        public const double LimbsPerMetre = 0.2;
        public const int MostLimbs = 6;

        /// <summary>Every kind of work on a thing, in the order the crosshair offers them.</summary>
        public static readonly WorkKind[] Kinds = { WorkKind.Break, WorkKind.Strip, WorkKind.Point, WorkKind.Twist, WorkKind.StripTrunk, WorkKind.CutFibre, WorkKind.PullTuft, WorkKind.CutTrunk };

        /// <summary>Every kind of work on the ground, in the order the crosshair offers them.</summary>
        public static readonly WorkKind[] GroundKinds = { WorkKind.ClearGround, WorkKind.Dig };

        /// <summary>Whether a kind of work is done to the ground of a cell rather than to a thing.</summary>
        public static bool IsGroundWork(WorkKind kind) => kind == WorkKind.ClearGround || kind == WorkKind.Dig;

        /// <summary>The thickest stick of a wood a person breaks over the knee, m: π d³ σ / 32 against the knee's moment.</summary>
        public static double MaxBreakableDiameterM(double ruptureMPa) => Math.Pow(32.0 * KneeMomentNm / (Math.PI * ruptureMPa * 1e6), 1.0 / 3.0);

        private static bool IsStick(Definition d) => d != null && d.Substance == Substance.Wood;
        private static bool IsStone(Definition d) => d != null && d.Substance == Substance.Stone;
        /// <summary>Whether a target is a standing trunk: a tall plant's own definition (BF.3).</summary>
        public static bool IsTrunk(Definition d, out PlantSpecies species)
        {
            species = d != null && d.Kind == DefinitionKind.Plant ? d.Row as PlantSpecies : null;
            return species != null && StandCodes.IsTall(species);
        }
        /// <summary>Whether a target is a tuft of the understorey: a plant's own definition that stands as no tree (BF.3).</summary>
        public static bool IsTuft(Definition d, out PlantSpecies species)
        {
            species = d != null && d.Kind == DefinitionKind.Plant ? d.Row as PlantSpecies : null;
            return species != null && !StandCodes.IsTall(species);
        }
        private static double RuptureOf(Definition d) { Wood w = DefinitionCatalogue.WoodOf(d); return w != null ? w.RuptureMPa : PlainRuptureMPa; }
        private static double SoftnessOf(Definition d) { Wood w = DefinitionCatalogue.WoodOf(d); return w != null ? w.Softness01 : 0.5; }
        private static double DiameterOf(Definition d, in ThingState s) => s.Has(ThingFields.Diameter) ? s.DiameterM : 2.0 * d.RadiusM;
        private static double LengthOf(in ThingState s) => s.Has(ThingFields.Length) ? s.LengthM : 1.0;
        private static double MoistureOf(in ThingState s) => s.Has(ThingFields.Moisture) ? s.Moisture : 0.15;
        private static bool Marked(in ThingState s, ushort mark) => s.Has(ThingFields.Marks) && (s.Marks & mark) != 0;
        /// <summary>How far a trunk's cut has gone, 0 to 1: what its soundness has lost (<see cref="StandingThings.TrunkState"/>).</summary>
        private static double CutDone(in ThingState s) => s.Has(ThingFields.Condition) ? SimMath.Clamp01(1.0 - s.Condition01) : 0.0;

        /// <summary>Whether the thing in hand is an edge fit to carve: a stone with an edge at least <see cref="Knapping.UsableEdge"/>.</summary>
        public static bool IsEdge(Definition tool, in ThingState toolState) => IsStone(tool) && toolState.Has(ThingFields.Edge) && toolState.Edge01 >= Knapping.UsableEdge;

        /// <summary>Whether the thing in hand is a stick a founder digs with: pointed (BF.3).</summary>
        public static bool IsDiggingStick(Definition tool, in ThingState toolState) => IsStick(tool) && Marked(toolState, ThingMarks.Pointed);

        private static WorkOffer Refuse(WorkKind kind, VerbOutcome outcome, string words) => new WorkOffer { Kind = kind, Outcome = outcome, Seconds = 0.0, Words = words };
        private static WorkOffer Yes(WorkKind kind, double seconds, string words) => new WorkOffer { Kind = kind, Outcome = VerbOutcome.Done, Seconds = seconds, Words = words };

        /// <summary>A time in a person's words: seconds under a minute and a half, minutes under an hour and a half, hours beyond.</summary>
        public static string About(double seconds)
        {
            if (seconds < 90.0) return "about " + Math.Max(1, Math.Round(seconds)).ToString("0", CultureInfo.InvariantCulture) + " s";
            if (seconds < 5400.0) return "about " + Math.Max(1, Math.Round(seconds / 60.0)).ToString("0", CultureInfo.InvariantCulture) + " min";
            return "about " + (seconds / 3600.0).ToString("0.#", CultureInfo.InvariantCulture) + " h";
        }

        /// <summary>The volume a point takes off a stick, cm³: a cone three diameters long, π/12 d² × 3d.</summary>
        private static double PointVolumeCm3(double diameterM) => Math.PI / 4.0 * diameterM * diameterM * diameterM * 1e6;

        /// <summary>
        /// How long the rest of a felling cut takes, s: the notch's volume at the cut's height, less what is cut already, at a
        /// rate the edge, the thing's mass and the wood's softness set (a stated model, DEBTS).
        /// </summary>
        public static double CutSecondsLeft(PlantSpecies tree, double heightM, double done01, Definition tool, in ThingState toolState)
        {
            TreeGeometry geometry = TreeGeometries.For(tree);
            double d = 2.0 * TreeGeometries.TrunkRadiusAt(geometry, heightM, CutHeightM);
            double volume = CutVolumeShare * d * d * d * 1e6;
            Wood wood = Wood.Of(tree);
            double softness = wood != null ? wood.Softness01 : 0.5;
            double rate = CutCm3PerSecond * toolState.Edge01 * (ThingWords.MassOf(tool, toolState) / CutLeastMassKg) * (0.3 + 0.7 * softness);
            return Math.Max(1.0, volume * (1.0 - SimMath.Clamp01(done01)) / Math.Max(1e-6, rate));
        }

        /// <summary>Whether this work can be done to this thing with that in hand, how long it takes, and the words for it or for why not.</summary>
        public static WorkOffer Judge(WorkKind kind, Definition tool, in ThingState toolState, Definition target, in ThingState targetState)
        {
            if (target == null) return Refuse(kind, VerbOutcome.NotThere, "nothing there");
            switch (kind)
            {
                case WorkKind.Break:
                {
                    if (!IsStick(target)) return Refuse(kind, VerbOutcome.WontWork, target.DisplayName + " is no stick to break");
                    double d = DiameterOf(target, targetState);
                    double limit = MaxBreakableDiameterM(RuptureOf(target));
                    if (d > limit) return Refuse(kind, VerbOutcome.WontWork, "too thick to break over the knee, " + ThingWords.ThicknessWord(d) + "; an edge would do it");
                    return Yes(kind, BreakSeconds, "break the stick over your knee");
                }
                case WorkKind.Strip:
                {
                    if (!IsStick(target)) return Refuse(kind, VerbOutcome.WontWork, "no bark on " + target.DisplayName);
                    PlantSpecies species = target.Row as PlantSpecies;
                    if (species == null) return Refuse(kind, VerbOutcome.WontWork, "a stick of no tree: no bark to name");
                    if (species.StrippableBarkM <= 0.0) return Refuse(kind, VerbOutcome.WontWork, "its bark will not come away");
                    if (Marked(targetState, ThingMarks.Stripped)) return Refuse(kind, VerbOutcome.WontWork, "already stripped");
                    double seconds = LengthOf(targetState) * StripSecondsPerMetre;
                    return Yes(kind, seconds, "strip the bark, " + About(seconds));
                }
                case WorkKind.Point:
                {
                    if (tool == null) return Refuse(kind, VerbOutcome.NoTool, "nothing in hand to point it with: an edge is wanted");
                    if (!IsEdge(tool, toolState)) return Refuse(kind, VerbOutcome.NoTool, ThingWords.Describe(tool, toolState) + " is no edge to carve with");
                    if (!IsStick(target)) return Refuse(kind, VerbOutcome.WontWork, target.DisplayName + " takes no point");
                    if (Marked(targetState, ThingMarks.Pointed)) return Refuse(kind, VerbOutcome.WontWork, "already pointed");
                    double d = DiameterOf(target, targetState);
                    if (d > PointableDiameterM) return Refuse(kind, VerbOutcome.WontWork, "too thick to point, " + ThingWords.ThicknessWord(d) + "; it wants splitting first");
                    double rate = CarveCm3PerSecond * toolState.Edge01 * (0.3 + 0.7 * SoftnessOf(target));
                    double seconds = PointVolumeCm3(d) / rate;
                    return Yes(kind, seconds, "point the stick with " + tool.DisplayName + ", " + About(seconds));
                }
                case WorkKind.Twist:
                {
                    if (tool == null || (tool.Substance != Substance.Bark && tool.Substance != Substance.Cord && tool.Substance != Substance.Fibre))
                        return Refuse(kind, VerbOutcome.NoTool, "cord wants a strip or a cord in hand");
                    if (target.Substance != Substance.Bark && target.Substance != Substance.Fibre) return Refuse(kind, VerbOutcome.WontWork, target.DisplayName + " will not lay into cord");
                    return Yes(kind, TwistSecondsPerJoin, "lay the strip into cord, " + About(TwistSecondsPerJoin));
                }
                case WorkKind.StripTrunk:
                {
                    if (!IsTrunk(target, out PlantSpecies tree)) return Refuse(kind, VerbOutcome.WontWork, target.DisplayName + " is no trunk to strip");
                    if (tree.StrippableBarkM <= 0.0) return Refuse(kind, VerbOutcome.WontWork, "its bark will not come away");
                    if (Marked(targetState, ThingMarks.Stripped)) return Refuse(kind, VerbOutcome.WontWork, "its bark is taken already");
                    return Yes(kind, StripTrunkSeconds, "strip the bark off the " + tree.DisplayName + ", " + About(StripTrunkSeconds));
                }
                case WorkKind.CutFibre:
                {
                    if (tool == null) return Refuse(kind, VerbOutcome.NoTool, "nothing in hand to cut with: an edge is wanted");
                    if (!IsEdge(tool, toolState)) return Refuse(kind, VerbOutcome.NoTool, ThingWords.Describe(tool, toolState) + " is no edge to cut with");
                    if (!IsTuft(target, out PlantSpecies plant)) return Refuse(kind, VerbOutcome.WontWork, target.DisplayName + " gives no fibre");
                    if (!plant.Fibre) return Refuse(kind, VerbOutcome.WontWork, plant.DisplayName + " gives no fibre worth the cutting");
                    return Yes(kind, CutFibreSeconds, "cut fibre from the " + plant.DisplayName + " with " + tool.DisplayName + ", " + About(CutFibreSeconds));
                }
                case WorkKind.PullTuft:
                {
                    if (!IsTuft(target, out PlantSpecies plant)) return Refuse(kind, VerbOutcome.WontWork, target.DisplayName + " does not pull");
                    TuftShape? shape = Tufts.ShapeOf(plant);
                    if (shape == null || !Tufts.Pullable(shape.Value)) return Refuse(kind, VerbOutcome.WontWork, plant.DisplayName + " is woody: it will not pull");
                    return Yes(kind, PullSeconds, "pull the " + plant.DisplayName + " up, " + About(PullSeconds));
                }
                case WorkKind.CutTrunk:
                {
                    if (tool == null) return Refuse(kind, VerbOutcome.NoTool, "nothing in hand to cut with: a heavy edge is wanted");
                    if (!IsEdge(tool, toolState) || toolState.Edge01 < CutLeastEdge) return Refuse(kind, VerbOutcome.NoTool, ThingWords.Describe(tool, toolState) + " is no edge to cut a tree with");
                    if (ThingWords.MassOf(tool, toolState) < CutLeastMassKg) return Refuse(kind, VerbOutcome.NoTool, ThingWords.Describe(tool, toolState) + " is too light to cut a tree with");
                    if (!IsTrunk(target, out PlantSpecies tree)) return Refuse(kind, VerbOutcome.WontWork, target.DisplayName + " is no tree to cut");
                    double seconds = CutSecondsLeft(tree, LengthOf(targetState), CutDone(targetState), tool, toolState);
                    return Yes(kind, seconds, "cut the " + tree.DisplayName + " through with " + tool.DisplayName + ", " + About(seconds));
                }
                default:
                    return Refuse(kind, VerbOutcome.NotNow, "no such work");
            }
        }

        /// <summary>Every kind of work on a thing judged for what is looked at and what is in hand, in the stated order.</summary>
        public static IReadOnlyList<WorkOffer> Offers(Definition tool, in ThingState toolState, Definition target, in ThingState targetState)
        {
            WorkOffer[] offers = new WorkOffer[Kinds.Length];
            for (int i = 0; i < Kinds.Length; i++) offers[i] = Judge(Kinds[i], tool, toolState, target, targetState);
            return offers;
        }

        /// <summary>The first offer that can be done, or none.</summary>
        public static WorkOffer? First(IReadOnlyList<WorkOffer> offers)
        {
            if (offers == null) return null;
            for (int i = 0; i < offers.Count; i++) if (offers[i].Possible) return offers[i];
            return null;
        }

        private static double U(ulong salt, int k) => (StandLayout.Mix(salt ^ ((ulong)(uint)k << 40) ^ 0x2B7E151628AED2A6UL) & 0xFFFFFFUL) / 16777216.0;
        private static byte LookOf(ulong salt, int k) => (byte)(StandLayout.Mix(salt ^ ((ulong)(uint)k << 40) ^ 0x3C6EF372FE94F82BUL) % (ulong)StandLayout.Looks);

        /// <summary>
        /// What a finished work leaves, judged again first: a work that cannot be done leaves nothing and says why. The salt
        /// seeds the distribution (§9): a break's split, the new things' looks.
        /// </summary>
        public static WorkResult Apply(WorkKind kind, Definition tool, in ThingState toolState, Definition target, in ThingState targetState, ulong salt)
        {
            WorkResult r = new WorkResult();
            WorkOffer offer = Judge(kind, tool, toolState, target, targetState);
            if (!offer.Possible)
            {
                r.Words = offer.Words;
                return r;
            }
            switch (kind)
            {
                case WorkKind.Break:
                {
                    double length = LengthOf(targetState);
                    double mass = targetState.Has(ThingFields.Mass) ? targetState.MassKg : target.MassKg;
                    double share = LeastBreakShare + (1.0 - 2.0 * LeastBreakShare) * U(salt, 1);
                    for (int i = 0; i < 2; i++)
                    {
                        double part = i == 0 ? share : 1.0 - share;
                        ThingState s = targetState;
                        s.SetLength((float)(length * part));
                        s.SetMass((float)(mass * part));
                        s.SetLook(LookOf(salt, 2 + i));
                        r.Made.Add(new MadeThing { Definition = target, State = s });
                    }
                    r.TargetSpent = true;
                    r.Words = "the stick broke in two: " + ThingWords.LengthWord(r.Made[0].State.LengthM) + " and " + ThingWords.LengthWord(r.Made[1].State.LengthM);
                    return r;
                }
                case WorkKind.Strip:
                {
                    PlantSpecies species = (PlantSpecies)target.Row;
                    double length = LengthOf(targetState);
                    double d = DiameterOf(target, targetState);
                    double moisture = MoistureOf(targetState);
                    int strips = Math.Max(1, (int)Math.Round(length / StripLengthM));
                    double thickness = Math.Min(species.StrippableBarkM, BarkShareOfDiameter * d);
                    double each = Math.PI * d * StripLengthM * thickness * BarkDensityKgM3 * (1.0 + moisture);
                    Definition bark = DefinitionCatalogue.BarkOf(species);
                    for (int i = 0; i < strips; i++)
                    {
                        ThingState s = default;
                        s.SetLength((float)StripLengthM);
                        s.SetMoisture((float)moisture);
                        s.SetMass((float)each);
                        s.SetLook(LookOf(salt, 10 + i));
                        r.Made.Add(new MadeThing { Definition = bark, State = s });
                    }
                    ThingState after = targetState;
                    double mass = targetState.Has(ThingFields.Mass) ? targetState.MassKg : target.MassKg;
                    after.SetMass((float)Math.Max(0.001, mass - strips * each));
                    after.SetMarks((ushort)((after.Has(ThingFields.Marks) ? after.Marks : 0) | ThingMarks.Stripped));
                    r.TargetChanged = true;
                    r.TargetAfter = after;
                    r.Words = "the bark came away in " + strips.ToString(CultureInfo.InvariantCulture) + (strips == 1 ? " strip" : " strips");
                    return r;
                }
                case WorkKind.Point:
                {
                    double d = DiameterOf(target, targetState);
                    double volume = PointVolumeCm3(d);
                    ThingState after = targetState;
                    after.SetMarks((ushort)((after.Has(ThingFields.Marks) ? after.Marks : 0) | ThingMarks.Pointed));
                    r.TargetChanged = true;
                    r.TargetAfter = after;
                    ThingState worn = toolState;
                    double wear = EdgeWearPer10Cm3 * (volume / 10.0) * (1.0 + (1.0 - SoftnessOf(target)));
                    worn.SetEdge((float)Math.Max(0.0, toolState.Edge01 - wear));
                    r.ToolChanged = true;
                    r.ToolAfter = worn;
                    r.Words = "the stick is pointed; the edge is " + ThingWords.EdgeWord(worn.Edge01);
                    return r;
                }
                case WorkKind.Twist:
                {
                    double stripLength = LengthOf(targetState);
                    double stripMass = targetState.Has(ThingFields.Mass) ? targetState.MassKg : target.MassKg;
                    double stripMoisture = MoistureOf(targetState);
                    r.TargetSpent = true;
                    if (tool.Substance == Substance.Cord)
                    {
                        ThingState grown = toolState;
                        double mass = toolState.Has(ThingFields.Mass) ? toolState.MassKg : tool.MassKg;
                        grown.SetLength((float)(LengthOf(toolState) + CordPerStripLength * stripLength));
                        grown.SetMoisture((float)((MoistureOf(toolState) * mass + stripMoisture * stripMass) / (mass + stripMass)));
                        grown.SetMass((float)(mass + stripMass));
                        r.ToolChanged = true;
                        r.ToolAfter = grown;
                        r.Words = "the cord is " + ThingWords.LengthWord(grown.LengthM) + " now";
                        return r;
                    }
                    double toolLength = LengthOf(toolState);
                    double toolMass = toolState.Has(ThingFields.Mass) ? toolState.MassKg : tool.MassKg;
                    ThingState cord = default;
                    cord.SetLength((float)(CordPerStripLength * (toolLength + stripLength)));
                    cord.SetDiameter((float)CordDiameterM);
                    cord.SetMoisture((float)((MoistureOf(toolState) * toolMass + stripMoisture * stripMass) / (toolMass + stripMass)));
                    cord.SetMass((float)(toolMass + stripMass));
                    cord.SetLook(LookOf(salt, 20));
                    r.Made.Add(new MadeThing { Definition = DefinitionCatalogue.Cord, State = cord });
                    r.ToolSpent = true;
                    r.Words = "two strips laid into cord, " + ThingWords.LengthWord(cord.LengthM);
                    return r;
                }
                case WorkKind.StripTrunk:
                {
                    PlantSpecies tree = (PlantSpecies)target.Row;
                    double girth = Math.PI * DiameterOf(target, targetState);
                    double moisture = MoistureOf(targetState);
                    int strips = Math.Max(1, (int)Math.Round(girth / TrunkStripPerGirthM));
                    double each = TrunkStripLengthM * TrunkStripWidthM * tree.StrippableBarkM * BarkDensityKgM3 * (1.0 + moisture);
                    Definition bark = DefinitionCatalogue.BarkOf(tree);
                    for (int i = 0; i < strips; i++)
                    {
                        ThingState s = default;
                        s.SetLength((float)TrunkStripLengthM);
                        s.SetMoisture((float)moisture);
                        s.SetMass((float)each);
                        s.SetLook(LookOf(salt, 30 + i));
                        r.Made.Add(new MadeThing { Definition = bark, State = s });
                    }
                    ThingState after = targetState;
                    after.SetMarks((ushort)((after.Has(ThingFields.Marks) ? after.Marks : 0) | ThingMarks.Stripped));
                    r.TargetChanged = true;
                    r.TargetAfter = after;
                    r.TrunkFlags = TrunkChange.BarkTaken;
                    r.Words = "the bark came away in " + strips.ToString(CultureInfo.InvariantCulture) + (strips == 1 ? " strip" : " strips") + " round the trunk";
                    return r;
                }
                case WorkKind.CutFibre:
                {
                    PlantSpecies plant = (PlantSpecies)target.Row;
                    double moisture = MoistureOf(targetState);
                    Definition fibre = DefinitionCatalogue.FibreOf(plant);
                    for (int i = 0; i < FibreStrips; i++)
                    {
                        ThingState s = default;
                        s.SetLength((float)FibreLengthM);
                        s.SetMoisture((float)moisture);
                        s.SetMass((float)(FibreMassKg * (1.0 + moisture)));
                        s.SetLook(LookOf(salt, 40 + i));
                        r.Made.Add(new MadeThing { Definition = fibre, State = s });
                    }
                    r.TargetSpent = true;
                    r.Words = FibreStrips.ToString(CultureInfo.InvariantCulture) + " strips of " + plant.DisplayName + " fibre cut";
                    return r;
                }
                case WorkKind.PullTuft:
                {
                    PlantSpecies plant = (PlantSpecies)target.Row;
                    ThingState s = default;
                    s.SetMoisture((float)MoistureOf(targetState));
                    s.SetMass((float)(BundleKg * (1.0 + MoistureOf(targetState))));
                    s.SetLook(LookOf(salt, 50));
                    r.Made.Add(new MadeThing { Definition = DefinitionCatalogue.BundleOf(plant), State = s });
                    r.TargetSpent = true;
                    r.Words = "the " + plant.DisplayName + " came up whole: a bundle";
                    return r;
                }
                case WorkKind.CutTrunk:
                {
                    PlantSpecies tree = (PlantSpecies)target.Row;
                    TreeGeometry geometry = TreeGeometries.For(tree);
                    Wood wood = Wood.Of(tree);
                    double density = wood != null ? wood.DensityDryKgM3 : LyingProperties.PlainStickDensityKgM3;
                    double green = wood != null ? wood.GreenMoisture : 0.6;
                    double height = LengthOf(targetState);
                    double trunk = TreeGeometries.TrunkLengthM(geometry, height);
                    int logs = Math.Max(1, (int)Math.Ceiling(trunk / LogLengthM));
                    Definition log = DefinitionCatalogue.LogOf(tree);
                    for (int i = 0; i < logs; i++)
                    {
                        double from = i * LogLengthM;
                        double length = Math.Min(LogLengthM, trunk - from);
                        if (length < 0.2) break;
                        double d = 2.0 * TreeGeometries.TrunkRadiusAt(geometry, height, from + 0.5 * length);
                        ThingState s = default;
                        s.SetLength((float)length);
                        s.SetDiameter((float)d);
                        s.SetMoisture((float)green);
                        s.SetMass((float)(density * (1.0 + green) * Math.PI / 4.0 * d * d * length));
                        s.SetLook(LookOf(salt, 60 + i));
                        r.Made.Add(new MadeThing { Definition = log, State = s, AlongM = 1.0 + from + 0.5 * length, AcrossM = 0.0 });
                    }
                    int limbs = Math.Min(MostLimbs, Math.Max(1, (int)Math.Round(height * LimbsPerMetre)));
                    Definition stick = DefinitionCatalogue.StickOf(tree);
                    for (int i = 0; i < limbs; i++)
                    {
                        ThingState s = default;
                        s.SetLength((float)LimbLengthM);
                        s.SetDiameter((float)LimbDiameterM);
                        s.SetMoisture((float)green);
                        s.SetMass((float)(density * (1.0 + green) * Math.PI / 4.0 * LimbDiameterM * LimbDiameterM * LimbLengthM));
                        s.SetLook(LookOf(salt, 80 + i));
                        r.Made.Add(new MadeThing { Definition = stick, State = s, AlongM = 1.5 + trunk + 0.6 * i, AcrossM = (i % 2 == 0 ? 0.8 : -0.8) });
                    }
                    r.TargetSpent = true;
                    r.TrunkFlags = (byte)(TrunkChange.Felled | TrunkChange.LimbsTaken);
                    int made = r.Made.Count - limbs;
                    r.Words = "the " + tree.DisplayName + " came down: " + made.ToString(CultureInfo.InvariantCulture) + (made == 1 ? " log" : " logs") + " and "
                              + limbs.ToString(CultureInfo.InvariantCulture) + (limbs == 1 ? " limb lie" : " limbs lie") + " where it fell";
                    return r;
                }
                default:
                    r.Words = "no such work";
                    return r;
            }
        }

        /// <summary>Whether this work can be done to this cell of the ground with that in hand, how long it takes, and the words (BF.3).</summary>
        public static WorkOffer JudgeGround(WorkKind kind, Definition tool, in ThingState toolState, in GroundSite site)
        {
            switch (kind)
            {
                case WorkKind.ClearGround:
                {
                    if (tool != null) return Refuse(kind, VerbOutcome.WontWork, "clearing wants empty hands: put " + tool.DisplayName + " down");
                    if (site.Cleared) return Refuse(kind, VerbOutcome.WontWork, "the ground is cleared already");
                    int left = site.TuftsLeft;
                    if (left == 0) return Refuse(kind, VerbOutcome.WontWork, "nothing grows here to clear");
                    double seconds = ClearSecondsPerTuft * left;
                    return Yes(kind, seconds, "clear the ground of its " + left.ToString(CultureInfo.InvariantCulture) + (left == 1 ? " tuft, " : " tufts, ") + About(seconds));
                }
                case WorkKind.Dig:
                {
                    if (tool == null) return Refuse(kind, VerbOutcome.NoTool, "nothing in hand to dig with: a pointed stick is wanted");
                    if (!IsDiggingStick(tool, toolState)) return Refuse(kind, VerbOutcome.NoTool, ThingWords.Describe(tool, toolState) + " is no digging stick");
                    if (site.WaterDepthM > 0.0) return Refuse(kind, VerbOutcome.WontWork, "under water: the hole would fill");
                    switch (site.Cover)
                    {
                        case GroundCover.Rock: return Refuse(kind, VerbOutcome.WontWork, "rock will not dig");
                        case GroundCover.Sea:
                        case GroundCover.FreshWater: return Refuse(kind, VerbOutcome.WontWork, "under water: the hole would fill");
                        case GroundCover.SwampFloor: return Refuse(kind, VerbOutcome.WontWork, "the swamp's floor is wet mud: it will not hold a hole");
                        case GroundCover.Unknown: return Refuse(kind, VerbOutcome.WontWork, "the ground here is not known");
                    }
                    if (site.RockStands) return Refuse(kind, VerbOutcome.WontWork, "a boulder sits on this ground");
                    if (site.SoilDepthM < LeastSoilM) return Refuse(kind, VerbOutcome.WontWork, "the soil is too thin here: the stick meets stone");
                    if (site.SoilDepthM - site.DugCm / 100.0 < DigStepCm / 100.0 - 1e-6) return Refuse(kind, VerbOutcome.WontWork, "the hole is down to the stone");
                    bool sand = site.Cover == GroundCover.Sand || site.Cover == GroundCover.DuneSand;
                    double seconds = (sand ? DigSecondsSand : DigSecondsSoil) * (DigStepCm / 10.0);
                    return Yes(kind, seconds, "dig with " + tool.DisplayName + ", " + About(seconds));
                }
                default:
                    return Refuse(kind, VerbOutcome.NotNow, "no such work on the ground");
            }
        }

        /// <summary>Every kind of work on the ground judged for the cell looked at and what is in hand, in the stated order.</summary>
        public static IReadOnlyList<WorkOffer> GroundOffers(Definition tool, in ThingState toolState, in GroundSite site)
        {
            WorkOffer[] offers = new WorkOffer[GroundKinds.Length];
            for (int i = 0; i < GroundKinds.Length; i++) offers[i] = JudgeGround(GroundKinds[i], tool, toolState, site);
            return offers;
        }

        /// <summary>What a finished work on the ground leaves, judged again first (BF.3): the bundles of a cleared cell where its tufts stood, a dig's depth and its tuber.</summary>
        public static GroundResult ApplyGround(WorkKind kind, Definition tool, in ThingState toolState, in GroundSite site, ulong salt)
        {
            GroundResult r = new GroundResult { DugCm = site.DugCm };
            WorkOffer offer = JudgeGround(kind, tool, toolState, site);
            if (!offer.Possible)
            {
                r.Words = offer.Words;
                return r;
            }
            switch (kind)
            {
                case WorkKind.ClearGround:
                {
                    int count = Math.Min(site.Tufts.Count, WorldChanges.MostTufts);
                    double moisture = LyingProperties.MoistureByQuarter[Tufts.Quarter(site.Quarter)];
                    if (!(site.CellM > 0.0)) throw new ArgumentOutOfRangeException(nameof(site), "a ground site names its cell's size; this one's is " + site.CellM);
                    int cellCm = (int)Math.Round(site.CellM * 100.0);
                    int bundles = 0;
                    for (int k = 0; k < count; k++)
                    {
                        if (!site.Stands(k)) continue;
                        r.TuftsTaken |= (ushort)(1 << k);
                        ulong hash = Tufts.HashOf(site.Row, site.Col, k);
                        TuftShape shape = Tufts.ShapeOfIndex(site.Tufts, k, hash);
                        PlantSpecies plant = Tufts.SpeciesOf(shape, site.Understory);
                        if (plant == null) continue;
                        StandLayout.Place(site.Row, site.Col, StandLayout.Kind.Tuft, k, cellCm, out int eastCm, out int northCm, out _);
                        ThingState s = default;
                        s.SetMoisture((float)moisture);
                        s.SetMass((float)(BundleKg * (1.0 + moisture)));
                        s.SetLook(LookOf(salt, 100 + k));
                        r.Made.Add(new MadeThing { Definition = DefinitionCatalogue.BundleOf(plant), State = s, AlongM = northCm / 100.0, AcrossM = eastCm / 100.0 });
                        bundles++;
                    }
                    r.Cleared = true;
                    r.Words = "the ground is cleared: " + bundles.ToString(CultureInfo.InvariantCulture) + (bundles == 1 ? " bundle lies" : " bundles lie") + " on it";
                    return r;
                }
                case WorkKind.Dig:
                {
                    int before = site.DugCm;
                    r.DugCm = (byte)Math.Min(255, before + DigStepCm);
                    PlantSpecies plant = site.Understory;
                    if (plant != null && plant.TuberKg > 0.0 && before < TuberDepthCm)
                    {
                        ThingState s = default;
                        s.SetMass((float)(plant.TuberKg * (0.7 + 0.6 * U(salt, 3))));
                        s.SetMoisture(0.7f);
                        s.SetLook(LookOf(salt, 120));
                        r.Made.Add(new MadeThing { Definition = DefinitionCatalogue.TuberOf(plant), State = s });
                        r.Words = "dug a hand deeper, " + (r.DugCm / 100.0).ToString("0.0", CultureInfo.InvariantCulture) + " m down; a " + plant.DisplayName + " tuber came up";
                    }
                    else r.Words = "dug a hand deeper, " + (r.DugCm / 100.0).ToString("0.0", CultureInfo.InvariantCulture) + " m down; nothing but earth";
                    return r;
                }
                default:
                    r.Words = "no such work on the ground";
                    return r;
            }
        }
    }
}
