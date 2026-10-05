// Event-driven visual effects: sparks, dust, shockwaves, floor cracks, stars, confetti. Pooled / persistent systems.
using UnityEngine;

namespace Tobe.View
{
    public sealed class FxManager : MonoBehaviour
    {
        public static FxManager Instance { get; private set; }

        ParticleSystem sparks, dust, stars, confetti, glow;

        sealed class Ring
        {
            public Transform t; public Material mat; public float age, life, maxScale; public Color col; public bool active;
        }
        readonly Ring[] rings = new Ring[6];
        readonly Ring[] cracks = new Ring[4];
        readonly Ring[] crackGlow = new Ring[4];
        int ringIdx, crackIdx;

        // camera-facing flat effects (speed lines, shock rings, onomatopoeia)
        sealed class Bb
        {
            public Transform t; public Material mat; public TextMesh text; public float age, life, s0, s1, roll; public Color col; public bool active;
        }
        readonly Bb[] speedLines = new Bb[4];
        readonly Bb[] shockBb = new Bb[4];
        readonly Bb[] words = new Bb[3];
        int slIdx, sbIdx, wIdx;
        // full screen "impact frame" (ink flash with radial lines), parented to the camera while active
        Transform frameQuad; Material frameMat; float frameT, frameLife; Camera frameCam;
        Texture2D crackTex;

        public static FxManager Create(Transform parent)
        {
            var go = new GameObject("FxManager");
            go.transform.SetParent(parent, false);
            return go.AddComponent<FxManager>();
        }

