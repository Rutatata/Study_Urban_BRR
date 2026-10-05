// Visual for one player: model, colors, smoothing, ring / chevron, aura, landing dust. Nameplates live in NameplateLayer (screen space).
using System;
using UnityEngine;

namespace Tobe.View
{
    public sealed class PlayerView : MonoBehaviour
    {
        public int Id { get; private set; }
        public Vector3 VisualPos => smPos;
        public float VisualYaw => smYaw;
        /// <summary>True once a real model (VRM clone or the fallback humanoid) is attached. Nothing is drawn before that (no capsule placeholders).</summary>
        public bool HasModel => model != null;
        public float HeadHeight { get; private set; } = 2.05f;

        Transform visual;
        GameObject model;
        ProceduralPoser poser;        // fallback animation (no mocap / non-humanoid model / animator failure)
        PlayerAnimator pa;            // mocap + key-pose animation
        bool usingPa;
        PlayerProfile curProfile;
        int curAppear = -1;
        Transform ring, chevron;
        Material ringMat, chevMat;
        ParticleSystem aura;
        ParticleSystem.EmissionModule auraEm;

        Vector3 smPos;
        float smYaw;
        bool inited, wasAir, isLocal;
        int loadToken;
        int curModel = -1, curHair = -1, curTeam = -1, curStyle = -1;

        public static PlayerView Create(Transform parent, in PlayerSnap s)
        {
            var go = new GameObject("Player_" + s.id);
            go.transform.SetParent(parent, false);
            var pv = go.AddComponent<PlayerView>();
            pv.Id = s.id;
            pv.Build(s);
            return pv;
        }

        // ------------------------------------------------------------------ build
        void Build(in PlayerSnap s)
        {
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);

            visual.gameObject.SetActive(false);       // shown when the model arrives

            // ground ring (world-space aligned, positioned each frame)
            ringMat = Mats.Instance(Mats.Unlit("pv_ring", Color.white, Mats.RingTex, true, true));
            ring = Mats.FloorQuad("Ring", transform, Vector3.zero, 1.4f, 1.4f, ringMat).transform;

            chevMat = Mats.Instance(Mats.Unlit("pv_chev", Color.white, Mats.ChevronTex, true, true));
            chevron = Mats.Quad("Chevron", transform, Vector3.zero, Vector3.zero, new Vector3(0.3f, 0.3f, 1f), chevMat).transform;
            chevron.gameObject.SetActive(false);

            ring.gameObject.SetActive(false);
            chevron.gameObject.SetActive(false);

            BuildAura();
            ApplyProfileChanges(s, true);
        }

        void BuildAura()
        {
            aura = FxManager.MakeSystem(transform, "Aura", Mats.Particle("pv_aura", Mats.SoftDot, true), 120, 0.9f, 0.18f, -0.4f);
            var main = aura.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            auraEm = aura.emission;
            auraEm.enabled = false;
            auraEm.rateOverTime = 45f;
            var sh = aura.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 10f;
            sh.radius = 0.4f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            aura.transform.localPosition = new Vector3(0, 0.1f, 0);
        }

        // ------------------------------------------------------------------ model loading
        void ApplyProfileChanges(in PlayerSnap s, bool force)
        {
            int m = s.profile.model, h = s.profile.hair, t = s.team, st = (int)s.profile.style;
            bool modelChanged = force || m != curModel;
            bool colorChanged = modelChanged || h != curHair || t != curTeam;
            curModel = m; curHair = h; curTeam = t;
            curProfile = s.profile;
            if (st != curStyle)
            {
                curStyle = st;
                var def = Styles.Get(s.profile.style);
                var main = aura.main;
                main.startColor = new ParticleSystem.MinMaxGradient(def.c1 * 2f, def.c2 * 2f);
            }
            if (modelChanged) LoadModelAsync(m);
            else if (colorChanged && model != null) CharacterLibrary.Recolor(model, curTeam, HairColor(curHair));
        }

        static Color HairColor(int i)
        {
            var arr = TeamLook.HairPresets;
            return arr[Mathf.Clamp(i, 0, arr.Length - 1)];
        }

