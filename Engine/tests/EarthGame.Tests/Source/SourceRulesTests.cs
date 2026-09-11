using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace EarthGame.Tests.Source
{
    /// <summary>
    /// Rules a running program cannot see but the text shows plainly (STANDARDS 1, 3, 12). Each rule fails loudly
    /// when it scans nothing: a scan that finds no files is a broken scan, not a clean tree — v1 shipped a
    /// source rule that read a renamed-away member and passed forever.
    /// </summary>
    public sealed class SourceRulesTests
    {
        private static readonly string Root = TestPaths.Root;

        private static IEnumerable<string> EngineFreeSources()
        {
            string packages = Path.Combine(Root, "Engine", "packages");
            foreach (string file in Directory.GetFiles(packages, "*.cs", SearchOption.AllDirectories))
            {
                // LiteNetLib is vendored verbatim and carries its own #if UNITY_* branches; it is the one exception,
                // confined to its folder (THIRD_PARTY_NOTICES.md).
                if (file.Replace('\\', '/').Contains("/Runtime/LiteNetLib/")) continue;
                yield return file;
            }
        }

        [Test]
        public void EngineFreePackagesHaveNoUnityAndNoPreprocessorBranches()
        {
            int scanned = 0;
            List<string> offences = new List<string>();
            foreach (string file in EngineFreeSources())
            {
                scanned++;
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("#if", StringComparison.Ordinal) || trimmed.StartsWith("#elif", StringComparison.Ordinal))
                        offences.Add(Rel(file) + ":" + (i + 1) + " preprocessor branch: " + trimmed);
                    if (line.Contains("UnityEngine") || line.Contains("UnityEditor"))
                        offences.Add(Rel(file) + ":" + (i + 1) + " Unity reference: " + trimmed);
                }
            }
            Assert.That(scanned, Is.GreaterThan(10), "the scan found almost nothing; is the packages folder where this test thinks it is?");
            Assert.That(offences, Is.Empty, string.Join("\n", offences));
        }

        [Test]
        public void EngineFreePackagesReadNoClockAndNoUnseededRandom()
        {
            string[] banned = { "DateTime.Now", "DateTime.UtcNow", "Environment.TickCount", "new Random(", "System.Random", "Guid.NewGuid", "Stopwatch." };
            int scanned = 0;
            List<string> offences = new List<string>();
            foreach (string file in EngineFreeSources())
            {
                scanned++;
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = StripComment(lines[i]);
                    foreach (string b in banned)
                        if (code.Contains(b)) offences.Add(Rel(file) + ":" + (i + 1) + " " + b);
                }
            }
            Assert.That(scanned, Is.GreaterThan(10));
            Assert.That(offences, Is.Empty, string.Join("\n", offences));
        }

        [Test]
        public void NoTodoCommentsAnywhereDebtsGoInTheRegister()
        {
            string[] roots = { Path.Combine(Root, "Engine"), Path.Combine(Root, "Unity", "Assets", "EarthGame"), Path.Combine(Root, "Tools") };
            string[] markers = { "TODO", "FIXME", "HACK", "XXX" };
            int scanned = 0;
            List<string> offences = new List<string>();
            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (string file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
                {
                    string norm = file.Replace('\\', '/');
                    if (norm.Contains("/.build/") || norm.Contains("/LiteNetLib/") || norm.Contains("/.venv/") || norm.Contains("/__pycache__/")) continue;
                    if (!(norm.EndsWith(".cs") || norm.EndsWith(".py") || norm.EndsWith(".json") || norm.EndsWith(".asmdef") || norm.EndsWith(".csproj"))) continue;
                    if (norm.EndsWith("SourceRulesTests.cs")) continue; // this file names the markers to look for
                    scanned++;
                    string[] lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                        foreach (string m in markers)
                            if (lines[i].Contains(m + ":") || lines[i].Contains(m + " ")) offences.Add(Rel(file) + ":" + (i + 1) + " " + lines[i].Trim());
                }
            }
            Assert.That(scanned, Is.GreaterThan(10));
            Assert.That(offences, Is.Empty, string.Join("\n", offences));
        }

        /// <summary>
        /// One path opens a saved world (M1.4 loading, 2026-09-10). `WorldPreparation.Load` reads the terrain a
        /// world owns and refuses a missing or mismatched one; the dedicated host and the game both call it. Until
        /// that date each had its own reader that fell back to the region's bake, so a world whose heights layer
        /// had gone came back standing on different ground without a word. Production code that restores a save
        /// without going through the preparation is that path growing back.
        /// </summary>
        [Test]
        public void OnlyWorldPreparationOpensASavedWorld()
        {
            string[] roots = { Path.Combine(Root, "Engine", "packages"), Path.Combine(Root, "Engine", "tools"), Path.Combine(Root, "Unity", "Assets", "EarthGame") };
            int scanned = 0;
            List<string> offences = new List<string>();
            foreach (string root in roots)
            {
                Assert.That(Directory.Exists(root), Is.True, root);
                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string norm = file.Replace('\\', '/');
                    if (norm.Contains("/Runtime/LiteNetLib/")) continue;
                    if (norm.EndsWith("/WorldPreparation.cs", StringComparison.Ordinal)) continue;   // the one owner
                    scanned++;
                    string[] lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string code = StripComment(lines[i]);
                        if (code.Contains("WorldSave.Restore(")) offences.Add(Rel(file) + ":" + (i + 1) + " " + lines[i].Trim());
                    }
                }
            }
            Assert.That(scanned, Is.GreaterThan(20), "the scan found almost nothing; are the source roots where this test thinks they are?");
            Assert.That(offences, Is.Empty, "a saved world is opened by WorldPreparation.Load alone:\n" + string.Join("\n", offences));
        }

        /// <summary>
        /// Every binding lives in the controls asset (M1.5a): game code names actions, never keys or buttons, so the asset
        /// is the one place a key is chosen and M1.9's rebinding has one thing to change. Until 2026-09-11 the input source
        /// bound keys of its own when the asset was incomplete, and the screenshot read F12 straight off the keyboard.
        /// </summary>
        [Test]
        public void GameCodeNamesActionsAndTheControlsAssetBindsThem()
        {
            string root = Path.Combine(Root, "Unity", "Assets", "EarthGame");
            string[] banned =
            {
                "Keyboard.current", "Mouse.current", "Gamepad.current", "Joystick.current", "\"<Keyboard>", "\"<Mouse>", "\"<Gamepad>", "\"<Pointer>",
                "AddBinding(", "AddCompositeBinding(", "Input.GetKey", "Input.GetMouseButton",
            };
            int scanned = 0;
            List<string> offences = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                // A test may name a binding to check the asset holds it.
                if (file.Replace('\\', '/').Contains("/Tests/")) continue;
                scanned++;
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = StripComment(lines[i]);
                    foreach (string b in banned)
                        if (code.Contains(b)) offences.Add(Rel(file) + ":" + (i + 1) + " " + b);
                }
            }
            Assert.That(scanned, Is.GreaterThan(10), "the scan found almost nothing; is Unity/Assets/EarthGame where this test thinks it is?");
            Assert.That(offences, Is.Empty, "every binding lives in Assets/InputSystem_Actions.inputactions:\n" + string.Join("\n", offences));
        }

        private static string StripComment(string line)
        {
            int idx = line.IndexOf("//", StringComparison.Ordinal);
            return idx < 0 ? line : line.Substring(0, idx);
        }

        private static string Rel(string file) => file.Substring(Root.Length + 1).Replace('\\', '/');
    }
}
