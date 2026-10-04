using System.Collections.Generic;
using UnityEngine;

namespace Tobe.UI
{
    /// <summary>
    /// Loads clips from Resources/Audio/&lt;name&gt; (spike, bump, set, whistle, crowd_loop, cheer, net, floor, click);
    /// any missing clip is synthesized procedurally. Plays game events through a pool of 3D sources.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        const int SampleRate = 44100;
        const int PoolSize = 16;
        static readonly string[] ClipNames = { "spike", "bump", "set", "whistle", "crowd_loop", "cheer", "net", "floor", "click" };

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        AudioSource[] pool;
        float[] poolStart;
        int poolNext;
        AudioSource ui, crowd;
        float crowdVol;

        public static AudioManager Create()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("TobeAudio");
            DontDestroyOnLoad(go);
            return go.AddComponent<AudioManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            for (int i = 0; i < ClipNames.Length; i++)
            {
                string n = ClipNames[i];
                AudioClip c = null;
                try { c = Resources.Load<AudioClip>("Audio/" + n); } catch (System.Exception) { }
                // crowd sounds are never synthesized (noise sounds like surf); they play only when real recordings are provided
                if (c == null && n != "crowd_loop" && n != "cheer") c = Synth(n);
                clips[n] = c;
            }

            pool = new AudioSource[PoolSize];
            poolStart = new float[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var s = new GameObject("Sfx" + i).AddComponent<AudioSource>();
                s.transform.SetParent(transform, false);
                s.playOnAwake = false; s.spatialBlend = 0.6f; s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 6f; s.maxDistance = 60f; s.dopplerLevel = 0f;
                pool[i] = s;
            }
            ui = gameObject.AddComponent<AudioSource>();
            ui.playOnAwake = false; ui.spatialBlend = 0f;

