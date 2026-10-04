// Finds and loads character models (VRM from StreamingAssets/Characters, or a primitive-built fallback).
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UniVRM10;

namespace Tobe.View
{
    public static class CharacterLibrary
    {
        static string[] paths = new string[0];
        static bool scanned;

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

        /// <summary>Loads model #index (VRM) or builds the fallback humanoid. Never returns null.</summary>
        public static async Task<GameObject> LoadModel(int index)
        {
            if (!scanned || paths.Length == 0) Scan();
            if (paths.Length > 0)
            {
                string path = paths[((index % paths.Length) + paths.Length) % paths.Length];
                try
                {
                    var inst = await Vrm10.LoadPathAsync(path, canLoadVrm0X: true, controlRigGenerationOption: ControlRigGenerationOption.None, showMeshes: true,
                        materialGenerator: new UrpVrm10MaterialDescriptorGenerator());
                    if (inst != null)
                    {
                        var rgi = inst.GetComponent<UniGLTF.RuntimeGltfInstance>();
                        if (rgi != null) rgi.EnableUpdateWhenOffscreen();
                        CharacterAppearance.StyleMaterials(inst.gameObject);      // outline / rim / ramp
                        return inst.gameObject;
                    }
                }
                catch (Exception e) { Debug.LogWarning("[Tobe] VRM load failed (" + path + "): " + e.Message); }
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
