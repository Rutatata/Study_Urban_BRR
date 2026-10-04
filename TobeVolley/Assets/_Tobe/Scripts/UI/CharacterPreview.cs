using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Tobe.View;

namespace Tobe.UI
{
    /// <summary>
    /// Live 3D character preview for the editor: a dedicated camera renders a far-away stage (own lights, floor disc)
    /// into a RenderTexture that CharacterScreen shows through a RawImage. Not part of the arena scene.
    /// </summary>
    public sealed class CharacterPreview : MonoBehaviour
    {
        public static readonly Vector3 StagePos = new Vector3(0f, -200f, 0f);
        const int TexW = 912, TexH = 1020;
        const float Debounce = 0.12f;

        public RenderTexture Texture { get; private set; }
        public bool AutoSpin = true;
        public bool Busy { get { return model == null || loading; } }

        Camera cam;
        Transform pivot, floor;
        GameObject model;
        ProceduralPoser poser;
        PlayerProfile want, shown;
        int team;
        int shownModel = -1;
        bool dirty, active, loading;
        int loadToken;
        float debounceT, yaw = 20f, idleSince = 0f, poseT;
        PoseId pose = PoseId.Idle;
        float poseUntil;

        public static CharacterPreview Create()
        {
            var go = new GameObject("TobeCharacterPreview");
            DontDestroyOnLoad(go);
            return go.AddComponent<CharacterPreview>();
        }

        void Awake()
        {
            transform.position = StagePos;
            Texture = new RenderTexture(TexW, TexH, 24, RenderTextureFormat.ARGB32) { name = "TobePreviewRT", antiAliasing = 4 };
            Texture.Create();

            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(transform, false);

            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.15f, 4.5f);
            camGo.transform.localRotation = Quaternion.Euler(4f, 180f, 0f);
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 28f;
            cam.nearClipPlane = 0.3f; cam.farClipPlane = 30f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.035f, 0.04f, 0.06f, 1f);
            cam.allowHDR = false; cam.allowMSAA = false;
            cam.targetTexture = Texture;
            cam.depth = -50f;
            cam.enabled = false;
            try
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) { data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; }
            }
            catch (Exception) { }

            BuildStage();
        }

        void BuildStage()
        {
            // floor disc + team ring (home = orange)
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Floor";
            var col = disc.GetComponent<Collider>(); if (col != null) Destroy(col);
            disc.transform.SetParent(transform, false);
            disc.transform.localPosition = new Vector3(0f, -0.03f, 0f);
            disc.transform.localScale = new Vector3(2.6f, 0.03f, 2.6f);
            var r = disc.GetComponent<Renderer>();
            r.sharedMaterial = Mats.Lit("preview_floor", new Color(0.09f, 0.1f, 0.14f), 0.6f);
            floor = disc.transform;

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Ring";
            var rc = ring.GetComponent<Collider>(); if (rc != null) Destroy(rc);
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, -0.005f, 0f);
            ring.transform.localScale = new Vector3(2.7f, 0.01f, 2.7f);
            ring.GetComponent<Renderer>().sharedMaterial = Mats.Unlit("preview_ring", TeamLook.Trim[0]);

            AddSpot("Key", new Vector3(2.2f, 3.2f, 3.0f), new Color(1f, 0.95f, 0.88f), 26f, 70f);
            AddSpot("Rim", new Vector3(-2.4f, 2.6f, -2.6f), new Color(0.35f, 0.9f, 1f), 30f, 80f);
            AddSpot("Fill", new Vector3(-2.2f, 1.4f, 3.2f), new Color(1f, 0.6f, 0.35f), 9f, 80f);
        }

        void AddSpot(string name, Vector3 localPos, Color c, float intensity, float angle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.LookAt(transform.position + new Vector3(0f, 0.9f, 0f));
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot; l.spotAngle = angle; l.range = 14f;
            l.color = c; l.intensity = intensity; l.shadows = LightShadows.None;
        }

        void OnDestroy()
        {
            if (Texture != null) { Texture.Release(); Destroy(Texture); }
        }

        // ------------------------------------------------------------------ API
        public void SetActive(bool on)
        {
            active = on;
            if (cam != null) cam.enabled = on;
            if (on) { idleSince = Time.unscaledTime; }
            if (!on) { loadToken++; loading = false; }
        }

        /// <summary>Request the preview to show this profile (debounced; the model is reused unless the model index changed).</summary>
        public void SetProfile(PlayerProfile p, int teamIdx, bool immediate = false)
        {
            want = p; team = Mathf.Clamp(teamIdx, 0, 1);
            dirty = true;
            debounceT = immediate || model == null ? 0f : Debounce;
        }

        public void Drag(float dx)
        {
            yaw -= dx * 0.45f;
            idleSince = Time.unscaledTime;
        }

        /// <summary>Plays a one-shot pose (e.g. a cheer after picking a style).</summary>
        public void Play(PoseId p, float seconds)
        {
            pose = p; poseT = 0f; poseUntil = Time.unscaledTime + seconds;
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            if (!active) return;
            float dt = Time.unscaledDeltaTime;

            if (dirty)
            {
                debounceT -= dt;
                if (debounceT <= 0f && !loading) Apply();
            }

            if (AutoSpin && Time.unscaledTime - idleSince > 2.5f) yaw += dt * 18f;
            if (pivot != null) pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);

            if (poser != null)
            {
                poseT += dt;
                if (pose != PoseId.Idle && Time.unscaledTime > poseUntil) { pose = PoseId.Idle; poseT = 0f; }
                poser.pose = pose; poser.poseT = poseT; poser.air = false; poser.localVel = Vector3.zero;
            }
        }

        void Apply()
        {
            dirty = false;
            if (model == null || shownModel != want.model)
            {
                LoadModel(want.model);
                return;
            }
            shown = want;
            CharacterAppearance.Apply(model, model.GetComponentInChildren<Animator>(), want, team);
        }

        async void LoadModel(int idx)
        {
            loading = true;
            int token = ++loadToken;
            GameObject go = null;
            try { go = await CharacterLibrary.LoadModel(idx); }
            catch (Exception e) { Debug.LogWarning("[Tobe] preview model load failed: " + e.Message); }
            if (this == null) { if (go != null) Destroy(go); return; }
            if (token != loadToken) { if (go != null) Destroy(go); loading = false; if (active) dirty = true; return; }
            loading = false;
            if (go == null) go = CharacterLibrary.BuildFallback();
            Attach(go, idx);
        }

        void Attach(GameObject go, int idx)
        {
            if (model != null) Destroy(model);
            model = go;
            shownModel = idx;
            // park far from the arena so the arena camera never sees it
            go.transform.SetParent(pivot, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            Fit(go);
            CharacterAppearance.Apply(go, go.GetComponentInChildren<Animator>(), want, team);
            shown = want;
            poser = go.GetComponent<ProceduralPoser>();
            if (poser == null) poser = go.AddComponent<ProceduralPoser>();
            poser.Init(go);
            poseT = 0f;
            Play(PoseId.Celebrate, 1.1f);
            if (dirty) debounceT = 0f;
        }

        /// <summary>Scale the model to a 1.8 m reference height (same normalization as PlayerView).</summary>
        static void Fit(GameObject go)
        {
            bool any = false; Bounds b = default;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (any && b.size.y > 0.3f) go.transform.localScale = Vector3.one * (1.8f / b.size.y);
        }
    }
}
