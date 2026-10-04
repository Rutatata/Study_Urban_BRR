// Individual character look: hair / skin / eye color, team uniform (libero in contrasting colors), jersey numbers, gear meshes on bones,
// height + build, and the cel-shading (MToon10 outline / rim / hair highlight) of every loaded model.
// Works on VRoid-style VRMs (materials *_Hair_*, *_Body_*_SKIN, *_Tops_*, *_Bottoms_*, *_Shoes_*, *_FaceSkin_*, *_EyeIris_*) and on the fallback primitive humanoid.
using System.Collections.Generic;
using UnityEngine;
using VRM10.MToon10;

namespace Tobe.View
{
    public static class CharacterAppearance
    {
        // ------------------------------------------------------------------ public API
        /// <summary>Applies the full profile to a loaded model. Safe to call repeatedly (gear / scale are rebuilt, never stacked).
        /// Call it after the model has been parented and normalized to its final size.</summary>
        public static void Apply(GameObject modelRoot, Animator animator, PlayerProfile profile, int team)
        {
            if (modelRoot == null) return;
            team = Mathf.Clamp(team, 0, 1);
            var state = modelRoot.GetComponent<AppearanceState>();
            if (state == null) state = modelRoot.AddComponent<AppearanceState>();
            if (animator == null) animator = modelRoot.GetComponentInChildren<Animator>();

            RestoreBones(state);
            ClearGear(modelRoot.transform);
            if (!state.hasBase) { state.baseScale = modelRoot.transform.localScale; state.hasBase = true; }
            modelRoot.transform.localScale = state.baseScale * profile.HeightScale;

            bool libero = profile.style == PlayStyle.Libero;
            Color shirt = libero ? TeamLook.Trim[team] : TeamLook.Shirt[team];
            Color trim = libero ? TeamLook.Shirt[team] : TeamLook.Trim[team];
            Color shorts = TeamLook.Shorts[team];
            Color hair = Pick(TeamLook.HairPresets, profile.hair);
            Color skin = Pick(TeamLook.SkinTones, profile.skin);
            Color eye = Pick(TeamLook.EyeColors, profile.eyes);

            PaintAll(modelRoot, shirt, shorts, hair, skin, eye);
            StyleMaterials(modelRoot);

            var rig = new Rig(modelRoot, animator);
            BuildGear(rig, state, profile, team, shirt, trim);
            BuildNumber(rig, profile, shirt, trim);
            ApplyBuild(rig, state, profile);
        }

        /// <summary>Old quick path (team uniform + hair only). Kept so existing callers keep working.</summary>
        public static void Recolor(GameObject model, int team, Color hair)
        {
            if (model == null) return;
            team = Mathf.Clamp(team, 0, 1);
            PaintAll(model, TeamLook.Shirt[team], TeamLook.Shorts[team], hair, null, null);
            StyleMaterials(model);
        }

        // ------------------------------------------------------------------ materials
        enum Part { None, Hair, Skin, Iris, EyeOther, Shirt, Shorts, Shoes }

        static Part Classify(string n)
        {
            n = n.ToLowerInvariant();
            if (n.Contains("mouth") || n.Contains("tongue") || n.Contains("teeth")) return Part.EyeOther;
            if (n.Contains("iris")) return Part.Iris;
            if (n.Contains("eye") || n.Contains("lash") || n.Contains("brow") || n.Contains("highlight") || n.Contains("pupil")) return Part.EyeOther;
            if (n.Contains("shoe") || n.Contains("sock") || n.Contains("boot")) return Part.Shoes;
            if (n.Contains("hair")) return Part.Hair;
            if (n.Contains("skin") || n.Contains("face") || n.Contains("body_00") || n.Equals("skin")) return Part.Skin;
            if (n.Contains("bottom") || n.Contains("skirt") || n.Contains("pants") || n.Contains("shorts")) return Part.Shorts;
            if (n.Contains("tops") || n.Contains("cloth") || n.Contains("shirt") || n.Contains("onepiece") || n.Contains("jacket") || n.Contains("body")) return Part.Shirt;
            return Part.None;
        }

