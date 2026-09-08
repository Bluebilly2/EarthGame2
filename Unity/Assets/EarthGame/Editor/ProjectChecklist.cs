using System.Collections.Generic;
using EarthGame.Shared;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EarthGame.Editor
{
    /// <summary>
    /// The settings checklist of ARCHITECTURE.md §8, as the one class that both APPLIES it (the batch-mode setup)
    /// and VERIFIES it (the edit-mode test). Two copies of "what the project settings must be" is the named bug
    /// shape; here the expected value and the assertion share a line. Every item that has a prerequisite is
    /// applied in the load-bearing order the URP manual gives: BatchRendererGroup variants kept, then Forward+,
    /// then the GPU Resident Drawer, then Render Graph, then occlusion, then depth priming, then STP.
    /// </summary>
    public static class ProjectChecklist
    {
        public const string PipelineAssetPath = "Assets/Settings/PC_RPAsset.asset";
        public const string RendererDataPath = "Assets/Settings/PC_Renderer.asset";
        public const string BootScenePath = "Assets/EarthGame/Scenes/Boot.unity";
        public const string ScriptingDefine = "SIMULATE_NETWORK";
        public const float DefaultRenderScale = 0.7f;
        public const float ShadowDistanceM = 150f;
        public const int ShadowCascades = 2;

        /// <summary>Applies every item, in order. Idempotent. Returns what it changed, for the log.</summary>
        public static List<string> Apply()
        {
            List<string> changed = new List<string>();

            if (PlayerSettings.colorSpace != ColorSpace.Linear) { PlayerSettings.colorSpace = ColorSpace.Linear; changed.Add("colour space → Linear"); }
            if (PlayerSettings.productName != "EarthGame2") { PlayerSettings.productName = "EarthGame2"; changed.Add("productName"); }
            if (PlayerSettings.companyName != "EarthGame2") { PlayerSettings.companyName = "EarthGame2"; changed.Add("companyName"); }
            if (!PlayerSettings.runInBackground) { PlayerSettings.runInBackground = true; changed.Add("runInBackground"); }
            if (PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.Standalone) != ApiCompatibilityLevel.NET_Standard)
            { PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard); changed.Add("api compatibility → .NET Standard 2.1"); }
            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            if (!defines.Contains(ScriptingDefine))
            { PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, string.IsNullOrEmpty(defines) ? ScriptingDefine : defines + ";" + ScriptingDefine); changed.Add("define " + ScriptingDefine); }
            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            if (apis.Length < 2 || apis[0] != GraphicsDeviceType.Direct3D12 || apis[1] != GraphicsDeviceType.Direct3D11)
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Direct3D11 });
                changed.Add("graphics APIs → DX12, DX11");
            }

            // 1. BatchRendererGroup variants kept, before anything that needs them. The editor exposes only a getter,
            //    so the value is written through the serialized graphics settings and read back through the getter.
            if (EditorGraphicsSettings.batchRendererGroupShaderStrippingMode != BatchRendererGroupStrippingMode.KeepAll)
            {
                SerializedObject gfx = new SerializedObject(GraphicsSettings.GetGraphicsSettings());
                SerializedProperty brg = gfx.FindProperty("m_BrgStripping");
                if (brg == null) changed.Add("BRG VARIANTS FIELD NOT FOUND — not applied");
                else
                {
                    brg.intValue = (int)BatchRendererGroupStrippingMode.KeepAll;
                    gfx.ApplyModifiedPropertiesWithoutUndo();
                    changed.Add("BRG variants → Keep All");
                }
            }

            UniversalRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
            if (asset == null || renderer == null) { changed.Add("PIPELINE ASSET OR RENDERER MISSING — nothing applied"); return changed; }

            // 2. Forward+ before the drawer, which refuses to start on Forward or Deferred.
            if (renderer.renderingMode != RenderingMode.ForwardPlus) { renderer.renderingMode = RenderingMode.ForwardPlus; changed.Add("renderer → Forward+"); }
            // 3. The drawer, with the SRP Batcher it needs.
            if (!asset.useSRPBatcher) { asset.useSRPBatcher = true; changed.Add("SRP Batcher on"); }
            if (asset.gpuResidentDrawerMode != GPUResidentDrawerMode.InstancedDrawing) { asset.gpuResidentDrawerMode = GPUResidentDrawerMode.InstancedDrawing; changed.Add("GPU Resident Drawer → Instanced Drawing"); }
            // 4. Render Graph is mandatory in 6.3 (Compatibility Mode is gone); nothing to set.
            // 5. Occlusion culling, which needs the drawer. Written through a SerializedObject because the field has
            //    no public setter — and applied IMMEDIATELY, before any further direct property set on the asset:
            //    a SerializedObject is a snapshot, and applying it after direct sets writes the snapshot's stale
            //    values back over them (the first run of this method lost four settings exactly that way).
            using (SerializedObject so = new SerializedObject(asset))
            {
                SerializedProperty occlusion = so.FindProperty("m_GPUResidentDrawerEnableOcclusionCullingInCameras");
                if (occlusion != null && !occlusion.boolValue)
                {
                    occlusion.boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed.Add("GPU occlusion on");
                }
            }
            // 6. Depth priming, Unity's own mitigation for the drawer's GPU cost on a GPU-bound card. (Suspected
            //    and cleared on 2026-09-08 when the terrain drew nothing: the cause was stripped instancing
            //    variants, see the terrain material below.)
            if (renderer.depthPrimingMode != DepthPrimingMode.Forced) { renderer.depthPrimingMode = DepthPrimingMode.Forced; changed.Add("depth priming → Forced"); }
            // 7. STP at render scale 0.7: the one-slider escape hatch, wired before it is needed.
            if (asset.upscalingFilter != UpscalingFilterSelection.STP) { asset.upscalingFilter = UpscalingFilterSelection.STP; changed.Add("upscaling → STP"); }
            if (!Mathf.Approximately(asset.renderScale, DefaultRenderScale)) { asset.renderScale = DefaultRenderScale; changed.Add("render scale → " + DefaultRenderScale); }

            // The budget's shadow and AO rows (ARCHITECTURE.md §8): 150 m, two cascades, SSAO off until measured.
            if (!Mathf.Approximately(asset.shadowDistance, ShadowDistanceM)) { asset.shadowDistance = ShadowDistanceM; changed.Add("shadow distance → " + ShadowDistanceM); }
            if (asset.shadowCascadeCount != ShadowCascades) { asset.shadowCascadeCount = ShadowCascades; changed.Add("shadow cascades → " + ShadowCascades); }
            if (asset.msaaSampleCount != 1) { asset.msaaSampleCount = 1; changed.Add("MSAA off (STP's TAA instead)"); }
            if (!asset.supportsCameraDepthTexture) { asset.supportsCameraDepthTexture = true; changed.Add("depth texture on"); }
            foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
                if (feature != null && feature.name.Contains("AmbientOcclusion") && feature.isActive) { feature.SetActive(false); changed.Add("SSAO off until measured"); }

            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(renderer);

            // Quality: one level, no vsync by default (the measurement protocol wants uncapped; the player's own
            // setting will re-enable it), the URP asset on it.
            if (QualitySettings.vSyncCount != 0) { QualitySettings.vSyncCount = 0; changed.Add("vsync off by default"); }

            // Named layers, written from the one list in Shared.Layers.
            SerializedObject tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tags.FindProperty("layers");
            for (int i = 0; i < Layers.Names.Length; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(Layers.Indices[i]);
                if (slot.stringValue != Layers.Names[i]) { slot.stringValue = Layers.Names[i]; changed.Add("layer " + Layers.Indices[i] + " → " + Layers.Names[i]); }
            }
            tags.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            return changed;
        }

        /// <summary>Every item that is not as it must be, named. Empty means the checklist holds.</summary>
        public static List<string> Verify()
        {
            List<string> wrong = new List<string>();
            if (PlayerSettings.colorSpace != ColorSpace.Linear) wrong.Add("colour space is not Linear");
            if (PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.Standalone) != ApiCompatibilityLevel.NET_Standard) wrong.Add("api compatibility is not .NET Standard 2.1");
            if (!PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone).Contains(ScriptingDefine)) wrong.Add("define " + ScriptingDefine + " missing");
            if (!PlayerSettings.runInBackground) wrong.Add("runInBackground is off");
            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            if (apis.Length < 2 || apis[0] != GraphicsDeviceType.Direct3D12 || apis[1] != GraphicsDeviceType.Direct3D11) wrong.Add("graphics APIs are not DX12 then DX11");
            SerializedObject player = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            SerializedProperty inputHandler = player.FindProperty("activeInputHandler");
            if (inputHandler == null || inputHandler.intValue != 1) wrong.Add("active input handling is not the Input System package");
            if (EditorGraphicsSettings.batchRendererGroupShaderStrippingMode != BatchRendererGroupStrippingMode.KeepAll) wrong.Add("BatchRendererGroup variants are not Keep All");
            if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset)) wrong.Add("the default render pipeline is not URP");

            UniversalRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
            if (asset == null) { wrong.Add("pipeline asset missing at " + PipelineAssetPath); return wrong; }
            if (renderer == null) { wrong.Add("renderer data missing at " + RendererDataPath); return wrong; }
            if (GraphicsSettings.defaultRenderPipeline != asset) wrong.Add("the default render pipeline is not " + PipelineAssetPath);
            if (renderer.renderingMode != RenderingMode.ForwardPlus) wrong.Add("renderer is not Forward+");
            if (!asset.useSRPBatcher) wrong.Add("SRP Batcher is off");
            if (asset.gpuResidentDrawerMode != GPUResidentDrawerMode.InstancedDrawing) wrong.Add("GPU Resident Drawer is not Instanced Drawing");
            SerializedProperty occlusion = new SerializedObject(asset).FindProperty("m_GPUResidentDrawerEnableOcclusionCullingInCameras");
            if (occlusion == null || !occlusion.boolValue) wrong.Add("GPU occlusion culling is off");
            if (renderer.depthPrimingMode != DepthPrimingMode.Forced) wrong.Add("depth priming is not Forced");
            if (asset.upscalingFilter != UpscalingFilterSelection.STP) wrong.Add("upscaling filter is not STP");
            if (!Mathf.Approximately(asset.renderScale, DefaultRenderScale)) wrong.Add("render scale is not " + DefaultRenderScale);
            if (!Mathf.Approximately(asset.shadowDistance, ShadowDistanceM)) wrong.Add("shadow distance is not " + ShadowDistanceM);
            if (asset.shadowCascadeCount != ShadowCascades) wrong.Add("shadow cascades are not " + ShadowCascades);
            if (asset.msaaSampleCount != 1) wrong.Add("MSAA is on");
            foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
                if (feature != null && feature.name.Contains("AmbientOcclusion") && feature.isActive) wrong.Add("SSAO is on before it was measured");
            if (QualitySettings.vSyncCount != 0) wrong.Add("vsync is on by default");

            SerializedObject tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tags.FindProperty("layers");
            for (int i = 0; i < Layers.Names.Length; i++)
                if (layers.GetArrayElementAtIndex(Layers.Indices[i]).stringValue != Layers.Names[i]) wrong.Add("layer " + Layers.Indices[i] + " is not " + Layers.Names[i]);

            bool bootInBuild = false;
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes) if (s.enabled && s.path == BootScenePath) bootInBuild = true;
            if (!bootInBuild) wrong.Add("the Boot scene is not in the build settings");
            return wrong;
        }
    }
}
