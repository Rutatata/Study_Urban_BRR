// Finds and preloads character models (VRM from StreamingAssets/Characters, or a primitive-built fallback).
// Every model file is loaded ONCE as a hidden template (during the main menu); players get cheap clones of it.
//
// Why cloning a loaded Vrm10Instance is safe here (checked against UniVRM 0.131 sources):
//  * the clone's RuntimeGltfInstance is removed (its resources stay owned by the template) so Vrm10Instance takes its "scene prefab instance" path:
//    initial pose is read from the transforms and a standalone FastSpringBone runtime is created in Start (the runtime fields are not serialized, so
//    nothing is shared with the template). The template itself was loaded with a no-op spring bone runtime and stays inactive.
//  * meshes / textures / Avatar / VRM10Object stay shared with the template (never destroyed while the game runs); materials are duplicated per clone
//    (ModelMaterialOwner destroys them), so recoloring never leaks between players.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UniVRM10;

namespace Tobe.View
{
    public static class CharacterLibrary
    {
        sealed class Entry { public Task<GameObject> task; public bool done, failed; }

        static string[] paths = new string[0];
        static bool scanned, preloadStarted;
        static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        static Transform templateRoot;

        public static string Dir => Path.Combine(Application.streamingAssetsPath, "Characters");

        public static void Scan()
        {
            scanned = true;
            try
            {
                if (Directory.Exists(Dir))
                {
                    paths = Directory.GetFiles(Dir, "*.vrm", SearchOption.TopDirectoryOnly);
                    Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
                }
                else paths = new string[0];
            }
            catch (Exception e) { Debug.LogWarning("[Tobe] Character scan failed: " + e.Message); paths = new string[0]; }
            var names = new string[paths.Length];
            for (int i = 0; i < paths.Length; i++) names[i] = Path.GetFileNameWithoutExtension(paths[i]);
            GameHub.ModelNames = names;
        }

        static string PathOf(int index)
        {
            if (!scanned || paths.Length == 0) Scan();
            if (paths.Length == 0) return null;
            return paths[((index % paths.Length) + paths.Length) % paths.Length];
        }

        /// <summary>True when model #index can be cloned instantly (template loaded, or there is nothing to load / it failed -> fallback).</summary>
        public static bool IsReady(int index)
        {
            string path = PathOf(index);
            if (path == null) return true;
            return entries.TryGetValue(path, out var e) && e.done;
        }

        /// <summary>True when every model file has finished loading (or failed).</summary>
        public static bool AllReady
        {
            get
            {
                if (!scanned) return false;
                foreach (var p in paths) if (!entries.TryGetValue(p, out var e) || !e.done) return false;
                return true;
            }
        }

        /// <summary>Starts loading every model file one after another (call at startup, the main menu hides the work).</summary>
        public static void Preload()
        {
            if (preloadStarted) return;
            preloadStarted = true;
            if (!scanned) Scan();
            PreloadAll();
        }

        static async void PreloadAll()
        {
            foreach (var p in (string[])paths.Clone())
            {
                try { await GetTemplate(p); }
                catch (Exception e) { Debug.LogWarning("[Tobe] preload failed (" + p + "): " + e.Message); }
            }
        }

        static Task<GameObject> GetTemplate(string path)
        {
            if (entries.TryGetValue(path, out var e)) return e.task;
            e = new Entry();
            entries[path] = e;
            e.task = LoadTemplate(path, e);
            return e.task;
        }

