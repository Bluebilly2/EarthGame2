using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using EarthGame.Engine;

namespace EarthGame.Server
{
    /// <summary>
    /// A world folder held by the one program that opened it (M1.3d): a file in the folder, open and shared with no one,
    /// deleted when it is closed, and closed by the operating system when the program dies. The bug hunt of 2026-09-13
    /// found that a second program opening a world a first still ran recovered the folder under it, deleting the files
    /// the first was writing aside, and the first's record then named files that were gone, so the world was refused for
    /// good. A second program is now refused before it reads or changes anything.
    /// </summary>
    public sealed class WorldLock : IDisposable
    {
        public const string FileName = "world.lock";
        private FileStream _held;

        private WorldLock(FileStream held)
        {
            _held = held;
        }

        /// <summary>Holds the folder, making it when it is not there; an IOException in plain words when another program holds it.</summary>
        public static WorldLock Take(string worldDir)
        {
            Directory.CreateDirectory(worldDir);
            string path = Path.Combine(worldDir, FileName);
            try
            {
                return new WorldLock(new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose));
            }
            catch (Exception ex) when ((ex is IOException && !(ex is FileNotFoundException) && !(ex is DirectoryNotFoundException))
                                       || ex is UnauthorizedAccessException)
            {
                throw new IOException("This world is already open in another copy of the game or on a server (" + worldDir + ").", ex);
            }
        }

