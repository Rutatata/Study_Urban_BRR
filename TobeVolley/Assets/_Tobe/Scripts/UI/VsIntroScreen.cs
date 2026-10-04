using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tobe.UI
{
    /// <summary>Pre-match "VS" splash: two team panels slide in with rosters, "VS" slams in the middle (2 s total).</summary>
    public sealed class VsIntroScreen : UiScreen
    {
        const float Total = 2.0f;

        sealed class RowAnim { public RectTransform rt; public float delay; public float side; }

        RectTransform[] panel = new RectTransform[2];
        RectTransform[] rowsRoot = new RectTransform[2];
        Text[] teamTitle = new Text[2];
        Text vs, info;
        Image dim, flash;
        readonly List<RowAnim> rows = new List<RowAnim>();
        float t = -1f;

        protected override Vector2 SlideIn { get { return Vector2.zero; } }

        protected override void Build()
        {
            dim = UiKit.Box(Rt, "Dim", new Color(0.01f, 0.012f, 0.025f, 0.7f));
            UiKit.Stretch(dim.rectTransform);

            for (int tm = 0; tm < 2; tm++)
            {
                var g = UiKit.Slant(Rt, "Team" + tm, Color.white, 90f);
                g.slantClamp = 0.99f;
                if (tm == 0) g.Set(90f, new Color(0.05f, 0.05f, 0.07f), new Color(0.92f, 0.42f, 0.04f));
                else g.Set(90f, new Color(0.88f, 0.97f, 0.98f), new Color(0.08f, 0.55f, 0.58f));
                g.SetV(Color.white, new Color(0.8f, 0.8f, 0.8f, 1f));
                UiKit.AddShadow(g.gameObject, 10f, 0.5f);
                UiKit.At(g.rectTransform, UiKit.C, new Vector2(tm == 0 ? -520 : 520, 0), new Vector2(1000, 640));
                panel[tm] = g.rectTransform;
                var lines = UiKit.SpeedLines(g.transform, "Lines", new Color(1, 1, 1, 0.2f), 16, 0.5f);
                lines.thickness = 2.5f;

                var tag = UiKit.Label(g.transform, tm == 0 ? "ДОМАШНЯЯ КОМАНДА" : "ГОСТИ", 22, Color.white, tm == 0 ? TextAnchor.UpperLeft : TextAnchor.UpperRight, FontStyle.BoldAndItalic, true, true);
                UiKit.Pad(tag.rectTransform, 130, 34, 130, 0);
                teamTitle[tm] = UiKit.Label(g.transform, TeamLook.Names[tm], 110, Color.white, tm == 0 ? TextAnchor.UpperLeft : TextAnchor.UpperRight, FontStyle.BoldAndItalic, true, true);
                UiKit.Pad(teamTitle[tm].rectTransform, 130, 64, 130, 0);
                teamTitle[tm].horizontalOverflow = HorizontalWrapMode.Overflow;
                var bar = UiKit.Slant(g.transform, "Bar", TeamLook.Trim[tm], 0f);
                bar.Set(0f, UiKit.WithA(TeamLook.Trim[tm], 1f), UiKit.WithA(TeamLook.Trim[tm], 0f));
                UiKit.At(bar.rectTransform, tm == 0 ? UiKit.TL : UiKit.TR, new Vector2(tm == 0 ? 130 : -130, -200), new Vector2(740, 6));
                if (tm == 1) bar.Set(0f, UiKit.WithA(TeamLook.Trim[tm], 0f), UiKit.WithA(TeamLook.Trim[tm], 1f));

                rowsRoot[tm] = UiKit.NewRect("Rows", g.transform);
                UiKit.At(rowsRoot[tm], UiKit.TL, new Vector2(130, -220), new Vector2(740, 400));
            }

            vs = UiKit.Label(Rt, "VS", 260, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true, true);
            UiKit.At(vs.rectTransform, UiKit.C, new Vector2(0, 20), new Vector2(520, 320));
            vs.horizontalOverflow = HorizontalWrapMode.Overflow;
            info = UiKit.Label(Rt, "", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true, true);
            UiKit.At(info.rectTransform, UiKit.C, new Vector2(0, -400), new Vector2(1200, 50));
            flash = UiKit.Box(Rt, "Flash", new Color(1, 1, 1, 0));
            UiKit.Stretch(flash.rectTransform);
        }

        /// <summary>Builds the rosters from the current match view and runs the 2 s intro.</summary>
        public void Play()
        {
            var v = GameHub.View;
            Show(true);
            for (int i = rows.Count - 1; i >= 0; i--) if (rows[i].rt != null) Destroy(rows[i].rt.gameObject);
            rows.Clear();
            int[] cnt = new int[2];
            for (int k = 0; k < v.players.Length; k++)
            {
                var p = v.players[k];
                int tm = p.team > 1 ? 1 : p.team;
                int idx = cnt[tm]++;
                if (idx >= 6) continue;
                AddRow(tm, idx, p, p.id == v.localPlayerId);
            }
            info.text = "ДО " + v.targetScore + " ОЧКОВ  ·  " + v.teamSize + "×" + v.teamSize;
            t = 0f;
            Tick();
        }

        void AddRow(int tm, int idx, PlayerSnap p, bool local)
        {
            var st = Styles.Get(p.profile.style);
            var rt = UiKit.NewRect("Row", rowsRoot[tm]);
            UiKit.AtP(rt, UiKit.TL, UiKit.TL, new Vector2(0, -idx * 62f), new Vector2(740, 54));
            var bg = UiKit.Slant(rt, "Bg", local ? UiKit.WithA(UiKit.Gold, 0.5f) : new Color(0f, 0f, 0f, 0.4f), 12f);
            UiKit.Stretch(bg.rectTransform);
            var ic = UiKit.StyleIcon(rt, p.profile.style, 44);
            UiKit.At(ic, UiKit.ML, new Vector2(tm == 0 ? 12 : 684, 0), new Vector2(44, 44));
            string nm = string.IsNullOrEmpty(p.profile.nick) ? "Игрок" : p.profile.nick;
            var name = UiKit.Label(rt, nm.ToUpperInvariant(), 30, Color.white, tm == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, FontStyle.BoldAndItalic, true, true);
            UiKit.Pad(name.rectTransform, tm == 0 ? 70 : 20, 0, tm == 0 ? 220 : 70, 0);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            var info2 = UiKit.Label(rt, "#" + p.profile.number + "  " + st.name.ToUpperInvariant(), 20, Color.white, tm == 0 ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, FontStyle.BoldAndItalic, true, true);
            UiKit.Pad(info2.rectTransform, tm == 0 ? 300 : 70, 0, tm == 0 ? 20 : 300, 0);
            info2.horizontalOverflow = HorizontalWrapMode.Overflow;
            rows.Add(new RowAnim { rt = rt, delay = 0.35f + idx * 0.07f, side = tm == 0 ? -1f : 1f });
        }

        protected override void OnHidden() { t = -1f; }

        void Update()
        {
            if (t < 0f) return;
            t += Time.unscaledDeltaTime;
            if (t >= Total) { t = -1f; Show(false); return; }
            Tick();
        }

        void Tick()
        {
            float inK = Mathf.Clamp01(t / 0.38f);
            float outK = Mathf.Clamp01((t - (Total - 0.3f)) / 0.3f);
            float e = UiKit.EaseOutBack(inK);
            for (int tm = 0; tm < 2; tm++)
            {
                float sx = tm == 0 ? -1f : 1f;
                float x = sx * 520f + sx * (1f - e) * 1300f + sx * outK * outK * 1300f;
                panel[tm].anchoredPosition = new Vector2(x, 0f);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.rt == null) continue;
                float k = Mathf.Clamp01((t - r.delay) / 0.3f);
                float ke = 1f - (1f - k) * (1f - k);
                var ap = r.rt.anchoredPosition;
                r.rt.anchoredPosition = new Vector2(r.side * (1f - ke) * 500f, ap.y);
                r.rt.localScale = Vector3.one * (k > 0f ? 1f : 0f);
            }
            float vk = Mathf.Clamp01((t - 0.3f) / 0.28f);
            float vsScale = vk <= 0f ? 0f : Mathf.Lerp(3.2f, 1f, UiKit.EaseOutBack(vk));
            vs.rectTransform.localScale = Vector3.one * (vsScale * (1f + 0.025f * Mathf.Sin(t * 8f)));
            vs.rectTransform.localRotation = Quaternion.Euler(0, 0, -6f);
            info.color = new Color(1, 1, 1, Mathf.Clamp01((t - 0.6f) / 0.25f));
            float fl = t > 0.3f && t < 0.5f ? 1f - (t - 0.3f) / 0.2f : 0f;
            flash.color = new Color(1, 1, 1, fl * 0.5f);
            dim.color = new Color(0.01f, 0.012f, 0.025f, 0.7f * (1f - outK));
            vs.color = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 1f - outK);
        }
    }
}
