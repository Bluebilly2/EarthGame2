using System;

namespace EarthGame.Engine
{
    /// <summary>What kind of rock stands on a cell (BF.4 stage three): none, a boulder resting on the ground, or a ledge standing out of a steep face.</summary>
    public enum RockForm : byte
    {
        None = 0,
        Boulder = 1,
        Ledge = 2,
    }

    /// <summary>Which row of the rule's table put a rock where it is (BF.4 stage three).</summary>
    public enum RockPlace : byte
    {
        /// <summary>Bare rock gentler than 15°: a shore platform, flat rock.</summary>
        FlatRock = 0,
        /// <summary>Bare rock from 15° to 40°: below a cliff, a steep rock face.</summary>
        SteepRock = 1,
        /// <summary>Bare rock at 40° and steeper: a cliff.</summary>
        Cliff = 2,
        /// <summary>A dry cover with cobbles on it: thin soil over stone.</summary>
        ThinSoil = 3,
    }

    /// <summary>
    /// A rock that stands on a cell (BF.4 stage three, 2026-09-25), as <see cref="StandingRocks"/> places it on both sides: a
    /// superellipsoid (Barr, "Superquadrics and angle-preserving transformations", 1981) of three half-axes and two
    /// squarenesses, its long axis at a whole-degree yaw and its widest part at <see cref="MidUp"/>. What a reader needs to
    /// draw it, give it a body, stand on it or keep a thing out of it.
    /// </summary>
    public struct StandingRock
    {
        public int Row;
        public int Col;
        public RockForm Form;
        /// <summary>Which row of the rule's table it stands by.</summary>
        public RockPlace Place;
        /// <summary>Its stone, an index into <see cref="StoneType.All"/>.</summary>
        public int Stone;
        /// <summary>Which of its stone's <see cref="StandingRocks.Variants"/> shapes it has.</summary>
        public int Variant;
        public double East;
        public double North;
        /// <summary>The height of its widest part, m.</summary>
        public double MidUp;
        /// <summary>Half its length along its yaw, m.</summary>
        public double HalfLength;
        /// <summary>Half its width across its yaw, m.</summary>
        public double HalfWidth;
        /// <summary>Half its height, m.</summary>
        public double HalfHeight;
        /// <summary>Its long axis's bearing, whole degrees clockwise from north.</summary>
        public int YawDeg;
        /// <summary>How rounded it is up and down: 1 an ellipsoid's curve, toward 0 a box's (Barr's ε1).</summary>
        public double SquareUp;
        /// <summary>How rounded it is round about (Barr's ε2).</summary>
        public double SquareAround;

        public StoneType StoneType => Stone >= 0 && Stone < StoneType.All.Count ? StoneType.All[Stone] : null;

        /// <summary>The rock's highest point, m.</summary>
        public double TopUp => MidUp + HalfHeight;

        /// <summary>A point in the rock's own frame, m: along its long axis and across it, from its centre.</summary>
        public void ToLocal(double east, double north, out double along, out double across)
        {
            double yaw = YawDeg * (Math.PI / 180.0);
            double s = Math.Sin(yaw), c = Math.Cos(yaw);
            double de = east - East, dn = north - North;
            along = de * s + dn * c;
            across = de * c - dn * s;
        }

        /// <summary>A point of the rock's own frame in the world's, m east and north.</summary>
        public void ToWorld(double along, double across, out double east, out double north)
        {
            double yaw = YawDeg * (Math.PI / 180.0);
            double s = Math.Sin(yaw), c = Math.Cos(yaw);
            east = East + along * s + across * c;
            north = North + along * c - across * s;
        }

        /// <summary>
        /// How far out a point of the rock's frame lies in its widest outline: 0 at its centre, 1 on its rim, and in between
        /// in proportion along any line from the centre (the superellipse's own measure, of the first degree).
        /// </summary>
        public double OutlineAt(double along, double across)
        {
            double p = 2.0 / SquareAround;
            double sum = Math.Pow(Math.Abs(along) / HalfLength, p) + Math.Pow(Math.Abs(across) / HalfWidth, p);
            return Math.Pow(sum, SquareAround * 0.5);
        }

        /// <summary>Whether a point lies inside the rock's widest outline.</summary>
        public bool Covers(double east, double north)
        {
            ToLocal(east, north, out double along, out double across);
            return OutlineAt(along, across) < 1.0;
        }