            crowd = new GameObject("Crowd").AddComponent<AudioSource>();
            crowd.transform.SetParent(transform, false);
            crowd.clip = clips["crowd_loop"]; crowd.loop = true; crowd.spatialBlend = 0f; crowd.volume = 0f;
            crowd.playOnAwake = false;
            if (crowd.clip != null) crowd.Play();
        }

        void OnEnable() { GameHub.OnEvent += OnEvent; }
        void OnDisable() { GameHub.OnEvent -= OnEvent; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            var ph = GameHub.View.phase;
            bool inMatch = ph == MatchPhase.Serve || ph == MatchPhase.Rally || ph == MatchPhase.Point;
            float target = inMatch ? 0.32f : 0.12f;
            crowdVol = Mathf.MoveTowards(crowdVol, target, Time.unscaledDeltaTime * 0.3f);
            if (crowd != null) { crowd.volume = crowdVol; if (!crowd.isPlaying && crowd.clip != null) crowd.Play(); }
        }

        // ------------------------------------------------------------ playback
        public void PlayClick()
        {
            AudioClip c;
            if (ui != null && clips.TryGetValue("click", out c) && c != null) ui.PlayOneShot(c, 0.6f);
        }

        public void Play(string name, Vector3 pos, float volume = 1f, float pitch = 1f, bool spatial = true)
        {
            AudioClip c;
            if (string.IsNullOrEmpty(name) || !clips.TryGetValue(name, out c) || c == null) return;
            AudioSource s = null;
            for (int k = 0; k < PoolSize; k++)
            {
                int i = (poolNext + k) % PoolSize;
                if (!pool[i].isPlaying) { s = pool[i]; poolNext = (i + 1) % PoolSize; break; }
            }
            if (s == null)
            {
                int oldest = 0;
                for (int i = 1; i < PoolSize; i++) if (poolStart[i] < poolStart[oldest]) oldest = i;
                s = pool[oldest];
            }
            int idx = System.Array.IndexOf(pool, s);
            poolStart[idx] = Time.unscaledTime;
            s.transform.position = pos;
            s.spatialBlend = spatial ? 0.6f : 0f;
            s.clip = c; s.volume = Mathf.Clamp01(volume); s.pitch = pitch;
            s.Play();
        }

        void OnEvent(GameEvent e)
        {
            float rp = 0.94f + Random.value * 0.12f;
            switch (e.type)
            {
                case GameEventType.Sound:
                    Play(e.text, e.pos, e.floatArg > 0f ? e.floatArg : 1f, rp);
                    break;
                case GameEventType.Hit:
                    {
                        float vol = Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(e.floatArg));
                        switch ((PoseId)e.intArg)
                        {
                            case PoseId.Bump:
                            case PoseId.Dive: Play("bump", e.pos, vol, rp); break;
                            case PoseId.Set: Play("set", e.pos, vol, rp); break;
                            case PoseId.Spike:
                            case PoseId.ServeHit: Play("spike", e.pos, vol, rp); break;
                        }
                        break;
                    }
                case GameEventType.Impact:
                    Play("floor", e.pos, e.intArg == 1 ? 1f : 0.55f, e.intArg == 1 ? 0.8f : rp);
                    break;
                case GameEventType.Block:
                    Play("spike", e.pos, 0.8f, 0.75f);
                    Play("net", e.pos, 0.6f, rp);
                    break;
                case GameEventType.Whistle:
                    Play("whistle", Vector3.zero, 0.8f, 1f, false);
                    break;
                case GameEventType.Point:
                    Play("cheer", Vector3.zero, 0.7f, 1f, false);
                    break;
            }
        }

        // ------------------------------------------------------------ synthesis
        static AudioClip Synth(string name)
        {
            switch (name)
            {
                case "spike": return Make(name, 0.32f, (t, d, r) =>
                    {
                        float env = Mathf.Exp(-t * 14f);
                        float tone = Mathf.Sin(2 * Mathf.PI * (Mathf.Lerp(260f, 70f, Mathf.Min(1f, t * 6f)) * t));
                        return (r.Next(-1000, 1000) / 1000f * 0.7f * Mathf.Exp(-t * 28f) + tone * 0.8f) * env;
                    });
                case "bump": return Make(name, 0.22f, (t, d, r) =>
                    {
                        float env = Mathf.Exp(-t * 20f);
                        float tone = Mathf.Sin(2 * Mathf.PI * (Mathf.Lerp(190f, 90f, Mathf.Min(1f, t * 8f)) * t));
                        return (tone * 0.9f + r.Next(-1000, 1000) / 1000f * 0.25f * Mathf.Exp(-t * 60f)) * env;
                    });
                case "set": return Make(name, 0.16f, (t, d, r) =>
                    {
                        float env = Mathf.Exp(-t * 28f);
                        float tone = Mathf.Sin(2 * Mathf.PI * 520f * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * 780f * t) * 0.25f;
                        return (tone + r.Next(-1000, 1000) / 1000f * 0.2f * Mathf.Exp(-t * 90f)) * env;
                    });
                case "whistle": return Make(name, 0.6f, (t, d, r) =>
                    {
                        float env = Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((d - t) / 0.08f);
                        float trill = 1f + 0.35f * Mathf.Sin(2 * Mathf.PI * 32f * t) * 0.5f;
                        float ph = 2 * Mathf.PI * 2850f * t;
                        return (Mathf.Sin(ph * trill) * 0.5f + Mathf.Sin(ph * 2.01f) * 0.12f + r.Next(-1000, 1000) / 1000f * 0.06f) * env * 0.7f;
                    });
                case "crowd_loop": return MakeCrowd(name);
                case "cheer": return Make(name, 1.6f, (t, d, r) =>
                    {
                        float env = Mathf.Clamp01(t / 0.25f) * Mathf.Exp(-Mathf.Max(0, t - 0.4f) * 1.8f);
                        float n = r.Next(-1000, 1000) / 1000f;
                        float mod = 0.6f + 0.4f * Mathf.Sin(2 * Mathf.PI * 7f * t);
                        return n * mod * env * 0.55f;
                    }, true);
                case "net": return Make(name, 0.3f, (t, d, r) =>
                    {
                        float env = Mathf.Exp(-t * 12f);
                        float rattle = 0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * 45f * t);
                        return r.Next(-1000, 1000) / 1000f * rattle * env * 0.5f + Mathf.Sin(2 * Mathf.PI * 330f * t) * 0.1f * env;
                    }, true);
                case "floor": return Make(name, 0.28f, (t, d, r) =>
                    {
                        float env = Mathf.Exp(-t * 16f);
                        float tone = Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(120f, 55f, Mathf.Min(1f, t * 8f)) * t);
                        return (tone * 0.9f + r.Next(-1000, 1000) / 1000f * 0.3f * Mathf.Exp(-t * 50f)) * env;
                    });
                case "click":
                default: return Make(name, 0.07f, (t, d, r) =>
                    {
                        float env = Mathf.Exp(-t * 55f);
                        return (Mathf.Sin(2 * Mathf.PI * 1000f * t) * 0.6f + Mathf.Sin(2 * Mathf.PI * 1500f * t) * 0.3f) * env * 0.7f;
                    });
            }
        }

        delegate float Gen(float t, float duration, System.Random rng);

        static AudioClip Make(string name, float dur, Gen g, bool smooth = false)
        {
            int n = Mathf.Max(1, (int)(dur * SampleRate));
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float v = g(i / (float)SampleRate, dur, rng);
                if (smooth) { lp += (v - lp) * 0.35f; v = lp * 1.6f; }
                data[i] = Mathf.Clamp(v, -1f, 1f);
            }
            var c = AudioClip.Create(name, n, 1, SampleRate, false);
            c.SetData(data, 0);
            return c;
        }

        static AudioClip MakeCrowd(string name)
        {
            const float dur = 4f, fade = 0.5f;
            int n = (int)(dur * SampleRate), f = (int)(fade * SampleRate);
            var raw = new float[n + f];
            var rng = new System.Random(1234);
            float lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)SampleRate;
                float w = rng.Next(-1000, 1000) / 1000f;
                lp1 += (w - lp1) * 0.06f;
                lp2 += (lp1 - lp2) * 0.25f;
                float mod = 0.7f + 0.2f * Mathf.Sin(2 * Mathf.PI * 0.5f * t) + 0.1f * Mathf.Sin(2 * Mathf.PI * 1.25f * t);
                raw[i] = lp2 * 3.2f * mod;
            }
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (i < f) { float k = i / (float)f; data[i] = Mathf.Lerp(raw[n + i], raw[i], k); }
                else data[i] = raw[i];
                data[i] = Mathf.Clamp(data[i], -1f, 1f);
            }
            var c = AudioClip.Create(name, n, 1, SampleRate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
