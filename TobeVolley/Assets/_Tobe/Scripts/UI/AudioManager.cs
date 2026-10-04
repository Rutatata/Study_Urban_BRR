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
        static readonly string[] ClipNames = { "spike", "bump", "set", "whistle", "crowd_loop", "cheer", "net", "floor", "click", "squeak" };

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
                // настоящие записи: петля трибун склеивается, у остальных срезается тишина; у коротких ударов берётся только первый удар
                if (c != null) c = n == "crowd_loop" ? SeamlessLoop(c) : TrimSilence(c, n != "whistle" && n != "cheer");
                else if (n != "crowd_loop" && n != "cheer" && n != "squeak") c = Synth(n);
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
            if (inMatch) Squeaks(Time.unscaledDeltaTime);
        }

        // ------------------------------------------------------------ скрип кроссовок
        // Резкое торможение или смена направления на полу (а не плавный разгон) даёт короткий скрип у ног игрока.
        struct Feet { public Vector2 vSlow; public float cooldown; public bool init; }
        readonly Dictionary<byte, Feet> feet = new Dictionary<byte, Feet>();

        void Squeaks(float dt)
        {
            if (dt <= 0f || !clips.TryGetValue("squeak", out var sq) || sq == null) return;
            var players = GameHub.View.players;
            if (players == null) return;
            var cam = Camera.main;
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                feet.TryGetValue(p.id, out var f);
                var v = new Vector2(p.vel.x, p.vel.z);
                if (!f.init) { f.vSlow = v; f.init = true; }
                f.cooldown -= dt;
                // «медленная» скорость отстаёт от настоящей примерно на 0,12 с: их разница = насколько резко поменялось движение
                f.vSlow = Vector2.Lerp(f.vSlow, v, 1f - Mathf.Exp(-dt / 0.12f));
                float jerk = (v - f.vSlow).magnitude;
                bool braking = f.vSlow.magnitude > 2.6f && (v.magnitude < f.vSlow.magnitude * 0.6f || Vector2.Dot(v, f.vSlow) < 0f);
                if (!p.air && f.cooldown <= 0f && jerk > 2.2f && braking)
                {
                    float dist = cam != null ? Vector3.Distance(cam.transform.position, p.pos) : 10f;
                    float vol = Mathf.Clamp01(jerk / 6f) * Mathf.Lerp(0.55f, 0.18f, Mathf.InverseLerp(5f, 25f, dist));
                    Play("squeak", p.pos, vol, 0.9f + Random.value * 0.25f);
                    f.cooldown = 0.45f;
                }
                feet[p.id] = f;
            }
        }

        // ------------------------------------------------------------ обработка настоящих записей
        /// <summary>Срезает тишину в начале (иначе удар звучит с опозданием) и почти полную тишину в конце.
        /// firstHitOnly: в файле может быть несколько ударов / скрипов подряд, тогда остаётся только первый (до паузы длиннее 120 мс).
        /// Если данные недоступны, возвращает клип как есть.</summary>
        static AudioClip TrimSilence(AudioClip c, bool firstHitOnly)
        {
            if (!ReadAll(c, out var data)) return c;
            int ch = c.channels, frames = data.Length / ch, sr = c.frequency;
            float peak = 0f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            if (peak < 1e-4f) return c;
            int first = 0, last = frames - 1;
            while (first < frames && MaxAbs(data, first, ch) < peak * 0.04f) first++;
            while (last > first && MaxAbs(data, last, ch) < peak * 0.003f) last--;   // около -50 дБ: дальше только тишина
            if (firstHitOnly)
            {
                // огибающая окнами по 10 мс: конец первого события = начало паузы ниже 3 % пика длиной от 120 мс
                int win = sr / 100, quiet = 0;
                for (int w = first + sr / 25; w + win < last; w += win)
                {
                    float m = 0f;
                    for (int k = 0; k < win; k++) m = Mathf.Max(m, MaxAbs(data, w + k, ch));
                    if (m < peak * 0.03f) { if (++quiet * win >= sr * 0.12f) { last = w - (quiet - 1) * win; break; } }
                    else quiet = 0;
                }
            }
            first = Mathf.Max(0, first - sr / 500);                                  // 2 мс запаса перед атакой
            last = Mathf.Min(frames - 1, last + sr / 20);                            // 50 мс хвоста
            if (first < c.frequency / 100 && frames - 1 - last < c.frequency / 20) return c;   // резать почти нечего
            int n = last - first + 1, fade = Mathf.Min(n / 4, c.frequency / 50);  // 20 мс затухания в конце, чтобы не щёлкало
            var outData = new float[n * ch];
            System.Array.Copy(data, first * ch, outData, 0, n * ch);
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                for (int s = 0; s < ch; s++) outData[(n - 1 - i) * ch + s] *= k;
            }
            var res = AudioClip.Create(c.name, n, ch, c.frequency, false);
            res.SetData(outData, 0);
            return res;
        }

        /// <summary>Петля трибун: последние 1,5 с плавно перетекают в начало, поэтому на повторе нет щелчка и скачка громкости.</summary>
        static AudioClip SeamlessLoop(AudioClip c)
        {
            if (!ReadAll(c, out var data)) return c;
            int ch = c.channels, frames = data.Length / ch;
            int fade = Mathf.Min(frames / 4, (int)(c.frequency * 1.5f));
            if (fade < 64) return c;
            int n = frames - fade;
            var outData = new float[n * ch];
            for (int i = 0; i < n; i++)
            {
                for (int s = 0; s < ch; s++)
                {
                    float v = data[i * ch + s];
                    if (i < fade)
                    {   // равномощное смешивание хвоста (n + i) и начала (i)
                        float k = i / (float)fade;
                        v = v * Mathf.Sin(k * Mathf.PI * 0.5f) + data[(n + i) * ch + s] * Mathf.Cos(k * Mathf.PI * 0.5f);
                    }
                    outData[i * ch + s] = v;
                }
            }
            var res = AudioClip.Create(c.name, n, ch, c.frequency, false);
            res.SetData(outData, 0);
            return res;
        }

        static bool ReadAll(AudioClip c, out float[] data)
        {
            data = null;
            try
            {
                if (c.loadType != AudioClipLoadType.DecompressOnLoad) return false;   // у потоковых клипов сэмплы не читаются
                if (c.loadState != AudioDataLoadState.Loaded) c.LoadAudioData();
                if (c.loadState != AudioDataLoadState.Loaded || c.samples <= 0) return false;
                data = new float[c.samples * c.channels];
                return c.GetData(data, 0);
            }
            catch (System.Exception e) { Debug.LogWarning("[Tobe] не удалось прочитать звук " + c.name + ": " + e.Message); return false; }
        }

        static float MaxAbs(float[] d, int frame, int ch)
        {
            float m = 0f;
            for (int s = 0; s < ch; s++) m = Mathf.Max(m, Mathf.Abs(d[frame * ch + s]));
            return m;
        }

        /// <summary>Для меню разработчика: какие звуки загружены и их длительность после обработки.</summary>
        public string Describe()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var kv in clips)
                sb.AppendLine(kv.Value == null ? $"{kv.Key}: нет" : $"{kv.Key}: {kv.Value.length:F2} с, {kv.Value.channels} кан., {kv.Value.frequency} Гц");
            return sb.ToString();
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
