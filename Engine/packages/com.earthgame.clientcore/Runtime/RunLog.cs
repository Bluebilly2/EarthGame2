using System;
using System.IO;
using System.Text;
using EarthGame.Engine;

namespace EarthGame.ClientCore
{
    /// <summary>
    /// The recorder's log, <c>run.jsonl</c> (format <c>eg2.run</c>, version 1; a contracted format under
    /// ARCHITECTURE.md §10 that the owner's <c>corpus_check.py</c> and <c>join_check.py</c> read). One JSON object
    /// per line: the first line is the header, carrying <c>format</c> and <c>version</c> first and whatever the
    /// run wants to say about itself after; every later line is a record whose first three keys are <c>t</c>
    /// (real seconds since the run started), <c>tick</c> (the server tick the client last knew) and <c>kind</c>,
    /// followed by the record's own fields. Flushed after every line, so a crash leaves everything up to it.
    /// Time and dates are the caller's: this class reads no clock.
    /// </summary>
    public sealed class RunLog : IDisposable
    {
        public const string Format = "eg2.run";
        public const int Version = 1;

        private readonly TextWriter _writer;
        private readonly bool _ownsWriter;

        public RunLog(TextWriter writer, JsonObject header, bool ownsWriter = false)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _ownsWriter = ownsWriter;
            JsonObject first = new JsonObject().With("format", Format).With("version", Version);
            if (header != null)
                foreach (var pair in header)
                    if (pair.Key != "format" && pair.Key != "version") first[pair.Key] = pair.Value;
            WriteLine(first);
        }

        /// <summary>Opens (or truncates) the file, creating its folder, and writes the header.</summary>
        public static RunLog Open(string path, JsonObject header)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            StreamWriter w = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
            return new RunLog(w, header, ownsWriter: true);
        }

        public int Records { get; private set; }

        /// <summary>One record. Field order is kept, after the three keys every record starts with.</summary>
        public void Record(double t, long tick, string kind, JsonObject fields = null)
        {
            if (string.IsNullOrEmpty(kind)) throw new ArgumentException("a record needs a kind", nameof(kind));
            JsonObject line = new JsonObject().With("t", t).With("tick", tick).With("kind", kind);
            if (fields != null)
                foreach (var pair in fields)
                    if (pair.Key != "t" && pair.Key != "tick" && pair.Key != "kind") line[pair.Key] = pair.Value;
            WriteLine(line);
            Records++;
        }

        private void WriteLine(JsonObject o)
        {
            _writer.Write(Json.Write(o));
            _writer.Write('\n');
            _writer.Flush();
        }

        public void Dispose()
        {
            _writer.Flush();
            if (_ownsWriter) _writer.Dispose();
        }
    }
}
