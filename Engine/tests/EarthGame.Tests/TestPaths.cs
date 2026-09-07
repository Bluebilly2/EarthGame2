using System;
using System.IO;

namespace EarthGame.Tests
{
    /// <summary>
    /// Where the repository is, found from the test assembly's own location by walking up to <c>global.json</c>.
    /// One owner for the fact; every test that reads a fixture or scans the tree asks here.
    /// </summary>
    public static class TestPaths
    {
        public static readonly string Root = FindRoot();

        public static string Fixture(params string[] parts)
            => Path.Combine(Path.Combine(Root, "Data", "fixtures"), Path.Combine(parts));

        private static string FindRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "global.json")))
                dir = Path.GetDirectoryName(dir);
            if (dir == null)
                throw new InvalidOperationException("repository root (global.json) not found above " + AppContext.BaseDirectory);
            return dir;
        }
    }
}
