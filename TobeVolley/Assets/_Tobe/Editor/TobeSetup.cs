// One-time project setup, run automatically when the project is opened (or via menu Tobe/Setup Project):
// URP pipeline asset + renderer, main scene in Build Settings, always-included shaders, player settings.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);

            // --- shaders created from code at runtime must be included in builds ---
            AddAlwaysIncluded("Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit",
                "Universal Render Pipeline/Particles/Unlit", "Universal Render Pipeline/Particles/Lit",
                "Universal Render Pipeline/Simple Lit", "Sprites/Default", "UI/Default", "VRM10/Universal Render Pipeline/MToon10");

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
