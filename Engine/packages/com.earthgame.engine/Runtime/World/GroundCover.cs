using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// What a person standing on a square metre of this country would say is under their boots. Wire-visible (a
    /// tile of <see cref="TileLayer.GroundCover"/> carries these) and never renumbered: a retired cover keeps its
    /// number.
    ///
    /// <para>These are grounds, not species and not landforms. A species list will grow — the ecosystem's second
    /// contract adds to it — and a ground colour that had to be extended with every plant would be a second copy
    /// of the species list. What varies inside one of these is carried by the wetness quarter beside it.</para>
    /// </summary>
    public enum GroundCover : byte
    {
        /// <summary>Nothing said: a world with no cover layer, or a code from a later version.</summary>
        Unknown = 0,
        /// <summary>Under salt water.</summary>
        Sea = 1,
        /// <summary>Under fresh water: a lake, a stream, a creek.</summary>
        FreshWater = 2,
        /// <summary>A beach.</summary>
        Sand = 3,
        /// <summary>A dune.</summary>
        DuneSand = 4,
        /// <summary>A cliff, a shore platform, or ground too thin to hold anything.</summary>
        Rock = 5,
        /// <summary>Soil with nothing growing on it.</summary>
        BareEarth = 6,
        /// <summary>Woody shrubs: the heath banksia, the grass tree.</summary>
        Heath = 7,
        /// <summary>Bracken.</summary>
        Bracken = 8,
        /// <summary>Tussocky herbs: lomandra, saw-sedge.</summary>
        Sedge = 9,
        /// <summary>Grasses: kangaroo grass, spinifex.</summary>
        Grass = 10,
        /// <summary>Litter under a canopy with nothing growing beneath it.</summary>
        ForestFloor = 11,
        /// <summary>The floor of a wetland.</summary>
        SwampFloor = 12,
    }

    /// <summary>
    /// The world's answer to "what covers this cell", and the byte it travels in (M1.4d). The low six bits are the
    /// <see cref="GroundCover"/>; the top two are which quarter of the land's own wetness the cell falls in, so a
    /// dry spur and a dark gully of the same cover are told apart without sending the wetness field itself.
    ///
    /// <para>The field was priced before it was designed: over the nine tiles around the wake, the wetness layer
    /// costs 476 KB and the soil depth 501 KB against the ground's own 418 KB, because a continuous field over
    /// noisy country does not compress; this byte costs 164 KB (M1.4d contract, 2026-09-10). So the client is
    /// sent what the ingredients come to rather than the ingredients.</para>
    /// </summary>
    public static class GroundCovers
    {
        /// <summary>How many bands the land's wetness is told in.</summary>
        public const int Quarters = 4;

        /// <summary>The bits of a code that carry the cover.</summary>
        public const byte CoverMask = 0x3F;

        /// <summary>Soil thinner than this holds nothing, so what shows is the rock under it, m.</summary>
        public const double BareSoilM = 0.05;

        /// <summary>Every cover, in order, for anything that walks the set rather than being told it.</summary>
        public static readonly GroundCover[] All =
        {
            GroundCover.Unknown, GroundCover.Sea, GroundCover.FreshWater, GroundCover.Sand, GroundCover.DuneSand,
            GroundCover.Rock, GroundCover.BareEarth, GroundCover.Heath, GroundCover.Bracken, GroundCover.Sedge,
            GroundCover.Grass, GroundCover.ForestFloor, GroundCover.SwampFloor,
        };

        /// <summary>
        /// What covers a cell, from what the world made of it. The order is the whole rule and it reads as a
        /// person reads country: what is under water, then what is wet ground, then what is sand or rock, then
        /// what grows on it, and only then the litter of a canopy with nothing underneath.
        ///
        /// <para>The understory is asked before the canopy because the ground under an open banksia is the
        /// ground layer, not the tree: asking the canopy first painted a third of the peninsula as one colour
        /// when the layers were priced (2026-09-10). The canopy is drawn as trees when trees are drawn.</para>
        /// </summary>
        public static GroundCover Of(WaterClass water, uint topology, PlantSpecies understory, PlantSpecies overstory, double soilDepthM)
        {
            if (water == WaterClass.Sea) return GroundCover.Sea;
            if (water == WaterClass.Lake || water == WaterClass.Stream || water == WaterClass.Creek) return GroundCover.FreshWater;
            if ((topology & (uint)Topology.Wetland) != 0) return GroundCover.SwampFloor;
            if ((topology & (uint)Topology.Beach) != 0) return GroundCover.Sand;
            if ((topology & (uint)Topology.Dune) != 0) return GroundCover.DuneSand;
            if ((topology & (uint)(Topology.Cliff | Topology.ShorePlatform)) != 0) return GroundCover.Rock;
            if (!(soilDepthM > BareSoilM)) return GroundCover.Rock;
            if (understory != null)
                switch (understory.Form)
                {
                    case PlantForm.Shrub: return GroundCover.Heath;
                    // The only plant named here. Bracken shares its form with the tussocks and shares nothing
                    // else: it is a fern a metre high over a tenth of this peninsula, and it is not their colour.
                    case PlantForm.Herb: return understory == PlantSpecies.Bracken ? GroundCover.Bracken : GroundCover.Sedge;
                    case PlantForm.Grass: return GroundCover.Grass;
                }
            if (overstory != null) return GroundCover.ForestFloor;
            return GroundCover.BareEarth;
        }

        /// <summary>Which quarter of the land's own wetness a reading falls in; anything unreadable is the driest.</summary>
        public static int QuarterFor(double wetness01)
        {
            if (double.IsNaN(wetness01)) return 0;
            int quarter = (int)(wetness01 * Quarters);
            if (quarter < 0) return 0;
            return quarter >= Quarters ? Quarters - 1 : quarter;
        }

        /// <summary>The byte a cover and a quarter travel in.</summary>
        public static byte Pack(GroundCover cover, int quarter)
        {
            if (quarter < 0 || quarter >= Quarters) throw new ArgumentOutOfRangeException(nameof(quarter), "the land's wetness is told in " + Quarters + " quarters, not " + (quarter + 1));
            return (byte)(((byte)cover & CoverMask) | (quarter << 6));
        }

        public static GroundCover CoverOf(byte code) => (GroundCover)(code & CoverMask);

        public static int QuarterOf(byte code) => code >> 6;

        /// <summary>The cover's name as the verifier and the census print it; a cover this version does not know reads as unknown.</summary>
        public static string NameOf(GroundCover cover)
        {
            switch (cover)
            {
                case GroundCover.Sea: return "sea";
                case GroundCover.FreshWater: return "fresh water";
                case GroundCover.Sand: return "sand";
                case GroundCover.DuneSand: return "dune sand";
                case GroundCover.Rock: return "rock";
                case GroundCover.BareEarth: return "bare earth";
                case GroundCover.Heath: return "heath";
                case GroundCover.Bracken: return "bracken";
                case GroundCover.Sedge: return "sedge";
                case GroundCover.Grass: return "grass";
                case GroundCover.ForestFloor: return "forest floor";
                case GroundCover.SwampFloor: return "swamp floor";
                default: return "unknown";
            }
        }

        /// <summary>The legend a cover layer's sidecar carries, so a reader needs nothing but the file.</summary>
        public static string Legend()
        {
            string covers = "";
            foreach (GroundCover cover in All) covers += (covers.Length == 0 ? "" : ", ") + (byte)cover + "=" + NameOf(cover);
            return "the low six bits are the GroundCover (" + covers + "); the top two are which quarter of the land's own wetness the cell falls in";
        }
    }
}
