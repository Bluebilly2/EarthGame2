using System;

namespace EarthGame.Engine
{
    /// <summary>
    /// The server's collision: a heightfield and nothing else. Ground is wherever the surface is; a "wall" is
    /// ground ahead that rises more than a stride can step, or rises at all where it is too steep to stand; water
    /// is the sea at a fixed level wherever the ground is below it (lakes arrive with the water layer in M1.2).
    /// The sweep marches the path in sub-strides of half a capsule radius, which is how a heightfield is swept
    /// without a physics engine and why it disagrees with PhysX's interpolated Terrain collider exactly where
    /// the ground is steepest (DEBTS.md: the named defect class). Also the tests' collision world, over any
    /// <see cref="IHeightSource"/>.
    ///
    /// <para>Normals are taken the way feet feel them: per axis, the gentler of the forward and backward
    /// differences five centimetres wide. A central difference straddling a step reports a cliff from the flat
    /// ground beside it, and a body pressed against a wall was being told it had nothing to stand on.</para>
    /// </summary>
    public sealed class HeightfieldCollision : IWorldCollision
    {
        private const double NormalWidth = 0.05;

        private readonly IHeightSource _ground;
        private readonly double _maxStepRise;
        private readonly double _walkableSlopeDeg;
        private readonly double _seaLevel;
        private readonly bool _hasSea;

        /// <param name="maxStepRise">The mover's step height: a rise beyond it inside one sub-stride is a wall.</param>
        /// <param name="walkableSlopeDeg">The mover's standable slope: ground rising ahead steeper than this is a wall too.</param>
        public HeightfieldCollision(IHeightSource ground, double maxStepRise, double walkableSlopeDeg, bool hasSea = true, double seaLevel = 0.0)
        {
            _ground = ground ?? throw new ArgumentNullException(nameof(ground));
            _maxStepRise = maxStepRise;
            _walkableSlopeDeg = walkableSlopeDeg;
            _hasSea = hasSea;
            _seaLevel = seaLevel;
        }

        /// <summary>The collision world a mover configuration implies over a ground.</summary>
        public static HeightfieldCollision For(IHeightSource ground, MoverConfig cfg, bool hasSea = true, double seaLevel = 0.0)
            => new HeightfieldCollision(ground, cfg.StepHeight, cfg.WalkableSlopeDeg, hasSea, seaLevel);

        public double HeightAt(double east, double north) => _ground.HeightAt(east, north);

        public bool ProbeGround(Double3 feet, double radius, double stepUp, double maxDown, out double groundUp, out Double3 normal)
        {
            double g = _ground.HeightAt(feet.X, feet.Z);
            if (g <= feet.Y + stepUp && g >= feet.Y - maxDown)
            {
                groundUp = g;
                normal = NormalAt(feet.X, feet.Z);
                return true;
            }
            groundUp = 0.0;
            normal = Double3.Up;
            return false;
        }

        public bool SweepCapsule(Double3 feet, double radius, double height, Double3 delta, out double fraction, out Double3 normal)
        {
            double length = delta.Length;
            fraction = 1.0;
            normal = Double3.Up;
            if (length <= 1e-12) return false;
            double stride = Math.Max(0.01, radius * 0.5);
            int n = (int)Math.Ceiling(length / stride);
            if (n < 1) n = 1;
            bool descending = delta.Y < -1e-12;
            double horizontal = Math.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
            double prevClearance = feet.Y - _ground.HeightAt(feet.X, feet.Z);
            for (int i = 1; i <= n; i++)
            {
                double t = (double)i / n;
                Double3 p = feet + delta * t;
                double g = _ground.HeightAt(p.X, p.Z);
                double rise = g - p.Y;
                // A wall is judged against the feet's own height, not the sample's: a body falling straight onto
                // a steep slope meets ground, not a face, and must land on it and slide.
                double ahead = g - feet.Y;
                if (horizontal > 1e-12 && (ahead > _maxStepRise || (ahead > 1e-9 && Mover.SlopeDeg(NormalAt(p.X, p.Z)) > _walkableSlopeDeg)))
                {
                    // Ground ahead that no stride steps onto: a face, met at the last free sample. Its normal is
                    // horizontal and against the motion, so sliding along it never climbs it.
                    fraction = (double)(i - 1) / n;
                    normal = new Double3(-delta.X / horizontal, 0.0, -delta.Z / horizontal);
                    return true;
                }
                if (descending && rise > 0.0)
                {
                    // Falling feet crossed the surface between this sample and the last: land where the line crosses.
                    double a = prevClearance;
                    double b = -rise;
                    double within = a - b > 1e-12 ? a / (a - b) : 0.0;
                    fraction = ((i - 1) + SimMath.Clamp01(within)) / n;
                    Double3 at = feet + delta * fraction;
                    normal = NormalAt(at.X, at.Z);
                    return true;
                }
                prevClearance = -rise;
            }
            return false;
        }

        public double WaterSurfaceAt(double east, double north)
        {
            if (!_hasSea) return double.NaN;
            return _ground.HeightAt(east, north) < _seaLevel ? _seaLevel : double.NaN;
        }

        /// <summary>The normal feet feel: per axis the gentler of the two one-sided slopes. Up on flat ground.</summary>
        public Double3 NormalAt(double east, double north)
        {
            double h0 = _ground.HeightAt(east, north);
            double forwardE = (_ground.HeightAt(east + NormalWidth, north) - h0) / NormalWidth;
            double backE = (h0 - _ground.HeightAt(east - NormalWidth, north)) / NormalWidth;
            double forwardN = (_ground.HeightAt(east, north + NormalWidth) - h0) / NormalWidth;
            double backN = (h0 - _ground.HeightAt(east, north - NormalWidth)) / NormalWidth;
            double de = Math.Abs(forwardE) < Math.Abs(backE) ? forwardE : backE;
            double dn = Math.Abs(forwardN) < Math.Abs(backN) ? forwardN : backN;
            return new Double3(-de, 1.0, -dn).Normalized;
        }
    }
}
