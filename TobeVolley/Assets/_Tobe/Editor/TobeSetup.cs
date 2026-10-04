// One-time project setup, run automatically when the project is opened (or via menu Tobe/Setup Project):
// URP pipeline asset + renderer (with the VRM MToon outline renderer feature, Forward+, soft shadows), main scene in Build Settings,
// always-included shaders, texture import fixes, player settings.
using System.Collections.Generic;
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Tobe.EditorTools
{
    [InitializeOnLoad]
    public static class TobeSetup
    {
        const string Dir = "Assets/_Tobe/Settings";
        const string RendererPath = Dir + "/TobeURP_Renderer.asset";
        const string PipelinePath = Dir + "/TobeURP.asset";
        const string ScenePath = "Assets/_Tobe/Scenes/Main.unity";

        static TobeSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("tobe.setup.checked", false)) return;
                SessionState.SetBool("tobe.setup.checked", true);
                if (!File.Exists(PipelinePath) || !File.Exists(ScenePath) || GraphicsSettings.defaultRenderPipeline == null) Run();
                else EnsureLook();     // cheap + idempotent: outline renderer feature, pipeline quality, normal-map import settings
            };
        }

        [MenuItem("Tobe/Setup Project")]
        public static void Run()
        {
            Directory.CreateDirectory(Dir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            // --- URP ---
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.supportsHDR = true;
                pipeline.msaaSampleCount = 4;
                pipeline.shadowDistance = 45f;
                pipeline.renderScale = 1f;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
                AssetDatabase.SaveAssets();
            }
            EnsureLook();
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;

            // --- shaders created from code at runtime must be included in builds ---
            AddAlwaysIncluded("Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit",
                "Universal Render Pipeline/Particles/Unlit", "Universal Render Pipeline/Particles/Lit",
                "Universal Render Pipeline/Simple Lit", "Hidden/Universal Render Pipeline/FallbackError", "Sprites/Default", "UI/Default", "GUI/Text Shader", "VRM10/Universal Render Pipeline/MToon10");

            // --- scene ---
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Bootstrap (game is built from code: Tobe.Core.GameBootstrap)");
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            // --- player ---
            PlayerSettings.companyName = "Tobe";
            PlayerSettings.productName = "TOBE VOLLEY";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920; PlayerSettings.defaultScreenHeight = 1080;
            SetActiveInputHandler();

            AssetDatabase.SaveAssets();
            Debug.Log("[Tobe] Project setup complete. Open Assets/_Tobe/Scenes/Main.unity and press Play.");
            if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorSceneManager.OpenScene(ScenePath);
        }

        // ------------------------------------------------------------------ look (cel shading, outlines, lighting quality)
        /// <summary>Makes sure the URP renderer used by the game has the MToon outline feature (character outlines) and Forward+ lighting,
        /// the pipeline asset has good shadow settings, and floor normal maps are imported as normal maps.</summary>
        [MenuItem("Tobe/Setup Look (outlines, lighting)")]
        public static void EnsureLook()
        {
            try
            {
                var pipelines = new List<UniversalRenderPipelineAsset>();
                void AddP(RenderPipelineAsset a) { if (a is UniversalRenderPipelineAsset u && !pipelines.Contains(u)) pipelines.Add(u); }
                AddP(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath));
                AddP(GraphicsSettings.defaultRenderPipeline);
                AddP(QualitySettings.renderPipeline);
                bool dirty = false;
                foreach (var pipeline in pipelines)
                {
                    dirty |= SetPipelineQuality(pipeline);
                    foreach (var rd in RendererDatas(pipeline))
                    {
                        dirty |= EnsureOutlineFeature(rd);
                        dirty |= SetInt(rd, "m_RenderingMode", 2);          // Forward+ : many lights (spots, ball glow, rims) without per-object limits
                    }
                }
                dirty |= FixNormalMapImport("floor_wood_normal");
                if (dirty) { AssetDatabase.SaveAssets(); Debug.Log("[Tobe] Look setup updated (outline renderer feature / Forward+ / shadows)."); }
            }
            catch (Exception e) { Debug.LogWarning("[Tobe] Look setup failed: " + e.Message); }
        }

        static System.Collections.Generic.IEnumerable<ScriptableObject> RendererDatas(UniversalRenderPipelineAsset pipeline)
        {
            var so = new SerializedObject(pipeline);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null || !list.isArray) yield break;
            for (int i = 0; i < list.arraySize; i++)
            {
                var rd = list.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableObject;
                if (rd != null) yield return rd;
            }
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        static bool EnsureOutlineFeature(ScriptableObject rendererData)
        {
            var rd = rendererData as ScriptableRendererData;
            if (rd == null) return false;
            var featType = FindType("VRM10.MToon10.MToonOutlineRenderFeature");
            if (featType == null) { Debug.LogWarning("[Tobe] MToonOutlineRenderFeature type not found (UniVRM MToon10 package missing?); character outlines disabled."); return false; }
            foreach (var f in rd.rendererFeatures) if (f != null && f.GetType() == featType) return false;

            var feat = ScriptableObject.CreateInstance(featType) as ScriptableRendererFeature;
            if (feat == null) return false;
            feat.name = "MToonOutline";
            AssetDatabase.AddObjectToAsset(feat, rd);
            var so = new SerializedObject(rd);
            var list = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            if (list == null || map == null) { Debug.LogWarning("[Tobe] Could not register the MToon outline renderer feature (unexpected URP asset layout). Add 'MToonOutlineRenderFeature' manually to the renderer."); return false; }
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feat;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feat, out string guid, out long localId);
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rd);
            Debug.Log("[Tobe] Added MToonOutlineRenderFeature to the URP renderer (character outlines).");
            return true;
        }

        static bool SetInt(Object obj, string prop, int value)
        {
            var so = new SerializedObject(obj);
            var p = so.FindProperty(prop);
            if (p == null || p.propertyType != SerializedPropertyType.Enum && p.propertyType != SerializedPropertyType.Integer || p.intValue == value) return false;
            p.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(obj);
            return true;
        }

        static bool SetPipelineQuality(UniversalRenderPipelineAsset pipeline)
        {
            bool d = false;
            var so = new SerializedObject(pipeline);
            void Int(string n, int v) { var p = so.FindProperty(n); if (p != null && (p.propertyType == SerializedPropertyType.Integer || p.propertyType == SerializedPropertyType.Enum) && p.intValue != v) { p.intValue = v; d = true; } }
            void Bool(string n, bool v) { var p = so.FindProperty(n); if (p != null && p.propertyType == SerializedPropertyType.Boolean && p.boolValue != v) { p.boolValue = v; d = true; } }
            void Float(string n, float v) { var p = so.FindProperty(n); if (p != null && p.propertyType == SerializedPropertyType.Float && Mathf.Abs(p.floatValue - v) > 0.01f) { p.floatValue = v; d = true; } }
            Int("m_MainLightShadowmapResolution", 2048);
            Int("m_ShadowCascadeCount", 3);
            Bool("m_SoftShadowsSupported", true);
            Int("m_SoftShadowQuality", 2);
            Float("m_ShadowDistance", 40f);
            Bool("m_SupportsHDR", true);
            Int("m_MSAA", 4);
            if (d) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(pipeline); }
            return d;
        }

        static bool FixNormalMapImport(string nameContains)
        {
            bool changed = false;
            foreach (var guid in AssetDatabase.FindAssets(nameContains + " t:Texture2D"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null || ti.textureType == TextureImporterType.NormalMap) continue;
                ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
                changed = true;
            }
            return changed;
        }

        static void AddAlwaysIncluded(params string[] shaderNames)
        {
            var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (gs == null) return;
            var so = new SerializedObject(gs);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;
            var have = new HashSet<Object>();
            for (int i = 0; i < arr.arraySize; i++) have.Add(arr.GetArrayElementAtIndex(i).objectReferenceValue);
            foreach (var n in shaderNames)
            {
                var sh = Shader.Find(n);
                if (sh == null || have.Contains(sh)) continue;
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = sh;
                have.Add(sh);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Input System only (the game reads Keyboard.current / Mouse.current).
        static void SetActiveInputHandler()
        {
            var ps = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/ProjectSettings.asset");
            if (ps == null) return;
            var so = new SerializedObject(ps);
            var prop = so.FindProperty("activeInputHandler");
            if (prop != null && prop.intValue == 0)
            {
                prop.intValue = 2; // Both: keeps legacy APIs working for any third-party code
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Tobe] Active Input Handling set to 'Both'. Unity may ask to restart the editor.");
            }
        }
    }
}
