using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EarthGame.Engine;
using EarthGame.Protocol;

namespace EarthGame.Server
{
    /// <summary>Where a player was when the world was last written, and what they carried: the spawn for their next join.</summary>
    public struct SavedPlayer
    {
        public string Name;
        public MoverState Body;
        public float YawDeg;
        public float PitchDeg;
        public long SavedTick;
        /// <summary>What they carried, by place (M1.5a); null for nothing.</summary>
        public CarriedThing[] Carried;
        /// <summary>The place that was their hand; 0 for none.</summary>
        public byte Hand;
    }

    /// <summary>A cell's takings from the loose layer as a region file recorded them (M1.5b), with the 512 m cell of the file that held them.</summary>
    public struct SavedTaking
    {
        public int RegionX;
        public int RegionZ;
        public LooseTaken.Cell Cell;
    }

    /// <summary>What a world folder says about itself, read back without building the world yet.</summary>
    public sealed class WorldSaveInfo
    {
        public ulong Seed;
        public string RegionId;
        public double TotalHours;
        public double StartedAtHours;
        public long Tick;
        public string CreatedUtc;
        public ushort ProtocolVersion;
        public readonly Dictionary<string, SavedPlayer> Players = new Dictionary<string, SavedPlayer>(StringComparer.Ordinal);
        /// <summary>The wake the pipeline chose, or null for a world saved before it had layers.</summary>
        public Double3? Wake;
        /// <summary>Layer name → the raw file's sha256, as written at creation and carried forward by every save.</summary>
        public readonly Dictionary<string, string> Layers = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>The entities of every region file, in id order (M1.3).</summary>
        public readonly List<SavedEntity> Entities = new List<SavedEntity>();
        /// <summary>What was taken from the loose layer, from every region file's layer diffs (M1.5b).</summary>
        public readonly List<SavedTaking> Taken = new List<SavedTaking>();
        /// <summary>The id the next spawn takes; 1 for a world saved before it had entities.</summary>
        public ulong NextEntityId = 1;
        /// <summary>The digest the server wrote beside the world, or null for a world saved before it wrote one.</summary>
        public string Digest;
    }

    /// <summary>
    /// The world folder (ARCHITECTURE §6): <c>world.json</c> (format <c>eg2.world</c>, version 1: identity, the
    /// clock's two numbers, the tick, the wake, the layers' checksums and, since M1.3, <c>next_entity_id</c>),
    /// <c>players/&lt;name&gt;.egp</c> (each player's resting place and, since M1.5a, what they carry; version 1's JSON
    /// is still read and replaced),
    /// <c>regions/r.X.Y.egr</c> (the entities by 512 m cell and, since M1.5b, what has been taken from the loose layer)
    /// and <c>digest.txt</c> (the world's name, for a test or
    /// a verifier to compare). The server is the only writer; every file is written to a <c>.part</c> and moved
    /// into place so a crash mid-write leaves the previous save intact. Dates are the host's: the server reads no
    /// clock.
    /// </summary>
    public static class WorldSave
    {
        public const string Format = "eg2.world";
        public const int Version = 1;
        public const string WorldFile = "world.json";
        public const string PlayersFolder = "players";
        public const string DigestFile = "digest.txt";

