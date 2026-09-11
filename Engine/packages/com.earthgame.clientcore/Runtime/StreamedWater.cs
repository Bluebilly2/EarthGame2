using System;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// Where water stands over the ground the client holds (M1.5d): the ground's height and the depth the server streams
    /// over each tile (M1.4b), added, wherever that depth is more than none. The depth is the server's surface less its
    /// ground at every post, and both tiles are read between posts the same bilinear way, so their sum is the server's
    /// own surface to the centimetre the wire carries. Where no depth tile is held — before it arrives, or on a world with
    /// no water layer — the sea stands at the datum wherever the ground is below it, as it did before any water was
    /// streamed.
    /// </summary>
    public sealed class StreamedWater : IHeightSource
    {
        private readonly IHeightSource _ground;
        private readonly TileHeightfield _depth;

        /// <param name="ground">The ground the client holds.</param>
        /// <param name="depth">The depth tiles the client holds, a field of their own; null on a world that streams none.</param>
        public StreamedWater(IHeightSource ground, TileHeightfield depth)
        {
            _ground = ground ?? throw new ArgumentNullException(nameof(ground));
            _depth = depth;
        }

        /// <summary>The water's surface at a point, or NaN where none stands or the ground there is not held.</summary>
        public double HeightAt(double east, double north)
        {
            double ground = _ground.HeightAt(east, north);
            if (double.IsNaN(ground)) return double.NaN;
            if (_depth != null && _depth.TryHeightAt(east, north, out double depth))
                return depth > 0.0 ? ground + depth : double.NaN;
            return ground < Heightfield.SeaLevelM ? Heightfield.SeaLevelM : double.NaN;
        }
    }
}