        static bool InGear(Transform t)
        {
            for (; t != null; t = t.parent) if (t.name.StartsWith("TG_")) return true;
            return false;
        }

        static Color Pick(Color[] arr, int i) => arr[Mathf.Clamp(i, 0, arr.Length - 1)];

        static readonly Color RefSkin = TeamLook.SkinTones[1];

        static void SetBase(Material m, Color c, Color shade, bool flat)
        {
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_ShadeColor")) m.SetColor("_ShadeColor", shade);
            if (flat)
            {
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", Texture2D.whiteTexture);
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", Texture2D.whiteTexture);
                if (m.HasProperty("_ShadeTex")) m.SetTexture("_ShadeTex", Texture2D.whiteTexture);
            }
        }

        static Color ShadeOf(Color c) => new Color(c.r * 0.66f, c.g * 0.62f, Mathf.Min(1f, c.b * 0.78f), 1f);

        static void PaintAll(GameObject model, Color shirt, Color shorts, Color hair, Color? skin, Color? eye)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || InGear(r.transform)) continue;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    bool toon = Mats.IsToon(m);
                    switch (Classify(m.name))
                    {
                        case Part.Hair: SetBase(m, hair, ShadeOf(hair), true); break;
                        case Part.Shirt: SetBase(m, shirt, ShadeOf(shirt), true); break;
                        case Part.Shorts: SetBase(m, shorts, ShadeOf(shorts), true); break;
                        case Part.Skin:
                            if (!skin.HasValue) break;
                            if (toon)
                            {
                                // keep the painted face / blush: tint the textures relative to the reference tone
                                var s = skin.Value;
                                var t = new Color(Mathf.Clamp(s.r / RefSkin.r, 0.3f, 1.25f), Mathf.Clamp(s.g / RefSkin.g, 0.3f, 1.25f), Mathf.Clamp(s.b / RefSkin.b, 0.3f, 1.25f), 1f);
                                SetBase(m, t, t, false);
                            }
                            else SetBase(m, skin.Value, ShadeOf(skin.Value), true);
                            break;
                        case Part.Iris:
                            if (!eye.HasValue) break;
                            var e = eye.Value;
                            var te = Color.Lerp(Color.white, new Color(Mathf.Min(1f, e.r * 1.7f), Mathf.Min(1f, e.g * 1.7f), Mathf.Min(1f, e.b * 1.7f), 1f), 0.9f);
                            if (toon) SetBase(m, te, te, false); else SetBase(m, e, e, true);
                            break;
                    }
                }
            }
        }

        /// <summary>Cel-shading pass for loaded MToon materials: world-space outline (needs MToonOutlineRenderFeature on the URP renderer, added by TobeSetup),
        /// rim light, crisp toon ramp and an anime highlight ring on the hair. Idempotent.</summary>
        public static void StyleMaterials(GameObject model)
        {
            if (model == null) return;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || InGear(r.transform)) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null || !Mats.IsToon(m)) continue;
                    var part = Classify(m.name);
                    var c = new MToon10Context(m);
                    Color baseC = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
                    float lum = baseC.r * 0.3f + baseC.g * 0.59f + baseC.b * 0.11f;
                    c.ShadingToonyFactor = 0.92f;
                    c.GiEqualizationFactor = 0.8f;
                    c.ParametricRimColorFactorSrgb = part == Part.EyeOther || part == Part.Iris ? Color.black : new Color(0.26f, 0.32f, 0.5f);
                    c.ParametricRimFresnelPowerFactor = 4.5f;
                    c.ParametricRimLiftFactor = 0.1f;
                    c.RimLightingMixFactor = 0.75f;
                    bool opaque = c.AlphaMode == MToon10AlphaMode.Opaque || part == Part.Hair;
                    bool outline = opaque && part != Part.EyeOther && part != Part.Iris;
                    if (outline)
                    {
                        c.OutlineWidthMode = MToon10OutlineMode.World;
                        c.OutlineWidthFactor = part == Part.Skin ? 0.0028f : 0.0038f;
                        // colored ink line: darkened version of the surface color, never pure black
                        var oc = part == Part.Skin ? new Color(0.36f, 0.18f, 0.16f) : new Color(baseC.r * 0.3f, baseC.g * 0.28f, baseC.b * 0.36f);
                        if (part == Part.Shirt && lum > 0.6f) oc = new Color(0.32f, 0.38f, 0.5f);
                        c.OutlineColorFactorSrgb = oc;
                        c.OutlineLightingMixFactor = 0.45f;
                    }
                    else c.OutlineWidthMode = MToon10OutlineMode.None;
                    if (part == Part.Hair)
                    {
                        c.MatcapTexture = ProcTex.HairMatcap();
                        c.MatcapColorFactorSrgb = new Color(0.55f, 0.55f, 0.6f);
                    }
                    c.Validate();
                }
            }
        }

        // ------------------------------------------------------------------ skeleton access
        sealed class Rig
        {
            public readonly GameObject root; public readonly Animator anim;
            public readonly Dictionary<string, Transform> byName = new Dictionary<string, Transform>();
            public readonly bool fallback;
            public float S = 1f;                    // body scale relative to a 1.7 m person
            public Vector3 fwd = Vector3.forward;
            public Transform hips, spine, chest, neck, head, lEye, rEye;
            public Transform uArmL, uArmR, lArmL, lArmR, handL, handR, uLegL, uLegR, lLegL, lLegR, footL, footR;
            public Bounds face; public bool hasFace;

            public Rig(GameObject root, Animator anim)
            {
                this.root = root; this.anim = anim;
                fallback = root.name.Contains("Fallback");
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant();
                    int colon = n.LastIndexOf(':'); if (colon >= 0) n = n.Substring(colon + 1);
                    if (!byName.ContainsKey(n)) byName[n] = t;
                }
                hips = B(HumanBodyBones.Hips, "hips");
                spine = B(HumanBodyBones.Spine, "spine");
                chest = B(HumanBodyBones.UpperChest, "upperchest") ?? B(HumanBodyBones.Chest, "chest");
                neck = B(HumanBodyBones.Neck, "neck");
                head = B(HumanBodyBones.Head, "head");
                lEye = B(HumanBodyBones.LeftEye, "lefteye", "eye_l", "eyel"); rEye = B(HumanBodyBones.RightEye, "righteye", "eye_r", "eyer");
                uArmL = B(HumanBodyBones.LeftUpperArm, "leftupperarm", "upperarm_l", "upperarml"); uArmR = B(HumanBodyBones.RightUpperArm, "rightupperarm", "upperarm_r", "upperarmr");
                lArmL = B(HumanBodyBones.LeftLowerArm, "leftlowerarm", "lowerarm_l", "lowerarml"); lArmR = B(HumanBodyBones.RightLowerArm, "rightlowerarm", "lowerarm_r", "lowerarmr");
                handL = B(HumanBodyBones.LeftHand, "lefthand", "hand_l", "handl"); handR = B(HumanBodyBones.RightHand, "righthand", "hand_r", "handr");
                uLegL = B(HumanBodyBones.LeftUpperLeg, "leftupperleg", "upperleg_l", "upperlegl"); uLegR = B(HumanBodyBones.RightUpperLeg, "rightupperleg", "upperleg_r", "upperlegr");
                lLegL = B(HumanBodyBones.LeftLowerLeg, "leftlowerleg", "lowerleg_l", "lowerlegl"); lLegR = B(HumanBodyBones.RightLowerLeg, "rightlowerleg", "lowerleg_r", "lowerlegr");
                footL = B(HumanBodyBones.LeftFoot, "leftfoot", "foot_l", "footl"); footR = B(HumanBodyBones.RightFoot, "rightfoot", "foot_r", "footr");

                fwd = root.transform.forward; fwd.y = 0f;
                fwd = fwd.sqrMagnitude < 1e-4f ? Vector3.forward : fwd.normalized;
                if (head != null && footL != null)
                {
                    float h = head.position.y - Mathf.Min(footL.position.y, footR != null ? footR.position.y : footL.position.y);
                    if (h > 0.2f) S = h / (fallback ? 1.25f : 1.45f);
                }
                // face bounds from the face mesh (VRoid: "Face"), else everything hanging off the head bone
                Bounds fb = default; bool any = false;
                foreach (var rd in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (rd is ParticleSystemRenderer || InGear(rd.transform)) continue;
                    string n = rd.name.ToLowerInvariant();
                    bool isFace = n == "face" || n.StartsWith("face.") || n.Contains("face_") && !n.Contains("brow") && !n.Contains("eye");
                    if (!isFace && !(fallback && head != null && rd.transform.IsChildOf(head) && rd.name == "Part")) continue;
                    if (!any) { fb = rd.bounds; any = true; } else fb.Encapsulate(rd.bounds);
                }
                if (any && fb.size.y > 0.08f * S && fb.size.y < 0.6f * S) { face = fb; hasFace = true; }
            }

            Transform B(HumanBodyBones hb, params string[] names)
            {
                if (anim != null && anim.isHuman) { var t = anim.GetBoneTransform(hb); if (t != null) return t; }
                foreach (var n in names) if (byName.TryGetValue(n, out var t2)) return t2;
                return null;
            }

            public Vector3 FaceCenter => hasFace ? face.center : (head != null ? head.position + Vector3.up * 0.1f * S : root.transform.position + Vector3.up * 1.5f * S);
        }

        // ------------------------------------------------------------------ gear
        static void ClearGear(Transform root)
        {
            var kill = new List<GameObject>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name.StartsWith("TG_") && (t.parent == null || !t.parent.name.StartsWith("TG_"))) kill.Add(t.gameObject);
            foreach (var g in kill) { g.SetActive(false); Object.Destroy(g); }
        }

        static void Attach(Transform container, Transform bone)
        {
            if (container == null) return;
            if (bone != null) container.SetParent(bone, true);
        }

        static Material GearMat(string key, Color c) => Mats.Toon(key, c, new Color(c.r * 0.6f, c.g * 0.6f, Mathf.Min(1f, c.b * 0.72f), 1f), false, null, 0.1f);

        static void Ring(MeshBatch b, Material m, Vector3 center, Vector3 axis, Vector3 refDir, float rx, float rz, float height, float thick, int seg = 24)
        {
            axis.Normalize();
            Vector3 e1 = Vector3.ProjectOnPlane(refDir, axis);
            if (e1.sqrMagnitude < 1e-5f) e1 = Vector3.ProjectOnPlane(Vector3.right, axis);
            e1.Normalize();
            Vector3 e2 = Vector3.Cross(axis, e1);
            Vector3 hh = axis * height * .5f;
            Vector3 P(float a, float r) => center + e1 * (Mathf.Cos(a) * (rx + r)) + e2 * (Mathf.Sin(a) * (rz + r));
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2f, a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                b.Quad(m, P(a0, thick) - hh, P(a1, thick) - hh, P(a1, thick) + hh, P(a0, thick) + hh, Color.white, true);
                b.Quad(m, P(a0, 0) - hh, P(a1, 0) - hh, P(a1, 0) + hh, P(a0, 0) + hh, Color.white, true);
                b.Quad(m, P(a0, 0) + hh, P(a1, 0) + hh, P(a1, thick) + hh, P(a0, thick) + hh, Color.white, true);
                b.Quad(m, P(a0, 0) - hh, P(a1, 0) - hh, P(a1, thick) - hh, P(a0, thick) - hh, Color.white, true);
            }
        }

        static void BuildGear(Rig g, AppearanceState state, PlayerProfile p, int team, Color shirt, Color trim)
        {
            var gear = (Gear)p.gear;
            float S = g.S;
            float thick = g.fallback ? 1.7f : 1f;      // primitive limbs are fatter than VRoid ones
            Color dark = new Color(0.1f, 0.1f, 0.13f);
            Color white = new Color(0.96f, 0.96f, 0.94f);
            Color strong = TeamLook.Trim[team];

            if ((gear & Gear.KneePads) != 0)
            {
                var m = GearMat("gear_knee_" + team, new Color(0.09f, 0.09f, 0.12f));
                var stripe = GearMat("gear_knee_s_" + team, strong);
                foreach (var pair in new[] { (g.uLegL, g.lLegL, g.footL), (g.uLegR, g.lLegR, g.footR) })
                {
                    if (pair.Item2 == null || pair.Item1 == null || pair.Item3 == null) continue;
                    var b = new MeshBatch("TG_KneePad");
                    Vector3 knee = pair.Item2.position;
                    Vector3 dir = (pair.Item3.position - pair.Item1.position).normalized;
                    float r = 0.066f * S * thick;
                    b.Cyl(m, knee - dir * 0.075f * S, knee + dir * 0.08f * S, r, r, 14, null, true);
                    b.Cyl(stripe, knee + dir * 0.08f * S, knee + dir * 0.092f * S, r * 1.01f, r * 1.01f, 14, null, true);
                    b.Sphere(m, knee + g.fwd * 0.04f * S * thick, 0.05f * S * thick, 10, 8, null, new Vector3(1.1f, 1.2f, 0.7f));
                    Attach(b.Flush(null, false), pair.Item2);
                }
            }
            if ((gear & Gear.Wristbands) != 0)
            {
                var m = GearMat("gear_wrist", white);
                foreach (var pair in new[] { (g.lArmL, g.handL), (g.lArmR, g.handR) })
                {
                    if (pair.Item1 == null || pair.Item2 == null) continue;
                    var b = new MeshBatch("TG_Wristband");
                    Vector3 dir = (pair.Item2.position - pair.Item1.position).normalized;
                    Vector3 c = pair.Item2.position - dir * 0.045f * S;
                    b.Cyl(m, c - dir * 0.025f * S, c + dir * 0.025f * S, 0.034f * S * thick, 0.034f * S * thick, 12, null, true);
                    Attach(b.Flush(null, false), pair.Item1);
                }
            }
            if ((gear & Gear.ArmSleeve) != 0)
            {
                var m = GearMat("gear_sleeve_" + team, Color.Lerp(strong, white, team == 0 ? 0.0f : 0.1f) * (team == 0 ? 0.85f : 1f));
                foreach (var pair in new[] { (g.lArmR, g.handR) })      // right arm only, like a real sleeve
                {
                    if (pair.Item1 == null || pair.Item2 == null) continue;
                    var b = new MeshBatch("TG_ArmSleeve");
                    Vector3 a = pair.Item1.position, e = pair.Item2.position;
                    Vector3 dir = (e - a).normalized;
                    b.Cyl(m, a + dir * 0.015f * S, e - dir * 0.035f * S, 0.038f * S * thick, 0.03f * S * thick, 12, null, true);
                    Attach(b.Flush(null, false), pair.Item1);
                }
            }
            if ((gear & Gear.AnkleTape) != 0)
            {
                var m = GearMat("gear_ankle", white);
                foreach (var pair in new[] { (g.lLegL, g.footL), (g.lLegR, g.footR) })
                {
                    if (pair.Item1 == null || pair.Item2 == null) continue;
                    var b = new MeshBatch("TG_AnkleTape");
                    Vector3 dir = (pair.Item1.position - pair.Item2.position).normalized;       // up the leg
                    Vector3 c = pair.Item2.position + dir * 0.04f * S;
                    b.Cyl(m, c - dir * 0.03f * S, c + dir * 0.03f * S, 0.045f * S * thick, 0.045f * S * thick, 12, null, true);
                    Attach(b.Flush(null, false), pair.Item2);
                }
            }
            if ((gear & Gear.Headband) != 0 && g.head != null)
            {
                var m = GearMat("gear_head_" + team, strong);
                var b = new MeshBatch("TG_Headband");
                Vector3 c = g.FaceCenter;
                float rx = 0.1f * S, rz = 0.105f * S, h = 0.03f * S;
                if (g.hasFace)
                {
                    c = new Vector3(g.face.center.x, g.face.min.y + g.face.size.y * 0.8f, g.face.center.z);
                    rx = Mathf.Max(0.05f, g.face.extents.x * 1.04f); rz = Mathf.Max(0.05f, g.face.extents.z * 1.02f);
                    h = Mathf.Max(0.02f, g.face.size.y * 0.13f);
                }
                else c += Vector3.up * 0.045f * S;
                Ring(b, m, c, Vector3.up, g.fwd, rx, rz, h, 0.012f * S);
                Attach(b.Flush(null, false), g.head);
            }
            if ((gear & Gear.Glasses) != 0 && g.head != null)
            {
                var m = GearMat("gear_glasses", new Color(0.07f, 0.07f, 0.09f));
                var b = new MeshBatch("TG_Glasses");
                Vector3 eL, eR;
                if (g.lEye != null && g.rEye != null) { eL = g.lEye.position; eR = g.rEye.position; }
                else
                {
                    Vector3 fc = g.FaceCenter + Vector3.up * 0.01f * S;
                    Vector3 right = Vector3.Cross(Vector3.up, g.fwd);
                    eL = fc - right * 0.035f * S; eR = fc + right * 0.035f * S;
                    float front = g.hasFace ? g.face.extents.z : 0.08f * S;
                    eL += g.fwd * (front - 0.03f * S); eR += g.fwd * (front - 0.03f * S);
                }
                float off = (g.lEye != null ? 0.03f : 0.012f) * S;
                eL += g.fwd * off; eR += g.fwd * off;
                float rl = Mathf.Clamp(Vector3.Distance(eL, eR) * 0.42f, 0.02f * S, 0.045f * S);
                Vector3 side = (eR - eL).normalized;
                Ring(b, m, eL, g.fwd, Vector3.up, rl, rl, 0.006f * S, 0.006f * S, 18);
                Ring(b, m, eR, g.fwd, Vector3.up, rl, rl, 0.006f * S, 0.006f * S, 18);
                b.Beam(m, eL + side * (rl + 0.006f * S), eR - side * (rl + 0.006f * S), 0.006f * S);
                b.Beam(m, eL - side * (rl + 0.006f * S), eL - side * (rl + 0.03f * S) - g.fwd * 0.11f * S, 0.005f * S);
                b.Beam(m, eR + side * (rl + 0.006f * S), eR + side * (rl + 0.03f * S) - g.fwd * 0.11f * S, 0.005f * S);
                // faint lens tint
                var lens = Mats.Unlit("gear_lens", new Color(0.7f, 0.9f, 1f, 0.12f), null, true, false, true);
                b.Cyl(lens, eL, eL + g.fwd * 0.002f * S, rl, rl, 14, null, true);
                b.Cyl(lens, eR, eR + g.fwd * 0.002f * S, rl, rl, 14, null, true);
                Attach(b.Flush(null, false), g.head);
            }
            _ = dark; _ = state; _ = shirt; _ = trim;
        }

        // ------------------------------------------------------------------ jersey numbers
        static float Luma(Color c) => c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;

        static void BuildNumber(Rig g, PlayerProfile p, Color shirt, Color trim)
        {
            Transform bone = g.chest ?? g.spine ?? g.hips;
            if (bone == null || g.hips == null || g.neck == null) return;
            string txt = Mathf.Clamp(p.number, (byte)0, (byte)99).ToString();
            float S = g.S;
            Vector3 P = Vector3.Lerp(g.hips.position, g.neck.position, 0.64f);
            Color numCol = Luma(shirt) > 0.5f ? Color.Lerp(trim, new Color(0.05f, 0.1f, 0.2f), 0.35f) : (Luma(trim) > 0.35f ? trim : Color.white);
            Color shadow = Luma(numCol) > 0.5f ? new Color(0.05f, 0.05f, 0.08f, 0.9f) : new Color(1f, 1f, 1f, 0.85f);
            float back = g.fallback ? 0.2f : 0.115f, front = g.fallback ? 0.2f : 0.125f;

            MakeNumber(bone, txt, P - g.fwd * back * S, Quaternion.LookRotation(g.fwd, Vector3.up), 0.0345f * S, numCol, shadow, S);
            MakeNumber(bone, txt, P + g.fwd * front * S + Vector3.up * 0.06f * S, Quaternion.LookRotation(-g.fwd, Vector3.up), 0.0165f * S, numCol, shadow, S);
        }

        static void MakeNumber(Transform bone, string txt, Vector3 pos, Quaternion rot, float charSize, Color col, Color shadow, float S)
        {
            var holder = new GameObject("TG_Number").transform;
            holder.position = pos; holder.rotation = rot;
            var sh = Mats.Text(holder, txt, charSize, 64, shadow);
            sh.transform.localPosition = new Vector3(0.0035f * S, -0.0035f * S, 0.0035f * S);
            var tm = Mats.Text(holder, txt, charSize, 64, col);
            tm.transform.localPosition = Vector3.zero;
            holder.SetParent(bone, true);
        }

        // ------------------------------------------------------------------ build (body thickness)
        static void RestoreBones(AppearanceState st)
        {
            foreach (var kv in st.boneScales) if (kv.Key != null) kv.Key.localScale = kv.Value;
            st.boneScales.Clear();
        }

        static void ScaleAcross(AppearanceState st, Transform bone, Transform child, float k)
        {
            if (bone == null || Mathf.Abs(k - 1f) < 0.003f) return;
            Vector3 dirW = child != null ? (child.position - bone.position) : bone.up;
            if (dirW.sqrMagnitude < 1e-8f) return;
            Vector3 d = bone.InverseTransformDirection(dirW.normalized);
            int ax = Mathf.Abs(d.x) >= Mathf.Abs(d.y) && Mathf.Abs(d.x) >= Mathf.Abs(d.z) ? 0 : (Mathf.Abs(d.y) >= Mathf.Abs(d.z) ? 1 : 2);
            Vector3 s = new Vector3(k, k, k); s[ax] = 1f;
            if (!st.boneScales.ContainsKey(bone)) st.boneScales[bone] = bone.localScale;
            bone.localScale = Vector3.Scale(bone.localScale, s);
        }

        static void ApplyBuild(Rig g, AppearanceState st, PlayerProfile p)
        {
            float k = (p.build / 255f - 0.5f) * 2f;                       // -1 slim .. +1 athletic
            float limb = 1f + 0.12f * k, thigh = 1f + 0.10f * k;
            ScaleAcross(st, g.uArmL, g.lArmL, limb); ScaleAcross(st, g.uArmR, g.lArmR, limb);
            ScaleAcross(st, g.uLegL, g.lLegL, thigh); ScaleAcross(st, g.uLegR, g.lLegR, thigh);
            // shoulders: widen the chest bone along the body's left-right axis
            if (g.chest != null && Mathf.Abs(k) > 0.01f)
            {
                Vector3 right = Vector3.Cross(Vector3.up, g.fwd);
                Vector3 l = g.chest.InverseTransformDirection(right);
                int ax = Mathf.Abs(l.x) >= Mathf.Abs(l.y) && Mathf.Abs(l.x) >= Mathf.Abs(l.z) ? 0 : (Mathf.Abs(l.y) >= Mathf.Abs(l.z) ? 1 : 2);
                Vector3 s = Vector3.one; s[ax] = 1f + 0.08f * k;
                if (!st.boneScales.ContainsKey(g.chest)) st.boneScales[g.chest] = g.chest.localScale;
                g.chest.localScale = Vector3.Scale(g.chest.localScale, s);
            }
        }
    }
}