        public static void Write(string dir, WorldState world, IReadOnlyList<SavedPlayer> players, string nowUtcText,
                                 IReadOnlyDictionary<string, string> layerChecksums = null)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Directory.CreateDirectory(dir);
            string worldPath = Path.Combine(dir, WorldFile);
            string created = nowUtcText ?? string.Empty;
            JsonObject layers = null;
            if (File.Exists(worldPath))
            {
                // The creation date and the layers' checksums are written once; every later save carries them forward.
                try
                {
                    JsonObject existing = Json.ParseObject(File.ReadAllText(worldPath, Encoding.UTF8));
                    created = existing.StringOr("created_utc", created);
                    if (existing.Contains("layers")) layers = existing.Object("layers");
                }
                catch (JsonException)
                {
                    // An unreadable previous file is overwritten by a good one; its date is lost, not the world.
                }
            }
            JsonObject doc = new JsonObject()
                .With("format", Format)
                .With("version", Version)
                .With("region", world.RegionId)
                .With("seed", world.Seed)
                .With("extent_m", world.Region.ExtentM)
                .With("created_utc", created)
                .With("saved_utc", nowUtcText ?? string.Empty)
                .With("protocol_version", (int)ProtocolInfo.Version)
                .With("tick", world.Tick)
                .With("clock", new JsonObject().With("total_hours", world.Clock.TotalHours).With("started_at_hours", world.Clock.StartedAtHours))
                .With("next_entity_id", world.Entities.NextId);
            if (world.Wake.HasValue) doc.With("wake_east", world.Wake.Value.X).With("wake_up", world.Wake.Value.Y).With("wake_north", world.Wake.Value.Z);
            if (layerChecksums != null)
            {
                layers = new JsonObject();
                foreach (var pair in layerChecksums) layers.With(pair.Key, pair.Value);
            }
            if (layers != null) doc.With("layers", layers);
            WriteAtomic(worldPath, Encoding.UTF8.GetBytes(Json.Write(doc, indent: true)));

            WritePlayers(dir, players);
            WriteRegions(dir, world);

            List<KeyValuePair<string, MoverState>> bodies = new List<KeyValuePair<string, MoverState>>();
            List<CarrierRecord> carriers = new List<CarrierRecord>();
            if (players != null)
                foreach (SavedPlayer p in players)
                {
                    if (string.IsNullOrEmpty(p.Name)) continue;
                    bodies.Add(new KeyValuePair<string, MoverState>(p.Name, p.Body));
                    carriers.Add(new CarrierRecord { Name = p.Name, Hand = p.Hand, Things = p.Carried ?? Array.Empty<CarriedThing>() });
                }
            WriteAtomic(Path.Combine(dir, DigestFile), Encoding.UTF8.GetBytes(WorldDigest.World(world, bodies, carriers) + "\n"));
        }

        private static void WritePlayers(string dir, IReadOnlyList<SavedPlayer> players)
        {
            if (players == null) return;
            string playersDir = Path.Combine(dir, PlayersFolder);
            Directory.CreateDirectory(playersDir);
            foreach (SavedPlayer s in players)
            {
                if (string.IsNullOrEmpty(s.Name)) continue;
                string stem = Path.Combine(playersDir, FileNameFor(s.Name));
                WriteAtomic(stem + PlayerFile.Extension, PlayerFile.Encode(s));
                // A version-1 JSON file for the same name is superseded by the binary file, never left to disagree with it.
                if (File.Exists(stem + ".json")) File.Delete(stem + ".json");
            }
        }

