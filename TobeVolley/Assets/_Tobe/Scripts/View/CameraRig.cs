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
        float punch;                 // «толчок» камеры при сильном ударе: кратко сужает обзор и подаёт камеру вперёд
        float lean;                  // крен камеры в сторону бокового движения
        float dip, dipIn;            // затемнение между очками (скрывает расстановку игроков на позиции)
        MatchPhase prevPhase;

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
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.13f);
            cam.allowHDR = true;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }

        // ------------------------------------------------------------------ public API
        public void Shake(float amount) { trauma = Mathf.Clamp01(Mathf.Max(trauma, amount)); }
        public void Flash(float amount) { flash = Mathf.Clamp(Mathf.Max(flash, amount), 0f, 1.5f); }
        public void Punch(float amount) { punch = Mathf.Clamp01(Mathf.Max(punch, amount)); }

        // ---- аниме-вступление матча (тренировка и полигон: сервер стоит на паузе). Пропуск — пробел, клик или Esc.
        // Планы: снизу на своего игрока с наездом → соперники через сетку → крупно глаза со вспышкой → отъезд к игре.
        public static bool IntroActive => Instance != null && Instance.introT >= 0f;
        public static void PlayIntro()
        {
            if (Instance == null || !NetApi.PauseAllowed) return;
            Instance.introT = 0f; Instance.introFlash = false;
            NetApi.SetPaused?.Invoke(true);
        }
        float introT = -1f; bool introFlash;
        const float IntroLen = 4.6f;
        void EndIntro() { introT = -1f; NetApi.SetPaused?.Invoke(false); }

        void IntroShot(Vector3 L, int team, Vector3 gPos, Quaternion gRot, ref Vector3 pos, ref Quaternion rot, ref float fovOut)
        {
            var view = GameHub.View;
            Vector3 f = team == 0 ? Vector3.forward : Vector3.back, right = Vector3.Cross(Vector3.up, f);
            float head = 1.5f;
            if (view.localPlayerId >= 0 && ViewRoot.Instance != null && ViewRoot.Instance.TryGetView(view.localPlayerId, out var pv) && pv.HasModel)
            {
                head = 1.5f * pv.HeadHeight / 2f;   // HeadHeight — высота таблички (2 м × рост)
                Vector3 face = Quaternion.Euler(0f, pv.VisualYaw, 0f) * Vector3.forward;   // герой — лицом в кадр, куда бы ни смотрел
                f = face; right = Vector3.Cross(Vector3.up, f);
            }
            Vector3 net = team == 0 ? Vector3.forward : Vector3.back;
            float t = introT;
            if (t < 1.3f)
            {   // снизу, с наездом и лёгким креном — герой перед матчем
                float u = t / 1.3f;
                pos = L + f * (2.5f - 0.7f * u) + right * 0.55f + Vector3.up * (0.35f + 0.12f * u);
                rot = Quaternion.LookRotation(L + Vector3.up * (head - 0.15f) - pos) * Quaternion.Euler(0f, 0f, 5f);
                fovOut = 46f;
            }
            else if (t < 2.5f)
            {   // соперники за сеткой: камера у сетки на своей стороне, проезд вбок
                float u = (t - 1.3f) / 1.2f;
                Vector3 O = Vector3.zero; int n = 0;
                foreach (var p in view.players) if (p.team != team) { O += p.pos; n++; }
                O = n > 0 ? O / n : new Vector3(Court.Width * 0.5f, 0f, Court.WorldZ(1 - team, 4f));
                pos = new Vector3(O.x + Mathf.Lerp(-1.8f, 1.8f, u), 1.9f, Court.NetZ - net.z * 2.2f);
                rot = Quaternion.LookRotation(O + Vector3.up * 1.2f - pos);
                fovOut = 38f;
            }
            else if (t < 3.5f)
            {   // крупно глаза — вспышка и толчок, как стоп-кадр в аниме
                if (!introFlash) { introFlash = true; Flash(0.8f); Shake(0.25f); }
                float u = (t - 2.5f) / 1.0f;
                Vector3 eye = L + Vector3.up * (head - 0.1f);
                pos = eye + f * (0.62f - 0.08f * u) + right * 0.04f;
                rot = Quaternion.LookRotation(eye - pos);
                fovOut = 26f;
            }
            else
            {   // отъезд от лица к игровой камере
                float u = Mathf.Clamp01((t - 3.5f) / (IntroLen - 3.5f)); u = u * u * (3f - 2f * u);
                Vector3 eye = L + Vector3.up * (head - 0.1f), cp = eye + f * 0.54f;
                pos = Vector3.Lerp(cp, gPos, u);
                rot = Quaternion.Slerp(Quaternion.LookRotation(eye - cp), gRot, u);
                fovOut = Mathf.Lerp(26f, fovOut, u);
            }
        }

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

            if (haveLocal && !GameHub.UiBlocking)
            {
                Vector2 d = Controls.Look(dt);   // мышь + правый стик геймпада
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -10f, 35f);
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
            if (!haveLocal && Tobe.View.LockerRoom.TryCamera(out var lrPos, out var lrRot)) { nPos = lrPos; nRot = lrRot; }   // меню: раздевалка
            ComputeAim(nPos, nRot, haveLocal, lpos);

            Vector3 pos = nPos; Quaternion rot = nRot;
            float targetFov = BaseFov - (view.slowMo > 0f ? 5f : 0f);

            // --- динамика: на рывке обзор шире (ощущение скорости), камера кренится в сторону бокового движения
            float latVel = 0f;
            if (haveLocal && view.TryGetLocal(out var me))
            {
                var hv = new Vector3(me.vel.x, 0f, me.vel.z);
                targetFov += Mathf.Clamp01((hv.magnitude - 4.2f) / 2.5f) * 8f;
                latVel = Vector3.Dot(hv, nRot * Vector3.right);
            }
            lean = Mathf.Lerp(lean, Mathf.Clamp(-latVel * 0.45f, -2.5f, 2.5f), 1f - Mathf.Exp(-dt * 5f));
            if (punch > 0.001f)
            {   // резко внутрь, мягко обратно
                float pk = punch * punch;
                targetFov -= pk * 9f;
                pos += rot * Vector3.forward * (pk * 0.35f);
                punch = Mathf.Max(0f, punch - dt * 3.2f);
            }

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

            bool intro = false;
            if (introT >= 0f)
            {
                if (!haveLocal || view.phase == MatchPhase.End) EndIntro();
                else
                {
                    introT += dt;
                    bool skip = Controls.Down(Act.Jump) || Controls.Down(Act.Pause) || Controls.Down(Act.Attack);
                    if (skip && introT < IntroLen - 0.5f) introT = IntroLen - 0.5f;   // пропуск: быстрый отъезд к игре
                    if (introT >= IntroLen) EndIntro();
                    else { IntroShot(lpos, team, nPos, nRot, ref pos, ref rot, ref targetFov); intro = true; }
                }
            }

            fov = intro ? targetFov : Mathf.Lerp(fov, targetFov, 1f - Mathf.Exp(-dt * (punch > 0.05f ? 22f : 8f)));   // во вступлении — монтажные склейки
            cam.fieldOfView = fov;

            // --- shake
            float roll = lean;
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
            // --- затемнение между очками: темнеет в последние 0,3 с паузы после очка, игроки встают на позиции в темноте,
            //     картинка возвращается за 0,4 с
            if (view.phase == MatchPhase.Point && view.phaseTime < 0.3f) dip = Mathf.Max(dip, 1f - Mathf.Clamp01(view.phaseTime / 0.3f));
            else if (prevPhase == MatchPhase.Point && view.phase == MatchPhase.Serve) { dip = 1f; dipIn = 0f; }
            else if (dip > 0f) { dipIn += dt; dip = Mathf.Max(0f, 1f - dipIn / 0.4f); }
            prevPhase = view.phase;
            float dk = dip * dip * (3f - 2f * dip);
            if (ArenaBuilder.Adjust != null) ArenaBuilder.Adjust.postExposure.Override(ArenaBuilder.BaseExposure + flash * 2.5f - dk * 6f + LockerRoom.Whiteout * 5f);   // + уход в свет двери раздевалки
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
