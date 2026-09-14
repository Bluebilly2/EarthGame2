using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using EarthGame.Engine;

namespace EarthGame.Server
{
    /// <summary>
    /// Reads a world's own layers (WG.0c, 2026-09-14). The manifest promises existence and raw bytes; each sidecar
    /// also belongs to the selected geographic frame. Optional legacy layers may be absent only when not promised.
    /// Runtime consumers keep only what they need; verification of the other layers does not retain their grids.
    /// </summary>
    internal sealed class SavedWorldLayers
    {
        private readonly string _dir;
        private readonly Region _region;
        private readonly IReadOnlyDictionary<string, string> _manifest;
        private readonly HashSet<string> _read = new HashSet<string>(StringComparer.Ordinal);
        private readonly CancellationToken _cancellation;

        public SavedWorldLayers(string worldDir, Region region, IReadOnlyDictionary<string, string> manifest, CancellationToken cancellation)
        {
            _dir = Path.Combine(worldDir, WorldCreation.LayersFolder);
            _region = region;
            _manifest = manifest;
            _cancellation = cancellation;
        }

        public RegionRaster Read(string name)
        {
            _cancellation.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || name == "." || name == "..")
                throw new InvalidDataException("invalid layer name in the world's manifest: '" + name + "'");
            string path = Path.Combine(_dir, name + ".json");
            bool promised = _manifest.TryGetValue(name, out string expected);
            if (!File.Exists(path))
            {
                if (promised) throw new FileNotFoundException("The world's manifest promises missing layer '" + name + "'. Expected " + path, path);
                return null;
            }
            RegionRaster layer = RegionRaster.Load(path);
            if (promised && !string.Equals(layer.Sha256, expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Layer '" + name + "' does not match this world's manifest: actual " + layer.Sha256 + ", expected " + expected);
            if (layer.Layer != name)
                throw new InvalidDataException("Layer '" + name + "' describes itself as '" + layer.Layer + "'.");
            CheckRegion(layer, _region);
            _read.Add(name);
            return layer;
        }

        public void VerifyRemaining()
        {
            foreach (string name in _manifest.Keys)
                if (!_read.Contains(name)) Read(name);
        }

        /// <summary>Sampling pitch may differ; a layer's place and extent must still be the region it describes.</summary>
        public static void CheckRegion(RegionRaster layer, Region region)
        {
            if (layer.RegionId != region.Id || layer.CentreLatDeg != region.CentreLatitudeDeg
                || layer.CentreLonDeg != region.CentreLongitudeDeg || layer.ExtentM != region.ExtentM)
                throw new InvalidDataException("Layer '" + layer.Layer + "' has region/frame (" + layer.RegionId + ", "
                    + layer.CentreLatDeg + ", " + layer.CentreLonDeg + ", " + layer.ExtentM + "), expected ("
                    + region.Id + ", " + region.CentreLatitudeDeg + ", " + region.CentreLongitudeDeg + ", " + region.ExtentM + ").");
        }
    }
}
