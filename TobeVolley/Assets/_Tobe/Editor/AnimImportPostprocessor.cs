// Import settings for animation models (Mixamo FBX etc.) dropped into Assets/_Tobe/Resources/Anim/:
//   Humanoid rig built from the file itself, materials off, root rotation / height / XZ baked into the pose (the game owns the
//   root position), loop on for the locomotion clips (idle, ready, run, sprint, walk, shuffle_*, backpedal), clip named after the file.
// File names: idle, ready, shuffle_left, shuffle_right, backpedal, run, sprint, jump_vertical, jump_approach, land, bump, set, spike,
// block, serve_float, serve_jump, dive, celebrate, sad  (also "Model@name.fbx"). Runtime: PlayerAnimator / MotionLoader (Resources.LoadAll<AnimationClip>("Anim")).
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Tobe.EditorTools
{
    public sealed class AnimImportPostprocessor : AssetPostprocessor
    {
        const string Folder = "Assets/_Tobe/Resources/Anim/";
        const string Marker = "tobe-anim-v1";
        static readonly string[] LoopNames = { "idle", "ready", "run", "sprint", "walk", "shuffle_left", "shuffle_right", "shuffle", "backpedal" };

        static bool Applies(string path) => path.StartsWith(Folder, StringComparison.OrdinalIgnoreCase);

        /// <summary>"Assets/.../Model@Ready.fbx" -> "ready".</summary>
        static string KeyOf(string path)
        {
            string n = Path.GetFileNameWithoutExtension(path);
            int at = n.LastIndexOf('@');
            if (at >= 0) n = n.Substring(at + 1);
            return n.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        }

        static bool IsLoop(string key)
        {
            foreach (var l in LoopNames) if (key == l || key.StartsWith(l + "_", StringComparison.Ordinal) && l == "shuffle") return true;
            return false;
        }

        void OnPreprocessModel()
        {
            if (!Applies(assetPath)) return;
            var mi = assetImporter as ModelImporter;
            if (mi == null) return;
            ApplyRig(mi);
            if (mi.defaultClipAnimations != null && mi.defaultClipAnimations.Length > 0) ApplyClips(mi);
        }

        // On the very first import the clip list only exists after the file was read: finish the setup and re-import once.
        void OnPostprocessModel(GameObject g)
        {
            if (!Applies(assetPath)) return;
            var mi = assetImporter as ModelImporter;
            if (mi == null || mi.userData == Marker) return;
            if (mi.defaultClipAnimations == null || mi.defaultClipAnimations.Length == 0) return;
            ApplyClips(mi);
            string path = assetPath;
            EditorApplication.delayCall += () => { if (File.Exists(path)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); };
        }

        static void ApplyRig(ModelImporter mi)
        {
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.importAnimation = true;
        }

        /// <summary>Клипы Quaternius Universal Animation Library (CC0) -> имена анимаций игры. Остальные клипы набора игра не трогает.</summary>
        static readonly System.Collections.Generic.Dictionary<string, string> Rename = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Idle_Loop", "idle" }, { "Walk_Loop", "walk" }, { "Jog_Fwd_Loop", "run" }, { "Sprint_Loop", "sprint" }, { "Jump_Land", "land" },
        };

        void ApplyClips(ModelImporter mi)
        {
            var src = mi.defaultClipAnimations;
            if (src == null || src.Length == 0) return;
            string key = KeyOf(assetPath);
            bool loop = IsLoop(key);
            var clips = new ModelImporterClipAnimation[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                var c = src[i];
                if (src.Length == 1) c.name = key;                    // single take (Mixamo): the clip takes the file name
                else if (Rename.TryGetValue(c.name.Substring(c.name.LastIndexOf('|') + 1), out var rn)) c.name = rn;   // набор с многими клипами (Quaternius, «Armature|Idle_Loop»): имена -> наши
                string ck = src.Length == 1 ? key : c.name.ToLowerInvariant();
                bool l = src.Length == 1 ? loop : IsLoop(ck);
                c.loopTime = l;
                c.loopPose = l;
                // bake root rotation, height and XZ into the pose: the simulation owns the root position
                c.lockRootRotation = true; c.keepOriginalOrientation = true;
                c.lockRootHeightY = true; c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true;
                clips[i] = c;
            }
            mi.clipAnimations = clips;
            mi.userData = Marker;
        }
    }
}
