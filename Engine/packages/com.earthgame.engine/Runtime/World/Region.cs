using System;
using System.Collections.Generic;

namespace EarthGame.Engine
{
    /// <summary>
    /// A dam at its published point, and a published point on the creek it dams below its wall (WG.1b, 2026-09-24). The tiles
    /// carry its wall as ground, and the water it holds would leave its reservoir over the lowest saddle of the flat the tiles
    /// also carry, not across the wall where the creek ran before the dam: the water is let across the wall there and down
    /// the creek to the point below (<c>WorldLayers</c>).
    /// </summary>
    public sealed class Dam
    {
        public string Name { get; }
        public double LatitudeDeg { get; }
        public double LongitudeDeg { get; }
        /// <summary>A point on the dammed creek below the wall, lower than the reservoir.</summary>
        public double BelowLatitudeDeg { get; }
        public double BelowLongitudeDeg { get; }

        public Dam(string name, double latitudeDeg, double longitudeDeg, double belowLatitudeDeg, double belowLongitudeDeg)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            LatitudeDeg = latitudeDeg;
            LongitudeDeg = longitudeDeg;
            BelowLatitudeDeg = belowLatitudeDeg;
            BelowLongitudeDeg = belowLongitudeDeg;
        }
    }

    /// <summary>
    /// A bounded piece of the real Earth the game is set in: where it is, how big it is, and when the founder
    /// wakes there. Where in it they wake is the world's to choose (`WakeScorer`), not the region's: the point this
    /// class once named was withdrawn by CANON ruling 20 (2026-09-10). One owner for the numbers every other file used to repeat (the server host, the tests
    /// and the Unity bootstrap each carried "150.675" before this class existed; three copies of one fact is the
    /// named bug shape). The extent is stated here and nowhere else; a source scan enforces that no other file
    /// spells out a metre literal for the world's size (STANDARDS 2; plan §4.2).
    /// </summary>
    public sealed class Region
    {
        /// <summary>Stable id used in saves, the wire and the region raster folder.</summary>
        public string Id { get; }
        /// <summary>What the place is called, using landform names (CANON ruling 8).</summary>
        public string DisplayName { get; }
        /// <summary>The tangent plane's origin, degrees, north and east positive.</summary>
        public double CentreLatitudeDeg { get; }
        public double CentreLongitudeDeg { get; }
        /// <summary>Side of the square region in metres; the world ends here.</summary>
        public double ExtentM { get; }
        /// <summary>The canonical wake: day of the year (non-leap) and local solar hour.</summary>
        public int WakeDayOfYear { get; }
        public double WakeLocalHour { get; }

        /// <summary>
        /// The lakes that hold their water, by the names the water bake gives them (WG.1b, 2026-09-24): what drains into one ends
        /// in it, where every other lake passes its water on at its own level. A lake is named here only with a published reason,
        /// stated where the region is.
        /// </summary>
        public IReadOnlyList<string> HeldLakes { get; }

        /// <summary>The dams whose walls the water is let across, at their published points (WG.1b).</summary>
        public IReadOnlyList<Dam> Dams { get; }

        /// <summary>
        /// Which of the plant catalogue's plants the region's country carries, in the catalogue's order (WG.2c, 2026-09-25): the
        /// regional species pool. Which plants reached a country is its history, and the tolerances then place them within it
        /// (<see cref="PlantCommunity"/>), so the list says nothing about where in the region a plant stands. A region that names
        /// none carries the coast's (<see cref="CoastPlants"/>), as every world did before regions named their plants.
        /// </summary>
        public IReadOnlyList<PlantSpecies> Plants { get; }

        public Region(string id, string displayName, double centreLatitudeDeg, double centreLongitudeDeg,
                      double extentM, int wakeDayOfYear, double wakeLocalHour,
                      IReadOnlyList<string> heldLakes = null, IReadOnlyList<Dam> dams = null,
                      IReadOnlyList<PlantSpecies> plants = null)
        {
            Id = id;
            DisplayName = displayName;
            CentreLatitudeDeg = centreLatitudeDeg;
            CentreLongitudeDeg = centreLongitudeDeg;
            ExtentM = extentM;
            WakeDayOfYear = wakeDayOfYear;
            WakeLocalHour = wakeLocalHour;
            HeldLakes = heldLakes ?? Array.Empty<string>();
            Dams = dams ?? Array.Empty<Dam>();
            Plants = plants == null ? CoastPlants : InCatalogueOrder(plants);
        }

        /// <summary>
        /// A list of plants in the catalogue's order, each once. The draw sums the plants in the order it is given them, so the
        /// order is the catalogue's whatever order a list is written in: the coast's twelve are then summed exactly as the whole
        /// catalogue was before regions named their plants.
        /// </summary>
        private static PlantSpecies[] InCatalogueOrder(IReadOnlyList<PlantSpecies> plants)
        {
            var ordered = new List<PlantSpecies>(plants.Count);
            foreach (PlantSpecies plant in plants)
            {
                if (plant == null || PlantSpecies.NumberOf(plant) == 0) throw new ArgumentException("a region's plants are the catalogue's", nameof(plants));
                if (ordered.Contains(plant)) throw new ArgumentException(plant.Name + " is named twice in a region's plants", nameof(plants));
                ordered.Add(plant);
            }
            ordered.Sort((a, b) => PlantSpecies.NumberOf(a).CompareTo(PlantSpecies.NumberOf(b)));
            return ordered.ToArray();
        }

        /// <summary>
        /// The coast's twelve (M1.2; <c>ECOSYSTEM.md</c>, "The plants of Bherwerre"): Bherwerre's own, and the plants of a region
        /// that names none and of a world whose heights name no region this build knows (the tests' fixtures). Declared before
        /// the regions, whose construction reads it.
        /// </summary>
        public static readonly IReadOnlyList<PlantSpecies> CoastPlants = InCatalogueOrder(new[]
        {
            PlantSpecies.Blackbutt, PlantSpecies.Bangalay, PlantSpecies.OldManBanksia, PlantSpecies.CoastBanksia, PlantSpecies.SwampPaperbark,
            PlantSpecies.GrassTree, PlantSpecies.HeathBanksia, PlantSpecies.Bracken, PlantSpecies.Lomandra, PlantSpecies.SawSedge,
            PlantSpecies.KangarooGrass, PlantSpecies.Spinifex,
        });

        /// <summary>
        /// The Kangaroo Valley's fifteen (WG.2c, 2026-09-25), one list for both its regions, since they are one country. Of the
        /// coast's, the eight WG.2 keeps (blackbutt, old-man banksia, heath banksia, the grass tree, bracken, Lomandra, saw-sedge
        /// and kangaroo grass) and bangalay, whose records over the whole valley's box lie half on the Shoalhaven's floodplain and
        /// half 7 to 22 km inland in the valley's own forests (19 of its 36 usable records), where BioNet names a "Sydney Blue Gum x
        /// Bangalay" moist forest. The valley's own six: Sydney blue gum, the cabbage tree palm, silvertop ash, river oak, scribbly
        /// gum and lilly pilly. Coast banksia's and swamp paperbark's valley records lie on the floodplain within about 2 km of the
        /// sea, and spinifex has one.
        /// </summary>
        private static readonly IReadOnlyList<PlantSpecies> KangarooValleyPlants = InCatalogueOrder(new[]
        {
            PlantSpecies.Blackbutt, PlantSpecies.Bangalay, PlantSpecies.OldManBanksia, PlantSpecies.GrassTree, PlantSpecies.HeathBanksia, PlantSpecies.Bracken,
            PlantSpecies.Lomandra, PlantSpecies.SawSedge, PlantSpecies.KangarooGrass,
            PlantSpecies.SydneyBlueGum, PlantSpecies.CabbageTreePalm, PlantSpecies.SilvertopAsh, PlantSpecies.RiverOak,
            PlantSpecies.ScribblyGum, PlantSpecies.LillyPilly,
        });

        /// <summary>The plants of a world's region, or the coast's for a world of no region this build knows (<see cref="Plants"/>).</summary>
        public static IReadOnlyList<PlantSpecies> PlantsOf(Region region) => region != null ? region.Plants : CoastPlants;

        /// <summary>
        /// The first world: the southern shore of Jervis Bay, NSW (plan §4.2; CANON ruling 8). 8 × 8 km centred at
        /// 35.140°S 150.675°E; the founder wakes at 8 am local solar time on 25 August (day 237 of a non-leap year),
        /// wherever the world's wake scorer puts them.
        ///
        /// <para>Its three lakes hold their water (WG.1b, 2026-09-24): Parks Australia's geology page for Booderee says Lake
        /// Windermere and Lake McKenzie "evolved when streams were blocked by sand"; OpenStreetMap maps no stream within 900,
        /// 600 and 400 m of Windermere, McKenzie and Blacks Waterhole; and the tiles hold each 5.9 to 30 m below its basin's
        /// lowest lip. "Lake Mckenzie" is OpenStreetMap's spelling, the name the water bake gives it.</para>
        /// </summary>
        public static readonly Region Bherwerre = new Region(
            "bherwerre", "Bherwerre Peninsula, Jervis Bay", -35.140, 150.675, 8000.0, 237, 8.0,
            heldLakes: new[] { "Lake Windermere", "Lake Mckenzie", "Blacks Waterhole" }, plants: CoastPlants);

        /// <summary>
        /// Fitzroy Falls Dam on Yarrunga Creek (Wikipedia, "Fitzroy Falls Dam": 34°38′46″S 150°29′15″E, 14 m high, 1,530 m long,
        /// 1974), 500 m up the creek from the falls, whose point below it is the NSW Government's map point for Fitzroy Falls
        /// (34.648011 S 150.482544 E). The tiles hold its reservoir's flat at 662 m, 1 m below a saddle 1.7 km to the
        /// south-east, and its wall as a ridge 150 to 250 m wide at 673 to 677 m: without the dam, Yarrunga Creek's water went
        /// down the escarpment beside Barrengarry rather than over Fitzroy Falls (WG.1b's trial, 2026-09-24).
        /// </summary>
        private static readonly Dam FitzroyFallsDam = new Dam("Fitzroy Falls Dam", -34.64611, 150.48750, -34.648011, 150.482544);

        /// <summary>
        /// The beta's second region (CANON ruling 43, 2026-09-22; WG.2): the Kangaroo Valley under the Morton plateau's escarpment,
        /// Fitzroy Falls in its box, no coast. The same late-winter wake as Bherwerre's; its station is Nowra's (<c>Climate</c>).
        /// </summary>
        public static readonly Region KangarooValley = new Region(
            "kangaroo-valley", "Kangaroo Valley, Fitzroy Falls", -34.660, 150.500, 8000.0, 237, 8.0,
            dams: new[] { FitzroyFallsDam }, plants: KangarooValleyPlants);

        /// <summary>
        /// The whole Kangaroo Valley (CANON ruling 45, 2026-09-23; WG.2b): rim to rim with Fitzroy, Belmore and Carrington Falls,
        /// the Cambewarra Range and the floor between, centred on the middle of the valley's own outline in OpenStreetMap, 32 km a
        /// side by his choice. A region of its own, so the 8 km box above keeps its bake and the worlds made in it; the same
        /// late-winter wake, and Nowra's station (<c>Climate</c>).
        /// </summary>
        public static readonly Region KangarooValleyWhole = new Region(
            "kangaroo-valley-whole", "Kangaroo Valley, rim to rim", -34.705, 150.589, 32000.0, 237, 8.0,
            dams: new[] { FitzroyFallsDam }, plants: KangarooValleyPlants);

        /// <summary>The clock at the moment the founder wakes here.</summary>
        public WorldClock WakeClock() => WorldClock.FromLocal(WakeDayOfYear, WakeLocalHour, CentreLongitudeDeg);

        /// <summary>Half the side: positions are valid within ±this of the centre on each axis.</summary>
        public double HalfExtentM => ExtentM * 0.5;

        /// <summary>The region with this id, or null. Unknown ids are the caller's problem to report, never a default.</summary>
        public static Region ById(string id)
        {
            return id == Bherwerre.Id ? Bherwerre : id == KangarooValley.Id ? KangarooValley : id == KangarooValleyWhole.Id ? KangarooValleyWhole : null;
        }
    }
}
