using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// What a lying thing's place says of it (BF.1): the tall plant over it, the ground's cover and wetness under it, and
    /// the stone its cell names. Read by the server from its rasters and by a client from its tiles, then handed to
    /// <see cref="LyingProperties"/>, which is the same function on both sides.
    /// </summary>
    public readonly struct LyingSite
    {
        /// <summary>The tall plant on the cell, or the nearest trunk in its 3 × 3; null where none stands near.</summary>
        public readonly PlantSpecies Species;
        /// <summary>The cell's cover byte (<see cref="GroundCovers"/>): what covers it and which quarter of the land's wetness it is in.</summary>
        public readonly byte Cover;
        /// <summary>The stone the stone layer names on the cell; null where it names none.</summary>
        public readonly StoneType Stone;

        public LyingSite(PlantSpecies species, byte cover, StoneType stone)
        {
            Species = species;
            Cover = cover;
            Stone = stone;
        }
    }

    /// <summary>
    /// A lying thing's properties come from its place, as its position does (BF.1, 2026-09-22): a stick under a blackbutt
    /// is a blackbutt stick, of a length and a thickness its address hashes to, as damp as its ground, weighing what that
    /// wood at that size and water weighs; a cobble is of its cell's stone and weighs by that stone's density. Whole-number
    /// hashing by <see cref="StandLayout.Mix"/> with a salt of its own, so a server and a client name the same stick alike
    /// however each runtime rounds. Nothing here is stored: a thing taken up keeps the state this gives it, and the layer
    /// stays what it was.
    /// </summary>
    public static class LyingProperties
    {
        /// <summary>Above this share of dry mass the wood's cell walls are full and free water sits in it: it will not take a coal and burns badly.</summary>
        public const float FibreSaturation = 0.30f;

        /// <summary>A stick's water by the cover's quarter of the land's wetness, as a share of dry mass: air-dry on the driest ground, sodden on the wettest.</summary>
        public static readonly float[] MoistureByQuarter = { 0.15f, 0.22f, 0.32f, 0.45f };

        /// <summary>A stick of the litter is 0.4 to 1.6 m long and 12 to 45 mm thick: what a eucalypt or a banksia sheds.</summary>
        public const double LeastStickLengthM = 0.4, MostStickLengthM = 1.6, LeastStickDiameterM = 0.012, MostStickDiameterM = 0.045;

        /// <summary>A stick of no tree (driftwood, a stick where nothing stands) is weighed as a wood of the middle density.</summary>
        public const double PlainStickDensityKgM3 = 500.0;

        /// <summary>A cobble of the litter weighs 0.35 to 1.0 kg were it of the plain cobble's density, scaled by its stone's; and is a sphere of its mass flattened by 1.15.</summary>
        public const double LeastCobbleKg = 0.35, MostCobbleKg = 1.0, CobbleFlattening = 1.15;

        private const ulong Salt = 0x5EED0F1EAD5EEDUL;

        /// <summary>The state a thing lying at this address on this site has: the same on every machine that asks.</summary>
        public static ThingState StateOf(in LyingThing thing, in LyingSite site)
        {
            ulong h = StandLayout.Mix(((ulong)(uint)thing.Row << 32) | (uint)thing.Col);
            h = StandLayout.Mix(h ^ ((ulong)thing.Kind << 56) ^ (uint)thing.Index ^ Salt);
            double u1 = (h & 0xFFFFFFUL) / 16777216.0;
            double u2 = ((h >> 24) & 0xFFFFFFUL) / 16777216.0;
            ThingState s = default;
            s.SetLook((byte)StandLayout.LookOf(thing.Row, thing.Col, thing.Kind, thing.Index));
            if (thing.Kind == StandLayout.Kind.Stick)
            {
                double length = LeastStickLengthM + (MostStickLengthM - LeastStickLengthM) * u1;
                double diameter = LeastStickDiameterM + (MostStickDiameterM - LeastStickDiameterM) * u2;
                float moisture = MoistureByQuarter[GroundCovers.QuarterOf(site.Cover)];
                Wood wood = Wood.Of(site.Species);
                double density = wood != null ? wood.DensityDryKgM3 : PlainStickDensityKgM3;
                s.SetLength((float)length);
                s.SetDiameter((float)diameter);
                s.SetMoisture(moisture);
                s.SetMass((float)(density * Math.PI / 4.0 * s.DiameterM * s.DiameterM * s.LengthM * (1.0 + s.Moisture)));
            }
            else if (thing.Kind == StandLayout.Kind.Cobble)
            {
                double density = site.Stone != null ? site.Stone.DensityKgM3 : DefinitionCatalogue.CobbleDensityKgM3;
                double mass = (LeastCobbleKg + (MostCobbleKg - LeastCobbleKg) * u1) * density / DefinitionCatalogue.CobbleDensityKgM3;
                s.SetMass((float)mass);
                s.SetDiameter((float)(Math.Pow(6.0 * s.MassKg / (Math.PI * density), 1.0 / 3.0) * CobbleFlattening));
            }
            return s;
        }

        /// <summary>What a thing lying on this site becomes when taken up: the stick of its tree or the plain stick; the cobble of its stone or the plain cobble.</summary>
        public static Definition DefinitionOf(StandLayout.Kind kind, in LyingSite site)
        {
            if (kind == StandLayout.Kind.Stick) return site.Species != null && StandCodes.IsTall(site.Species) ? DefinitionCatalogue.StickOf(site.Species) : DefinitionCatalogue.Stick;
            return DefinitionCatalogue.CobbleOf(site.Stone);
        }
    }

    /// <summary>Where a lying thing's site is read from: the server's rasters, or a client's tiles through the same rule.</summary>
    public static class LyingSites
    {
        /// <summary>The nearest trunk to a thing's own place among its cell and the eight round it; null when none of the nine holds one.</summary>
        public static PlantSpecies NearestTrunk(in LyingThing thing, double cellM, Func<int, int, int> standCodeAt)
        {
            int cellCm = (int)Math.Round(cellM * 100.0);
            int own = standCodeAt(thing.Row, thing.Col);
            if (own > 0) return StandCodes.SpeciesOf((byte)own);
            StandLayout.Place(thing.Row, thing.Col, thing.Kind, thing.Index, cellCm, out int eastCm, out int northCm, out _);
            PlantSpecies best = null;
            long bestD2 = long.MaxValue;
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    int code = standCodeAt(thing.Row + dr, thing.Col + dc);
                    if (code <= 0) continue;
                    PlantSpecies species = StandCodes.SpeciesOf((byte)code);
                    if (species == null) continue;
                    StandLayout.Place(thing.Row + dr, thing.Col + dc, StandLayout.Kind.Trunk, 0, cellCm, out int te, out int tn, out _);
                    long de = (te + dc * cellCm) - eastCm, dn = (tn - dr * cellCm) - northCm;
                    long d2 = de * de + dn * dn;
                    if (d2 < bestD2) { bestD2 = d2; best = species; }
                }
            return best;
        }

        /// <summary>The site by three readers of codes: the stand's (negative outside what is held), the cover's and the stone's at a cell.</summary>
        public static LyingSite Of(in LyingThing thing, double cellM, Func<int, int, int> standCodeAt, byte cover, StoneType stone) =>
            new LyingSite(NearestTrunk(thing, cellM, standCodeAt), cover, stone);

        /// <summary>The site as the server holds it, from the world's own rasters; a raster the world lacks reads as nothing.</summary>
        public static LyingSite Of(WorldState world, in LyingThing thing)
        {
            RegionRaster loose = world.Loose;
            double cellM = loose != null ? loose.CellM : 4.0;
            RegionRaster stand = world.Stand;
            int StandAt(int row, int col) => stand != null && row >= 0 && col >= 0 && row < stand.Height && col < stand.Width ? (int)stand.Code(row, col) : -1;
            byte cover = 0;
            RegionRaster covers = world.Cover;
            if (covers != null && thing.Row >= 0 && thing.Col >= 0 && thing.Row < covers.Height && thing.Col < covers.Width) cover = (byte)covers.Code(thing.Row, thing.Col);
            StoneType stone = null;
            RegionRaster stones = world.Stone;
            if (stones != null && thing.Row >= 0 && thing.Col >= 0 && thing.Row < stones.Height && thing.Col < stones.Width)
            {
                uint code = stones.Code(thing.Row, thing.Col);
                if (code >= 1 && code <= StoneType.All.Count) stone = StoneType.All[(int)code - 1];
            }
            return Of(thing, cellM, StandAt, cover, stone);
        }
    }
}
