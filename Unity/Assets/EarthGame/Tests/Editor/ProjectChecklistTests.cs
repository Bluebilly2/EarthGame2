using System.Collections.Generic;
using System.IO;
using EarthGame.Editor;
using NUnit.Framework;
using UnityEngine;

namespace EarthGame.Tests.Editor
{
    /// <summary>The settings checklist (ARCHITECTURE.md §8) holds, or this is red with every wrong item named.</summary>
    public sealed class ProjectChecklistTests
    {
        [Test]
        public void ChecklistHolds()
        {
            List<string> wrong = ProjectChecklist.Verify();
            Assert.That(wrong, Is.Empty, string.Join("\n", wrong));
        }

        /// <summary>
        /// MaterialPropertyBlock is the documented opt-out from the SRP Batcher and the GPU Resident Drawer, and
        /// v1 put one on roughly 3,400 renderers. Property blocks are set from code at runtime, so the ban is
        /// enforced where it can be seen: in the source. A play-mode walk of renderers joins this at M1.4.
        /// </summary>
        [Test]
        public void NoMaterialPropertyBlockInTheUnityLayer()
        {
            string root = Path.Combine(Application.dataPath, "EarthGame");
            List<string> offences = new List<string>();
            int scanned = 0;
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Tests/")) continue;
                scanned++;
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                    if (lines[i].Contains("MaterialPropertyBlock") || lines[i].Contains("SetPropertyBlock"))
                        offences.Add(file.Substring(root.Length + 1) + ":" + (i + 1));
            }
            Assert.That(scanned, Is.GreaterThan(3), "the scan found almost nothing");
            Assert.That(offences, Is.Empty, string.Join("\n", offences));
        }
    }
}
