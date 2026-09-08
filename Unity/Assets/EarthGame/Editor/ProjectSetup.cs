using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace EarthGame.Editor
{
    /// <summary>
    /// Batch-mode entry points. <c>Apply</c> makes the project match the checklist, creates the Boot scene and the
    /// handful of runtime assets the client loads from Resources (materials whose shaders must reach the build,
    /// the UI Toolkit panel); run once after a fresh clone or a template import:
    /// <c>Unity.exe -batchmode -quit -projectPath Unity -executeMethod EarthGame.Editor.ProjectSetup.Apply -logFile setup.log</c>.
    /// The edit-mode test then proves the checklist holds; a failed check is a red, not a warning.
    /// </summary>
    public static class ProjectSetup
    {
        public const string ResourcesFolder = "Assets/EarthGame/Resources/EarthGame";
        public const string TerrainMaterialPath = ResourcesFolder + "/TerrainLit.mat";
        public const string GroundTexturePath = ResourcesFolder + "/GroundTex.asset";
        public const string GroundLayerPath = ResourcesFolder + "/GroundLayer.terrainlayer";
        public const string SkyMaterialPath = ResourcesFolder + "/Sky.mat";
        public const string HudPanelPath = ResourcesFolder + "/HudPanel.asset";
        public const string ThemePath = "Assets/UI Toolkit/UnityDefaultRuntimeTheme.tss";

        [MenuItem("EarthGame/Apply project checklist")]
        public static void Apply()
        {
            EnsureBootScene();
            EnsureRuntimeAssets();
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

        /// <summary>
        /// The assets the client loads by name at runtime. Created only when missing, so an artist's later edits
        /// survive a re-run. Materials exist as assets so their shaders are compiled into the player: a shader
        /// found by name at runtime is stripped from a build that has no material using it.
        /// </summary>
        public static void EnsureRuntimeAssets()
        {
            EnsureFolder(ResourcesFolder);
            List<string> created = new List<string>();

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTexturePath) == null)
            {
                Texture2D tex = new Texture2D(64, 64, TextureFormat.RGBA32, true);
                Color[] pixels = new Color[64 * 64];
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        // A dry sclerophyll ground: sand and litter, mottled so the surface reads at every scale.
                        float n = Mathf.PerlinNoise(x * 0.23f + 3.1f, y * 0.23f + 7.7f) * 0.55f + Mathf.PerlinNoise(x * 0.07f, y * 0.07f) * 0.45f;
                        float speck = Mathf.PerlinNoise(x * 0.9f + 11f, y * 0.9f + 5f) > 0.68f ? 0.12f : 0f;
                        pixels[y * 64 + x] = Color.Lerp(new Color(0.33f, 0.27f, 0.17f), new Color(0.66f, 0.58f, 0.40f), Mathf.Clamp01(n + speck));
                    }
                }
                tex.SetPixels(pixels);
                tex.Apply(true);
                tex.wrapMode = TextureWrapMode.Repeat;
                AssetDatabase.CreateAsset(tex, GroundTexturePath);
                created.Add(GroundTexturePath);
            }

            if (AssetDatabase.LoadAssetAtPath<TerrainLayer>(GroundLayerPath) == null)
            {
                TerrainLayer layer = new TerrainLayer();
                layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTexturePath);
                layer.tileSize = new Vector2(3f, 3f);
                AssetDatabase.CreateAsset(layer, GroundLayerPath);
                created.Add(GroundLayerPath);
            }

            EnsureMaterial(TerrainMaterialPath, "Universal Render Pipeline/Terrain/Lit", created);
            EnsureMaterial(SkyMaterialPath, "Skybox/Procedural", created);

            if (AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath) == null)
            {
                EnsureFolder(Path.GetDirectoryName(ThemePath).Replace('\\', '/'));
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), ThemePath), "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(ThemePath);
                created.Add(ThemePath);
            }

            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(HudPanelPath) == null)
            {
                PanelSettings panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1920, 1080);
                panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panel.match = 0.5f;
                AssetDatabase.CreateAsset(panel, HudPanelPath);
                created.Add(HudPanelPath);
            }

            AssetDatabase.SaveAssets();
            if (created.Count > 0) Debug.Log("[setup] created runtime assets: " + string.Join(", ", created));
        }

        private static void EnsureMaterial(string path, string shaderName, List<string> created)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("[setup] shader not found: " + shaderName + " (needed for " + path + ")");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            AssetDatabase.CreateAsset(new Material(shader), path);
            created.Add(path);
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetPath));
        }
    }
}
