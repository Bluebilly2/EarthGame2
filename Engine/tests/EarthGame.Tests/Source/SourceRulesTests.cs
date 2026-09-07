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

        private static string StripComment(string line)
        {
            int idx = line.IndexOf("//", StringComparison.Ordinal);
            return idx < 0 ? line : line.Substring(0, idx);
        }

        private static string Rel(string file) => file.Substring(Root.Length + 1).Replace('\\', '/');
    }
}