        // ------------------------------------------------------------------ shared particle system factory
        public static ParticleSystem MakeSystem(Transform parent, string name, Material mat, int max, float life, float size,
            float gravity, bool fade = true, bool shrink = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = life;
            main.startSpeed = 0f;
            main.startSize = size;
            main.startColor = Color.white;
            main.gravityModifier = gravity;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            if (fade)
            {
                var col = ps.colorOverLifetime; col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
            }
            if (shrink)
            {
                var sz = ps.sizeOverLifetime; sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f)));
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, Color c, float size, float life)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = pos, velocity = vel, startColor = c, startSize = size, startLifetime = life
            };
            ps.Emit(ep, 1);
        }

        // ------------------------------------------------------------------ lifecycle
        void Awake()
        {
            Instance = this;
            var soft = Mats.SoftDot;
            sparks = MakeSystem(transform, "Sparks", Mats.Particle("fx_spark", soft, true), 600, 0.4f, 0.08f, 1.2f);
            glow = MakeSystem(transform, "Glow", Mats.Particle("fx_glow", soft, true), 200, 0.4f, 0.5f, 0f);
            dust = MakeSystem(transform, "Dust", Mats.Particle("fx_dust", soft, false), 400, 0.9f, 0.4f, -0.05f);
            stars = MakeSystem(transform, "Stars", Mats.Particle("fx_star", Mats.StarTex, true), 200, 0.6f, 0.25f, 0.2f);
            confetti = MakeSystem(transform, "Confetti", Mats.Particle("fx_confetti", Mats.SquareTex, false), 1500, 3.5f, 0.14f, 0.12f, true, false);
            var rot = confetti.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var ringMatBase = Mats.Unlit("fx_ring", Color.white, Mats.RingTex, true, true);
            for (int i = 0; i < rings.Length; i++)
            {
                var m = Mats.Instance(ringMatBase);
                var go = Mats.FloorQuad("Shock" + i, transform, Vector3.zero, 1f, 1f, m);
                go.SetActive(false);
                rings[i] = new Ring { t = go.transform, mat = m };
            }
            crackTex = MakeCrackTexture();
            var crackBase = Mats.Unlit("fx_crack", new Color(0.05f, 0.03f, 0.04f, 1f), crackTex, true, false);
            for (int i = 0; i < cracks.Length; i++)
            {
                var m = Mats.Instance(crackBase);
                var go = Mats.FloorQuad("Crack" + i, transform, Vector3.zero, 1f, 1f, m);
                go.SetActive(false);
                cracks[i] = new Ring { t = go.transform, mat = m };
                var mg = Mats.Instance(Mats.Unlit("fx_crack_glow", Color.white, crackTex, true, true));
                var gg = Mats.FloorQuad("CrackGlow" + i, transform, Vector3.zero, 1f, 1f, mg);
                gg.SetActive(false);
                crackGlow[i] = new Ring { t = gg.transform, mat = mg };
            }

            var slBase = Mats.Unlit("fx_speedlines", Color.white, ProcTex.SpeedLinesTex, true, true, true);
            for (int i = 0; i < speedLines.Length; i++)
            {
                var m = Mats.Instance(slBase);
                var q = Mats.Quad("SpeedLines" + i, transform, Vector3.zero, Vector3.zero, Vector3.one, m);
                q.SetActive(false);
                speedLines[i] = new Bb { t = q.transform, mat = m };
            }
            var sbBase = Mats.Unlit("fx_shockbb", Color.white, Mats.RingTex, true, true, true);
            for (int i = 0; i < shockBb.Length; i++)
            {
                var m = Mats.Instance(sbBase);
                var q = Mats.Quad("ShockBb" + i, transform, Vector3.zero, Vector3.zero, Vector3.one, m);
                q.SetActive(false);
                shockBb[i] = new Bb { t = q.transform, mat = m };
            }
            for (int i = 0; i < words.Length; i++)
            {
                var tm = Mats.Text(transform, "", 0.05f, 64, Color.white);
                tm.gameObject.SetActive(false);
                words[i] = new Bb { t = tm.transform, text = tm };
            }
            frameMat = Mats.Unlit("fx_impactframe", Color.white, ProcTex.ImpactFrameTex, true, false, true);
            frameMat.renderQueue = 4000;
            var fq = Mats.Quad("ImpactFrame", transform, Vector3.zero, Vector3.zero, Vector3.one, frameMat);
            frameQuad = fq.transform;
            fq.SetActive(false);
        }

        // ------------------------------------------------------------------ anime impact frame
        /// <summary>Brief inverted ink flash with radial speed lines over the whole screen.</summary>
        public void ImpactFrame(float life)
        {
            var cam = Camera.main;
            if (cam == null) return;
            frameCam = cam;
            frameQuad.SetParent(cam.transform, false);
            frameQuad.gameObject.SetActive(true);
            frameT = 0f; frameLife = Mathf.Max(0.08f, life);
        }

        void UpdateFrame(float dt)
        {
            if (!frameQuad.gameObject.activeSelf) return;
            frameT += dt;
            if (frameT >= frameLife || frameCam == null) { frameQuad.gameObject.SetActive(false); frameQuad.SetParent(transform, false); return; }
            const float d = 0.35f;
            float h = 2f * d * Mathf.Tan(frameCam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
            frameQuad.localPosition = new Vector3(0f, 0f, d);
            frameQuad.localRotation = Quaternion.identity;
            frameQuad.localScale = new Vector3(h * frameCam.aspect, h, 1f);
            float k = frameT / frameLife;
            float a = k < 0.45f ? 0.92f : Mathf.Lerp(0.92f, 0f, (k - 0.45f) / 0.55f);
            // first part inverted (white lines on black) feel via alternating tint
            Color c = k < 0.2f ? new Color(1f, 1f, 1f, a) : new Color(1f, 0.92f, 0.8f, a);
            Mats.SetColor(frameMat, c);
        }

        // ------------------------------------------------------------------ billboards
        void StartSpeedLines(Vector3 pos, Color col, float size, float life)
        {
            var b = speedLines[slIdx++ % speedLines.Length];
            b.t.gameObject.SetActive(true);
            b.t.position = pos; b.age = 0f; b.life = life; b.s0 = size * 0.6f; b.s1 = size; b.roll = Random.value * 360f; b.col = col; b.active = true;
        }

        void StartShockBb(Vector3 pos, Color col, float size, float life)
        {
            var b = shockBb[sbIdx++ % shockBb.Length];
            b.t.gameObject.SetActive(true);
            b.t.position = pos; b.age = 0f; b.life = life; b.s0 = size * 0.15f; b.s1 = size; b.roll = 0f; b.col = col; b.active = true;
        }

        void StartWord(Vector3 pos, string text, Color col, float size)
        {
            var b = words[wIdx++ % words.Length];
            b.t.gameObject.SetActive(true);
            b.text.text = text; b.text.color = col;
            b.t.position = pos + Vector3.up * 0.4f; b.age = 0f; b.life = 0.75f; b.s0 = size * 0.4f; b.s1 = size; b.roll = Random.Range(-14f, 14f); b.col = col; b.active = true;
        }

        void UpdateBb(Bb[] arr, float dt, bool isWord)
        {
            var cam = Camera.main;
            for (int i = 0; i < arr.Length; i++)
            {
                var b = arr[i];
                if (!b.active) continue;
                b.age += dt;
                float k = b.age / b.life;
                if (k >= 1f) { b.active = false; b.t.gameObject.SetActive(false); continue; }
                float e = 1f - (1f - k) * (1f - k);
                float s = Mathf.Lerp(b.s0, b.s1, e);
                if (cam != null) b.t.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, b.roll);
                if (isWord)
                {
                    float pop = k < 0.15f ? Mathf.Lerp(0.5f, 1.25f, k / 0.15f) : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((k - 0.15f) / 0.2f));
                    b.t.localScale = Vector3.one * (s * pop);
                    var c = b.col; c.a = k < 0.7f ? 1f : (1f - k) / 0.3f;
                    b.text.color = c;
                    b.t.position += Vector3.up * dt * 0.5f;
                }
                else
                {
                    b.t.localScale = new Vector3(s, s, 1f);
                    var c = b.col; c.a = Mathf.Clamp01(1.4f * (1f - k)) * b.col.a;
                    Mats.SetColor(b.mat, c);
                }
            }
        }

        void OnEnable() { GameHub.OnEvent += OnEvent; }
        void OnDisable() { GameHub.OnEvent -= OnEvent; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < rings.Length; i++)
            {
                var r = rings[i];
                if (!r.active) continue;
                r.age += dt;
                float k = r.age / r.life;
                if (k >= 1f) { r.active = false; r.t.gameObject.SetActive(false); continue; }
                float e = 1f - (1f - k) * (1f - k);
                float s = Mathf.Lerp(0.6f, r.maxScale, e);
                r.t.localScale = new Vector3(s, s, 1f);
                var c = r.col; c.a = (1f - k) * r.col.a;
                Mats.SetColor(r.mat, c);
            }
            UpdateBb(speedLines, dt, false);
            UpdateBb(shockBb, dt, false);
            UpdateBb(words, dt, true);
            UpdateFrame(dt);
            for (int i = 0; i < crackGlow.Length; i++)
            {
                var r = crackGlow[i];
                if (!r.active) continue;
                r.age += dt;
                if (r.age >= r.life) { r.active = false; r.t.gameObject.SetActive(false); continue; }
                float a = Mathf.Clamp01(1f - r.age / r.life);
                Mats.SetColor(r.mat, new Color(r.col.r * 2.2f, r.col.g * 2.2f, r.col.b * 2.2f, a * a));
            }
            for (int i = 0; i < cracks.Length; i++)
            {
                var r = cracks[i];
                if (!r.active) continue;
                r.age += dt;
                if (r.age >= r.life) { r.active = false; r.t.gameObject.SetActive(false); continue; }
                float a = Mathf.Clamp01((r.life - r.age) / 1.5f) * Mathf.Clamp01(r.age / 0.05f);
                Mats.SetColor(r.mat, new Color(0.05f, 0.03f, 0.04f, a * 0.9f));
            }
        }

        // ------------------------------------------------------------------ events
        void OnEvent(GameEvent e)
        {
            switch (e.type)
            {
                case GameEventType.Hit: HitFx(e); break;
                case GameEventType.Impact: ImpactFx(e); break;
                case GameEventType.Block: BlockFx(e); break;
                case GameEventType.Point: PointFx(e); break;
                case GameEventType.Cinematic:
                    if (CameraRig.Instance != null) CameraRig.Instance.Cinematic(e.pos, e.floatArg > 0f ? e.floatArg : 1.5f);
                    break;
                case GameEventType.MatchEnd:
                    ArenaBuilder.Cheer(1.5f);
                    Confetti(e.intArg, 160, 0f);
                    break;
                case GameEventType.Cutin: ArenaBuilder.Cheer(0.25f); break;
            }
        }

        void HitFx(GameEvent e)
        {
            float p = Mathf.Clamp01(e.floatArg);
            int n = Mathf.RoundToInt(6f + p * 22f);
            for (int i = 0; i < n; i++)
            {
                Vector3 v = Random.onUnitSphere * Random.Range(2f, 3f + p * 5f);
                Color c = Color.Lerp(new Color(1f, 0.95f, 0.7f), new Color(1f, 0.6f, 0.2f), Random.value) * 2f;
                Emit(sparks, e.pos, v, c, Random.Range(0.05f, 0.1f), Random.Range(0.2f, 0.45f));
            }
            Emit(glow, e.pos, Vector3.zero, new Color(1f, 0.9f, 0.7f, 1f) * 1.5f, 0.35f + p * 0.6f, 0.15f);
            // sweat / shine sparkles
            if (p > 0.45f)
            {
                int ns = 2 + Mathf.RoundToInt(p * 5f);
                for (int i = 0; i < ns; i++)
                {
                    Vector3 v = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(1.2f, 3f), Random.Range(-1.5f, 1.5f));
                    Emit(stars, e.pos, v, new Color(0.7f, 0.95f, 1f, 1f) * 1.8f, Random.Range(0.1f, 0.2f), Random.Range(0.35f, 0.6f));
                }
            }
            if (p > 0.7f)
            {
                StartSpeedLines(e.pos, new Color(1f, 0.95f, 0.8f, 0.9f), 2.2f + p * 2.4f, 0.2f);
                if (p > 0.9f) StartShockBb(e.pos, new Color(1f, 0.9f, 0.6f, 1f), 2.2f, 0.25f);
            }
            if (p > 0.82f)
            {
                var pose = (PoseId)e.intArg;
                string w = pose == PoseId.Spike ? "バシッ!" : pose == PoseId.ServeHit ? "ドンッ!" : pose == PoseId.Block ? "ガッ!" : pose == PoseId.Dive ? "ズザッ!" : "ポンッ!";
                StartWord(e.pos, w, pose == PoseId.Spike ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 1f, 1f), 2.4f);
            }
            // сильные удары чувствуются камерой: тряска растёт с силой, мощная атака даёт «толчок» и вспышку
            if (CameraRig.Instance != null)
            {
                var hp = (PoseId)e.intArg;
                bool attack = hp == PoseId.Spike || hp == PoseId.ServeHit;
                if (p > 0.5f) CameraRig.Instance.Shake((attack ? 0.18f : 0.08f) + (p - 0.5f) * (attack ? 0.5f : 0.2f));
                if (attack && p > 0.6f) CameraRig.Instance.Punch(0.35f + (p - 0.6f) * 1.4f);
                if (attack && p > 0.9f) CameraRig.Instance.Flash(0.25f);
            }
        }

        void ImpactFx(GameEvent e)
        {
            Vector3 p = new Vector3(e.pos.x, 0.05f, e.pos.z);
            bool big = e.intArg == 1;
            int n = big ? 24 : 10;
            for (int i = 0; i < n; i++)
            {
                float a = Random.value * Mathf.PI * 2f, sp = Random.Range(1f, big ? 4.5f : 2.5f);
                Vector3 v = new Vector3(Mathf.Cos(a) * sp, Random.Range(0.4f, big ? 2.5f : 1.4f), Mathf.Sin(a) * sp);
                Emit(dust, p, v, new Color(0.85f, 0.8f, 0.75f, 0.55f), Random.Range(0.3f, big ? 0.9f : 0.6f), Random.Range(0.6f, 1.1f));
            }
            if (!big)
            {
                if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.1f);
                return;
            }
            Color col = e.color.a > 0.01f ? e.color : new Color(1f, 0.7f, 0.2f);
            StartRing(p, col, 9f, 0.7f);
            StartRing(p, Color.white, 5f, 0.45f);
            StartCrack(p, col);
            Vector3 mid = p + Vector3.up * 0.5f;
            StartSpeedLines(mid, new Color(col.r, col.g, col.b, 1f) * 1.2f + new Color(0.2f, 0.2f, 0.2f, 0f), 7f, 0.35f);
            StartShockBb(mid, Color.white, 6f, 0.4f);
            StartShockBb(mid, col, 9f, 0.55f);
            StartWord(mid + Vector3.up * 0.8f, new[] { "ドガァッ!", "ズドン!!", "バァン!!" }[Random.Range(0, 3)], new Color(1f, 0.9f, 0.3f), 4.2f);
            ImpactFrame(0.2f);
            for (int i = 0; i < 18; i++)
            {
                Vector3 v = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(1f, 4f), Random.Range(-2.5f, 2.5f));
                Emit(stars, p, v, Color.Lerp(col, Color.white, 0.5f) * 2f, Random.Range(0.12f, 0.3f), Random.Range(0.4f, 0.9f));
            }
            for (int i = 0; i < 40; i++)
            {
                float a = Random.value * Mathf.PI * 2f, sp = Random.Range(2f, 8f);
                Vector3 v = new Vector3(Mathf.Cos(a) * sp, Random.Range(2f, 8f), Mathf.Sin(a) * sp);
                Emit(sparks, p, v, col * 2.2f, Random.Range(0.06f, 0.14f), Random.Range(0.4f, 0.9f));
            }
            Emit(glow, p + Vector3.up * 0.2f, Vector3.zero, col * 1.8f, 2.4f, 0.35f);
            if (CameraRig.Instance != null) { CameraRig.Instance.Shake(0.9f); CameraRig.Instance.Flash(0.5f); }
            ArenaBuilder.Cheer(0.5f);
        }

        void BlockFx(GameEvent e)
        {
            for (int i = 0; i < 14; i++)
            {
                Vector3 v = Random.onUnitSphere * Random.Range(3f, 6f);
                Color c = Color.Lerp(Color.white, new Color(1f, 0.85f, 0.2f), Random.value) * 2f;
                Emit(stars, e.pos, v, c, Random.Range(0.2f, 0.38f), Random.Range(0.4f, 0.7f));
            }
            Emit(glow, e.pos, Vector3.zero, new Color(1f, 0.95f, 0.6f, 1f) * 1.5f, 0.8f, 0.2f);
            if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.15f);
        }

        void PointFx(GameEvent e)
        {
            ArenaBuilder.Cheer(1f);
            Confetti(e.intArg, 140, 0f);
            if (e.playerId != 255 && GameHub.View.TryGet(e.playerId, out var hero))
            {
                int team = hero.team;
                for (int i = 0; i < 45; i++)
                {
                    Vector3 v = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(3f, 7f), Random.Range(-2.5f, 2.5f));
                    Emit(confetti, hero.pos + Vector3.up * 1.2f, v, ConfettiColor(team), Random.Range(0.1f, 0.18f), Random.Range(2.5f, 3.5f));
                }
            }
        }

        static Color ConfettiColor(int team)
        {
            team = Mathf.Clamp(team, 0, 1);
            float r = Random.value;
            if (r < 0.4f) return TeamLook.Trim[team] * 1.2f;
            if (r < 0.6f) return new Color(1f, 0.85f, 0.3f);
            if (r < 0.8f) return Color.white;
            Color s = TeamLook.Shirt[team];
            return (s.r + s.g + s.b) < 0.5f ? new Color(0.9f, 0.3f, 0.5f) : s;
        }

        void Confetti(int team, int count, float unused)
        {
            team = Mathf.Clamp(team, 0, 1);
            float cz = team == 0 ? 4.5f : 13.5f;
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = new Vector3(Random.Range(0f, 9f), Random.Range(5f, 9f), cz + Random.Range(-4.5f, 4.5f));
                Vector3 v = new Vector3(Random.Range(-1f, 1f), Random.Range(-2f, 0.5f), Random.Range(-1f, 1f));
                Emit(confetti, pos, v, ConfettiColor(team), Random.Range(0.1f, 0.2f), Random.Range(2.8f, 4f));
            }
        }

        /// <summary>Public helper for landing dust etc.</summary>
        public void Dust(Vector3 pos, float strength)
        {
            int n = Mathf.RoundToInt(4 + strength * 8);
            for (int i = 0; i < n; i++)
            {
                float a = Random.value * Mathf.PI * 2f, sp = Random.Range(0.5f, 1.5f + strength * 1.5f);
                Vector3 v = new Vector3(Mathf.Cos(a) * sp, Random.Range(0.2f, 0.8f), Mathf.Sin(a) * sp);
                Emit(dust, new Vector3(pos.x, 0.05f, pos.z), v, new Color(0.85f, 0.8f, 0.75f, 0.4f), Random.Range(0.2f, 0.4f), Random.Range(0.4f, 0.8f));
            }
        }

        // ------------------------------------------------------------------ rings / cracks
        void StartRing(Vector3 pos, Color c, float maxScale, float life)
        {
            var r = rings[ringIdx++ % rings.Length];
            r.t.gameObject.SetActive(true);
            r.t.position = new Vector3(pos.x, 0.03f, pos.z);
            r.t.rotation = Quaternion.Euler(90f, 0f, 0f);
            r.age = 0f; r.life = life; r.maxScale = maxScale; r.col = new Color(c.r * 1.6f, c.g * 1.6f, c.b * 1.6f, 1f); r.active = true;
        }

        void StartCrack(Vector3 pos, Color glowCol)
        {
            int ci = crackIdx % cracks.Length;
            var r = cracks[crackIdx++ % cracks.Length];
            r.t.gameObject.SetActive(true);
            r.t.position = new Vector3(pos.x, 0.012f, pos.z);
            r.t.rotation = Quaternion.AngleAxis(Random.value * 360f, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
            float s = Random.Range(4f, 5.5f);
            r.t.localScale = new Vector3(s, s, 1f);
            r.age = 0f; r.life = 6f; r.active = true;
            var g = crackGlow[ci];
            g.t.gameObject.SetActive(true);
            g.t.position = new Vector3(pos.x, 0.014f, pos.z);
            g.t.rotation = r.t.rotation; g.t.localScale = r.t.localScale;
            g.age = 0f; g.life = 1.4f; g.col = glowCol; g.active = true;
        }

        static Texture2D MakeCrackTexture()
        {
            const int N = 256;
            var px = new Color32[N * N];
            var rng = new System.Random(77);
            void Dot(float fx, float fy, int rad)
            {
                int cx = (int)fx, cy = (int)fy;
                for (int dy = -rad; dy <= rad; dy++)
                    for (int dx = -rad; dx <= rad; dx++)
                    {
                        if (dx * dx + dy * dy > rad * rad) continue;
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0 || x >= N || y >= N) continue;
                        float d = Mathf.Sqrt((x - N * .5f) * (x - N * .5f) + (y - N * .5f) * (y - N * .5f)) / (N * .5f);
                        byte a = (byte)(Mathf.Clamp01(1.15f - d) * 255f);
                        if (px[y * N + x].a < a) px[y * N + x] = new Color32(255, 255, 255, a);
                    }
            }
            void Walk(float x, float y, float ang, float len, int depth)
            {
                for (float s = 0; s < len; s += 1f)
                {
                    ang += ((float)rng.NextDouble() - .5f) * 0.4f;
                    x += Mathf.Cos(ang); y += Mathf.Sin(ang);
                    Dot(x, y, (s < len * 0.5f && depth == 0) ? 2 : 1);
                    if (depth < 2 && rng.NextDouble() < 0.025)
                        Walk(x, y, ang + ((float)rng.NextDouble() - .5f) * 1.6f, len * 0.45f, depth + 1);
                }
            }
            for (int i = 0; i < 11; i++)
                Walk(N * .5f, N * .5f, i / 11f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.4f, 60f + (float)rng.NextDouble() * 60f, 0);
            Dot(N * .5f, N * .5f, 7);
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "crack", wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(px);
            t.Apply(true, false);
            return t;
        }
    }
}
