using System;
using System.IO;
using EarthGame.Engine;
using UnityEngine;

namespace EarthGame.Shared
{
    /// <summary>
    /// Which build this is (M1.Bb, 2026-09-16): the <c>version.json</c> that <c>Tools/build/install.py</c> writes beside the
    /// player's exe when it installs a build, read once. The game lives in one folder and is replaced in place
    /// (<c>Build/Player</c>, the owner's; <c>Build/Harness</c>, the runs'), because every new folder an exe runs from was a
    /// permission prompt in the harness and, if it opened a socket, a firewall box on the owner's screen; so a build is
    /// told apart by this file, the log's first line and the run log's header, not by a folder's name. In the editor, or
    /// beside a player nothing installed, it is unversioned and says so.
    /// </summary>
    public static class BuildInfo
    {
        public const string FileName = "version.json";

        /// <summary>What the build was called when installed (a slice's name, or what the tool was told).</summary>
        public static readonly string Label = "";
        /// <summary>The commit it was built from, short; "+dirty" in <see cref="Describe"/> when the tree had uncommitted changes.</summary>
        public static readonly string Commit = "";
        public static readonly string Branch = "";
        public static readonly string BuiltUtc = "";
        public static readonly bool Dirty;
        /// <summary>True when a version file was found and read beside the player.</summary>
        public static readonly bool Known;

        static BuildInfo()
        {
            try
            {
                string folder = Path.GetDirectoryName(Application.dataPath);
                string path = folder == null ? null : Path.Combine(folder, FileName);
                if (path == null || !File.Exists(path)) return;
                if (!(Json.Parse(File.ReadAllText(path)) is JsonObject o)) return;
                Label = o.StringOr("label", "");
                Commit = o.StringOr("commit_short", o.StringOr("commit", ""));
                Branch = o.StringOr("branch", "");
                BuiltUtc = o.StringOr("built_utc", "");
                Dirty = o.Contains("dirty") && o.Bool("dirty");
                Known = Commit.Length > 0;
            }
            catch (Exception ex)
            {
                // A version file that cannot be read is no reason not to start; the log says the build is unversioned.
                Debug.LogWarning("[build] " + FileName + " could not be read: " + ex.Message);
            }
        }

        /// <summary>One line naming the build, for the log's first line.</summary>
        public static string Describe() => Known
            ? (Label.Length > 0 ? Label + " " : "") + Commit + (Dirty ? "+dirty" : "") + (BuiltUtc.Length > 0 ? " built " + BuiltUtc : "") + (Branch.Length > 0 ? " on " + Branch : "")
            : "unversioned (no " + FileName + " beside the player: the editor, or a build Tools/build/install.py did not install)";
    }
}
