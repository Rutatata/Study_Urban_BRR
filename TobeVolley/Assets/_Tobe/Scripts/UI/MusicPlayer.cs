// Музыка: треки кладутся в StreamingAssets/Music/menu (меню и раздевалка) и StreamingAssets/Music/match (матч) —
// .ogg / .mp3 / .wav, любое число. Играют по кругу в случайном порядке, между меню и матчем — плавная смена,
// на аниме-вступлении и паузе после очка музыка приглушается. Громкость — PlayerPrefs «tobe.music» (0..1).
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Tobe.UI
{
    public sealed class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Instance { get; private set; }
        public static float Volume = 0.55f;

        readonly Dictionary<string, List<AudioClip>> sets = new Dictionary<string, List<AudioClip>>();
        AudioSource a, b;          // две дорожки для плавной смены
        string playing = "";
        int lastIdx = -1;

        public static void Create()
        {
            if (Instance != null) return;
            var go = new GameObject("TobeMusic"); DontDestroyOnLoad(go);
            Instance = go.AddComponent<MusicPlayer>();
        }

        void Awake()
        {
            try { Volume = PlayerPrefs.GetFloat("tobe.music", Volume); } catch (System.Exception) { }
            a = gameObject.AddComponent<AudioSource>(); b = gameObject.AddComponent<AudioSource>();
            foreach (var s in new[] { a, b }) { s.playOnAwake = false; s.loop = false; s.spatialBlend = 0f; s.volume = 0f; }
            StartCoroutine(LoadSet("menu")); StartCoroutine(LoadSet("match"));
        }

        IEnumerator LoadSet(string name)
        {
            var list = new List<AudioClip>(); sets[name] = list;
            string dir = Path.Combine(Application.streamingAssetsPath, "Music", name);
            if (!Directory.Exists(dir)) yield break;
            foreach (var f in Directory.GetFiles(dir))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                var type = ext == ".ogg" ? AudioType.OGGVORBIS : ext == ".mp3" ? AudioType.MPEG : ext == ".wav" ? AudioType.WAV : AudioType.UNKNOWN;
                if (type == AudioType.UNKNOWN) continue;
                using (var req = UnityWebRequestMultimedia.GetAudioClip("file:///" + f.Replace('\\', '/'), type))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success) { var c = DownloadHandlerAudioClip.GetContent(req); c.name = Path.GetFileNameWithoutExtension(f); list.Add(c); }
                    else Debug.LogWarning("[Tobe] трек не загрузился: " + f + " — " + req.error);
                }
            }
        }

        void Update()
        {
            var ph = GameHub.View.phase;
            bool match = NetApi.InSession && (ph == MatchPhase.Serve || ph == MatchPhase.Rally || ph == MatchPhase.Point);
            string want = match && sets.TryGetValue("match", out var ml) && ml.Count > 0 ? "match" : "menu";
            bool loaded = sets.TryGetValue(want, out var wl) && wl.Count > 0;
            if (want != playing || a.clip == null && loaded || !a.isPlaying && a.volume > 0.01f || a.clip != null && a.isPlaying && a.time > a.clip.length - 1.5f) Next(want);
            float duck = Tobe.View.CameraRig.IntroActive ? 0.35f : ph == MatchPhase.Point ? 0.6f : 1f;
            float dt = Time.unscaledDeltaTime;
            a.volume = Mathf.MoveTowards(a.volume, Volume * duck * (a.clip != null ? 1f : 0f), dt / 1.5f);
            b.volume = Mathf.MoveTowards(b.volume, 0f, dt / 1.5f);
            if (b.volume <= 0f && b.isPlaying) b.Stop();
        }

        void Next(string set)
        {
            if (!sets.TryGetValue(set, out var list) || list.Count == 0) { playing = set; return; }
            int i = list.Count == 1 ? 0 : (lastIdx + 1 + Random.Range(0, list.Count - 1)) % list.Count;   // не тот же трек подряд
            lastIdx = i; playing = set;
            var t = a; a = b; b = t;   // старая дорожка затихает, новая нарастает
            a.clip = list[i]; a.volume = 0f; a.Play();
        }

        public static void SetVolume(float v)
        {
            Volume = Mathf.Clamp01(v);
            try { PlayerPrefs.SetFloat("tobe.music", Volume); PlayerPrefs.Save(); } catch (System.Exception) { }
        }
    }
}
