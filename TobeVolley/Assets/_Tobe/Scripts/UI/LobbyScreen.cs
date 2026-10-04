using UnityEngine;
using UnityEngine.UI;

namespace Tobe.UI
{
    public sealed class LobbyScreen : UiScreen
    {
        const int MaxRows = 6;

        sealed class Row
        {
            public SlantGraphic bg;
            public Text name, style, bot;
            public Outline outline;
        }

        Text codeText, modeText, statusText, countText, copiedText;
        Row[,] rows = new Row[2, MaxRows];
        Text[] teamTitle = new Text[2];
        float copiedT = -10f;

        protected override void Build()
        {
            var bg = UiKit.Box(Rt, "Bg", new Color(0.02f, 0.025f, 0.04f, 0.84f), true);
            UiKit.Stretch(bg.rectTransform);
            UiKit.SpeedLines(Rt, "Speed", new Color(1f, 0.6f, 0.2f, 0.14f), 20, 0.12f);

            var title = UiKit.Label(Rt, "ЛОББИ", 64, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic, true);
            UiKit.At(title.rectTransform, UiKit.TL, new Vector2(80, -20), new Vector2(600, 100));

            // code box
            var cp = UiKit.MakePanel(Rt, "CodePanel", new Vector2(720, 100), 24f, UiKit.Panel, UiKit.Gold);
            UiKit.At(cp.rectTransform, UiKit.TR, new Vector2(-70, -30), new Vector2(720, 100));
            var cl = UiKit.Label(cp.transform, "КОД СЕССИИ", 20, UiKit.Gold, TextAnchor.UpperLeft);
            UiKit.Pad(cl.rectTransform, 40, 8, 10, 0);
            codeText = UiKit.Label(cp.transform, "", 56, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold, true);
            UiKit.Pad(codeText.rectTransform, 40, 24, 300, 0);
            var cb = UiKit.MakeButton(cp.transform, "СКОПИРОВАТЬ", new Vector2(250, 56), UiKit.Cyan, CopyCode, 22);
            UiKit.At(cb.GetComponent<RectTransform>(), UiKit.MR, new Vector2(-34, 0), new Vector2(250, 56));
            UiKit.SetButtonColor(cb, UiKit.Cyan, UiKit.Navy);
            copiedText = UiKit.Label(Rt, "", 22, UiKit.Cyan, TextAnchor.MiddleRight);
            UiKit.At(copiedText.rectTransform, UiKit.TR, new Vector2(-70, -134), new Vector2(720, 30));

            modeText = UiKit.Label(Rt, "", 36, UiKit.Cyan, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.At(modeText.rectTransform, UiKit.TL, new Vector2(84, -112), new Vector2(600, 50));

            // team columns
            for (int t = 0; t < 2; t++)
            {
                float cx = t == 0 ? -400f : 400f;
                Color tc = TeamLook.Trim[t];
                var col = UiKit.MakePanel(Rt, "Team" + t, new Vector2(700, 600), 28f, UiKit.Panel, tc);
                UiKit.At(col.rectTransform, UiKit.TC, new Vector2(cx, -190), new Vector2(700, 600));
                var head = UiKit.Slant(col.transform, "Head", tc, 28f);
                UiKit.At(head.rectTransform, UiKit.TL, Vector2.zero, new Vector2(700, 66));
                teamTitle[t] = UiKit.Label(head.transform, TeamLook.Names[t], 40, t == 0 ? Color.black : Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                for (int i = 0; i < MaxRows; i++)
                {
                    var r = new Row();
                    r.bg = UiKit.Slant(col.transform, "Row" + i, new Color(1, 1, 1, 0.07f), 16f);
                    UiKit.At(r.bg.rectTransform, UiKit.TL, new Vector2(34, -84 - i * 84), new Vector2(632, 72));
                    r.outline = r.bg.gameObject.AddComponent<Outline>();
                    r.outline.effectColor = UiKit.Gold; r.outline.effectDistance = new Vector2(3, -3); r.outline.enabled = false;
                    r.name = UiKit.Label(r.bg.transform, "", 30, Color.white, TextAnchor.MiddleLeft);
                    UiKit.Pad(r.name.rectTransform, 30, 0, 250, 0);
                    r.style = UiKit.Label(r.bg.transform, "", 22, UiKit.Cyan, TextAnchor.MiddleRight, FontStyle.Normal);
                    UiKit.Pad(r.style.rectTransform, 300, 0, 40, 0);
                    r.bot = UiKit.Label(r.bg.transform, "БОТ", 16, UiKit.Orange, TextAnchor.UpperRight);
                    UiKit.Pad(r.bot.rectTransform, 0, 3, 36, 0);
                    r.bot.gameObject.SetActive(false);
                    rows[t, i] = r;
                }
            }

            var vs = UiKit.Label(Rt, "VS", 80, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true);
            UiKit.At(vs.rectTransform, UiKit.TC, new Vector2(0, -400), new Vector2(200, 120));

            countText = UiKit.Label(Rt, "", 150, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UiKit.At(countText.rectTransform, UiKit.TC, new Vector2(0, -250), new Vector2(300, 200));

            statusText = UiKit.Label(Rt, "", 32, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.At(statusText.rectTransform, UiKit.BC, new Vector2(0, 160), new Vector2(1400, 50));

            var go = UiKit.MakeButton(Rt, "НАЧАТЬ СЕЙЧАС (боты займут места)", new Vector2(780, 76), UiKit.Orange, () => { if (NetApi.StartNow != null) NetApi.StartNow(); }, 30);
            UiKit.At(go.GetComponent<RectTransform>(), UiKit.BC, new Vector2(-250, 52), new Vector2(780, 76));
            var lv = UiKit.MakeButton(Rt, "ПОКИНУТЬ", new Vector2(380, 76), new Color(0.55f, 0.15f, 0.25f), () => { if (NetApi.Leave != null) NetApi.Leave(); }, 32);
            UiKit.At(lv.GetComponent<RectTransform>(), UiKit.BC, new Vector2(340, 52), new Vector2(380, 76));
        }

        void CopyCode()
        {
            GUIUtility.systemCopyBuffer = NetApi.SessionCode ?? "";
            copiedText.text = "Скопировано!";
            copiedT = Time.unscaledTime;
        }

        void Update()
        {
            var v = GameHub.View;
            string code = NetApi.SessionCode;
            codeText.text = string.IsNullOrEmpty(code) ? "—" : code;
            modeText.text = "РЕЖИМ  " + v.teamSize + "×" + v.teamSize;
            statusText.text = v.lobbyStatus ?? "";
            if (v.phase == MatchPhase.Countdown) countText.text = Mathf.Max(1, Mathf.CeilToInt(v.phaseTime)).ToString();
            else countText.text = "";
            if (copiedText.text.Length > 0 && Time.unscaledTime - copiedT > 2f) copiedText.text = "";

            int size = Mathf.Clamp(v.teamSize, 1, MaxRows);
            var pl = v.players;
            int[] cnt = new int[2];
            for (int t = 0; t < 2; t++)
                for (int i = 0; i < MaxRows; i++) rows[t, i].bg.gameObject.SetActive(i < size);

            for (int k = 0; k < pl.Length; k++)
            {
                var p = pl[k];
                int t = p.team > 1 ? 1 : p.team;
                int i = cnt[t]++;
                if (i >= size) continue;
                var r = rows[t, i];
                bool local = p.id == v.localPlayerId;
                var st = Styles.Get(p.profile.style);
                string nm = string.IsNullOrEmpty(p.profile.nick) ? "Игрок" : p.profile.nick;
                r.name.text = nm + "  #" + p.profile.number;
                r.name.color = p.isBot ? new Color(1, 1, 1, 0.7f) : Color.white;
                r.style.text = st.name;
                r.style.color = Color.Lerp(st.c1, Color.white, 0.4f);
                r.bot.gameObject.SetActive(p.isBot);
                r.bg.color = local ? new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.28f) : new Color(1, 1, 1, 0.09f);
                r.outline.enabled = local;
            }
            for (int t = 0; t < 2; t++)
                for (int i = cnt[t]; i < size; i++)
                {
                    var r = rows[t, i];
                    r.name.text = "— ожидание —"; r.name.color = new Color(1, 1, 1, 0.3f);
                    r.style.text = ""; r.bot.gameObject.SetActive(false);
                    r.bg.color = new Color(1, 1, 1, 0.04f); r.outline.enabled = false;
                }
        }
    }
}