        async void LoadModelAsync(int idx)
        {
            int token = ++loadToken;
            GameObject go = null;
            try { go = await CharacterLibrary.LoadModel(idx); }
            catch (Exception e) { Debug.LogWarning("[Tobe] model load error: " + e.Message); }
            if (this == null || token != loadToken)
            {
                if (go != null) Destroy(go);
                return;
            }
            if (go == null) go = CharacterLibrary.BuildFallback();
            AttachModel(go);
        }

        void AttachModel(GameObject go)
        {
            if (model != null) Destroy(model);
            model = go;
            model.SetActive(true);
            model.transform.SetParent(visual, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            var rends = model.GetComponentsInChildren<Renderer>(true);
            bool any = false; Bounds b = default;
            foreach (var r in rends)
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
                if (r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (any && b.size.y > 0.3f)
            {
                float s = 1.8f / b.size.y;
                model.transform.localScale = Vector3.one * s;
            }
            HeadHeight = 2.0f * Mathf.Max(0.9f, curProfile.HeightScale);
            CharacterLibrary.Recolor(model, curTeam, HairColor(curHair));

            ApplyAppearance();

            // animation: PlayerAnimator (needs a humanoid avatar + loaded mocap) with ProceduralPoser as the fallback.
            // Both capture the bind pose now, before any of them has moved a bone.
            poser = null; pa = null; usingPa = false;
            if (PlayerAnimator.CanAnimate(model))
            {
                pa = model.GetComponent<PlayerAnimator>();
                if (pa == null) pa = model.AddComponent<PlayerAnimator>();
                pa.enabled = false;
                if (!pa.Init(model)) { Destroy(pa); pa = null; }
            }
            poser = model.GetComponent<ProceduralPoser>();
            if (poser == null) poser = model.AddComponent<ProceduralPoser>();
            poser.Init(model);
            visual.gameObject.SetActive(true);
        }

        /// <summary>Character editor / roster changes: proportions, gear etc. are applied by CharacterAppearance (written elsewhere).</summary>
        void ApplyAppearance()
        {
            if (model == null) return;
            try
            {
                var animator = model.GetComponentInChildren<Animator>();
                CharacterAppearance.Apply(model, animator, curProfile, curTeam);
                curAppear = AppearKey(curProfile, curTeam);
            }
            catch (Exception e) { Debug.LogWarning("[Tobe] CharacterAppearance failed: " + e.Message); }
        }

        static int AppearKey(in PlayerProfile p, int team)
        {
            unchecked { return (((p.height * 31 + p.build) * 31 + p.skin) * 31 + p.eyes) * 31 + p.gear + team * 7919 + p.hair * 104729; }
        }

        /// <summary>Uses PlayerAnimator once the mocap is loaded and the model is a usable humanoid, otherwise the procedural poser.</summary>
        void SelectAnimator()
        {
            bool want = pa != null && pa.Usable && MotionLibrary.HasCore;
            if (want == usingPa) { if (!want && pa != null && !pa.Usable) { } return; }
            usingPa = want;
            if (pa != null) pa.enabled = want;
            if (poser != null)
            {
                if (want) poser.Release();   // hand the root transform back before PlayerAnimator takes over
                poser.enabled = !want;
            }
        }

        // ------------------------------------------------------------------ per frame
        public void Tick(in PlayerSnap s, bool local, float dt)
        {
            isLocal = local;
            if (s.profile.model != curModel || s.profile.hair != curHair || s.team != curTeam || (int)s.profile.style != curStyle)
                ApplyProfileChanges(s, false);

            if (model != null && curAppear != -1 && AppearKey(s.profile, s.team) != curAppear) { curProfile = s.profile; ApplyAppearance(); }

            Vector3 target = s.pos;
            if (!inited || (smPos - target).sqrMagnitude > 9f) { smPos = target; smYaw = s.yaw; inited = true; }
            float ky = 1f - Mathf.Exp(-dt * (local ? 30f : 14f));
            if (local) smPos = target;   // своё движение и так считается каждый кадр: сглаживание только запаздывало (висел над полом при приземлении)
            else
            {   // чужие: по горизонтали мягко, по высоте быстро (полёт уже экстраполирован с гравитацией в MatchClient)
                float kp = 1f - Mathf.Exp(-dt * 16f), kv = 1f - Mathf.Exp(-dt * 45f);
                smPos = new Vector3(Mathf.Lerp(smPos.x, target.x, kp), Mathf.Lerp(smPos.y, target.y, kv), Mathf.Lerp(smPos.z, target.z, kp));
            }
            smYaw = Mathf.LerpAngle(smYaw, s.yaw, ky);
            transform.position = smPos;
            visual.rotation = Quaternion.Euler(0f, smYaw, 0f);

            Vector3 lv = Quaternion.Inverse(Quaternion.Euler(0f, smYaw, 0f)) * s.vel;
            SelectAnimator();
            if (usingPa && pa != null) pa.Feed(in s, lv, smYaw);
            if (!usingPa && poser != null)
            {
                poser.pose = s.pose;
                poser.poseT = s.poseT;
                poser.air = s.air;
                poser.localVel = lv;
            }

            if (wasAir && !s.air && FxManager.Instance != null)
                FxManager.Instance.Dust(smPos, Mathf.Clamp01(Mathf.Abs(s.vel.y) / 6f + 0.3f));
            wasAir = s.air;
            FootDust(in s, dt);

            // nothing is drawn until the model exists
            ring.gameObject.SetActive(model != null);
            if (model == null) { chevron.gameObject.SetActive(false); return; }

            // ground ring
            var def = Styles.Get(s.profile.style);
            Color trim = TeamLook.Trim[Mathf.Clamp(s.team, 0, 1)];
            float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 4f);
            ring.position = new Vector3(smPos.x, 0.025f, smPos.z);
            if (local)
            {
                Color gold = new Color(1f, 0.82f, 0.2f);
                Mats.SetColor(ringMat, gold * 1.4f);
                ring.localScale = new Vector3(1.8f * pulse, 1.8f * pulse, 1f);
            }
            else
            {
                Mats.SetColor(ringMat, trim * 1.2f);
                ring.localScale = new Vector3(1.3f, 1.3f, 1f);
            }

            // local-player marker (billboard finished in LateUpdate); other players get a screen-space plate from NameplateLayer
            chevron.gameObject.SetActive(local);
            if (local)
            {
                Mats.SetColor(chevMat, new Color(1f, 0.82f, 0.2f) * 1.4f);
                chevron.position = smPos + new Vector3(0f, HeadHeight + 0.35f + 0.06f * Mathf.Sin(Time.time * 4f), 0f);
            }

            // aura
            bool on = s.energy >= 100f || s.armed;
            auraEm.enabled = on;
            if (on)
            {
                var main = aura.main;
                main.startColor = new ParticleSystem.MinMaxGradient(def.c1 * 2f, def.c2 * 2f);
            }
            aura.transform.position = new Vector3(smPos.x, smPos.y + 0.1f, smPos.z);
        }

        // ------------------------------------------------------------------ пыль из-под ног
        // На рывке ноги поднимают лёгкие облачка пыли, резкое торможение или смена направления — облачко побольше (вместе со скрипом).
        Vector2 dustVSlow; float dustT;

        void FootDust(in PlayerSnap s, float dt)
        {
            if (FxManager.Instance == null || model == null || dt <= 0f) return;
            var v = new Vector2(s.vel.x, s.vel.z);
            dustVSlow = Vector2.Lerp(dustVSlow, v, 1f - Mathf.Exp(-dt / 0.12f));
            dustT -= dt;
            if (s.air || dustT > 0f) return;
            float jerk = (v - dustVSlow).magnitude;
            Vector3 feet = new Vector3(smPos.x, 0.02f, smPos.z);
            if (dustVSlow.magnitude > 2.6f && jerk > 2.2f)
            {   // торможение / разворот: облачко в сторону, противоположную новому движению
                FxManager.Instance.Dust(feet - new Vector3(v.x, 0f, v.y).normalized * 0.15f, Mathf.Clamp01(jerk / 6f) * 0.55f);
                dustT = 0.3f;
            }
            else if (v.magnitude > 5.2f)
            {   // рывок: частые маленькие облачка за спиной
                FxManager.Instance.Dust(feet - new Vector3(v.x, 0f, v.y).normalized * 0.25f, 0.18f);
                dustT = 0.11f;
            }
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Quaternion q = cam.transform.rotation;
            if (chevron != null && chevron.gameObject.activeSelf) chevron.rotation = q;
        }
    }
}
