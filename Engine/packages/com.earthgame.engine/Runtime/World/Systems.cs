using System;
using System.Collections.Generic;

namespace EarthGame.Engine
{
    /// <summary>A system of the fast tick (20 Hz): called once per step, in the order <see cref="WorldState.Systems"/> lists them.</summary>
    public interface IFastSystem
    {
        string Name { get; }
        void Step(WorldState world, double dt);
    }

    /// <summary>
    /// A dropped thing falls at gravity until it meets the ground the server holds (the sea's floor under the sea),
    /// and rests there: where the server says (plan §4.4); the client animates what it is told. Free fall with no
    /// drag, no bounce and no roll: a cobble dropped from the hand is on the ground in half a second, and the
    /// rest is M1.5's.
    /// </summary>
    public sealed class ItemFall : IFastSystem
    {
        public const double GravityMps2 = 9.81;

        public string Name => "item fall";

        public void Step(WorldState world, double dt)
        {
            IReadOnlyList<Entity> all = world.Entities.All;
            for (int i = 0; i < all.Count; i++)
            {
                Entity e = all[i];
                if (!e.HasItem || e.Killed) continue;
                ItemComponent item = e.Item;
                if (item.Resting) continue;
                double speed = item.FallSpeed + GravityMps2 * dt;
                Double3 p = e.Position;
                double up = p.Y - speed * dt;
                double ground = world.GroundAt(p.X, p.Z);
                if (up <= ground)
                {
                    up = ground;
                    item.Resting = true;
                    item.FallSpeed = 0f;
                }
                else item.FallSpeed = (float)speed;
                e.Move(new Double3(p.X, up, p.Z), world.Tick);
                e.SetItem(item, world.Tick);
            }
        }
    }

    /// <summary>
    /// A layer that changes slowly (soil moisture, growth, depletion) and is advanced per 512 m cell to a world
    /// time rather than stepped: the scheduler calls it as often as the cell's distance from the players earns,
    /// and a cell that waited catches up in one call, so the layer must be analytic over the gap.
    /// </summary>
    public interface ISlowLayer
    {
        string Name { get; }
        void AdvanceTo(int cellX, int cellZ, double worldHours);
    }

    /// <summary>The 512 m cells the slow layers and the region files are kept by; cell (0, 0) at the south-west corner, like the tiles.</summary>
    public static class RegionCells
    {
        public const double CellM = 512.0;

        public static int CountAcross(double extentM) => Math.Max(1, (int)Math.Ceiling(extentM / CellM - 1e-9));

        public static int IndexOf(double coordinate, double extentM)
        {
            int count = CountAcross(extentM);
            int i = (int)Math.Floor((coordinate + extentM * 0.5) / CellM);
            return Math.Min(count - 1, Math.Max(0, i));
        }

        /// <summary>The horizontal distance from a point to a cell's rectangle, zero inside it.</summary>
        public static double DistanceTo(int cellX, int cellZ, double east, double north, double extentM)
        {
            double half = extentM * 0.5;
            double x0 = -half + cellX * CellM, x1 = Math.Min(half, x0 + CellM);
            double z0 = -half + cellZ * CellM, z1 = Math.Min(half, z0 + CellM);
            double dx = east < x0 ? x0 - east : east > x1 ? east - x1 : 0.0;
            double dz = north < z0 ? z0 - north : north > z1 ? north - z1 : 0.0;
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }

    /// <summary>
    /// Advances the slow layers by cell and by distance from the players (ARCHITECTURE §4): a cell within the
    /// interest radius of any player every tick, a cell farther away every <see cref="FarCadenceTicks"/> ticks,
    /// staggered so the far cells do not all land on one tick. What time a cell was last advanced to is kept,
    /// for a test or a census to ask; the layers keep their own.
    /// </summary>
    public sealed class SlowScheduler
    {
        public const int FarCadenceTicks = 60;

        public double InterestRadiusM = 1500.0;

        public readonly List<ISlowLayer> Layers = new List<ISlowLayer>();

        private readonly Dictionary<long, double> _advancedTo = new Dictionary<long, double>();

        private static long Key(int cellX, int cellZ) => ((long)cellX << 32) | (uint)cellZ;

        public void Tick(WorldState world, long tick)
        {
            if (Layers.Count == 0) return;
            double extent = world.Region.ExtentM;
            int count = RegionCells.CountAcross(extent);
            double hours = world.Clock.TotalHours;
            IReadOnlyList<Double3> points = world.InterestPoints;
            for (int cx = 0; cx < count; cx++)
                for (int cz = 0; cz < count; cz++)
                {
                    bool near = false;
                    for (int p = 0; p < points.Count && !near; p++)
                        near = RegionCells.DistanceTo(cx, cz, points[p].X, points[p].Z, extent) <= InterestRadiusM;
                    bool due = near || ((tick + cx * 7 + cz * 13) % FarCadenceTicks == 0);
                    if (!due) continue;
                    for (int l = 0; l < Layers.Count; l++) Layers[l].AdvanceTo(cx, cz, hours);
                    _advancedTo[Key(cx, cz)] = hours;
                }
        }

        public bool TryAdvancedTo(int cellX, int cellZ, out double worldHours) => _advancedTo.TryGetValue(Key(cellX, cellZ), out worldHours);
    }
}
