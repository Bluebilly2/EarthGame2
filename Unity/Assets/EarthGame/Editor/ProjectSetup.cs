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
        public const string TerrainMaterialPath = ResourcesFolder + "/Ground.mat";
        public const string GroundTexturePath = ResourcesFolder + "/GroundTex.asset";
        public const string GroundLayerPath = ResourcesFolder + "/GroundLayer.terrainlayer";
        public const string SkyMaterialPath = ResourcesFolder + "/Sky.mat";
        public const string SeaMaterialPath = ResourcesFolder + "/Sea.mat";
        public const string StandMaterialPath = ResourcesFolder + "/StandLit.mat";
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
                        Color ground = Color.Lerp(new Color(0.40f, 0.33f, 0.21f), new Color(0.82f, 0.74f, 0.54f), Mathf.Clamp01(n + speck));
                        ground.a = 0.12f; // alpha is smoothness to a terrain layer that reads it; dry either way
                        pixels[y * 64 + x] = ground;
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
                layer.smoothness = 0.12f;
                layer.metallic = 0f;
                // A new layer reads its smoothness from the diffuse texture's alpha, and an opaque texture is
                // fully glossy: the ground drew as a mirror with a sun streak (2026-09-08). The ground texture's
                // alpha therefore carries the dry value too, whichever source the layer uses.
                AssetDatabase.CreateAsset(layer, GroundLayerPath);
                created.Add(GroundLayerPath);
            }

            // The ground's own shader since M1.6e (2026-09-22): a Lambert with the tile's colour map and a grain made in
            // the shader, no smoothness to go glossy on (the pipeline's terrain material defaulted every layer to wet
            // rock, 2026-09-08) and no instancing to be stripped (the terrain draws non-instanced; the stock material
            // needed instancing on or the built player drew no terrain at all, 2026-09-08).
            EnsureMaterial(TerrainMaterialPath, "EarthGame/Ground", created);
            EnsureMaterial(SkyMaterialPath, "Skybox/Procedural", created);
            if (EnsureMaterial(SeaMaterialPath, "Universal Render Pipeline/Lit", created))
            {
                Material sea = AssetDatabase.LoadAssetAtPath<Material>(SeaMaterialPath);
                sea.SetColor("_BaseColor", new Color(0.05f, 0.16f, 0.24f, 1f));
                sea.SetFloat("_Smoothness", 0.92f);
                sea.SetFloat("_Metallic", 0f);
                EditorUtility.SetDirty(sea);
            }
            EnsureMaterial(StandMaterialPath, "EarthGame/StandLit", created);
            {
                // What stands and lies on the ground is drawn instanced, and its bands' materials were first made at
                // runtime from the shader alone, so no material in the build enabled instancing and the build stripped
                // the shader's instanced variants: the first player of M1.6a held its trees and none stood in its
                // frames (2026-09-11). The bands now copy this asset. Applied every run, as the terrain's is.
                Material stand = AssetDatabase.LoadAssetAtPath<Material>(StandMaterialPath);
                if (stand != null && !stand.enableInstancing)
                {
                    stand.enableInstancing = true;
                    EditorUtility.SetDirty(stand);
                    created.Add(StandMaterialPath + " (instancing on)");
                }
            }

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

        /// <summary>Creates the material when missing; true when it was created this time.</summary>
        private static bool EnsureMaterial(string path, string shaderName, List<string> created)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return false;
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("[setup] shader not found: " + shaderName + " (needed for " + path + ")");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return false;
            }
            AssetDatabase.CreateAsset(new Material(shader), path);
            created.Add(path);
            return true;
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
