using System;

namespace EarthGame.Engine
{
    /// <summary>Which of a thing's properties are its own rather than its kind's (BF.1); the bits of <see cref="ThingState.Fields"/>, never renumbered.</summary>
    [Flags]
    public enum ThingFields : ushort
    {
        None = 0,
        /// <summary>Its own mass, kg.</summary>
        Mass = 1,
        /// <summary>Its length, m: a stick's, a log's, a cord's.</summary>
        Length = 2,
        /// <summary>Its thickness, m: a stick's, a cobble's across.</summary>
        Diameter = 4,
        /// <summary>Its water as a share of its dry mass: 0.12 is air-dry wood, 0.30 the fibre saturation point, 1.0 green and wet through.</summary>
        Moisture = 8,
        /// <summary>The edge it carries, 0 to 1 (FP.3).</summary>
        Edge = 16,
        /// <summary>The angle a struck core's platform presents, degrees (FP.3).</summary>
        Platform = 32,
        /// <summary>Flakes taken off a core (FP.3).</summary>
        Flakes = 64,
        /// <summary>The shape it is drawn in, one of <see cref="StandLayout.Looks"/>: the shape it lay in, kept wherever it goes.</summary>
        Look = 128,
        /// <summary>How sound it is, 0 to 1: a tool's wear, a stick's rot.</summary>
        Condition = 256,
        /// <summary>What work has left on it, as bits the process model names (BF.2); none are set by BF.1.</summary>
        Marks = 512,
        All = Mass | Length | Diameter | Moisture | Edge | Platform | Flakes | Look | Condition | Marks,
    }

    /// <summary>
    /// A thing's own state (BF.1, 2026-09-22): what it has of its own beyond its kind. The mask says which fields are the
    /// thing's; a field the mask leaves out is not carried and reads as nothing, and the thing's kind answers for it (its
    /// definition's mass, its place's shape). One record for a thing lying, carried, saved and named: written and read by
    /// <c>ThingWire</c> alone, so a field added here is added in one layout, not four. It replaced FP.3's four stone fields,
    /// which were written by hand in three formats and dropped from the carrying message.
    /// </summary>
    public struct ThingState
    {
        public ThingFields Fields;
        public float MassKg;
        public float LengthM;
        public float DiameterM;
        public float Moisture;
        public float Edge01;
        public float PlatformDeg;
        public ushort FlakesTaken;
        public byte Look;
        public float Condition01;
        public ushort Marks;

        /// <summary>Whether every field asked for is the thing's own.</summary>
        public bool Has(ThingFields fields) => fields != ThingFields.None && (Fields & fields) == fields;

        /// <summary>Whether the thing has anything of its own.</summary>
        public bool HasAny => Fields != ThingFields.None;

        public void SetMass(float kg) { MassKg = kg; Fields |= ThingFields.Mass; }
        public void SetLength(float m) { LengthM = m; Fields |= ThingFields.Length; }
        public void SetDiameter(float m) { DiameterM = m; Fields |= ThingFields.Diameter; }
        public void SetMoisture(float shareOfDryMass) { Moisture = shareOfDryMass; Fields |= ThingFields.Moisture; }
        public void SetEdge(float edge01) { Edge01 = edge01; Fields |= ThingFields.Edge; }
        public void SetPlatform(float degrees) { PlatformDeg = degrees; Fields |= ThingFields.Platform; }
        public void SetFlakes(ushort flakes) { FlakesTaken = flakes; Fields |= ThingFields.Flakes; }
        public void SetLook(byte look) { Look = look; Fields |= ThingFields.Look; }
        public void SetCondition(float condition01) { Condition01 = condition01; Fields |= ThingFields.Condition; }
        public void SetMarks(ushort marks) { Marks = marks; Fields |= ThingFields.Marks; }

        /// <summary>
        /// Whether the numbers are ones a thing could have (M1.5g's rule: a number that is not one is refused at the door):
        /// finite and not negative, an edge and a condition within one, a platform within a flat face. The reason when not.
        /// </summary>
        public bool IsPossible(out string why)
        {
            if (Has(ThingFields.Mass) && !(MassKg >= 0f && MassKg < float.PositiveInfinity)) { why = "a mass of " + MassKg; return false; }
            if (Has(ThingFields.Length) && !(LengthM >= 0f && LengthM < float.PositiveInfinity)) { why = "a length of " + LengthM; return false; }
            if (Has(ThingFields.Diameter) && !(DiameterM >= 0f && DiameterM < float.PositiveInfinity)) { why = "a thickness of " + DiameterM; return false; }
            if (Has(ThingFields.Moisture) && !(Moisture >= 0f && Moisture < float.PositiveInfinity)) { why = "a moisture of " + Moisture; return false; }
            if (Has(ThingFields.Edge) && !(Edge01 >= 0f && Edge01 <= 1f)) { why = "an edge of " + Edge01; return false; }
            if (Has(ThingFields.Platform) && !(PlatformDeg >= 0f && PlatformDeg <= 180f)) { why = "a platform of " + PlatformDeg + " degrees"; return false; }
            if (Has(ThingFields.Condition) && !(Condition01 >= 0f && Condition01 <= 1f)) { why = "a condition of " + Condition01; return false; }
            why = null;
            return true;
        }

        /// <summary>
        /// The state FP.3's four fields meant (region files 3, player files 6, protocol 15): a thing with a mass of its own had
        /// been struck, and carried its edge, its platform and its flakes with it; one without had nothing of its own.
        /// </summary>
        public static ThingState FromStruckStone(float massKg, float edge01, float platformDeg, ushort flakesTaken)
        {
            ThingState s = default;
            if (!(massKg > 0f)) return s;
            s.SetMass(massKg);
            s.SetEdge(edge01);
            s.SetPlatform(platformDeg);
            s.SetFlakes(flakesTaken);
            return s;
        }
    }
}