        /// <summary>
        /// The rock's top over a point, m, and true, where the point lies inside its widest outline: the upper half of the
        /// superellipsoid, (1 - r^(2/ε1))^(ε1/2) of the half-height over its widest part, r the outline's measure.
        /// </summary>
        public bool TryTopAt(double east, double north, out double top)
        {
            ToLocal(east, north, out double along, out double across);
            double r = OutlineAt(along, across);
            if (!(r < 1.0))
            {
                top = double.NaN;
                return false;
            }
            top = MidUp + HalfHeight * Math.Pow(1.0 - Math.Pow(r, 2.0 / SquareUp), SquareUp * 0.5);
            return true;
        }

        /// <summary>
        /// A point inside the rock's outline moved out along the line from its centre to <paramref name="clearM"/> past its
        /// rim, and true; a point outside it is left where it is, and false. A point at the very centre goes out along the
        /// long axis.
        /// </summary>
        public bool TryMoveOut(double east, double north, double clearM, out double outEast, out double outNorth)
        {
            ToLocal(east, north, out double along, out double across);
            if (!(OutlineAt(along, across) < 1.0))
            {
                outEast = east;
                outNorth = north;
                return false;
            }
            double d = Math.Sqrt(along * along + across * across);
            double ua = d > 1e-9 ? along / d : 1.0, uc = d > 1e-9 ? across / d : 0.0;
            // The outline's measure of a point a metre out along the line is how many rims that metre is.
            double reach = 1.0 / OutlineAt(ua, uc) + clearM;
            ToWorld(ua * reach, uc * reach, out outEast, out outNorth);
            return true;
        }
    }

    /// <summary>
    /// Rock that stands (BF.4 stage three, promise 5 as amended 2026-09-25): boulders on bare rock and where cobbles lie on
    /// thin soil, and ledges on cliffs, decided cell by cell from what the server and every client hold alike: the cell's
    /// cover byte, its loose code's cobbles, its stone code, its stand code and the slope of the one ground across it, read
    /// without the hollows dug in it. Whole numbers wherever a choice is made, so both sides make the same one.
    ///
    /// <para>A rock keeps inside its own cell, clear of the cell's edge by <see cref="CellMarginM"/> (room for the foot of a
    /// trunk in the next cell), so whether a point is on a rock is its own cell's question, and a client preparing a tile holds
    /// all a rock is decided from. The table is a first model, to be judged in frames (DEBTS, "The rocks' table is stated, not
    /// measured").</para>
    /// </summary>
    public static class StandingRocks
    {
        /// <summary>How many shapes each stone's rocks come in, of each form.</summary>
        public const int Variants = 6;

        /// <summary>How far inside its cell's edge a rock keeps, m.</summary>
        public const double CellMarginM = 0.5;

        /// <summary>A boulder's widest part stands this share of its half-height above the lowest ground round its rim.</summary>
        public const double SeatShare = 0.3;

        /// <summary>How far from a cell's post the ground's slope is read, m: to the cell's own edge, each way.</summary>
        public const double ProbeM = 2.0;

        /// <summary>No rock within this many cells of the region's edge, where the two sides carry the ground past it differently.</summary>
        public const int EdgeCells = 2;

        /// <summary>The chances, in thousandths of the cells of each kind: a platform's and bare flat rock's, and more for each cobble; the slope below a cliff; a cliff's ledges; thin soil's, for each cobble.</summary>
        public const int PlatformChance = 60, PlatformPerCobble = 20, TalusChance = 300, LedgeChance = 550, ThinSoilPerCobble = 30;

        /// <summary>The squares of the tangents of 15° and 40°: gentler rock is a platform, steeper than the second a cliff. Written out, so no runtime's tangent decides.</summary>
        private static readonly double GentleSlope2 = 0.2679491924311227 * 0.2679491924311227;
        private static readonly double CliffSlope2 = 0.8390996311772800 * 0.8390996311772800;

