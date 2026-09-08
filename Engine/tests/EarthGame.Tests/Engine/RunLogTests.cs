using System.Collections.Generic;
using System.IO;

using EarthGame.Engine;
using NUnit.Framework;

namespace EarthGame.Tests.Engine
{
    /// <summary>The run log's shape is a contract the owner's checkers read; this pins it (ARCHITECTURE §10).</summary>
    public sealed class RunLogTests
    {
        [Test]
        public void TheFirstLineIsTheHeaderAndEveryRecordStartsWithTimeTickAndKind()
        {
            StringWriter sw = new StringWriter();
            using (RunLog log = new RunLog(sw, new JsonObject().With("scenario", "first-frame").With("format", "ignored")))
            {
                log.Record(0.5, 10, "welcome", new JsonObject().With("session", 1));
                log.Record(1.25, 25, "frame", new JsonObject().With("file", "frames/t0001_1440p.png").With("width", 2560).With("kind", "ignored"));
                log.Record(2.0, 40, "end");
                Assert.That(log.Records, Is.EqualTo(3));
            }
            string[] lines = sw.ToString().TrimEnd('\n').Split('\n');
            Assert.That(lines.Length, Is.EqualTo(4));
            Assert.That(lines[0], Is.EqualTo("{\"format\":\"eg2.run\",\"version\":1,\"scenario\":\"first-frame\"}"), "the header's own format wins over a caller's");
            Assert.That(lines[1], Is.EqualTo("{\"t\":0.5,\"tick\":10,\"kind\":\"welcome\",\"session\":1}"));
            Assert.That(lines[2], Is.EqualTo("{\"t\":1.25,\"tick\":25,\"kind\":\"frame\",\"file\":\"frames/t0001_1440p.png\",\"width\":2560}"));
            Assert.That(lines[3], Is.EqualTo("{\"t\":2,\"tick\":40,\"kind\":\"end\"}"));
            foreach (string line in lines)
                Assert.That(Json.ParseObject(line), Is.Not.Null, "every line parses on its own");
        }

        [Test]
        public void OpenWritesAFileAFolderDeepAndFlushesEveryLine()
        {
            string dir = Path.Combine(Path.GetTempPath(), "eg2-runlog-" + System.Guid.NewGuid().ToString("N"));
            string path = Path.Combine(dir, "run", "run.jsonl");
            try
            {
                using (RunLog log = RunLog.Open(path, new JsonObject().With("mode", "solo")))
                {
                    log.Record(0.0, 0, "start");
                    // Read while still open: the line must already be on disk.
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader r = new StreamReader(fs))
                    {
                        List<string> seen = new List<string>();
                        string l;
                        while ((l = r.ReadLine()) != null) seen.Add(l);
                        Assert.That(seen.Count, Is.EqualTo(2));
                        Assert.That(Json.ParseObject(seen[0]).String("mode"), Is.EqualTo("solo"));
                    }
                }
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void ARecordWithoutAKindIsRefused()
        {
            using (RunLog log = new RunLog(new StringWriter(), null))
                Assert.That(() => log.Record(0.0, 0, ""), Throws.ArgumentException);
        }
    }
}