        static async Task<GameObject> LoadTemplate(string path, Entry entry)
        {
            GameObject result = null;
            try
            {
                var inst = await Vrm10.LoadPathAsync(path, canLoadVrm0X: true, controlRigGenerationOption: ControlRigGenerationOption.None, showMeshes: true,
                    materialGenerator: new UrpVrm10MaterialDescriptorGenerator(), springboneRuntime: new Vrm10NopSpringboneRuntime());
                if (inst != null)
                {
                    var rgi = inst.GetComponent<UniGLTF.RuntimeGltfInstance>();
                    if (rgi != null) rgi.EnableUpdateWhenOffscreen();
                    var go = inst.gameObject;
                    go.name = "Template_" + Path.GetFileNameWithoutExtension(path);
                    if (templateRoot == null)
                    {
                        var holder = new GameObject("CharacterTemplates");
                        UnityEngine.Object.DontDestroyOnLoad(holder);
                        holder.SetActive(false);
                        templateRoot = holder.transform;
                    }
                    go.SetActive(false);
                    go.transform.SetParent(templateRoot, false);
                    CharacterAppearance.StyleMaterials(go);      // outline / rim / ramp, once on the shared materials
                    result = go;
                }
            }
            catch (Exception ex) { Debug.LogWarning("[Tobe] VRM load failed (" + path + "): " + ex.Message); }
            entry.failed = result == null;
            entry.done = true;
            return result;
        }

