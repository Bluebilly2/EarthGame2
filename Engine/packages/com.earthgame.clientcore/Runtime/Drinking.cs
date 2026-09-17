using System;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// How a scripted founder answers thirst (the corpus, 2026-09-16): when to leave the route for water, how near the
    /// water's edge to stand, how many times to drink, and where the streamed tiles say fresh water stands. The corpus's
    /// walkers (<c>ScenarioRunner</c>) had walked the loop since M1.B without a body; FP.1 gave them one that dries at
    /// the exertion's rate and FP.2 a death at 15% lost, so a soak's founders were dying of thirst on a loop laid past
    /// no water. A thirsty walker now looks for fresh water within <see cref="SearchM"/> of where they stand, walks to
    /// it, and drinks until the word is gone, the way the recorder's <c>drink</c> scenario does by hand.
    ///
    /// <para>The numbers are the body's own: the word comes from <see cref="Hydration.LevelOf"/>, a press gives
    /// <see cref="Hydration.MaxDrinkPerVisitL"/>, and the water counts where the engine says it stands
    /// (<see cref="WorldState.StandingWaterM"/>), so this file holds no threshold of its own. Engine-free, so the
    /// decisions can be tested without Unity; the walking, the pitching of the crosshair and the pressing of the use
    /// key stay in the Unity layer.</para>
    /// </summary>
    public static class Drinking
    {
        /// <summary>
        /// How far off the route a thirsty founder looks for fresh water, m: a few strides, so the divert stays beside
        /// the ground the loop was surveyed on (lay_loop.py) and a correction on the way is charged to the loop's own
        /// budget, not hidden in a long detour.
        /// </summary>
        public const double SearchM = 25.0;

        /// <summary>
        /// How near the water's edge to stand, m: inside the hands' reach from the eye (<see cref="Hands.ReachM"/>),
        /// with a stride to spare for the crosshair to meet the surface.
        /// </summary>
        public const double StandOffM = 2.5;

        /// <summary>How long a founder waits before looking again after water was found and gave nothing, s.</summary>
        public const double RetryAfterSeconds = 60.0;

        /// <summary>How long to wait for the server's answer to a press before trying the next pitch, s.</summary>
        public const double AnswerSeconds = 2.0;

        /// <summary>How long the walk to the water may take before it is given up, s.</summary>
        public const double WalkSeconds = 40.0;

        /// <summary>The pitches the crosshair tries at the water's edge, degrees below level: the drink scenario's own order (FP.1).</summary>
        public static readonly float[] Pitches = { 25f, 35f, 45f, 55f, 15f, 65f };

        /// <summary>
        /// The most presses one visit takes: enough litre-and-a-half drinks to fill a body at the lethal loss, and one
        /// over for the word to catch up. Derived from the body's constants, so a change to what a press gives changes
        /// this with it.
        /// </summary>
        public static readonly int MostPresses = (int)Math.Ceiling(Hydration.LethalWaterLoss * Hydration.TotalBodyWaterL / Hydration.MaxDrinkPerVisitL) + 1;

        /// <summary>Whether a founder with this much water leaves the route for water: from the first word, "thirsty".</summary>
        public static bool Wants(double water01) => Hydration.LevelOf(water01) >= ThirstLevel.Thirsty;

        /// <summary>
        /// Whether to press the use key again after an answer: the last press drank, the word is not yet gone, and the
        /// visit has presses left. A refusal (salt, nothing there, out of reach) ends the visit.
        /// </summary>
        public static bool PressAgain(VerbOutcome last, double water01, int presses)
            => last == VerbOutcome.Done && Hydration.LevelOf(water01) != ThirstLevel.Fine && presses < MostPresses;

        /// <summary>
        /// The nearest point, out to a distance, where the streamed tiles put fresh water standing: rings a metre apart,
        /// seventy-two bearings each. False, and nothing, where none is held or none stands.
        /// </summary>
        public static bool FreshWaterNear(Func<TileLayer, TileId, ReceivedTile> holding, TileGrid grid, double east, double north, double searchM,
                                          out double atEast, out double atNorth)
            => WaterNear(holding, grid, east, north, searchM, GroundCover.FreshWater, out atEast, out atNorth);

        /// <summary>
        /// The nearest point, out to a distance, where the streamed tiles put water of a cover standing (fresh water for a
        /// drink; the sea for the drink scenario's refusal): the one search for the corpus's walker and the recorder's
        /// drink scenario alike (2026-09-16; the recorder had its own copy). False, and nothing, where none is held or
        /// none stands.
        /// </summary>
        public static bool WaterNear(Func<TileLayer, TileId, ReceivedTile> holding, TileGrid grid, double east, double north, double searchM,
                                     GroundCover cover, out double atEast, out double atNorth)
        {
            if (holding == null) throw new ArgumentNullException(nameof(holding));
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            for (double r = 1.0; r <= searchM; r += 1.0)
                for (int k = 0; k < 72; k++)
                {
                    double a = k * Math.PI / 36.0;
                    double e = east + r * Math.Sin(a), n = north + r * Math.Cos(a);
                    if (Stands(holding, grid, e, n, cover))
                    {
                        atEast = e;
                        atNorth = n;
                        return true;
                    }
                }
            atEast = 0.0;
            atNorth = 0.0;
            return false;
        }

        /// <summary>Whether the streamed tiles put fresh water standing at a point (<see cref="Stands"/> for fresh water).</summary>
        public static bool StandsFresh(Func<TileLayer, TileId, ReceivedTile> holding, TileGrid grid, double east, double north)
            => Stands(holding, grid, east, north, GroundCover.FreshWater);

        /// <summary>
        /// Whether the streamed tiles put water of a cover standing at a point: the depth tile at least
        /// <see cref="WorldState.StandingWaterM"/> deep there, read between posts as the wader reads it, and the cover
        /// tile's code the cover asked for at the nearest post of the point's cell that is wet by its own depth, which is
        /// where the water between posts comes from (the server's <see cref="WorldState.WaterAt"/> reads the same way).
        /// Until 2026-09-18 the cover was the nearest post's, so at a creek's mouth the sea reaching into the stream's
        /// cell stood as fresh water (the corpus of 2026-09-16 drank it). The sea's depth is water too, and is never
        /// offered as a drink. False where either tile is not held.
        /// </summary>
        public static bool Stands(Func<TileLayer, TileId, ReceivedTile> holding, TileGrid grid, double east, double north, GroundCover cover)
        {
            TileId id = grid.ForPosition(east, north);
            ReceivedTile depth = holding(TileLayer.WaterDepth, id);
            if (depth?.Heights == null || !(TileGround.HeightAt(depth, east, north) >= WorldState.StandingWaterM)) return false;
            ReceivedTile covers = holding(TileLayer.GroundCover, id);
            if (covers?.Codes == null) return false;
            if (!NearestWetPost(depth, east, north, out double postEast, out double postNorth)) return false;
            int last = covers.Posts - 1;
            int x = Math.Min(last, Math.Max(0, (int)Math.Round((postEast - covers.OriginEast) / covers.CellM)));
            int z = Math.Min(last, Math.Max(0, (int)Math.Round((postNorth - covers.OriginNorth) / covers.CellM)));
            return GroundCovers.CoverOf(covers.Codes[z, x]) == cover;
        }

        /// <summary>
        /// The nearest post of the point's cell in a depth tile whose own depth reaches <see cref="WorldState.StandingWaterM"/>,
        /// as a position; false where none does (the depth between posts never exceeds its corners', so where water stands
        /// one always does).
        /// </summary>
        private static bool NearestWetPost(ReceivedTile depth, double east, double north, out double postEast, out double postNorth)
        {
            int last = depth.Posts - 1;
            double fx = Math.Min(last, Math.Max(0.0, (east - depth.OriginEast) / depth.CellM));
            double fz = Math.Min(last, Math.Max(0.0, (north - depth.OriginNorth) / depth.CellM));
            int x0 = Math.Min(last - 1, (int)Math.Floor(fx)), z0 = Math.Min(last - 1, (int)Math.Floor(fz));
            double best = double.PositiveInfinity;
            postEast = 0.0;
            postNorth = 0.0;
            for (int z = z0; z <= z0 + 1; z++)
                for (int x = x0; x <= x0 + 1; x++)
                {
                    if (!(depth.Heights[z, x] >= WorldState.StandingWaterM)) continue;
                    double d = (x - fx) * (x - fx) + (z - fz) * (z - fz);
                    if (d < best)
                    {
                        best = d;
                        postEast = depth.OriginEast + x * depth.CellM;
                        postNorth = depth.OriginNorth + z * depth.CellM;
                    }
                }
            return best < double.PositiveInfinity;
        }
    }
}
