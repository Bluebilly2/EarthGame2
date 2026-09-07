using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EarthGame.Editor
{
    /// <summary>
    /// Batch-mode entry points. <c>Apply</c> makes the project match the checklist and creates the Boot scene;
    /// run once after a fresh clone or a template import:
    /// <c>Unity.exe -batchmode -quit -projectPath Unity -executeMethod EarthGame.Editor.ProjectSetup.Apply -logFile setup.log</c>.
    /// The edit-mode test then proves the checklist holds; a failed check is a red, not a warning.
    /// </summary>
    public static class ProjectSetup
    {
        [MenuItem("EarthGame/Apply project checklist")]
        public static void Apply()
        {
            EnsureBootScene();
            List<string> changed = ProjectChecklist.Apply();
            Debug.Log("[setup] checklist applied; changed " + changed.Count + " item(s)" + (changed.Count > 0 ? ": " + string.Join("; ", changed) : ""));
            List<string> wrong = ProjectChecklist.Verify();
            if (wrong.Count > 0)
            {
                Debug.LogError("[setup] checklist still wrong after Apply: " + string.Join("; ", wrong));
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
            else Debug.Log("[setup] checklist verified");
        }

        /// <summary>
        /// The one scene: a camera, a light and the bootstrap object. Nothing world-shaped is ever placed in it;
        /// the world arrives from the server at runtime.
        /// </summary>
        private static void EnsureBootScene()
        {
            string dir = Path.GetDirectoryName(ProjectChecklist.BootScenePath);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), dir));
                AssetDatabase.Refresh();
            }
            if (!File.Exists(ProjectChecklist.BootScenePath))
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                GameObject boot = new GameObject("Bootstrap");
                boot.AddComponent<EarthGame.Bootstrap.Bootstrap>();
                EditorSceneManager.SaveScene(scene, ProjectChecklist.BootScenePath);
                Debug.Log("[setup] created " + ProjectChecklist.BootScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ProjectChecklist.BootScenePath, true) };
        }
    }
}