        /// <summary>
        /// The entities by cell, and the takings from the loose layer by the cell their raster cell's centre lies in
        /// (M1.5b); a cell's file is removed when it holds neither, so the folder says exactly what exists.
        /// </summary>
        private static void WriteRegions(string dir, WorldState world)
        {
            string regionsDir = Path.Combine(dir, RegionFile.Folder);
            Directory.CreateDirectory(regionsDir);
            double extent = world.Region.ExtentM;
            Dictionary<long, List<SavedEntity>> byCell = new Dictionary<long, List<SavedEntity>>();
            Dictionary<long, List<LooseTaken.Cell>> takenByCell = new Dictionary<long, List<LooseTaken.Cell>>();
            IReadOnlyList<Entity> all = world.Entities.All;
            for (int i = 0; i < all.Count; i++)
            {
                Entity e = all[i];
                if (e.Killed) continue;
                int cx = RegionCells.IndexOf(e.Position.X, extent), cz = RegionCells.IndexOf(e.Position.Z, extent);
                long key = ((long)cx << 32) | (uint)cz;
                if (!byCell.TryGetValue(key, out List<SavedEntity> list)) byCell[key] = list = new List<SavedEntity>();
                list.Add(SavedEntity.Of(e));
            }
            RegionRaster loose = world.Loose;
            if (loose != null)
                foreach (LooseTaken.Cell cell in world.Taken.Cells())
                {
                    StandLayout.CellCentre(cell.Row, cell.Col, loose.CellM, loose.ExtentM, out double east, out double north);
                    long key = ((long)RegionCells.IndexOf(east, extent) << 32) | (uint)RegionCells.IndexOf(north, extent);
                    if (!takenByCell.TryGetValue(key, out List<LooseTaken.Cell> list)) takenByCell[key] = list = new List<LooseTaken.Cell>();
                    list.Add(cell);
                }
            HashSet<long> keys = new HashSet<long>(byCell.Keys);
            keys.UnionWith(takenByCell.Keys);
            HashSet<string> written = new HashSet<string>(StringComparer.Ordinal);
            foreach (long key in keys)
            {
                int cx = (int)(key >> 32), cz = (int)(key & 0xFFFFFFFF);
                string name = RegionFile.NameFor(cx, cz);
                byCell.TryGetValue(key, out List<SavedEntity> entities);
                takenByCell.TryGetValue(key, out List<LooseTaken.Cell> taken);
                WriteAtomic(Path.Combine(regionsDir, name), RegionFile.Encode(cx, cz, (IReadOnlyList<SavedEntity>)entities ?? Array.Empty<SavedEntity>(), taken));
                written.Add(name);
            }
            foreach (string file in Directory.GetFiles(regionsDir, "r.*.egr"))
                if (!written.Contains(Path.GetFileName(file))) File.Delete(file);
        }

