using EarthGame.Engine;

namespace EarthGame.Protocol
{
    /// <summary>
    /// The one layout of a thing's own state (BF.1, protocol 17, region file 4, player file 7): u16 the mask of
    /// <see cref="ThingFields"/>, then each field the mask names in the mask's order — f32 mass kg, f32 length m, f32
    /// diameter m, f32 moisture as a share of dry mass, f32 edge 0–1, f32 platform degrees, u16 flakes taken, u8 look, f32
    /// condition 0–1, u16 marks. A thing with nothing of its own is the mask alone. A bit this build does not know is
    /// refused, never skipped, since its bytes would be read as something else; a number no thing could have is refused at
    /// the door (M1.5g). The entity wire, the carrying message, the region file, the player file and the digest's reader
    /// go through here and nowhere else.
    /// </summary>
    public static class ThingWire
    {
        public static void Write(PacketWriter w, in ThingState s)
        {
            w.WriteUInt16((ushort)s.Fields);
            if (s.Has(ThingFields.Mass)) w.WriteSingle(s.MassKg);
            if (s.Has(ThingFields.Length)) w.WriteSingle(s.LengthM);
            if (s.Has(ThingFields.Diameter)) w.WriteSingle(s.DiameterM);
            if (s.Has(ThingFields.Moisture)) w.WriteSingle(s.Moisture);
            if (s.Has(ThingFields.Edge)) w.WriteSingle(s.Edge01);
            if (s.Has(ThingFields.Platform)) w.WriteSingle(s.PlatformDeg);
            if (s.Has(ThingFields.Flakes)) w.WriteUInt16(s.FlakesTaken);
            if (s.Has(ThingFields.Look)) w.WriteByte(s.Look);
            if (s.Has(ThingFields.Condition)) w.WriteSingle(s.Condition01);
            if (s.Has(ThingFields.Marks)) w.WriteUInt16(s.Marks);
        }

        public static ThingState Read(PacketReader r)
        {
            ushort mask = r.ReadUInt16();
            if ((mask & ~(ushort)ThingFields.All) != 0)
                throw new ProtocolException("a thing's state carries fields this build does not know: " + mask.ToString("x4"));
            ThingState s = default;
            s.Fields = (ThingFields)mask;
            if (s.Has(ThingFields.Mass)) s.MassKg = r.ReadSingle();
            if (s.Has(ThingFields.Length)) s.LengthM = r.ReadSingle();
            if (s.Has(ThingFields.Diameter)) s.DiameterM = r.ReadSingle();
            if (s.Has(ThingFields.Moisture)) s.Moisture = r.ReadSingle();
            if (s.Has(ThingFields.Edge)) s.Edge01 = r.ReadSingle();
            if (s.Has(ThingFields.Platform)) s.PlatformDeg = r.ReadSingle();
            if (s.Has(ThingFields.Flakes)) s.FlakesTaken = r.ReadUInt16();
            if (s.Has(ThingFields.Look)) s.Look = r.ReadByte();
            if (s.Has(ThingFields.Condition)) s.Condition01 = r.ReadSingle();
            if (s.Has(ThingFields.Marks)) s.Marks = r.ReadUInt16();
            if (!s.IsPossible(out string why)) throw new ProtocolException("a thing's state has " + why + ", which no thing could have");
            return s;
        }

        /// <summary>
        /// FP.3's layout of a struck stone (protocol 15, region file 3, player file 6), still read from the older files: f32
        /// mass kg, f32 edge, f32 platform degrees, u16 flakes taken, zeros for a thing with nothing of its own.
        /// </summary>
        public static ThingState ReadStruckStone(PacketReader r)
        {
            float mass = r.ReadSingle();
            float edge = r.ReadSingle();
            float platform = r.ReadSingle();
            ushort flakes = r.ReadUInt16();
            if (!BodyWire.Finite(mass) || mass < 0f || !BodyWire.Finite(edge) || edge < 0f || edge > 1f || !BodyWire.Finite(platform) || platform < 0f || platform > 180f)
                throw new ProtocolException("a stone's state of mass " + mass + ", edge " + edge + " and platform " + platform + " is none a stone could have");
            return ThingState.FromStruckStone(mass, edge, platform, flakes);
        }
    }
}