        static GameObject Clone(GameObject template)
        {
            var clone = UnityEngine.Object.Instantiate(template);      // template is inactive -> clone is inactive until fixed up
            clone.name = template.name.Replace("Template_", "Model_");
            clone.transform.SetParent(null, false);

            // resources stay owned by the template: drop the clone's RuntimeGltfInstance so Vrm10Instance uses its prefab-instance path
            var rgi = clone.GetComponent<UniGLTF.RuntimeGltfInstance>();
            if (rgi != null) UnityEngine.Object.DestroyImmediate(rgi);

            // per-clone material instances (shared between renderers of the same clone like the original)
            var owner = clone.AddComponent<ModelMaterialOwner>();
            var map = new Dictionary<Material, Material>();
            foreach (var r in clone.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    if (src == null) continue;
                    if (!map.TryGetValue(src, out var inst))
                    {
                        inst = new Material(src) { name = src.name };
                        map[src] = inst;
                        owner.Add(inst);
                    }
                    mats[i] = inst;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
            clone.SetActive(true);
            return clone;
        }

        /// <summary>Model #index (a clone of the preloaded VRM template, or the fallback humanoid). Never returns null. The caller owns (and destroys) the result.</summary>
        public static async Task<GameObject> LoadModel(int index)
        {
            string path = PathOf(index);
            if (path != null)
            {
                try
                {
                    var template = await GetTemplate(path);
                    if (template != null) return Clone(template);
                }
                catch (Exception e) { Debug.LogWarning("[Tobe] model clone failed (" + path + "): " + e.Message); }
            }
            return BuildFallback();
        }

        // ------------------------------------------------------------------ recolor (moved to CharacterAppearance)
        /// <summary>Compatibility wrapper: team uniform + hair color only. Prefer CharacterAppearance.Apply.</summary>
        public static void Recolor(GameObject model, int team, Color hair) => CharacterAppearance.Recolor(model, team, hair);

        // ------------------------------------------------------------------ fallback humanoid
        static Transform Bone(Transform parent, string name, Vector3 worldPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            return go.transform;
        }

        static Material M(string name, Color c)
        {
            var m = Mats.Toon(null, c, new Color(c.r * 0.66f, c.g * 0.62f, Mathf.Min(1f, c.b * 0.8f)), false);
            m.name = name;
            Mats.SetColor(m, c);
            return m;
        }

        static void Limb(Transform bone, Vector3 a, Vector3 b, float r, Material m)
        {
            Vector3 d = b - a;
            var go = Mats.Prim(PrimitiveType.Capsule, bone, "Limb", Vector3.zero, Vector3.one, m, true);
            go.transform.position = (a + b) * .5f;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            go.transform.localScale = new Vector3(r * 2f, d.magnitude * .5f, r * 2f);
        }

        static void Blob(Transform bone, Vector3 pos, Vector3 size, Material m, PrimitiveType t = PrimitiveType.Sphere)
        {
            var go = Mats.Prim(t, bone, "Part", Vector3.zero, size, m, true);
            go.transform.position = pos;
        }

        public static GameObject BuildFallback()
        {
            var root = new GameObject("FallbackHumanoid");
            var skin = M("Skin", new Color(0.98f, 0.8f, 0.68f));
            var shirt = M("Tops_CLOTH", TeamLook.Shirt[0]);
            var shorts = M("Bottoms_CLOTH", TeamLook.Shorts[0]);
            var hair = M("Hair_HAIR", TeamLook.HairPresets[0]);
            var shoe = M("Shoe", new Color(0.9f, 0.9f, 0.92f));
            var eye = M("Eye", new Color(0.03f, 0.03f, 0.05f));

            var hips = Bone(root.transform, "Hips", new Vector3(0, 0.78f, 0));
            var spine = Bone(hips, "Spine", new Vector3(0, 0.86f, 0));
            var chest = Bone(spine, "Chest", new Vector3(0, 1.02f, 0));
            var neck = Bone(chest, "Neck", new Vector3(0, 1.22f, 0));
            var head = Bone(neck, "Head", new Vector3(0, 1.30f, 0));

            Limb(hips, new Vector3(0, 0.70f, 0), new Vector3(0, 0.84f, 0), 0.165f, shorts);
            Limb(chest, new Vector3(0, 0.90f, 0), new Vector3(0, 1.20f, 0), 0.165f, shirt);
            Limb(neck, new Vector3(0, 1.2f, 0), new Vector3(0, 1.32f, 0), 0.05f, skin);
            Blob(head, new Vector3(0, 1.48f, 0), new Vector3(0.40f, 0.40f, 0.40f), skin);
            Blob(head, new Vector3(0, 1.58f, -0.02f), new Vector3(0.43f, 0.32f, 0.43f), hair);
            Blob(head, new Vector3(0, 1.48f, -0.09f), new Vector3(0.41f, 0.38f, 0.38f), hair);
            Blob(head, new Vector3(0.075f, 1.47f, 0.18f), new Vector3(0.07f, 0.08f, 0.04f), eye);
            Blob(head, new Vector3(-0.075f, 1.47f, 0.18f), new Vector3(0.07f, 0.08f, 0.04f), eye);

            foreach (int s in new[] { 1, -1 })
            {
                string side = s > 0 ? "Right" : "Left";
                var ua = Bone(chest, side + "UpperArm", new Vector3(0.17f * s, 1.17f, 0));
                var la = Bone(ua, side + "LowerArm", new Vector3(0.43f * s, 1.17f, 0));
                var hand = Bone(la, side + "Hand", new Vector3(0.66f * s, 1.17f, 0));
                Limb(ua, ua.position, la.position, 0.058f, shirt);
                Limb(la, la.position, hand.position, 0.047f, skin);
                Blob(hand, hand.position + new Vector3(0.05f * s, 0, 0), new Vector3(0.12f, 0.1f, 0.1f), skin);

                var ul = Bone(hips, side + "UpperLeg", new Vector3(0.09f * s, 0.76f, 0));
                var ll = Bone(ul, side + "LowerLeg", new Vector3(0.09f * s, 0.40f, 0));
                var foot = Bone(ll, side + "Foot", new Vector3(0.09f * s, 0.07f, 0));
                Bone(foot, side + "Toes", new Vector3(0.09f * s, 0.03f, 0.13f));
                Limb(ul, ul.position, ll.position, 0.08f, shorts);
                Limb(ll, ll.position, foot.position, 0.062f, skin);
                Blob(foot, new Vector3(0.09f * s, 0.04f, 0.05f), new Vector3(0.12f, 0.08f, 0.26f), shoe, PrimitiveType.Cube);
            }
            CharacterAppearance.StyleMaterials(root);
            return root;
        }
    }
}