        /// <summary>True when the folder holds a readable world.json.</summary>
        public static bool Exists(string dir) => !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, WorldFile));

        public static WorldSaveInfo Read(string dir)
        {
            string worldPath = Path.Combine(dir, WorldFile);
            if (!File.Exists(worldPath)) throw new FileNotFoundException("no world here", worldPath);
            JsonObject doc = Json.ParseObject(File.ReadAllText(worldPath, Encoding.UTF8));
            string format = doc.StringOr("format", "(none)");
            if (format != Format) throw new InvalidDataException(worldPath + ": format is '" + format + "', expected '" + Format + "'");
            int version = doc.Int("version");
            if (version != Version) throw new InvalidDataException(worldPath + ": version " + version + "; this build reads " + Version);
            WorldSaveInfo info = new WorldSaveInfo();
            info.RegionId = doc.String("region");
            info.Seed = (ulong)doc.Number("seed");
            info.Tick = (long)doc.Number("tick");
            info.CreatedUtc = doc.StringOr("created_utc", string.Empty);
            info.ProtocolVersion = (ushort)doc.Int("protocol_version");
            info.NextEntityId = (ulong)doc.NumberOr("next_entity_id", 1.0);
            if (doc.Contains("wake_east") && doc.Contains("wake_north"))
                info.Wake = new Double3(doc.Number("wake_east"), doc.NumberOr("wake_up", 0.0), doc.Number("wake_north"));
            if (doc.Contains("layers"))
                foreach (var pair in doc.Object("layers"))
                    info.Layers[pair.Key] = pair.Value as string ?? string.Empty;
            JsonObject clock = doc.Object("clock");
            info.TotalHours = clock.Number("total_hours");
            info.StartedAtHours = clock.Number("started_at_hours");

            ReadPlayers(dir, info);
            ReadRegions(dir, info, doc.NumberOr("extent_m", 0.0));
            CheckCarried(info);
            string digestPath = Path.Combine(dir, DigestFile);
            if (File.Exists(digestPath)) info.Digest = File.ReadAllText(digestPath, Encoding.UTF8).Trim();
            return info;
        }

        private static void ReadPlayers(string dir, WorldSaveInfo info)
        {
            string playersDir = Path.Combine(dir, PlayersFolder);
            if (!Directory.Exists(playersDir)) return;
            // Version 1: JSON, without wading and stance (they come back standing and dry).
            foreach (string file in Directory.GetFiles(playersDir, "*.json"))
            {
                JsonObject p = Json.ParseObject(File.ReadAllText(file, Encoding.UTF8));
                SavedPlayer sp = default;
                sp.Name = p.String("name");
                sp.Body = MoverState.AtRest(p.Number("east"), p.Number("up"), p.Number("north"));
                sp.Body.Grounded = p.Contains("grounded") && p.Bool("grounded");
                sp.YawDeg = (float)p.NumberOr("yaw_deg", 0.0);
                sp.PitchDeg = (float)p.NumberOr("pitch_deg", 0.0);
                sp.SavedTick = (long)p.NumberOr("saved_tick", 0.0);
                info.Players[sp.Name] = sp;
            }
            // The binary file wins over a version-1 file of the same name that a crash left behind.
            foreach (string file in Directory.GetFiles(playersDir, "*" + PlayerFile.Extension))
            {
                SavedPlayer sp = PlayerFile.Decode(File.ReadAllBytes(file));
                info.Players[sp.Name] = sp;
            }
        }

        /// <summary>
        /// A thing is in the world or in someone's hands, never both, and never under an id the world has not allocated
        /// (M1.5a): a folder that says otherwise was not written by a server, and is refused before a put-down could
        /// meet the same id twice.
        /// </summary>
        private static void CheckCarried(WorldSaveInfo info)
        {
            HashSet<ulong> lying = new HashSet<ulong>();
            foreach (SavedEntity e in info.Entities) lying.Add(e.Id);
            Dictionary<ulong, string> carriedBy = new Dictionary<ulong, string>();
            foreach (SavedPlayer p in info.Players.Values)
            {
                if (p.Carried == null) continue;
                foreach (CarriedThing t in p.Carried)
                {
                    if (t.Id == 0 || t.Id >= info.NextEntityId)
                        throw new InvalidDataException(p.Name + " carries entity " + t.Id + ", an id this world has not allocated (the next is " + info.NextEntityId + ")");
                    if (lying.Contains(t.Id))
                        throw new InvalidDataException(p.Name + " carries entity " + t.Id + ", which also lies in a region file");
                    if (carriedBy.TryGetValue(t.Id, out string other))
                        throw new InvalidDataException(p.Name + " and " + other + " both carry entity " + t.Id);
                    carriedBy[t.Id] = p.Name;
                }
            }
        }

        /// <summary>Every region file, its cell checked against its name and every entity's position against the cell.</summary>
        private static void ReadRegions(string dir, WorldSaveInfo info, double extentM)
        {
            string regionsDir = Path.Combine(dir, RegionFile.Folder);
            if (!Directory.Exists(regionsDir)) return;
            string[] files = Directory.GetFiles(regionsDir, "r.*.egr");
            Array.Sort(files, StringComparer.Ordinal);
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                if (!RegionFile.TryParseName(name, out int namedX, out int namedZ)) continue;
                List<SavedEntity> entities = RegionFile.Decode(File.ReadAllBytes(file), out int cx, out int cz, out List<LooseTaken.Cell> taken);
                if (cx != namedX || cz != namedZ) throw new InvalidDataException(name + " says it is cell (" + cx + ", " + cz + ")");
                foreach (LooseTaken.Cell cell in taken) info.Taken.Add(new SavedTaking { RegionX = cx, RegionZ = cz, Cell = cell });
                if (extentM > 0.0)
                    foreach (SavedEntity e in entities)
                        if (RegionCells.IndexOf(e.Position.X, extentM) != cx || RegionCells.IndexOf(e.Position.Z, extentM) != cz)
                            throw new InvalidDataException(name + " holds entity " + e.Id + " at (" + e.Position.X + ", " + e.Position.Z + "), which lies in another cell");
                info.Entities.AddRange(entities);
            }
            info.Entities.Sort((a, b) => a.Id.CompareTo(b.Id));
        }

        /// <summary>The world as it was, with the terrain the host loaded for it and every entity restored. The region must still be known to this build.</summary>
        /// <param name="region">The region the host runs, when it is not one <see cref="Region.ById"/> knows (a test's fixture); else looked up by the save's id.</param>
        public static WorldState Restore(WorldSaveInfo info, Heightfield terrain, Region region = null, WorldWater water = null, RegionRaster cover = null,
                                         RegionRaster stand = null, RegionRaster loose = null, RegionRaster stone = null)
        {
            if (region == null) region = Region.ById(info.RegionId);
            if (region == null) throw new InvalidDataException("the save is set in region '" + info.RegionId + "', which this build does not know");
            WorldState world = new WorldState(info.Seed, region, WorldClock.Restore(info.TotalHours, info.StartedAtHours), terrain, info.Tick, info.Wake, water, cover, stand, loose, stone);
            foreach (SavedTaking taking in info.Taken) world.Taken.Merge(CheckTaking(world, taking));
            foreach (SavedEntity s in info.Entities)
            {
                if (!DefinitionCatalogue.TryByKey(s.Key, out Definition definition))
                    throw new InvalidDataException("entity " + s.Id + " is a '" + s.Key + "', which this build does not know");
                Entity e = world.Entities.Restore(s.Id, definition, s.Position, s.YawDeg, s.SpawnTick);
                if (s.HasItem) e.SetItem(s.Item, s.SpawnTick);
            }
            world.Entities.SetNextId(Math.Max(info.NextEntityId, world.Entities.NextId));
            return world;
        }

        /// <summary>
        /// A taking read from a save, held against the world it is restored into (M1.5b): it names only things its cell's
        /// code counts, in a cell whose centre lies in the region file that recorded it. A folder that says otherwise was
        /// not written by this world's server, and is refused rather than drawn as a gap in the litter where nothing lay.
        /// </summary>
        private static LooseTaken.Cell CheckTaking(WorldState world, SavedTaking taking)
        {
            LooseTaken.Cell c = taking.Cell;
            RegionRaster loose = world.Loose;
            if (loose == null) throw new InvalidDataException("the save took things from cell (" + c.Row + ", " + c.Col + ") of a loose layer this world does not have");
            if (c.Row < 0 || c.Col < 0 || c.Row >= loose.Height || c.Col >= loose.Width)
                throw new InvalidDataException("the save took things from cell (" + c.Row + ", " + c.Col + "), outside the loose layer's " + loose.Width + " by " + loose.Height);
            byte code = (byte)loose.Code(c.Row, c.Col);
            int sticks = LooseCodes.SticksOf(code), cobbles = LooseCodes.CobblesOf(code);
            if ((c.Sticks >> sticks) != 0 || (c.Cobbles >> cobbles) != 0)
                throw new InvalidDataException("the save took from cell (" + c.Row + ", " + c.Col + ") sticks " + c.Sticks + " and cobbles " + c.Cobbles
                                               + " of a cell that held " + sticks + " sticks and " + cobbles + " cobbles");
            StandLayout.CellCentre(c.Row, c.Col, loose.CellM, loose.ExtentM, out double east, out double north);
            double extent = world.Region.ExtentM;
            if (RegionCells.IndexOf(east, extent) != taking.RegionX || RegionCells.IndexOf(north, extent) != taking.RegionZ)
                throw new InvalidDataException(RegionFile.NameFor(taking.RegionX, taking.RegionZ) + " holds the takings of cell (" + c.Row + ", " + c.Col + "), which lies in another");
            return c;
        }

        /// <summary>A player's name as a file name: letters, digits and a few marks; everything else becomes an underscore.</summary>
        public static string FileNameFor(string name)
        {
            StringBuilder sb = new StringBuilder(name.Length);
            foreach (char c in name)
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return sb.Length == 0 ? "_" : sb.ToString();
        }

        private static void WriteAtomic(string path, byte[] bytes)
        {
            string part = path + ".part";
            File.WriteAllBytes(part, bytes);
            if (File.Exists(path)) File.Delete(path);
            File.Move(part, path);
        }
    }
}
