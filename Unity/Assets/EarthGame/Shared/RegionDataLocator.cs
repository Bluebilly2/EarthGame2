using System;
using System.IO;
using EarthGame.Engine;
using UnityEngine;

namespace EarthGame.Shared
{
    /// <summary>
    /// Where this process finds the region's baked data and where it keeps its worlds. In a checkout the
    /// repository root is found by walking up from the Unity data folder to <c>global.json</c> (the editor's
    /// <c>Unity/Assets</c> and a player under <c>Build/Player/</c> both sit inside it); otherwise the persistent
    /// data path serves. <c>-eg-data</c> and <c>-eg-saves</c> override either. In M1.A both the server and the
    /// client read the raster from disk; a joining client without the data is M1.B's baseline streaming.
    /// </summary>
    public static class RegionDataLocator
    {
        private static string _repoRoot;
        private static bool _searched;

        /// <summary>The repository root above the running process, or null when there is none.</summary>
        public static string RepoRoot
        {
            get
            {
                if (_searched) return _repoRoot;
                _searched = true;
                string dir = Application.dataPath;
                while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "global.json")))
                    dir = Path.GetDirectoryName(dir);
                _repoRoot = string.IsNullOrEmpty(dir) ? null : dir;
                return _repoRoot;
            }
        }

        public static string DataDir(Region region)
        {
            string over = LaunchArgs.Get("data", null);
            if (!string.IsNullOrEmpty(over)) return Path.GetFullPath(over);
            string root = RepoRoot ?? Application.persistentDataPath;
            return Path.Combine(root, "Data", "regions", region.Id);
        }

        public static string SavesDir()
        {
            string over = LaunchArgs.Get("saves", null);
            if (!string.IsNullOrEmpty(over)) return Path.GetFullPath(over);
            string root = RepoRoot ?? Application.persistentDataPath;
            return Path.Combine(root, "Saves");
        }

        /// <summary>
        /// The region's heights as a heightfield, or null with the reason in <paramref name="message"/>. Never
        /// throws for a missing file: a server without its data runs and says so; a raster that is present but
        /// wrong (version, checksum) is an error the caller sees in the message too.
        /// </summary>
        public static Heightfield TryLoadHeightfield(Region region, out string message)
        {
            string sidecar = Path.Combine(DataDir(region), "heights.json");
            if (!File.Exists(sidecar))
            {
                message = "no region data: " + sidecar + " not found (bake it with Tools/data/bake_region.py, or pass -eg-data)";
                return null;
            }
            try
            {
                RegionRaster raster = RegionRaster.Load(sidecar);
                message = "region data " + sidecar + ": " + raster.Width + "x" + raster.Height + " at " + raster.CellM.ToString("0.#") + " m, " + raster.MinM.ToString("0") + ".." + raster.MaxM.ToString("0") + " m";
                return new Heightfield(raster);
            }
            catch (Exception ex)
            {
                message = "region data refused: " + ex.Message;
                return null;
            }
        }
    }
}