        /// <summary>
        /// The sixteen ways round a rock its seat is read, along and across it, written out. Eight left a boulder standing a
        /// tenth of a metre high between them where the ground folds over a scarp's edge (the tests' fixture, 2026-09-25).
        /// </summary>
        private static readonly double[] RimAlong = { 1.0, 0.9238795325112867, 0.7071067811865476, 0.3826834323650898, 0.0, -0.3826834323650898, -0.7071067811865476, -0.9238795325112867, -1.0, -0.9238795325112867, -0.7071067811865476, -0.3826834323650898, 0.0, 0.3826834323650898, 0.7071067811865476, 0.9238795325112867 };
        private static readonly double[] RimAcross = { 0.0, 0.3826834323650898, 0.7071067811865476, 0.9238795325112867, 1.0, 0.9238795325112867, 0.7071067811865476, 0.3826834323650898, 0.0, -0.3826834323650898, -0.7071067811865476, -0.9238795325112867, -1.0, -0.9238795325112867, -0.7071067811865476, -0.3826834323650898 };

        private const ulong Salt = 0x524F434B53UL;

        /// <summary>How far past a rock's rim a stick or a cobble whose place fell inside it lies instead, m.</summary>
        public const double LyingClearM = 0.1;

        /// <summary>
        /// Where a thing of the loose layer lies, given its place by the layout and whether a rock stands on its cell: its own
        /// place, or, where that falls inside the rock, the rock's foot past its rim on the line from the rock's centre. The
        /// server finds a thing there and the client draws it there, so a stick never lies inside a boulder.
        /// </summary>
        public static void LyingPlace(bool hasRock, in StandingRock rock, ref double east, ref double north)
        {
            if (hasRock) rock.TryMoveOut(east, north, LyingClearM, out east, out north);
        }