        public void Dispose()
        {
            _held?.Dispose();
            _held = null;
        }
    }

    /// <summary>
    /// Prepares a world before a server owns it. Paths and time are supplied by the host; no Unity state
    /// is accessed, so the desktop host can run this on a worker (M1.4 loading, 2026-09-10).
    /// The progress callback runs on the caller's thread; cancellation is checked between stages.
    /// </summary>
    public static class WorldPreparation
    {
        public sealed class Result : IDisposable
        {
            public WorldState World;
            public WorldSaveInfo Saved;
            public IReadOnlyDictionary<string, string> Checksums;
            public string Census;
            /// <summary>The folder held for the program that prepared the world (M1.3d), until it has written its last save and disposes this.</summary>
            public WorldLock Hold;

            public void Dispose() => Hold?.Dispose();
        }

        /// <summary>
        /// Opens the world in <paramref name="worldDir"/>, or makes it there, holding the folder first (M1.3d): reading a
        /// folder recovers it, and recovering deletes what a save left aside, which a second program would delete from under
        /// the first. A load that fails lets the folder go; one that succeeds holds it until its result is disposed.
        /// </summary>
        public static Result Load(string worldDir, string dataDir, Region region, ulong seed, string nowUtc,
            Action<string> progress, CancellationToken cancellation)
        {
            WorldLock hold = WorldLock.Take(worldDir);
            try
            {
                Result result = LoadHeld(worldDir, dataDir, region, seed, nowUtc, progress, cancellation);
                result.Hold = hold;
                return result;
            }
            catch
            {
                hold.Dispose();
                throw;
            }
        }

        private static Result LoadHeld(string worldDir, string dataDir, Region region, ulong seed, string nowUtc,
            Action<string> progress, CancellationToken cancellation)
        {
            void Report(string stage)
            {
                cancellation.ThrowIfCancellationRequested();
                progress?.Invoke(stage);
                cancellation.ThrowIfCancellationRequested();
            }

            Report("Reading world");
            if (WorldSave.Exists(worldDir))
            {
                WorldSaveInfo saved = WorldSave.Read(worldDir);
                // The world owns which piece of the Earth it is, not the launcher. Callers pass the region they
                // were started with (the host's +server.region, the game's own), and two regions of one extent
                // would otherwise swap under a save without a word: the same local metres, another coast.
                if (!string.Equals(saved.RegionId, region.Id, StringComparison.Ordinal))
                    throw new InvalidDataException("This world is set in '" + saved.RegionId + "', and this one was opened as '" + region.Id + "'.");
                Report("Reading saved terrain");
                string path = Path.Combine(worldDir, WorldCreation.LayersFolder, "heights.json");
                Heightfield terrain;
                // A manifest promises world-owned terrain: its absence or corruption must not be hidden by a bake.
                if (File.Exists(path) || saved.Layers.ContainsKey("heights"))
                    terrain = new Heightfield(RegionRaster.Load(path));
                else
                    terrain = ReadBake(dataDir);
                if (saved.Layers.TryGetValue("heights", out string expected) && terrain.Raster.Sha256 != expected)
                    throw new InvalidDataException("The saved terrain does not match this world's layer manifest.");
                Report("Reading the water");
                WorldWater water = ReadWater(worldDir);
                RegionRaster cover = ReadCodes(worldDir, "cover");
                Report("Reading what stands and lies on the ground");
                RegionRaster stand = ReadCodes(worldDir, "stand");
                RegionRaster loose = ReadCodes(worldDir, "loose");
                RegionRaster stone = ReadCodes(worldDir, "stone");
                Report("Reading what the ground feeds");
                CapacitySquares feeds = ReadCapacity(worldDir, region.ExtentM);
                Report("Restoring the world");
                WorldState world = WorldSave.Restore(saved, terrain, region, water, cover, stand, loose, stone, feeds);
                Report("World ready");
                return new Result { World = world, Saved = saved, Checksums = saved.Layers };
            }

            Report("Reading landscape");
            Heightfield bake = ReadBake(dataDir);
            Report("Reading lakes and wetlands");
            string waterPath = Path.Combine(dataDir, "water_bodies.json");
            RegionRaster outlines = File.Exists(waterPath) ? RegionRaster.Load(waterPath) : null;
            WorldCreation.Result created = WorldCreation.Create(worldDir, region, seed, bake.Raster, nowUtc, outlines, Report);
            Report("Reading prepared terrain");
            Heightfield ground = new Heightfield(RegionRaster.Load(created.Layers["heights"]));
            Report("Reading what the ground feeds");
            CapacitySquares capacity = ReadCapacity(worldDir, region.ExtentM);
            WorldState made = new WorldState(seed, region, region.WakeClock(), ground, 0,
                new Double3(created.Wake.East, 0, created.Wake.North), ReadWater(worldDir), ReadCodes(worldDir, "cover"),
                ReadCodes(worldDir, "stand"), ReadCodes(worldDir, "loose"), ReadCodes(worldDir, "stone"), capacity);
            Report("Saving the world");
            WorldSave.Write(worldDir, made, null, nowUtc, created.Checksums);
            Report("World ready");
            return new Result { World = made, Checksums = created.Checksums, Census = created.Census };
        }

        /// <summary>
        /// The water layers a world folder holds, for the server to stream (M1.4b); null when it has neither, which
        /// is any world made before M1.2. One present without the other is a folder half written, and is refused.
        /// </summary>
        private static WorldWater ReadWater(string worldDir)
        {
            string surface = Path.Combine(worldDir, WorldCreation.LayersFolder, "surface.json");
            string classes = Path.Combine(worldDir, WorldCreation.LayersFolder, "water.json");
            bool hasSurface = File.Exists(surface), hasClasses = File.Exists(classes);
            if (!hasSurface && !hasClasses) return null;
            if (hasSurface != hasClasses)
                throw new InvalidDataException("this world has " + (hasSurface ? "a water surface without its classes" : "water classes without their surface") + "; its layers are half written");
            return new WorldWater(RegionRaster.Load(surface), RegionRaster.Load(classes));
        }

        /// <summary>
        /// A layer of codes a world folder holds for the server: the ground cover since M1.4d, what stands and what lies
        /// loose since M1.6a, streamed; and the stone, which a cobble taken up is made of (M1.5b). Null on a world made
        /// before the layer was, whose client draws without it: a flat ground colour, and nothing standing.
        /// </summary>
        private static RegionRaster ReadCodes(string worldDir, string name)
        {
            string path = Path.Combine(worldDir, WorldCreation.LayersFolder, name + ".json");
            return File.Exists(path) ? RegionRaster.Load(path) : null;
        }

        /// <summary>
        /// What each square of presence feeds, from every kind's capacity layer the world folder holds (M1.7a), each read,
        /// averaged over its squares and let go; null on a world made before the layers were (M1.2), round whose founders no
        /// animal stands.
        /// </summary>
        private static CapacitySquares ReadCapacity(string worldDir, double extentM)
        {
            CapacitySquares squares = null;
            foreach (AnimalSpecies species in AnimalSpecies.All)
            {
                string path = Path.Combine(worldDir, WorldCreation.LayersFolder, WorldCreation.CapacityLayer(species) + ".json");
                if (!File.Exists(path)) continue;
                if (squares == null) squares = new CapacitySquares(extentM);
                squares.Add(species, RegionRaster.Load(path));
            }
            return squares;
        }

        private static Heightfield ReadBake(string dataDir)
        {
            string path = Path.Combine(dataDir, "heights.json");
            if (!File.Exists(path))
                throw new FileNotFoundException("The landscape data is missing. Expected " + path, path);
            return new Heightfield(RegionRaster.Load(path));
        }
    }
}
