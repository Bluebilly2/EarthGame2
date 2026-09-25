using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using EarthGame.Engine;

namespace EarthGame.Server
{
    /// <summary>
    /// How a new world's clock is set when it is first made (CANON ruling 52, 2026-09-25): at the real date and time it is first
    /// started, "it grabs the real time, and syncs", which is every world William starts; or at its region's wake (25 August,
    /// 08:00 at Bherwerre), for the test worlds whose frames must stay daylit and comparable. A world already made keeps its own
    /// clock either way. The host takes it as <c>+world.start now|wake</c> and the game as <c>-eg-start now|wake</c>.
    /// </summary>
    public enum WorldStart
    {
        /// <summary>The real instant the world is first started: the time its maker hands the preparation.</summary>
        Now = 0,
        /// <summary>The region's own wake (<see cref="Region.WakeClock"/>).</summary>
        Wake = 1,
    }

    public static class WorldStarts
    {
        /// <summary>The start a word names: "now" or "wake", as the host's and the game's launch arguments give it.</summary>
        public static bool TryParse(string word, out WorldStart start)
        {
            switch ((word ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "now": start = WorldStart.Now; return true;
                case "wake": start = WorldStart.Wake; return true;
                default: start = WorldStart.Now; return false;
            }
        }
    }

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
        /// the first. A load that fails lets the folder go; one that succeeds holds it until its result is disposed. A world made
        /// here starts at <paramref name="nowUtc"/>, the real time, unless <paramref name="start"/> asks for its region's wake.
        /// </summary>
        public static Result Load(string worldDir, string dataDir, Region region, ulong seed, string nowUtc,
            Action<string> progress, CancellationToken cancellation, IMakingWatcher watcher = null, WorldStart start = WorldStart.Now)
        {
            WorldLock hold = WorldLock.Take(worldDir);
            try
            {
                Result result = LoadHeld(worldDir, dataDir, region, seed, nowUtc, progress, cancellation, watcher, start);
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
            Action<string> progress, CancellationToken cancellation, IMakingWatcher watcher, WorldStart start)
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
                var layers = new SavedWorldLayers(worldDir, region, saved.Layers, cancellation);
                RegionRaster heights = layers.Read("heights");
                Heightfield terrain = heights == null ? ReadBake(dataDir) : new Heightfield(heights);
                SavedWorldLayers.CheckRegion(terrain.Raster, region);
                Report("Reading the water");
                WorldWater water = ReadWater(layers);
                RegionRaster cover = layers.Read("cover");
                Report("Reading what stands and lies on the ground");
                RegionRaster stand = layers.Read("stand");
                RegionRaster loose = layers.Read("loose");
                RegionRaster stone = layers.Read("stone");
                // The shore's distance, for the salt wind at a founder (FP.2); a world from before the layer blows by its openness alone.
                RegionRaster shore = layers.Read("shore_distance");
                // What grows in the ground and how deep it is (BF.3): a tuft's plant, a dig's tuber and its floor.
                RegionRaster understory = layers.Read("understory");
                RegionRaster soil = layers.Read("soil_depth");
                Report("Reading what the ground feeds");
                CapacitySquares feeds = ReadCapacity(layers, region.ExtentM);
                Report("Restoring the world");
                layers.VerifyRemaining();
                WorldState world = WorldSave.Restore(saved, terrain, region, water, cover, stand, loose, stone, feeds, shore, understory, soil);
                Report("World ready");
                return new Result { World = world, Saved = saved, Checksums = saved.Layers };
            }

            // The new world's clock first, so a start time that is not one is refused before anything is made.
            WorldClock clock = start == WorldStart.Wake ? region.WakeClock() : ClockAt(nowUtc);
            Report("Reading landscape");
            Heightfield bake = ReadBake(dataDir);
            Report("Reading lakes and wetlands");
            string waterPath = Path.Combine(dataDir, "water_bodies.json");
            RegionRaster outlines = File.Exists(waterPath) ? RegionRaster.Load(waterPath) : null;
            WorldCreation.Result created = WorldCreation.Create(worldDir, region, seed, bake.Raster, nowUtc, outlines, Report, watcher);
            // The layers as computed are on disk now; letting them go before the world is read back keeps the two from being held
            // at once (WG.2b, 2026-09-23: at 32 km they are some 7 GB, and the read-back a further gigabyte).
            created.Computed = null;
            bake = null;
            outlines = null;
            WorldCreation.Settle();
            var createdLayers = new SavedWorldLayers(worldDir, region, created.Checksums, cancellation);
            Report("Reading prepared terrain");
            Heightfield ground = new Heightfield(createdLayers.Read("heights"));
            Report("Reading what the ground feeds");
            CapacitySquares capacity = ReadCapacity(createdLayers, region.ExtentM);
            WorldState made = new WorldState(seed, region, clock, ground, 0,
                new Double3(created.Wake.East, 0, created.Wake.North), ReadWater(createdLayers), createdLayers.Read("cover"),
                createdLayers.Read("stand"), createdLayers.Read("loose"), createdLayers.Read("stone"), capacity, createdLayers.Read("shore_distance"),
                createdLayers.Read("understory"), createdLayers.Read("soil_depth"));
            Report("Saving the world");
            WorldSave.Write(worldDir, made, null, nowUtc, created.Checksums);
            Report("World ready");
            return new Result { World = made, Checksums = created.Checksums, Census = created.Census };
        }

        /// <summary>
        /// The clock of a world first started at <paramref name="nowUtc"/>, the real time as its maker writes it (an ISO 8601
        /// instant at Greenwich, <c>2026-09-25T06:40:00Z</c>): that instant (CANON ruling 52). A time that is not one is refused,
        /// since a world started at a mistaken hour wakes at it for good.
        /// </summary>
        public static WorldClock ClockAt(string nowUtc)
        {
            if (!DateTime.TryParse(nowUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime utc))
                throw new ArgumentException("a new world starts at the real time, and '" + nowUtc + "' is not one", nameof(nowUtc));
            return WorldClock.FromUtc(utc.DayOfYear, utc.TimeOfDay.TotalHours);
        }

        /// <summary>
        /// The water layers a world folder holds, for the server to stream (M1.4b); null when it has neither, which
        /// is any world made before M1.2. One present without the other is a folder half written, and is refused.
        /// </summary>
        private static WorldWater ReadWater(SavedWorldLayers layers)
        {
            RegionRaster surface = layers.Read("surface"), classes = layers.Read("water");
            bool hasSurface = surface != null, hasClasses = classes != null;
            if (!hasSurface && !hasClasses) return null;
            if (hasSurface != hasClasses)
                throw new InvalidDataException("this world has " + (hasSurface ? "a water surface without its classes" : "water classes without their surface") + "; its layers are half written");
            return new WorldWater(surface, classes);
        }

        /// <summary>
        /// What each square of presence feeds, from every kind's capacity layer the world folder holds (M1.7a), each read,
        /// averaged over its squares and let go; null on a world made before the layers were (M1.2), round whose founders no
        /// animal stands.
        /// </summary>
        private static CapacitySquares ReadCapacity(SavedWorldLayers layers, double extentM)
        {
            CapacitySquares squares = null;
            foreach (AnimalSpecies species in AnimalSpecies.All)
            {
                RegionRaster layer = layers.Read(WorldCreation.CapacityLayer(species));
                if (layer == null) continue;
                if (squares == null) squares = new CapacitySquares(extentM);
                squares.Add(species, layer);
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