        /// <summary>
        /// The rock that stands on a cell, and true; false where none does. <paramref name="undug"/> is the one ground without its
        /// hollows, NaN where it is not known: a rock is seated on the ground as it was made, and a dig does not move it. The
        /// stand code is read only as whether a trunk stands on the cell (not zero), which the two-byte code of WG.2c keeps.
        /// </summary>
        public static bool TryDecide(int row, int col, byte coverCode, byte looseCode, byte stoneCode, ushort standCode, double cellM, double extentM,
                                     IHeightSource undug, out StandingRock rock)
        {
            rock = default;
            if (stoneCode == 0 || stoneCode > StoneType.All.Count || standCode != 0 || undug == null || !(cellM > 0.0)) return false;
            int posts = (int)Math.Round(extentM / cellM) + 1;
            if (row < EdgeCells || col < EdgeCells || row > posts - 1 - EdgeCells || col > posts - 1 - EdgeCells) return false;
            GroundCover cover = GroundCovers.CoverOf(coverCode);
            int cobbles = LooseCodes.CobblesOf(looseCode);
            ulong h = StandLayout.Mix((((ulong)(uint)row << 32) | (uint)col) ^ Salt);
            int roll = (int)(Draw(h, 0) % 1000UL);
            StandLayout.CellCentre(row, col, cellM, extentM, out double postEast, out double postNorth);
            RockForm form;
            RockPlace place;
            int least, span;
            double gradEast = 0.0, gradNorth = 0.0;
            switch (cover)
            {
                case GroundCover.Rock:
                {
                    int platform = PlatformChance + PlatformPerCobble * cobbles;
                    if (roll >= Math.Max(LedgeChance, Math.Max(TalusChance, platform))) return false;
                    double east = undug.HeightAt(postEast + ProbeM, postNorth), west = undug.HeightAt(postEast - ProbeM, postNorth);
                    double north = undug.HeightAt(postEast, postNorth + ProbeM), south = undug.HeightAt(postEast, postNorth - ProbeM);
                    if (double.IsNaN(east + west + north + south)) return false;
                    gradEast = (east - west) / (2.0 * ProbeM);
                    gradNorth = (north - south) / (2.0 * ProbeM);
                    double slope2 = gradEast * gradEast + gradNorth * gradNorth;
                    if (slope2 >= CliffSlope2)
                    {
                        if (roll >= LedgeChance) return false;
                        form = RockForm.Ledge;
                        place = RockPlace.Cliff;
                        least = span = 0;
                    }
                    else if (slope2 >= GentleSlope2)
                    {
                        if (roll >= TalusChance) return false;
                        form = RockForm.Boulder;
                        place = RockPlace.SteepRock;
                        least = 50;
                        span = 180;
                    }
                    else
                    {
                        if (roll >= platform) return false;
                        form = RockForm.Boulder;
                        place = RockPlace.FlatRock;
                        least = 35;
                        span = 145;
                    }
                    break;
                }
                case GroundCover.BareEarth:
                case GroundCover.Heath:
                case GroundCover.Bracken:
                case GroundCover.Sedge:
                case GroundCover.Grass:
                case GroundCover.ForestFloor:
                    if (cobbles == 0 || roll >= ThinSoilPerCobble * cobbles) return false;
                    form = RockForm.Boulder;
                    place = RockPlace.ThinSoil;
                    least = 30;
                    span = 90;
                    break;
                default:
                    return false;
            }

            int stone = BedrockOf(stoneCode - 1);
            int aCm, bCm, cCm;
            if (form == RockForm.Ledge)
            {
                aCm = (160 + (int)(Draw(h, 1) % 141UL)) / 2;  // 1.6 to 3.0 m long
                bCm = (80 + (int)(Draw(h, 2) % 61UL)) / 2;    // 0.8 to 1.4 m out
                cCm = (35 + (int)(Draw(h, 3) % 46UL)) / 2;    // 0.35 to 0.8 m thick
            }
            else
            {
                // Across: the product of two even draws, so most are small and a few are large.
                long p1 = (long)(Draw(h, 1) % 1000UL), p2 = (long)(Draw(h, 2) % 1000UL);
                int half = (least + (int)(span * p1 * p2 / 998001L)) / 2;
                int stretch = (int)(Draw(h, 3) % 26UL);
                aCm = half + half * stretch / 100;
                bCm = half - half * stretch / 100;
                HeightShare(stone, out int lo, out int hi);
                cCm = half * (lo + (int)(Draw(h, 4) % (ulong)(hi - lo + 1))) / 100;
            }
            int roomCm = (int)Math.Round(cellM * 100.0) / 2 - (int)Math.Round(CellMarginM * 100.0);
            // Whatever the squareness, the outline lies inside the box of its half-axes, so inside this reach of its centre.
            int reachCm = (int)Math.Ceiling(Math.Sqrt((double)aCm * aCm + (double)bCm * bCm));
            if (reachCm > roomCm)
            {
                aCm = aCm * roomCm / reachCm;
                bCm = bCm * roomCm / reachCm;
                cCm = cCm * roomCm / reachCm;
                reachCm = (int)Math.Ceiling(Math.Sqrt((double)aCm * aCm + (double)bCm * bCm));
            }
            if (aCm < 5 || bCm < 5 || cCm < 5 || reachCm > roomCm) return false;
            int spanCm = roomCm - reachCm;
            int offEast = (int)(Draw(h, 5) % (ulong)(2 * spanCm + 1)) - spanCm;
            int offNorth = (int)(Draw(h, 6) % (ulong)(2 * spanCm + 1)) - spanCm;

            int yaw;
            if (form == RockForm.Ledge)
            {
                // Along the contour: square to the way the ground falls.
                double bearing = Math.Atan2(gradEast, gradNorth) * (180.0 / Math.PI);
                yaw = (((int)Math.Round(bearing) + 90) % 360 + 360) % 360;
            }
            else yaw = (int)(Draw(h, 7) % 360UL);
            int variant = (int)(Draw(h, 8) % (ulong)Variants);
            ShapeOf(stone, form, variant, out double squareUp, out double squareAround);

            rock.Row = row;
            rock.Col = col;
            rock.Form = form;
            rock.Place = place;
            rock.Stone = stone;
            rock.Variant = variant;
            rock.East = postEast + offEast / 100.0;
            rock.North = postNorth + offNorth / 100.0;
            rock.HalfLength = aCm / 100.0;
            rock.HalfWidth = bCm / 100.0;
            rock.HalfHeight = cCm / 100.0;
            rock.YawDeg = yaw;
            rock.SquareUp = squareUp;
            rock.SquareAround = squareAround;

            double centre = undug.HeightAt(rock.East, rock.North);
            if (double.IsNaN(centre)) return false;
            if (form == RockForm.Ledge)
            {
                // A ledge straddles the face, half in it.
                rock.MidUp = centre;
                return true;
            }
            // A boulder rests on the lowest of the ground round it, so it sits on a slope without standing off it.
            double lowest = centre;
            for (int k = 0; k < RimAlong.Length; k++)
            {
                double reach = 1.0 / rock.OutlineAt(RimAlong[k], RimAcross[k]);
                rock.ToWorld(RimAlong[k] * reach, RimAcross[k] * reach, out double e, out double n);
                double g = undug.HeightAt(e, n);
                if (double.IsNaN(g)) return false;
                if (g < lowest) lowest = g;
            }
            rock.MidUp = lowest + SeatShare * rock.HalfHeight;
            return true;
        }

