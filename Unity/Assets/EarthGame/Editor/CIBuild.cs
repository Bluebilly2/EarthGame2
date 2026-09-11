using System.Collections.Generic;
using System.IO;
using EarthGame.Client;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EarthGame.Editor
{
    /// <summary>
    /// Builds the Windows player from the command line:
    /// <c>Unity.exe -batchmode -quit -projectPath Unity -executeMethod EarthGame.Editor.CIBuild.BuildWindows -logFile build.log</c>.
    /// The exit code is the verdict (STANDARDS 8): a failed build exits 1, and the tools that call this read the
    /// exit code, never the log for a success sentence. Output goes beside the repository's other build products
    /// in <c>Build/Player/</c> (gitignored), or where <c>-buildOut &lt;folder&gt;</c> says when a player from the
    /// previous build is still running there.
    /// </summary>
    public static class CIBuild
    {
        public static void BuildWindows()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string outDir = Path.Combine(repoRoot, "Build", "Player");
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-buildOut") outDir = Path.GetFullPath(args[i + 1]);
            // Every binding lives in the controls asset (M1.5a); a player whose project-wide actions lacked one would leave
            // that control dead under the owner's hands, so the build is refused here instead.
            List<string> missing = Controls.Missing(InputSystem.actions);
            if (missing.Count > 0)
            {
                Debug.LogError("[build] the project-wide controls lack " + string.Join(", ", missing) + "; every binding lives in Assets/InputSystem_Actions.inputactions");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            Directory.CreateDirectory(outDir);
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectChecklist.BootScenePath },
                locationPathName = Path.Combine(outDir, "EarthGame2.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[build] " + summary.outputPath + " (" + summary.totalSize + " bytes, " + summary.totalTime.TotalSeconds.ToString("0") + " s)");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[build] " + summary.result + " with " + summary.totalErrors + " error(s)");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
