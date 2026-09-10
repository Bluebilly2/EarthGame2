using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EarthGame.Engine
{
    /// <summary>A cell scored as a place to wake, with the distances the score was made of.</summary>
    public struct WakeScore
    {
        public int Row, Col;
        public double East, North;
        public double Score;
        public double WaterM, StoneM, FibreM, FirewoodM, ShelterM;
    }

    /// <summary>
    /// Where a naked person should wake: the cell that best meets the criteria the plan states (§4.2) — fresh
    /// water within 500 m, knappable stone within a kilometre, fibre and firewood within 500 m, shelter rock
    /// within two — on ground that can be stood on. Each criterion is graded, nearer being better, all the way
    /// from the thing itself to three times the stated distance, so that the product has one best place rather
    /// than a plateau of places that merely pass: the first world created (2026-09-09) scored a whole coast at
    /// 1.000 and woke the founder at the first cell in row order, on the region's north edge. The same scorer,
    /// read out in words, is the census: what lies within each distance of a place, so that a person who knows
    /// the country can say where it reads wrong (M1.2 promise 8). Nothing here names Cave Beach; the contract's
    /// expectation that the winner lands near it is a check on the layers, not an input to them.
    /// </summary>
    public sealed class WakeScorer
    {
        public const double WaterWithinM = 500.0;
        public const double StoneWithinM = 1000.0;
        public const double FibreWithinM = 500.0;
        public const double FirewoodWithinM = 500.0;
        public const double ShelterWithinM = 2000.0;
        /// <summary>A stone the founder can flake at all (the knapping floor v1 carried).</summary>
        public const double KnappableFloor = 0.25;
        /// <summary>The wake stands above the sea's reach and on ground gentler than ten degrees.</summary>
        public const double MinHeightM = 2.0;
        public const double MaxSlope = 0.176;
        /// <summary>
        /// The world stops at the region's edge, so the wake keeps this far inside it: a founder woken at the edge
        /// walks off the map on the first morning, and the distance fields there see only half a country. The
        /// first creation (2026-09-09) woke on the north edge, at the first of a coast of saturated cells.
        /// </summary>
        public const double EdgeMarginM = 500.0;

        private readonly WorldLayers _layers;
        private readonly float[] _stoneM;
        private readonly float[] _fibreM;
        private readonly float[] _firewoodM;
        private readonly float[] _shelterM;

        public WakeScorer(WorldLayers layers)
        {
            _layers = layers ?? throw new ArgumentNullException(nameof(layers));
            int count = layers.Width * layers.Height;
            bool[] stone = new bool[count], fibre = new bool[count], firewood = new bool[count], shelter = new bool[count];
            for (int r = 0; r < layers.Height; r++)
                for (int c = 0; c < layers.Width; c++)
                {
                    int i = r * layers.Width + c;
                    StoneType s = layers.StoneAt(r, c);
                    stone[i] = s != null && s.Knappability >= KnappableFloor;
                    PlantSpecies floor = layers.UnderstoryAt(r, c);
                    fibre[i] = floor != null && IsFibre(floor);
                    firewood[i] = layers.OverstoryAt(r, c) != null;
                    shelter[i] = layers.Has(r, c, Topology.Cliff) || layers.Has(r, c, Topology.ShorePlatform);
                }
            _stoneM = Distance(layers, stone);
            _fibreM = Distance(layers, fibre);
            _firewoodM = Distance(layers, firewood);
            _shelterM = Distance(layers, shelter);
        }

        /// <summary>The plants a first cord comes off: lomandra's leaves, the sedge's, spinifex's runners.</summary>
        public static bool IsFibre(PlantSpecies species)
            => ReferenceEquals(species, PlantSpecies.Lomandra) || ReferenceEquals(species, PlantSpecies.SawSedge) || ReferenceEquals(species, PlantSpecies.Spinifex);

        /// <summary>Full marks at the thing itself, two thirds at the criterion's distance, nothing at three times it.</summary>
        public static double Factor(double distanceM, double withinM)
        {
            if (double.IsNaN(distanceM) || double.IsInfinity(distanceM)) return 0.0;
            return SimMath.Clamp01(1.0 - distanceM / (3.0 * withinM));
        }

        /// <summary>The distance fields the score is made of, for the world folder and the verifier.</summary>
        public float[] StoneDistanceM => _stoneM;
        public float[] FibreDistanceM => _fibreM;
        public float[] FirewoodDistanceM => _firewoodM;
        public float[] ShelterDistanceM => _shelterM;

        /// <summary>Every cell's score, 0 to 1, as <see cref="At"/> computes it.</summary>
        public float[] ScoreField()
        {
            float[] field = new float[_layers.Width * _layers.Height];
            for (int r = 0; r < _layers.Height; r++)
                for (int c = 0; c < _layers.Width; c++)
                    field[r * _layers.Width + c] = (float)At(r, c).Score;
            return field;
        }

        public WakeScore At(int row, int col)
        {
            int i = row * _layers.Width + col;
            double half = _layers.Heights.ExtentM * 0.5;
            WakeScore s;
            s.Row = row;
            s.Col = col;
            s.East = col * _layers.CellM - half;
            s.North = half - row * _layers.CellM;
            s.WaterM = _layers.FreshWaterDistanceM[i];
            s.StoneM = _stoneM[i];
            s.FibreM = _fibreM[i];
            s.FirewoodM = _firewoodM[i];
            s.ShelterM = _shelterM[i];
            s.Score = Unstandable(row, col) == null
                ? Factor(s.WaterM, WaterWithinM) * Factor(s.StoneM, StoneWithinM) * Factor(s.FibreM, FibreWithinM)
                  * Factor(s.FirewoodM, FirewoodWithinM) * Factor(s.ShelterM, ShelterWithinM)
                : 0.0;
            return s;
        }

        /// <summary>
        /// Why a cell is no place to wake, or null when it is one: the sea, the region's edge, ground within the
        /// sea's reach, a slope over ten degrees, water underfoot (a lake, a swamp, a creek, a stream: the founder
        /// wakes beside water, not in it), ground as wet as a swamp's (no one sleeps in a bog) or the shelter
        /// rock itself (a cliff, a platform: beside it, not on it). The second creation (2026-09-09) woke the
        /// founder in a creek mouth on a shore platform with every distance nought, and the third at a paperbark
        /// swamp's edge, until those were excluded.
        /// </summary>
        public string Unstandable(int row, int col)
        {
            int i = row * _layers.Width + col;
            double half = _layers.Heights.ExtentM * 0.5;
            double east = col * _layers.CellM - half, north = half - row * _layers.CellM;
            if (_layers.Drainage.IsSea(col, row)) return "the sea";
            if (Math.Abs(east) > half - EdgeMarginM || Math.Abs(north) > half - EdgeMarginM) return "the region's edge";
            if (_layers.Heights[row, col] < MinHeightM) return "within the sea's reach";
            if (_layers.Slope[i] > MaxSlope) return "too steep";
            WaterClass water = (WaterClass)_layers.Water[i];
            if (water == WaterClass.Lake) return "a lake";
            if (water == WaterClass.Swamp) return "a swamp";
            if (water == WaterClass.Creek || water == WaterClass.Stream) return "in the creek";
            if (_layers.Soil.WetnessAt(col, row) >= WorldLayers.SwampWetness) return "sodden ground";
            if (_layers.Has(row, col, Topology.Cliff)) return "the cliff itself";
            if (_layers.Has(row, col, Topology.ShorePlatform)) return "the platform itself";
            return null;
        }

        /// <summary>Scores this close are one score; the distances are whole steps of the grid and tie often.</summary>
        public const double TieTolerance = 1e-9;

        /// <summary>
        /// The best cell. Ties go to the most sheltered from the wind (the site's exposure, the coast's salt wind
        /// included), which is what a swale is; what is still tied keeps the first in row order, so the answer is
        /// as fixed as the layers.
        /// </summary>
        public WakeScore Best()
        {
            WakeScore best = default;
            double bestExposure = double.MaxValue;
            bool found = false;
            for (int r = 0; r < _layers.Height; r++)
                for (int c = 0; c < _layers.Width; c++)
                {
                    WakeScore s = At(r, c);
                    if (found && s.Score < best.Score - TieTolerance) continue;
                    double exposure = _layers.SiteAt(r, c).Exposure;
                    if (!found || s.Score > best.Score + TieTolerance || exposure < bestExposure)
                    {
                        best = s;
                        bestExposure = exposure;
                        found = true;
                    }
                }
            return best;
        }

        /// <summary>
        /// The census: the wake read out as country, one line per thing a person would look for, with the
        /// distance and the criterion beside it.
        /// </summary>
        public string Census(WakeScore s)
        {
            StringBuilder sb = new StringBuilder();
            int i = s.Row * _layers.Width + s.Col;
            sb.Append("wake at east ").Append(F(s.East)).Append(" north ").Append(F(s.North))
              .Append(" (row ").Append(s.Row).Append(", col ").Append(s.Col).Append("), ").Append(F(_layers.Heights[s.Row, s.Col])).Append(" m above the sea; score ")
              .Append(s.Score.ToString("0.000", CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("the ground: ").Append(Topologies(_layers.TopologyMask[i])).Append("; soil ")
              .Append(_layers.Soil.DepthAt(s.Col, s.Row).ToString("0.00", CultureInfo.InvariantCulture)).Append(" m, wetness ")
              .Append(_layers.Soil.WetnessAt(s.Col, s.Row).ToString("0.00", CultureInfo.InvariantCulture)).Append(", slope ")
              .Append((Math.Atan(_layers.Slope[i]) * GeoMath.RadToDeg).ToString("0.0", CultureInfo.InvariantCulture)).Append(" degrees, wind exposure ")
              .Append(_layers.SiteAt(s.Row, s.Col).Exposure.ToString("0.00", CultureInfo.InvariantCulture)).Append('\n');
            PlantSpecies canopy = _layers.OverstoryAt(s.Row, s.Col);
            PlantSpecies floor = _layers.UnderstoryAt(s.Row, s.Col);
            sb.Append("standing here: ").Append(canopy == null ? "no canopy" : canopy.DisplayName).Append(" over ").Append(floor == null ? "bare ground" : floor.DisplayName).Append('\n');
            StoneType stone = _layers.StoneAt(s.Row, s.Col);
            sb.Append("underfoot: ").Append(stone == null ? "no stone" : stone.Name.ToLowerInvariant()).Append('\n');
            Line(sb, "fresh water", s.WaterM, WaterWithinM);
            Line(sb, "knappable stone", s.StoneM, StoneWithinM);
            Line(sb, "fibre", s.FibreM, FibreWithinM);
            Line(sb, "firewood", s.FirewoodM, FirewoodWithinM);
            Line(sb, "shelter rock", s.ShelterM, ShelterWithinM);
            sb.Append("the sea: ").Append(F(_layers.ShoreDistanceM[i])).Append(" m\n");
            return sb.ToString();
        }

        private static void Line(StringBuilder sb, string what, double distanceM, double withinM)
        {
            sb.Append(what).Append(": ").Append(double.IsInfinity(distanceM) ? "none in the region" : F(distanceM) + " m")
              .Append(" (asked within ").Append(F(withinM)).Append(" m; factor ").Append(Factor(distanceM, withinM).ToString("0.00", CultureInfo.InvariantCulture)).Append(")\n");
        }

        private static readonly (Topology Bit, string Name)[] TopologyNames =
        {
            (Topology.Sea, "sea"), (Topology.ShorePlatform, "shore platform"), (Topology.Beach, "beach"), (Topology.Dune, "dune"),
            (Topology.Cliff, "cliff"), (Topology.Crest, "crest"), (Topology.Lake, "lake"), (Topology.Wetland, "wetland"),
            (Topology.Creek, "creek"), (Topology.Forest, "forest"), (Topology.Heath, "heath"),
        };

        /// <summary>The topology bits as words, in the order a person would name them.</summary>
        public static string Topologies(uint mask)
        {
            var names = new List<string>();
            foreach (var (bit, name) in TopologyNames)
                if ((mask & (uint)bit) != 0) names.Add(name);
            return names.Count == 0 ? "open ground" : string.Join(", ", names);
        }

        private static string F(double v) => v.ToString("0", CultureInfo.InvariantCulture);
        private static string D(double distanceM) => double.IsInfinity(distanceM) ? "none" : F(distanceM) + " m";

        private static float[] Distance(WorldLayers layers, bool[] source)
        {
            int width = layers.Width, height = layers.Height, count = width * height;
            float[] d = new float[count];
            for (int i = 0; i < count; i++) d[i] = float.PositiveInfinity;
            var queue = new Queue<int>();
            for (int i = 0; i < count; i++) if (source[i]) { d[i] = 0f; queue.Enqueue(i); }
            float straight = (float)layers.CellM, diagonal = (float)(layers.CellM * 1.4142135623730951);
            int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
            int[] dz = { 0, 1, 1, 1, 0, -1, -1, -1 };
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int c = i % width, r = i / width;
                for (int k = 0; k < 8; k++)
                {
                    int nc = c + dx[k], nr = r + dz[k];
                    if (nc < 0 || nr < 0 || nc >= width || nr >= height) continue;
                    int n = nr * width + nc;
                    float step = (dx[k] != 0 && dz[k] != 0) ? diagonal : straight;
                    if (d[i] + step < d[n] - 1e-3f)
                    {
                        d[n] = d[i] + step;
                        queue.Enqueue(n);
                    }
                }
            }
            return d;
        }
    }
}