        /// <summary>
        /// The stone a rock that stands is of, from the stone the layer says lies about on its cell (a model until the stone of
        /// the place, promise 6). The layer names what a founder picks up there, and a boulder is the rock beneath. Where it names
        /// a stone both places have only as pebbles and veins, rolled in or weathered out (quartz, rhyolite and quartzite on the
        /// coast's platforms, chert, flint, obsidian), the rock beneath is the sandstone both places stand on, the Sydney
        /// Basin's; the first count on Bherwerre (2026-09-25) found 3,454 of its 3,935 rocks rhyolite and quartz before this.
        /// </summary>
        public static int BedrockOf(int stone)
        {
            StoneType s = stone >= 0 && stone < StoneType.All.Count ? StoneType.All[stone] : null;
            bool pebbles = ReferenceEquals(s, StoneType.Quartz) || ReferenceEquals(s, StoneType.Rhyolite) || ReferenceEquals(s, StoneType.Quartzite)
                           || ReferenceEquals(s, StoneType.Chert) || ReferenceEquals(s, StoneType.Flint) || ReferenceEquals(s, StoneType.Obsidian);
            if (!pebbles) return stone;
            for (int i = 0; i < StoneType.All.Count; i++)
                if (ReferenceEquals(StoneType.All[i], StoneType.Sandstone)) return i;
            return stone;
        }

        /// <summary>
        /// How rounded a stone's rocks are, up and down and round about, in one of its variants (a model, DEBTS): sandstone
        /// in blocks with worn corners, the quartzites, cherts and volcanics angular, basalt sub-rounded, granite rounded, shale
        /// in slabs, and a ledge of any stone a flat slab. Each variant a little rounder or squarer, by 0.03 a step.
        /// </summary>
        public static void ShapeOf(int stone, RockForm form, int variant, out double squareUp, out double squareAround)
        {
            if (form == RockForm.Ledge)
            {
                squareUp = 0.25;
                squareAround = 0.35;
            }
            else
            {
                StoneType s = stone >= 0 && stone < StoneType.All.Count ? StoneType.All[stone] : null;
                if (ReferenceEquals(s, StoneType.Sandstone)) { squareUp = 0.45; squareAround = 0.60; }
                else if (ReferenceEquals(s, StoneType.Basalt) || ReferenceEquals(s, StoneType.Obsidian)) { squareUp = 0.75; squareAround = 0.80; }
                else if (ReferenceEquals(s, StoneType.Granite)) { squareUp = 0.90; squareAround = 0.90; }
                else if (ReferenceEquals(s, StoneType.Shale)) { squareUp = 0.40; squareAround = 0.70; }
                else { squareUp = 0.55; squareAround = 0.65; }
            }
            int v = ((variant % Variants) + Variants) % Variants;
            squareUp += (v - 2.5) * 0.03;
            squareAround += ((v * 5) % Variants - 2.5) * 0.03;
        }

        /// <summary>A boulder's height against its width, per cent of the width's half, low and high (a model, DEBTS): shale in slabs, the rest chunky.</summary>
        private static void HeightShare(int stone, out int lo, out int hi)
        {
            StoneType s = StoneType.All[stone];
            if (ReferenceEquals(s, StoneType.Sandstone)) { lo = 55; hi = 80; }
            else if (ReferenceEquals(s, StoneType.Basalt) || ReferenceEquals(s, StoneType.Obsidian)) { lo = 60; hi = 90; }
            else if (ReferenceEquals(s, StoneType.Granite)) { lo = 55; hi = 85; }
            else if (ReferenceEquals(s, StoneType.Shale)) { lo = 30; hi = 45; }
            else { lo = 60; hi = 85; }
        }

        /// <summary>A cell's k-th draw: whole numbers, so both sides draw the same.</summary>
        private static ulong Draw(ulong cell, int k) => StandLayout.Mix(cell ^ ((ulong)(k + 1) << 48));

        // ---- as the server holds the world ----

