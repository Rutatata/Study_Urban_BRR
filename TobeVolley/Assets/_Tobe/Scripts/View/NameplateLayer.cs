// Screen-space nameplates (Rematch style): small pills "#7 NICK" with a team color stripe above players' heads, drawn on its own overlay canvas.
// Teammates are always shown, opponents only when close to the local player (fade with distance). The local player gets no plate (a small marker
// is drawn by PlayerView). Also shows the "ЗАГРУЗКА ИГРОКОВ..." overlay while any player model is still missing.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tobe.View
{
    public sealed class NameplateLayer : MonoBehaviour
    {
        sealed class Plate
        {
            public GameObject go;
            public RectTransform rt;
            public CanvasGroup cg;
            public Image bg, stripe;
            public Text label;
            public string text;
            public float width;
            public int team = -1;
        }

        const float PlateH = 30f;

        readonly Dictionary<int, Plate> plates = new Dictionary<int, Plate>();
        readonly HashSet<int> seen = new HashSet<int>();
        readonly List<int> stale = new List<int>();
        RectTransform root;
        GameObject loadingGo;
        Text loadingText;
        Font font;

        public static NameplateLayer Create(Transform parent)
        {
            var go = new GameObject("Nameplates", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<NameplateLayer>();
        }

        void Awake()
        {
            font = Mats.JpFont;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;          // below the HUD / menus (UiRoot = 100)
            root = (RectTransform)transform;

            loadingGo = new GameObject("Loading", typeof(RectTransform));
            loadingGo.transform.SetParent(transform, false);
            var lrt = (RectTransform)loadingGo.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var dim = loadingGo.AddComponent<Image>();
            dim.color = new Color(0.03f, 0.04f, 0.09f, 0.82f);
            dim.raycastTarget = false;
            loadingText = MakeText(lrt, 44, TextAnchor.MiddleCenter);
            loadingText.text = "ЗАГРУЗКА ИГРОКОВ...";
            loadingText.color = new Color(1f, 0.9f, 0.5f);
            var trt = loadingText.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            loadingGo.SetActive(false);
        }

        Text MakeText(Transform parent, int size, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.color = Color.white;
            return t;
        }

        static Image MakeImage(Transform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var im = go.AddComponent<Image>();
            im.color = c;
            im.raycastTarget = false;
            return im;
        }

        Plate NewPlate()
        {
            var p = new Plate();
            p.go = new GameObject("Plate", typeof(RectTransform));
            p.go.transform.SetParent(root, false);
            p.rt = (RectTransform)p.go.transform;
            p.rt.pivot = new Vector2(0.5f, 0f);
            p.cg = p.go.AddComponent<CanvasGroup>();
            p.cg.blocksRaycasts = false; p.cg.interactable = false;
            p.bg = MakeImage(p.rt, "Bg", new Color(0.05f, 0.07f, 0.13f, 0.78f));
            var brt = p.bg.rectTransform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
            p.stripe = MakeImage(p.rt, "Stripe", Color.white);
            var srt = p.stripe.rectTransform; srt.anchorMin = Vector2.zero; srt.anchorMax = new Vector2(0f, 1f); srt.pivot = new Vector2(0f, 0.5f);
            srt.offsetMin = Vector2.zero; srt.offsetMax = new Vector2(5f, 0f);
            p.label = MakeText(p.rt, 18, TextAnchor.MiddleCenter);
            var lrt = p.label.rectTransform; lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = new Vector2(8f, 0f); lrt.offsetMax = Vector2.zero;
            return p;
        }

        static string ShortNick(string nick)
        {
            if (string.IsNullOrEmpty(nick)) return "";
            nick = nick.Trim();
            return nick.Length > 10 ? nick.Substring(0, 9) + ".." : nick;
        }

        void LateUpdate()
        {
            var view = GameHub.View;
            var vr = ViewRoot.Instance;
            var ph = view.phase;
            bool inMatch = ph == MatchPhase.Serve || ph == MatchPhase.Rally || ph == MatchPhase.Point;
            bool loading = vr != null && vr.PlayersLoading && (inMatch || ph == MatchPhase.Countdown);
            if (loadingGo.activeSelf != loading) loadingGo.SetActive(loading);
            if (loading) loadingGo.transform.SetAsLastSibling();

            var cam = Camera.main;
            var players = view.players;
            seen.Clear();
            if (inMatch && cam != null && players != null && vr != null)
            {
                float k = Mathf.Max(0.5f, Screen.height / 1080f);
                bool haveLocal = view.TryGetLocal(out var lp);
                for (int i = 0; i < players.Length; i++)
                {
                    var s = players[i];
                    if (s.id == view.localPlayerId) continue;
                    if (!vr.TryGetView(s.id, out var pv) || !pv.HasModel) continue;
                    Vector3 head = pv.VisualPos + Vector3.up * (pv.HeadHeight + 0.22f);
                    Vector3 sp = cam.WorldToScreenPoint(head);
                    if (sp.z < 0.5f) continue;

                    float a;
                    if (haveLocal)
                    {
                        if (s.team == lp.team) a = 1f;
                        else
                        {
                            Vector3 d = pv.VisualPos - lp.pos; d.y = 0f;
                            a = Mathf.Clamp01((9.5f - d.magnitude) / 3f);
                        }
                    }
                    else a = 1f;
                    a *= Mathf.Clamp01((38f - sp.z) / 10f);
                    if (a < 0.02f) continue;

                    seen.Add(s.id);
                    if (!plates.TryGetValue(s.id, out var pl)) { pl = NewPlate(); plates[s.id] = pl; }
                    Refresh(pl, s);
                    pl.go.SetActive(true);
                    pl.cg.alpha = a;
                    float sc = Mathf.Clamp(15f / sp.z, 0.6f, 1.05f) * k;
                    pl.rt.localScale = new Vector3(sc, sc, 1f);
                    float hw = pl.width * 0.5f * sc;
                    float x = Mathf.Clamp(sp.x, hw + 4f, Screen.width - hw - 4f);
                    float y = Mathf.Clamp(sp.y, 4f, Screen.height - PlateH * sc - 4f);
                    pl.rt.position = new Vector3(x, y, 0f);
                }
            }
            stale.Clear();
            foreach (var kv in plates)
            {
                if (seen.Contains(kv.Key)) continue;
                if (kv.Value.go.activeSelf) kv.Value.go.SetActive(false);
                if (players == null || !HasId(players, kv.Key)) stale.Add(kv.Key);
            }
            for (int i = 0; i < stale.Count; i++) { Destroy(plates[stale[i]].go); plates.Remove(stale[i]); }
        }

        static bool HasId(PlayerSnap[] arr, int id)
        {
            for (int i = 0; i < arr.Length; i++) if (arr[i].id == id) return true;
            return false;
        }

        void Refresh(Plate pl, in PlayerSnap s)
        {
            int team = Mathf.Clamp(s.team, 0, 1);
            string nick = ShortNick(s.profile.nick);
            Color accent = TeamLook.Trim[team];
            Color num = Color.Lerp(accent, Color.white, 0.25f);
            string txt = "<color=#" + ColorUtility.ToHtmlStringRGB(num) + ">" + s.profile.number + "</color>  " + nick;
            if (txt != pl.text)
            {
                pl.text = txt;
                pl.label.text = txt;
                pl.width = Mathf.Max(70f, pl.label.preferredWidth + 26f);
                pl.rt.sizeDelta = new Vector2(pl.width, PlateH);
            }
            if (pl.team != team)
            {
                pl.team = team;
                pl.stripe.color = accent;
            }
        }
    }
}
