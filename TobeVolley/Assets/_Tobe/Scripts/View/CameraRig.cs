// Third-person shoulder camera (Rematch-style), menu orbit, cinematic fly-to, shake / flash, floor aim ray.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Tobe.View
{
    public sealed class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }

        const float Distance = 4.2f, PivotHeight = 1.6f, ShoulderX = 0.5f, ShoulderY = 0.4f, Sens = 0.1f;
        const float BaseFov = 60f;

        Camera cam;
        float yaw, pitch = 12f;
        bool hadLocal, lastUi = true, uiInit;
        int lastTeam = -1;
        float followBlend;
        Vector3 smFollow;
        bool followInit;
        float orbitAng;
        float trauma, flash;
        float noiseSeed;
        float fov = BaseFov;

        bool cine; float cineT, cineDur, cineSide; Vector3 cineFocus;

        public static CameraRig Create(Transform parent)
        {
            var go = new GameObject("CameraRig");
            go.transform.SetParent(parent, false);
            return go.AddComponent<CameraRig>();
        }

        void Awake()
        {
            Instance = this;
            noiseSeed = Random.value * 100f;
            AcquireCamera();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void AcquireCamera()
        {
            cam = Camera.main;
            if (cam == null) cam = FindFirstObjectByType<Camera>();
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                go.transform.SetParent(transform, false);
                cam = go.AddComponent<Camera>();
                if (FindFirstObjectByType<AudioListener>() == null) go.AddComponent<AudioListener>();
            }
            cam.fieldOfView = BaseFov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 150f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.02f, 0.06f);
            cam.allowHDR = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }

        // ------------------------------------------------------------------ public API
        public void Shake(float amount) { trauma = Mathf.Clamp01(Mathf.Max(trauma, amount)); }
        public void Flash(float amount) { flash = Mathf.Clamp(Mathf.Max(flash, amount), 0f, 1.5f); }

        public void Cinematic(Vector3 pos, float duration)
        {
            cine = true; cineT = 0f; cineDur = Mathf.Max(0.6f, duration);
            cineFocus = pos; cineSide = Random.value < 0.5f ? -1f : 1f;
        }

        // ------------------------------------------------------------------ update
        void LateUpdate()
        {
            if (cam == null) { AcquireCamera(); if (cam == null) return; }
            float dt = Time.unscaledDeltaTime;
            var view = GameHub.View;

            bool haveLocal = ViewRoot.TryGetLocalVisual(out Vector3 lpos, out int team);

            if (!uiInit || GameHub.UiBlocking != lastUi)
            {
                uiInit = true; lastUi = GameHub.UiBlocking;
                Cursor.lockState = lastUi ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = lastUi;
            }

            if (haveLocal && (!hadLocal || team != lastTeam))
            {
                yaw = team == 0 ? 0f : 180f;
                pitch = 12f;
                followInit = false;
            }
            hadLocal = haveLocal;
            if (haveLocal) lastTeam = team;

            if (haveLocal && !GameHub.UiBlocking && Mouse.current != null)
            {
                Vector2 d = Mouse.current.delta.ReadValue();
                yaw += d.x * Sens;
                pitch = Mathf.Clamp(pitch - d.y * Sens, -10f, 35f);
            }
            yaw = Mathf.Repeat(yaw, 360f);
            GameHub.CameraYaw = yaw;

            // --- follow pose
            Quaternion fRot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 fPos = smFollow;
            if (haveLocal)
            {
                Vector3 pivot = lpos + Vector3.up * PivotHeight;
                Vector3 want = pivot + fRot * new Vector3(ShoulderX, ShoulderY, -Distance);
                want.y = Mathf.Max(0.3f, want.y);
                if (!followInit) { smFollow = want; followInit = true; }
                smFollow = Vector3.Lerp(smFollow, want, 1f - Mathf.Exp(-dt * 30f));
                fPos = smFollow;
            }

            // --- orbit pose
            orbitAng += dt * 0.07f;
            Vector3 center = new Vector3(Court.Width * .5f, 1.5f, Court.NetZ);
            Vector3 oPos = new Vector3(center.x + Mathf.Sin(orbitAng) * 12f, 7f + Mathf.Sin(orbitAng * 0.7f) * 1.5f, center.z + Mathf.Cos(orbitAng) * 15f);
            Quaternion oRot = Quaternion.LookRotation(center - oPos, Vector3.up);

            followBlend = Mathf.MoveTowards(followBlend, haveLocal ? 1f : 0f, dt * 1.2f);
            float w = followBlend * followBlend * (3f - 2f * followBlend);
            Vector3 nPos = Vector3.Lerp(oPos, fPos, w);
            Quaternion nRot = Quaternion.Slerp(oRot, fRot, w);

            // --- aim ray from the (unshaken, non-cinematic) screen center
            ComputeAim(nPos, nRot, haveLocal, lpos);

            Vector3 pos = nPos; Quaternion rot = nRot;
            float targetFov = BaseFov - (view.slowMo > 0f ? 5f : 0f);

            // --- cinematic
            if (cine)
            {
                cineT += dt;
                if (cineT >= cineDur) cine = false;
                else
                {
                    float wi = Mathf.Clamp01(cineT / 0.35f), wo = Mathf.Clamp01((cineDur - cineT) / 0.45f);
                    float cw = Mathf.Min(wi, wo); cw = cw * cw * (3f - 2f * cw);
                    Vector3 fwd = nRot * Vector3.forward; fwd.y = 0f;
                    fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                    Vector3 right = Vector3.Cross(Vector3.up, fwd);
                    Vector3 cPos = cineFocus - fwd * 4.5f + right * (cineSide * (2f + cineT * 0.4f)) + Vector3.up * 0.6f;
                    cPos.y = Mathf.Max(0.35f, cPos.y);
                    Quaternion cRot = Quaternion.LookRotation(cineFocus + Vector3.up * 0.3f - cPos, Vector3.up);
                    pos = Vector3.Lerp(nPos, cPos, cw);
                    rot = Quaternion.Slerp(nRot, cRot, cw);
                    targetFov = Mathf.Lerp(targetFov, 42f, cw);
                }
            }

            fov = Mathf.Lerp(fov, targetFov, 1f - Mathf.Exp(-dt * 8f));
            cam.fieldOfView = fov;

            // --- shake
            float roll = 0f;
            if (trauma > 0.001f)
            {
                float a = trauma * trauma;
                float t = Time.unscaledTime * 28f;
                Vector3 off = new Vector3(Noise(t, 0f), Noise(t, 1f), Noise(t, 2f)) * (a * 0.45f);
                pos += rot * off;
                roll = Noise(t, 3f) * a * 4f;
                trauma = Mathf.Max(0f, trauma - dt * 1.8f);
            }
            cam.transform.SetPositionAndRotation(pos, rot * Quaternion.Euler(0f, 0f, roll));

            // --- flash via post exposure
            if (flash > 0f) flash = Mathf.Max(0f, flash - dt * 3f);
            if (ArenaBuilder.Adjust != null) ArenaBuilder.Adjust.postExposure.Override(flash * 2.5f);
        }

        float Noise(float t, float seedOff) => (Mathf.PerlinNoise(t, noiseSeed + seedOff * 17.3f) - 0.5f) * 2f;

        static readonly Plane Floor = new Plane(Vector3.up, Vector3.zero);

        static void ComputeAim(Vector3 origin, Quaternion rot, bool haveLocal, Vector3 lpos)
        {
            var ray = new Ray(origin, rot * Vector3.forward);
            if (Floor.Raycast(ray, out float enter) && enter > 0f && enter < 120f)
            {
                GameHub.AimPoint = ray.GetPoint(enter);
                GameHub.AimValid = true;
            }
            else
            {
                Vector3 f = rot * Vector3.forward; f.y = 0f;
                if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
                GameHub.AimPoint = (haveLocal ? lpos : origin) + f.normalized * 15f;
                GameHub.AimPoint.y = 0f;
                GameHub.AimValid = false;
            }
        }
    }
}
