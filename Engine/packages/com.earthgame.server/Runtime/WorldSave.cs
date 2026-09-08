using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EarthGame.Engine;
using EarthGame.Protocol;

namespace EarthGame.Server
{
    /// <summary>Where a player was when the world was last written: the spawn for their next join.</summary>
    public struct SavedPlayer
    {
        public string Name;
        public MoverState Body;
        public float YawDeg;
        public float PitchDeg;
        public long SavedTick;
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
    }

    /// <summary>
    /// The world folder's <c>world.json</c> (format <c>eg2.world</c>, version 1) and <c>players/&lt;name&gt;.json</c>
    /// (ARCHITECTURE §6): identity, the clock's two numbers, the tick, and each player's resting place. The server
    /// is the only writer; every file is written to a <c>.part</c> and moved into place so a crash mid-write
    /// leaves the previous save intact. Entities and layer diffs arrive with the entity store (M1.3) as the
    /// binary region files; this is the part a shell needs to offer "continue". Dates are the host's: the
    /// server reads no clock.
    /// </summary>
    public static class WorldSave
    {
        public const string Format = "eg2.world";
        public const int Version = 1;
        public const string WorldFile = "world.json";
        public const string PlayersFolder = "players";

        public static void Write(string dir, WorldState world, IReadOnlyList<SavedPlayer> players, string nowUtcText)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            Directory.CreateDirectory(dir);
            string worldPath = Path.Combine(dir, WorldFile);
            string created = nowUtcText ?? string.Empty;
            if (File.Exists(worldPath))
            {
                // The creation date is written once; every later save carries it forward.
                try
                {
                    JsonObject existing = Json.ParseObject(File.ReadAllText(worldPath, Encoding.UTF8));
                    created = existing.StringOr("created_utc", created);
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
                .With("clock", new JsonObject().With("total_hours", world.Clock.TotalHours).With("started_at_hours", world.Clock.StartedAtHours));
            WriteAtomic(worldPath, Json.Write(doc, indent: true));

            if (players == null) return;
            string playersDir = Path.Combine(dir, PlayersFolder);
            Directory.CreateDirectory(playersDir);
            foreach (SavedPlayer s in players)
            {
                if (string.IsNullOrEmpty(s.Name)) continue;
                JsonObject p = new JsonObject()
                    .With("format", "eg2.player")
                    .With("version", Version)
                    .With("name", s.Name)
                    .With("east", s.Body.East).With("up", s.Body.Up).With("north", s.Body.North)
                    .With("yaw_deg", (double)s.YawDeg).With("pitch_deg", (double)s.PitchDeg)
                    .With("grounded", s.Body.Grounded)
                    .With("saved_tick", s.SavedTick);
                WriteAtomic(Path.Combine(playersDir, FileNameFor(s.Name) + ".json"), Json.Write(p, indent: true));
            }
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
            JsonObject clock = doc.Object("clock");
            info.TotalHours = clock.Number("total_hours");
            info.StartedAtHours = clock.Number("started_at_hours");

            string playersDir = Path.Combine(dir, PlayersFolder);
            if (Directory.Exists(playersDir))
            {
                foreach (string file in Directory.GetFiles(playersDir, "*.json"))
                {
                    JsonObject p = Json.ParseObject(File.ReadAllText(file, Encoding.UTF8));
                    SavedPlayer sp;
                    sp.Name = p.String("name");
                    sp.Body = MoverState.AtRest(p.Number("east"), p.Number("up"), p.Number("north"));
                    sp.Body.Grounded = p.Contains("grounded") && p.Bool("grounded");
                    sp.YawDeg = (float)p.NumberOr("yaw_deg", 0.0);
                    sp.PitchDeg = (float)p.NumberOr("pitch_deg", 0.0);
                    sp.SavedTick = (long)p.NumberOr("saved_tick", 0.0);
                    info.Players[sp.Name] = sp;
                }
            }
            return info;
        }

        /// <summary>The world as it was, with the terrain the host loaded for it. The region must still be known to this build.</summary>
        public static WorldState Restore(WorldSaveInfo info, Heightfield terrain)
        {
            Region region = Region.ById(info.RegionId);
            if (region == null) throw new InvalidDataException("the save is set in region '" + info.RegionId + "', which this build does not know");
            return new WorldState(info.Seed, region, WorldClock.Restore(info.TotalHours, info.StartedAtHours), terrain, info.Tick);
        }

        /// <summary>A player's name as a file name: letters, digits and a few marks; everything else becomes an underscore.</summary>
        public static string FileNameFor(string name)
        {
            StringBuilder sb = new StringBuilder(name.Length);
            foreach (char c in name)
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return sb.Length == 0 ? "_" : sb.ToString();
        }

        private static void WriteAtomic(string path, string text)
        {
            string part = path + ".part";
            File.WriteAllText(part, text, new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(part, path);
        }
    }
}
