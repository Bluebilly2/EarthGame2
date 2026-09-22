using System;
using System.Collections.Generic;
using System.Globalization;

namespace EarthGame.Engine
{
    /// <summary>A kind of work a founder does on a thing (BF.2). Wire-visible and never renumbered.</summary>
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

    /// <summary>A thing a finished work made.</summary>
    public struct MadeThing
    {
        public Definition Definition;
        public ThingState State;
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
        public string Words = string.Empty;
    }

    /// <summary>
    /// One model of work (BF.2, 2026-09-22): a kind of work on a thing looked at, with what is in hand, judged from the
    /// things' properties and never from their keys (GAME_DESIGN §7B: a transformation has requirements, a rate, results and
    /// failure modes; §9: it makes a distribution). <see cref="Judge"/> says whether and how long; <see cref="Apply"/> says
    /// what a finished work leaves. Both are pure, so the client offers what the server will accept and the tests read the
    /// same numbers the server commits. The knap stays a blow of its own (FP.3); everything that takes time comes here.
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

        /// <summary>Every kind of work, in the order the crosshair offers them.</summary>
        public static readonly WorkKind[] Kinds = { WorkKind.Break, WorkKind.Strip, WorkKind.Point, WorkKind.Twist };

        /// <summary>The thickest stick of a wood a person breaks over the knee, m: π d³ σ / 32 against the knee's moment.</summary>
        public static double MaxBreakableDiameterM(double ruptureMPa) => Math.Pow(32.0 * KneeMomentNm / (Math.PI * ruptureMPa * 1e6), 1.0 / 3.0);

        private static bool IsStick(Definition d) => d != null && d.Substance == Substance.Wood;
        private static bool IsStone(Definition d) => d != null && d.Substance == Substance.Stone;
        private static double RuptureOf(Definition d) { Wood w = DefinitionCatalogue.WoodOf(d); return w != null ? w.RuptureMPa : PlainRuptureMPa; }
        private static double SoftnessOf(Definition d) { Wood w = DefinitionCatalogue.WoodOf(d); return w != null ? w.Softness01 : 0.5; }
        private static double DiameterOf(Definition d, in ThingState s) => s.Has(ThingFields.Diameter) ? s.DiameterM : 2.0 * d.RadiusM;
        private static double LengthOf(in ThingState s) => s.Has(ThingFields.Length) ? s.LengthM : 1.0;
        private static double MoistureOf(in ThingState s) => s.Has(ThingFields.Moisture) ? s.Moisture : 0.15;
        private static bool Marked(in ThingState s, ushort mark) => s.Has(ThingFields.Marks) && (s.Marks & mark) != 0;

        /// <summary>Whether the thing in hand is an edge fit to carve: a stone with an edge at least <see cref="Knapping.UsableEdge"/>.</summary>
        public static bool IsEdge(Definition tool, in ThingState toolState) => IsStone(tool) && toolState.Has(ThingFields.Edge) && toolState.Edge01 >= Knapping.UsableEdge;

        private static WorkOffer Refuse(WorkKind kind, VerbOutcome outcome, string words) => new WorkOffer { Kind = kind, Outcome = outcome, Seconds = 0.0, Words = words };
        private static WorkOffer Yes(WorkKind kind, double seconds, string words) => new WorkOffer { Kind = kind, Outcome = VerbOutcome.Done, Seconds = seconds, Words = words };

        private static string About(double seconds) => "about " + Math.Max(1, Math.Round(seconds)).ToString("0", CultureInfo.InvariantCulture) + " s";

        /// <summary>The volume a point takes off a stick, cm³: a cone three diameters long, π/12 d² × 3d.</summary>
        private static double PointVolumeCm3(double diameterM) => Math.PI / 4.0 * diameterM * diameterM * diameterM * 1e6;

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
                    if (tool == null || (tool.Substance != Substance.Bark && tool.Substance != Substance.Cord))
                        return Refuse(kind, VerbOutcome.NoTool, "cord wants a strip or a cord in hand");
                    if (target.Substance != Substance.Bark) return Refuse(kind, VerbOutcome.WontWork, target.DisplayName + " will not lay into cord");
                    return Yes(kind, TwistSecondsPerJoin, "lay the strip into cord, " + About(TwistSecondsPerJoin));
                }
                default:
                    return Refuse(kind, VerbOutcome.NotNow, "no such work");
            }
        }

        /// <summary>Every kind of work judged for what is looked at and what is in hand, in the stated order.</summary>
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
                default:
                    r.Words = "no such work";
                    return r;
            }
        }
    }
}