        /// <summary>The rock that stands on a cell of the world, and true; false where none does or the world has no such layers.</summary>
        public static bool TryOfCell(WorldState world, int row, int col, out StandingRock rock)
        {
            rock = default;
            RegionRaster cover = world?.Cover, stone = world?.Stone;
            if (cover == null || stone == null || world.Terrain == null) return false;
            if (!Inside(cover, row, col) || !Inside(stone, row, col)) return false;
            byte loose = Inside(world.Loose, row, col) ? (byte)world.Loose.Code(row, col) : (byte)0;
            ushort stand = Inside(world.Stand, row, col) ? StandCodes.CodeAt(world.Stand, row, col) : (ushort)0;
            return TryDecide(row, col, (byte)cover.Code(row, col), loose, (byte)stone.Code(row, col), stand, cover.CellM, cover.ExtentM,
                             new UndugGround(world), out rock);
        }

        /// <summary>The rock of the cell a point lies in, and true; false where none stands there.</summary>
        public static bool TryAt(WorldState world, double east, double north, out StandingRock rock)
        {
            rock = default;
            RegionRaster cover = world?.Cover;
            if (cover == null) return false;
            TileCodec.CellOf(cover.ExtentM, cover.CellM, east, north, out int row, out int col);
            return TryOfCell(world, row, col, out rock);
        }

        /// <summary>
        /// What a foot or a thing meets at a point (BF.4 stage three): the one ground, or the top of the rock standing there where
        /// that is higher.
        /// </summary>
        public static double SurfaceAt(WorldState world, double east, double north)
        {
            double ground = world.GroundAt(east, north);
            if (TryAt(world, east, north, out StandingRock rock) && rock.TryTopAt(east, north, out double top) && top > ground) return top;
            return ground;
        }

        /// <summary>
        /// Every rock of a world counted, by the row of the table that put it there and by its stone, with the tallest (BF.4 stage
        /// three): a record of what the rule makes of a real place, for the exit record and a dedicated host's "rocks", which
        /// times it (the engine reads no clock).
        /// </summary>
        public static string Census(WorldState world)
        {
            RegionRaster cover = world?.Cover;
            if (cover == null || world.Stone == null) return "no cover or stone layer: no rocks";
            int[] byPlace = new int[4];
            int[] byStone = new int[StoneType.All.Count];
            int total = 0;
            double tallest = 0.0, area = 0.0;
            for (int row = 0; row < cover.Height; row++)
                for (int col = 0; col < cover.Width; col++)
                {
                    if (!TryOfCell(world, row, col, out StandingRock rock)) continue;
                    total++;
                    byPlace[(int)rock.Place]++;
                    byStone[rock.Stone]++;
                    tallest = Math.Max(tallest, 2.0 * rock.HalfHeight);
                    area += Math.PI * rock.HalfLength * rock.HalfWidth;
                }
            double km2 = cover.ExtentM * cover.ExtentM / 1e6;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(total).Append(" rocks over ").Append(km2.ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" km², ")
              .Append((total / km2).ToString("0", System.Globalization.CultureInfo.InvariantCulture)).Append(" a km²; covering ")
              .Append((area / 1e4).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).Append(" ha; the tallest ")
              .Append(tallest.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(" m\n");
            sb.Append("boulders on flat rock ").Append(byPlace[(int)RockPlace.FlatRock]).Append(", on steep rock ").Append(byPlace[(int)RockPlace.SteepRock])
              .Append(", on thin soil ").Append(byPlace[(int)RockPlace.ThinSoil]).Append("; ledges on cliffs ").Append(byPlace[(int)RockPlace.Cliff]).Append("\n");
            sb.Append("by stone:");
            for (int s = 0; s < byStone.Length; s++)
                if (byStone[s] > 0) sb.Append(' ').Append(StoneType.All[s].Name.ToLowerInvariant()).Append(' ').Append(byStone[s]);
            sb.Append('\n');
            return sb.ToString();
        }

        private static bool Inside(RegionRaster raster, int row, int col) =>
            raster != null && row >= 0 && col >= 0 && row < raster.Height && col < raster.Width;

        /// <summary>The server's one ground without its hollows, as a rock is seated on it.</summary>
        private sealed class UndugGround : IHeightSource
        {
            private readonly WorldState _world;

            public UndugGround(WorldState world)
            {
                _world = world;
            }

            public double HeightAt(double east, double north) => FineGround.UndugAt(_world, east, north);
        }
    }
}
