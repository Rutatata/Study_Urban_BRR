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
        public static int DevExtraGear;   // меню Tobe/Dev: надеть экипировку всем (для проверки посадки)

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
            Color shirt = TeamLook.Kit(profile.kitShirt, libero ? TeamLook.Trim[team] : TeamLook.Shirt[team]);   // своя форма — вкладка «ФОРМА»
            Color trim = libero ? TeamLook.Shirt[team] : TeamLook.Trim[team];
            Color shorts = TeamLook.Kit(profile.kitShorts, TeamLook.Shorts[team]);
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
            // «huku» по-японски «одежда» (Seed-san). Голое «body» сюда НЕ входит: у orion / seedsan это всё тело одной текстурой
            // (голова, кожа, одежда), и перекраска в цвет формы делала персонажа целиком чёрным силуэтом.
            if (n.Contains("tops") || n.Contains("cloth") || n.Contains("shirt") || n.Contains("onepiece") || n.Contains("jacket") || n.Contains("huku")) return Part.Shirt;
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
                var old = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                if (old != null && old.name.StartsWith("shirt_print_")) Object.Destroy(old);   // прошлый принт номера этого игрока
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
                    if ((part == Part.Shirt || part == Part.Shorts || part == Part.Hair) && lum < 0.25f)
                    {   // тёмная ткань / волосы: яркий контур по краю силуэта, как в аниме, иначе чёрная форма сливается с полом и трибунами
                        c.ParametricRimColorFactorSrgb = new Color(0.55f, 0.52f, 0.62f);
                        c.ParametricRimFresnelPowerFactor = 3.2f;
                        c.ParametricRimLiftFactor = 0.16f;
                    }
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
            public SkinnedMeshRenderer faceRd;   // сетка лица — по ней повязка повторяет форму головы

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
                    if (faceRd == null && isFace) faceRd = rd as SkinnedMeshRenderer;
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

        static void Ring(MeshBatch b, Material m, Vector3 center, Vector3 axis, Vector3 refDir, float rx, float rz, float height, float thick, int seg = 24, float[] rad = null)
        {
            axis.Normalize();
            Vector3 e1 = Vector3.ProjectOnPlane(refDir, axis);
            if (e1.sqrMagnitude < 1e-5f) e1 = Vector3.ProjectOnPlane(Vector3.right, axis);
            e1.Normalize();
            Vector3 e2 = Vector3.Cross(axis, e1);
            Vector3 hh = axis * height * .5f;
            Vector3 P(float a, float r)
            {
                if (rad == null) return center + e1 * (Mathf.Cos(a) * (rx + r)) + e2 * (Mathf.Sin(a) * (rz + r));
                int i = Mathf.RoundToInt(a / (Mathf.PI * 2f) * seg);   // радиус по секторам: в вершине берём больший из двух соседних
                float rr = Mathf.Max(rad[(i - 1 + seg) % seg], rad[i % seg]) + r;
                return center + (e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a)) * rr;
            }
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2f, a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                b.Quad(m, P(a0, thick) - hh, P(a1, thick) - hh, P(a1, thick) + hh, P(a0, thick) + hh, Color.white, true);
                b.Quad(m, P(a0, 0) - hh, P(a1, 0) - hh, P(a1, 0) + hh, P(a0, 0) + hh, Color.white, true);
                b.Quad(m, P(a0, 0) + hh, P(a1, 0) + hh, P(a1, thick) + hh, P(a0, thick) + hh, Color.white, true);
                b.Quad(m, P(a0, 0) - hh, P(a1, 0) - hh, P(a1, thick) - hh, P(a0, thick) - hh, Color.white, true);
            }
        }

        /// <summary>Сечение сетки лица плоскостью y = c.y (точки пересечения рёбер треугольников): радиус головы
        /// по секторам (+3 мм) и центр по сечению. null — сетки нет или сечение не получилось.</summary>
        static float[] HeadSlice(SkinnedMeshRenderer smr, ref Vector3 c, Vector3 fwd, int seg)
        {
            if (smr == null || smr.sharedMesh == null || !smr.sharedMesh.isReadable) return null;
            var mesh = new Mesh(); smr.BakeMesh(mesh, true);
            var vs = mesh.vertices; var tri = mesh.triangles; var M = smr.transform.localToWorldMatrix; Object.Destroy(mesh);
            for (int i = 0; i < vs.Length; i++) vs[i] = M.MultiplyPoint3x4(vs[i]);
            float y0 = c.y;
            var pts = new List<Vector3>();
            void Edge(Vector3 a, Vector3 b)
            {
                float da = a.y - y0, db = b.y - y0;
                if (da * db < 0f) pts.Add(Vector3.Lerp(a, b, da / (da - db)));
            }
            for (int t = 0; t + 2 < tri.Length; t += 3)
            {
                Vector3 a = vs[tri[t]], b = vs[tri[t + 1]], d = vs[tri[t + 2]];
                Edge(a, b); Edge(b, d); Edge(d, a);
            }
            if (pts.Count < 8) return null;
            Vector3 e1 = fwd, e2 = Vector3.Cross(Vector3.up, fwd);
            float zmin = 1e9f, zmax = -1e9f, xmin = 1e9f, xmax = -1e9f;
            foreach (var w in pts)
            {
                float f = Vector3.Dot(w, e1), sd = Vector3.Dot(w, e2);
                zmin = Mathf.Min(zmin, f); zmax = Mathf.Max(zmax, f); xmin = Mathf.Min(xmin, sd); xmax = Mathf.Max(xmax, sd);
            }
            c = e1 * (0.5f * (zmin + zmax)) + e2 * (0.5f * (xmin + xmax)) + Vector3.up * y0;   // центр — середина сечения, а не лица с носом
            var rad = new float[seg]; int filled = 0;
            foreach (var w in pts)
            {
                var dd = w - c; float x = Vector3.Dot(dd, e1), z = Vector3.Dot(dd, e2);
                int k = (int)Mathf.Repeat(Mathf.Floor(Mathf.Atan2(z, x) / (Mathf.PI * 2f) * seg), seg);
                if (rad[k] == 0f) filled++;
                rad[k] = Mathf.Max(rad[k], Mathf.Sqrt(x * x + z * z));
            }
            if (filled < seg / 2) return null;
            for (int i = 0; i < seg; i++)
            {
                if (rad[i] > 0f) continue;   // пустой сектор (затылок без сетки) — по ближайшим заполненным
                int a = i, bI = i;
                while (rad[a = (a - 1 + seg) % seg] == 0f) { }
                while (rad[bI = (bI + 1) % seg] == 0f) { }
                rad[i] = 0.5f * (rad[a] + rad[bI]);
            }
            for (int i = 0; i < seg; i++) rad[i] += 0.003f;
            return rad;
        }

        static void BuildGear(Rig g, AppearanceState state, PlayerProfile p, int team, Color shirt, Color trim)
        {
            var gear = (Gear)(p.gear | DevExtraGear);
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
                var rad = HeadSlice(g.faceRd, ref c, g.fwd, 24);
                if (rad != null) Ring(b, m, c, Vector3.up, g.fwd, 0, 0, h, 0.008f * S, 24, rad);
                else Ring(b, m, c, Vector3.up, g.fwd, rx, rz, h, 0.012f * S);
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
            int n = Mathf.Clamp(p.number, (byte)0, (byte)99);
            float S = g.S;
            Vector3 P = Vector3.Lerp(g.hips.position, g.neck.position, 0.64f);
            Color numCol = p.kitNumber > 0 ? TeamLook.Kit(p.kitNumber, trim)
                : Luma(shirt) > 0.5f ? Color.Lerp(trim, new Color(0.05f, 0.1f, 0.2f), 0.35f) : (Luma(trim) > 0.35f ? trim : Color.white);
            Color outline = Luma(numCol) > 0.5f ? new Color(0.05f, 0.05f, 0.08f) : Color.white;
            float back = g.fallback ? 0.2f : 0.115f, front = g.fallback ? 0.2f : 0.125f;
            MeasureTorso(g, P, ref back, ref front);

            if (PrintNumber(g, n, shirt, numCol, outline, P, back, front)) return;   // форма VRoid: номер печатается в ткань
            var mat = JerseyNumbers.Material(n, numCol, outline, out float aspect);
            const float gap = 0.006f;     // decal floats just above the cloth (+ the ink outline of the shirt material)
            Decal(bone, mat, P - g.fwd * (back + gap * S), Quaternion.LookRotation(g.fwd, Vector3.up), 0.15f * S * aspect, 0.15f * S);
            Decal(bone, mat, P + g.fwd * (front + gap * S) + Vector3.up * 0.06f * S, Quaternion.LookRotation(-g.fwd, Vector3.up), 0.062f * S * aspect, 0.062f * S);
        }

        /// <summary>Номер как принт: своя текстура футболки игрока (фон — цвет формы, номер на спине и груди), номер рисуется
        /// прямо в развёртку ткани — по треугольникам спины/груди, без захода на соседние участки атласа.</summary>
        static bool PrintNumber(Rig g, int n, Color shirt, Color numCol, Color outline, Vector3 P, float back, float front)
        {
            try
            {
                foreach (var smr in g.root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mats = smr.sharedMaterials; int mi = -1;
                    for (int i = 0; i < mats.Length; i++) if (mats[i] != null && mats[i].name.StartsWith("Uniform_Tops")) { mi = i; break; }
                    if (mi < 0 || smr.sharedMesh == null || !smr.sharedMesh.isReadable) continue;
                    var src = smr.sharedMesh; var uv = src.uv; var tri = src.GetTriangles(mi);
                    var baked = new Mesh(); smr.BakeMesh(baked, true); var vs = baked.vertices; Object.Destroy(baked);
                    var M = smr.transform.localToWorldMatrix; for (int i = 0; i < vs.Length; i++) vs[i] = M.MultiplyPoint3x4(vs[i]);
                    if (uv == null || uv.Length != vs.Length) return false;
                    const int TS = 1024;
                    var px = new Color32[TS * TS]; Color32 bg = shirt; bg.a = 255;
                    for (int i = 0; i < px.Length; i++) px[i] = bg;
                    Color32 fill = numCol, ol = outline; fill.a = ol.a = 255;
                    float S = g.S; Vector3 right = Vector3.Cross(Vector3.up, g.fwd).normalized;
                    _ = back; _ = front;
                    // спина: крупно; грудь: мелко, чуть выше. На груди зеркалим x (смотрим на игрока спереди)
                    int painted = Paint(px, TS, vs, uv, tri, n, P, -g.fwd, right, 0.15f * S, 0.15f * S, fill, ol, false)
                                + Paint(px, TS, vs, uv, tri, n, P + Vector3.up * 0.06f * S, g.fwd, right, 0.062f * S, 0.062f * S, fill, ol, true);
                    if (painted == 0) return false;
                    var tex = new Texture2D(TS, TS, TextureFormat.RGBA32, true) { name = "shirt_print_" + n, wrapMode = TextureWrapMode.Clamp };
                    tex.SetPixels32(px); tex.Apply(true, true);
                    var m = mats[mi];
                    SetBase(m, Color.white, ShadeOf(Color.white), false);
                    if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                    if (m.HasProperty("_ShadeTex")) m.SetTexture("_ShadeTex", tex);
                    return true;
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Tobe] номер-принт не вышел, будет наклейка: " + e.Message); }
            return false;
        }

        /// <summary>Рисует номер в треугольниках ткани, обращённых в сторону face, внутри рамки w×h вокруг center. Возвращает число закрашенных тексселей.</summary>
        static int Paint(Color32[] px, int TS, Vector3[] vs, Vector2[] uv, int[] tri, int n, Vector3 center, Vector3 face, Vector3 right, float w0, float h, Color32 fill, Color32 ol, bool mirror)
        {
            float digits = n >= 10 ? 7f : 3f, w = h * (digits + 1f) / 6f * 1.0f;   // ширина под 1–2 цифры (клетка 3×5 + отступ)
            int cnt = 0;
            for (int t = 0; t + 2 < tri.Length; t += 3)
            {
                int a = tri[t], b = tri[t + 1], c = tri[t + 2];
                Vector3 pa = vs[a], pb = vs[b], pc = vs[c];
                Vector3 nrm = Vector3.Cross(pb - pa, pc - pa); if (nrm.sqrMagnitude < 1e-12f) continue;
                if (Vector3.Dot(nrm.normalized, face) < 0.35f) continue;
                Vector3 cen = (pa + pb + pc) / 3f;
                // координаты в рамке номера: x — вбок, y — вверх
                Vector2 Q(Vector3 p) { var d = p - center; float x = Vector3.Dot(d, right); return new Vector2(mirror ? -x : x, d.y); }
                Vector2 qa = Q(pa), qb = Q(pb), qc = Q(pc), qm = Q(cen);
                if (Mathf.Abs(qm.x) > w * 0.75f || Mathf.Abs(qm.y) > h * 0.75f) continue;
                Vector2 ua = uv[a] * TS, ub = uv[b] * TS, uc = uv[c] * TS;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ua.x, Mathf.Min(ub.x, uc.x)))), x1 = Mathf.Min(TS - 1, Mathf.CeilToInt(Mathf.Max(ua.x, Mathf.Max(ub.x, uc.x))));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ua.y, Mathf.Min(ub.y, uc.y)))), y1 = Mathf.Min(TS - 1, Mathf.CeilToInt(Mathf.Max(ua.y, Mathf.Max(ub.y, uc.y))));
                float den = (ub.y - uc.y) * (ua.x - uc.x) + (uc.x - ub.x) * (ua.y - uc.y);
                if (Mathf.Abs(den) < 1e-8f) continue;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float fx = x + 0.5f, fy = y + 0.5f;
                        float l1 = ((ub.y - uc.y) * (fx - uc.x) + (uc.x - ub.x) * (fy - uc.y)) / den;
                        float l2 = ((uc.y - ua.y) * (fx - uc.x) + (ua.x - uc.x) * (fy - uc.y)) / den;
                        float l3 = 1f - l1 - l2;
                        if (l1 < -0.02f || l2 < -0.02f || l3 < -0.02f) continue;
                        Vector2 q = qa * l1 + qb * l2 + qc * l3;
                        float s = q.x / w + 0.5f, tt = q.y / h + 0.5f;
                        if (s < 0f || s > 1f || tt < 0f || tt > 1f) continue;
                        int k = JerseyNumbers.Sample(n, s, tt);
                        if (k == 0) continue;
                        px[y * TS + x] = k == 2 ? fill : ol; cnt++;
                    }
            }
            return cnt;
        }

        static void Decal(Transform bone, Material mat, Vector3 pos, Quaternion rot, float w, float h)
        {
            var q = Mats.Quad("TG_Number", null, Vector3.zero, Vector3.zero, new Vector3(w, h, 1f), mat);
            q.transform.SetPositionAndRotation(pos, rot);
            q.transform.SetParent(bone, true);
        }

        /// <summary>Finds how far the shirt surface reaches behind / in front of point P (along the body's forward axis) from the real mesh vertices.</summary>
        static void MeasureTorso(Rig g, Vector3 P, ref float back, ref float front)
        {
            try
            {
                float S = g.S;
                Vector3 right = Vector3.Cross(Vector3.up, g.fwd);
                float maxBack = -1f, maxFront = -1f; int hits = 0;
                var list = new List<Vector3>();
                Mesh tmp = null;
                foreach (var r in g.root.GetComponentsInChildren<Renderer>(false))
                {
                    if (r is ParticleSystemRenderer || InGear(r.transform)) continue;
                    bool shirtMat = false;
                    foreach (var m in r.sharedMaterials) if (m != null && Classify(m.name) == Part.Shirt) { shirtMat = true; break; }
                    if (!shirtMat) continue;
                    list.Clear();
                    if (r is SkinnedMeshRenderer sk)
                    {
                        if (tmp == null) tmp = new Mesh();
                        sk.BakeMesh(tmp, true);
                        var vs = tmp.vertices;
                        var rt = sk.transform;
                        // BakeMesh(useScale) differs between versions: pick the interpretation that matches the renderer bounds
                        float hb = 0f; { float lo = float.MaxValue, hi = float.MinValue; foreach (var v in vs) { lo = Mathf.Min(lo, v.y); hi = Mathf.Max(hi, v.y); } hb = hi - lo; }
                        Vector3 sc = Vector3.one;
                        if (hb > 1e-4f && Mathf.Abs(sk.bounds.size.y / hb - 1f) > 0.25f) sc = rt.lossyScale;
                        var M = Matrix4x4.TRS(rt.position, rt.rotation, sc);
                        foreach (var v in vs) list.Add(M.MultiplyPoint3x4(v));
                    }
                    else if (r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
                    {
                        var M = r.transform.localToWorldMatrix;
                        foreach (var v in mf.sharedMesh.vertices) list.Add(M.MultiplyPoint3x4(v));
                    }
                    foreach (var w in list)
                    {
                        Vector3 d = w - P;
                        if (Mathf.Abs(d.y) > 0.06f * S || Mathf.Abs(Vector3.Dot(d, right)) > 0.09f * S) continue;
                        float z = Vector3.Dot(d, g.fwd);
                        hits++;
                        if (-z > maxBack) maxBack = -z;
                        if (z > maxFront) maxFront = z;
                    }
                }
                if (tmp != null) Object.Destroy(tmp);
                if (hits >= 6 && maxBack > 0.04f * S && maxBack < 0.3f * S) back = maxBack;
                if (hits >= 6 && maxFront > 0.04f * S && maxFront < 0.3f * S) front = maxFront;
            }
            catch (System.Exception e) { Debug.LogWarning("[Tobe] torso measure failed: " + e.Message); }
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
