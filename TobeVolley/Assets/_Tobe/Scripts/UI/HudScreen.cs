using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tobe.UI
{
    public sealed class HudScreen : UiScreen
    {
        protected override Vector2 SlideIn { get { return Vector2.zero; } }

        sealed class Pop
        {
            public Text text; public float born, life; public bool big;
        }

        const float CutinLen = 1.25f;
        const float PointLen = 2.2f;
        const float HintLen = 15f;

        // scoreboard
        Text[] teamName = new Text[2];
        Text[] scoreText = new Text[2];
        RectTransform[] scoreRt = new RectTransform[2];
        CircleGraphic[] serveBall = new CircleGraphic[2];
        Text targetText;
        int[] lastScore = { -1, -1 };
        float[] scorePunch = new float[2];

        // local panel
        RectTransform localPanel;
        Text localName, localStyle, localPrompt;
        SegBarGraphic staminaBar, energyBar;
        RectTransform energyRt, qChip;

        // prompt (key chip + text)
        RectTransform promptRoot, chipLkm, chipSpace;
        Text promptText;
        string promptShown = "";
        RectTransform promptChipShown;

        // popups
        RectTransform popRoot;
        readonly List<Pop> pops = new List<Pop>();

        // cut-in
        RectTransform cutRoot, bandRt;
        Image cutDim, cutFlash;
        SlantGraphic band;
        Text cutName, cutNick, cutQuote, cutNum;
        RectTransform[] lines = new RectTransform[16];
        float[] lineY = new float[16], lineLen = new float[16], lineSpd = new float[16], linePh = new float[16];
        float cutT = -1f;

        // point banner
        RectTransform pointRoot;
        Text pointTitle, pointReason, pointScore;
        SlantGraphic pointAccent, pointStripe;
        float pointT = -1f;
        int pointTeam;

        // letterbox for the cut-in
        RectTransform barTop, barBot;
        SlantGraphic band2;

        // hint
        RectTransform hintRoot;
        Text hintText;
        float hintT;

        // линии скорости по краям экрана на рывке (как в аниме при быстром беге)
        SpeedLinesGraphic dashL, dashR;
        float dashA;

        void BuildDashLines()
        {
            for (int side = 0; side < 2; side++)
            {
                var g = UiKit.SpeedLines(Rt, side == 0 ? "DashL" : "DashR", new Color(1f, 1f, 1f, 0f), 18, 0.9f);
                var rt = (RectTransform)g.transform;
                rt.anchorMin = new Vector2(side == 0 ? 0f : 0.82f, 0.1f); rt.anchorMax = new Vector2(side == 0 ? 0.18f : 1f, 0.9f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                g.seed = 11 + side * 17;
                if (side == 0) dashL = g; else dashR = g;
            }
        }

        void UpdateDashLines(MatchView v, float dt)
        {
            float want = 0f;
            if (v.TryGetLocal(out var me) && !me.air) want = Mathf.Clamp01((new Vector2(me.vel.x, me.vel.z).magnitude - 5f) / 2f) * 0.55f;
            dashA = Mathf.MoveTowards(dashA, want, dt * (want > dashA ? 3f : 1.5f));
            foreach (var g in new[] { dashL, dashR })
                if (g != null) { var c = g.color; c.a = dashA; g.color = c; g.enabled = dashA > 0.01f; }
        }

        protected override void Build()
        {
            BuildDashLines();
            BuildScoreboard();
            BuildLocalPanel();
            BuildCrosshair();

            BuildPrompt();

            popRoot = UiKit.NewRect("Popups", Rt);
            UiKit.Stretch(popRoot);

            BuildPointBanner();
            BuildCutin();
            BuildHint();
        }

        void BuildScoreboard()
        {
            // one compact scorebug: [name][score][ДО N][score][name], all parallelograms leaning the same way
            const float Y = -22f, H = 66f, SL = 18f;
            for (int t = 0; t < 2; t++)
            {
                float sign = t == 0 ? -1f : 1f;
                Color tc = TeamLook.Trim[t];
                var np = UiKit.MakePanel(Rt, "TeamPanel" + t, new Vector2(290, 50), SL, new Color(0.04f, 0.05f, 0.08f, 0.9f));
                UiKit.AtP(np.rectTransform, UiKit.TC, UiKit.TC, new Vector2(sign * 288f, Y - 8f), new Vector2(290, 50));
                var st = UiKit.AddAccent(np.transform, tc, SL, 7f);
                if (t == 1) { st.rectTransform.anchorMin = new Vector2(1, 0); st.rectTransform.anchorMax = new Vector2(1, 1); st.rectTransform.pivot = new Vector2(1, 0.5f); }
                teamName[t] = UiKit.Label(np.transform, TeamLook.Names[t], 28, Color.white, t == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, FontStyle.BoldAndItalic, false, true);
                UiKit.Pad(teamName[t].rectTransform, t == 0 ? 36 : 20, 0, t == 0 ? 20 : 36, 0);
                teamName[t].horizontalOverflow = HorizontalWrapMode.Overflow;

                var sp = UiKit.Slant(Rt, "ScorePanel" + t, tc, SL);
                sp.SetV(new Color(1.15f, 1.15f, 1.15f, 1f), new Color(0.82f, 0.82f, 0.82f, 1f));
                UiKit.AddShadow(sp.gameObject, 6f, 0.5f);
                UiKit.AtP(sp.rectTransform, UiKit.TC, UiKit.C, new Vector2(sign * 98f, Y - H * 0.5f), new Vector2(90, H));
                scoreRt[t] = sp.rectTransform;
                scoreText[t] = UiKit.Label(sp.transform, "0", 56, t == 0 ? new Color(0.04f, 0.04f, 0.06f) : Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false, true);
                scoreText[t].horizontalOverflow = HorizontalWrapMode.Overflow;
                foreach (var sh in scoreText[t].GetComponents<Shadow>()) Destroy(sh);

                var ball = UiKit.Circle(Rt, "Serve" + t, UiKit.Gold);
                UiKit.AtP(ball.rectTransform, UiKit.TC, UiKit.C, new Vector2(sign * 164f, Y - 33f), new Vector2(18, 18));
                UiKit.AddOutline(ball.gameObject, Color.black, 2f);
                serveBall[t] = ball;
            }
            var mid = UiKit.MakePanel(Rt, "Mid", new Vector2(100, 44), SL, new Color(0.02f, 0.025f, 0.04f, 0.95f));
            UiKit.AtP(mid.rectTransform, UiKit.TC, UiKit.C, new Vector2(0, Y - 33f), new Vector2(100, 44));
            targetText = UiKit.Label(mid.transform, "ДО 15", 22, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false, true);
            targetText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        void BuildLocalPanel()
        {
            var p = UiKit.MakePanel(Rt, "LocalPanel", new Vector2(520, 150), 22f, new Color(0.04f, 0.05f, 0.08f, 0.88f), UiKit.Orange);
            UiKit.At(p.rectTransform, UiKit.BL, new Vector2(40, 40), new Vector2(520, 150));
            localPanel = p.rectTransform;
            localName = UiKit.Label(p.transform, "", 30, Color.white, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic, false, true);
            UiKit.At(localName.rectTransform, UiKit.TL, new Vector2(46, -8), new Vector2(300, 38));
            localName.horizontalOverflow = HorizontalWrapMode.Overflow;
            localStyle = UiKit.Label(p.transform, "", 20, UiKit.Gold, TextAnchor.MiddleRight, FontStyle.BoldAndItalic, false, true);
            UiKit.At(localStyle.rectTransform, UiKit.TR, new Vector2(-30, -12), new Vector2(190, 30));
            localStyle.horizontalOverflow = HorizontalWrapMode.Overflow;

            var sl = UiKit.Label(p.transform, "ВЫНОС", 15, UiKit.Dim, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic, false, true);
            UiKit.At(sl.rectTransform, UiKit.TL, new Vector2(46, -50), new Vector2(90, 20));
            staminaBar = UiKit.SegBar(p.transform, new Vector2(370, 16), UiKit.Cyan, 14, 9f, 3f);
            UiKit.At(staminaBar.rectTransform, UiKit.TL, new Vector2(130, -52), new Vector2(370, 16));
            var el = UiKit.Label(p.transform, "ЭНЕРГИЯ", 15, UiKit.Dim, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic, false, true);
            UiKit.At(el.rectTransform, UiKit.TL, new Vector2(46, -76), new Vector2(90, 20));
            energyBar = UiKit.SegBar(p.transform, new Vector2(370, 22), UiKit.Orange, 12, 11f, 4f);
            UiKit.At(energyBar.rectTransform, UiKit.TL, new Vector2(130, -76), new Vector2(370, 22));
            energyRt = energyBar.rectTransform;

            qChip = UiKit.KeyChip(p.transform, "Q", 34, UiKit.Orange);
            UiKit.AtP(qChip, UiKit.BL, UiKit.BL, new Vector2(46, 12), qChip.sizeDelta);
            localPrompt = UiKit.Label(p.transform, "", 22, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic, false, true);
            UiKit.At(localPrompt.rectTransform, UiKit.BL, new Vector2(100, 12), new Vector2(400, 34));
            localPrompt.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        void BuildPrompt()
        {
            promptRoot = UiKit.NewRect("Prompt", Rt);
            UiKit.At(promptRoot, UiKit.BC, new Vector2(0, 235), new Vector2(10, 70));
            chipLkm = UiKit.KeyChip(promptRoot, "ЛКМ", 62, UiKit.Orange);
            chipSpace = UiKit.KeyChip(promptRoot, "ПРОБЕЛ", 62, UiKit.Orange);
            foreach (var c in new[] { chipLkm, chipSpace }) { c.anchorMin = c.anchorMax = new Vector2(0.5f, 0.5f); c.pivot = new Vector2(0, 0.5f); c.gameObject.SetActive(false); }
            promptText = UiKit.Label(promptRoot, "", 60, Color.white, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic, true, true);
            var pr = promptText.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f); pr.pivot = new Vector2(0, 0.5f); pr.sizeDelta = new Vector2(900, 80);
            promptText.horizontalOverflow = HorizontalWrapMode.Overflow;
            promptRoot.gameObject.SetActive(false);
        }

        void BuildCrosshair()
        {
            var root = UiKit.NewRect("Crosshair", Rt);
            UiKit.At(root, UiKit.C, Vector2.zero, new Vector2(60, 60));
            Color c = new Color(1, 1, 1, 0.85f);
            var dot = UiKit.Box(root, "Dot", c); UiKit.At(dot.rectTransform, UiKit.C, Vector2.zero, new Vector2(4, 4));
            AddTick(root, new Vector2(0, 16), new Vector2(3, 10), c);
            AddTick(root, new Vector2(0, -16), new Vector2(3, 10), c);
            AddTick(root, new Vector2(16, 0), new Vector2(10, 3), c);
            AddTick(root, new Vector2(-16, 0), new Vector2(10, 3), c);
        }

        static void AddTick(Transform p, Vector2 pos, Vector2 size, Color c)
        {
            var b = UiKit.Box(p, "Tick", c);
            UiKit.At(b.rectTransform, UiKit.C, pos, size);
            UiKit.AddOutline(b.gameObject, new Color(0, 0, 0, 0.8f), 1.5f);
        }

        void BuildPointBanner()
        {
            pointRoot = UiKit.NewRect("PointBanner", Rt);
            UiKit.At(pointRoot, UiKit.C, new Vector2(0, 300), new Vector2(1100, 150));
            var bg = UiKit.MakePanel(pointRoot, "Bg", new Vector2(1100, 150), 36f, new Color(0.03f, 0.035f, 0.06f, 0.94f));
            UiKit.Stretch(bg.rectTransform);
            pointRoot.gameObject.AddComponent<RectMask2D>();
            var lines = UiKit.SpeedLines(pointRoot, "Lines", new Color(1, 1, 1, 0.16f), 14, 0.6f);
            lines.thickness = 2f;
            pointAccent = UiKit.Slant(pointRoot, "Accent", UiKit.Orange, 36f);
            pointAccent.slantClamp = 0.99f;
            pointAccent.rectTransform.anchorMin = new Vector2(0, 0); pointAccent.rectTransform.anchorMax = new Vector2(0, 1);
            pointAccent.rectTransform.pivot = new Vector2(0, 0.5f); pointAccent.rectTransform.anchoredPosition = Vector2.zero;
            pointAccent.rectTransform.sizeDelta = new Vector2(36 + 14, 0);
            pointStripe = UiKit.Slant(pointRoot, "Stripe", UiKit.Orange, 0f);
            pointStripe.Set(0f, UiKit.WithA(UiKit.Orange, 0.45f), UiKit.WithA(UiKit.Orange, 0f));
            UiKit.At(pointStripe.rectTransform, UiKit.BL, new Vector2(50, 0), new Vector2(700, 6));
            pointTitle = UiKit.Label(pointRoot, "ОЧКО", 24, UiKit.Gold, TextAnchor.UpperLeft, FontStyle.BoldAndItalic, false, true);
            UiKit.Pad(pointTitle.rectTransform, 80, 14, 300, 0);
            pointReason = UiKit.Label(pointRoot, "", 78, Color.white, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic, true);
            UiKit.Pad(pointReason.rectTransform, 80, 36, 300, 8);
            pointReason.resizeTextForBestFit = true; pointReason.resizeTextMinSize = 32; pointReason.resizeTextMaxSize = 78;
            pointScore = UiKit.Label(pointRoot, "0 : 0", 76, Color.white, TextAnchor.MiddleRight, FontStyle.BoldAndItalic, true);
            UiKit.Pad(pointScore.rectTransform, 700, 10, 70, 10);
            pointScore.horizontalOverflow = HorizontalWrapMode.Overflow;
            pointRoot.gameObject.SetActive(false);
        }

        void BuildCutin()
        {
            cutRoot = UiKit.NewRect("Cutin", Rt);
            UiKit.Stretch(cutRoot);
            cutDim = UiKit.Box(cutRoot, "Dim", new Color(0.02f, 0f, 0.06f, 0.6f));
            UiKit.Stretch(cutDim.rectTransform);

            barTop = UiKit.Box(cutRoot, "BarTop", new Color(0, 0, 0, 0.92f)).rectTransform;
            barTop.anchorMin = new Vector2(0, 1); barTop.anchorMax = new Vector2(1, 1); barTop.pivot = new Vector2(0.5f, 1); barTop.sizeDelta = new Vector2(0, 110);
            barBot = UiKit.Box(cutRoot, "BarBot", new Color(0, 0, 0, 0.92f)).rectTransform;
            barBot.anchorMin = new Vector2(0, 0); barBot.anchorMax = new Vector2(1, 0); barBot.pivot = new Vector2(0.5f, 0); barBot.sizeDelta = new Vector2(0, 110);

            band2 = UiKit.Slant(cutRoot, "Band2", new Color(0.02f, 0.02f, 0.05f, 0.85f), 0f);
            UiKit.At(band2.rectTransform, UiKit.C, new Vector2(0, -14), new Vector2(2600, 400));
            band2.rectTransform.localRotation = Quaternion.Euler(0, 0, -3f);

            band = UiKit.Slant(cutRoot, "Band", Color.white, 0f);
            bandRt = band.rectTransform;
            UiKit.At(bandRt, UiKit.C, Vector2.zero, new Vector2(2600, 360));
            bandRt.localRotation = Quaternion.Euler(0, 0, 3.5f);
            UiKit.AddOutline(band.gameObject, Color.black, 6f);

            for (int i = 0; i < lines.Length; i++)
            {
                var l = UiKit.Box(bandRt, "Line", new Color(1, 1, 1, 0.55f));
                lines[i] = l.rectTransform;
                lineY[i] = Random.value; lineLen[i] = Random.value; lineSpd[i] = 1.5f + Random.value * 2.5f; linePh[i] = Random.value;
            }

            cutNum = UiKit.Label(bandRt, "", 300, new Color(1, 1, 1, 0.22f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            UiKit.At(cutNum.rectTransform, UiKit.C, new Vector2(-620, 0), new Vector2(600, 340));
            cutNum.horizontalOverflow = HorizontalWrapMode.Overflow;
            cutName = UiKit.Label(bandRt, "", 124, Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true);
            UiKit.At(cutName.rectTransform, UiKit.C, new Vector2(120, 45), new Vector2(1250, 170));
            cutName.resizeTextForBestFit = true; cutName.resizeTextMinSize = 40; cutName.resizeTextMaxSize = 124;
            cutNick = UiKit.Label(bandRt, "", 40, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UiKit.At(cutNick.rectTransform, UiKit.C, new Vector2(120, -55), new Vector2(1250, 54));
            cutQuote = UiKit.Label(bandRt, "", 32, Color.white, TextAnchor.MiddleCenter, FontStyle.Italic, true);
            UiKit.At(cutQuote.rectTransform, UiKit.C, new Vector2(120, -110), new Vector2(1250, 50));

            cutFlash = UiKit.Box(cutRoot, "Flash", new Color(1, 1, 1, 0));
            UiKit.Stretch(cutFlash.rectTransform);
            cutRoot.gameObject.SetActive(false);
        }

        void BuildHint()
        {
            var p = UiKit.MakePanel(Rt, "Hint", new Vector2(700, 150), 20f, new Color(0.04f, 0.05f, 0.08f, 0.82f), UiKit.Cyan);
            UiKit.At(p.rectTransform, UiKit.BR, new Vector2(-40, 40), new Vector2(700, 150));
            hintRoot = p.rectTransform;
            hintText = UiKit.Label(p.transform,
                "WASD — бег   Shift — рывок   Мышь — камера\nЛКМ — мяч (держи на подаче — сила)   Пробел — прыжок\nE — нырок   C — «Дай мне!»   Q — добивание   Esc — меню",
                20, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Pad(hintText.rectTransform, 36, 8, 20, 8);
            var cg = p.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; cg.interactable = false;
        }

        protected override void OnShown()
        {
            hintT = 0f;
            lastScore[0] = lastScore[1] = -1;
            ClearPops();
            cutT = -1f; cutRoot.gameObject.SetActive(false);
            pointT = -1f; pointRoot.gameObject.SetActive(false);
        }

        void ClearPops()
        {
            for (int i = 0; i < pops.Count; i++) if (pops[i].text != null) Destroy(pops[i].text.gameObject);
            pops.Clear();
        }

        // ---------------------------------------------------------------- events
        public void HandleEvent(GameEvent e)
        {
            if (!Visible) return;
            switch (e.type)
            {
                case GameEventType.Popup: AddPopup(e); break;
                case GameEventType.Cutin: StartCutin(e); break;
                case GameEventType.Point: StartPoint(e); break;
            }
        }

        void AddPopup(GameEvent e)
        {
            if (string.IsNullOrEmpty(e.text)) return;
            bool big = e.intArg == 1;
            if (big)
            {
                for (int i = pops.Count - 1; i >= 0; i--)
                    if (pops[i].big) { Destroy(pops[i].text.gameObject); pops.RemoveAt(i); }
            }
            Color c = e.color; if (c.a < 0.05f) c = Color.white; c.a = 1f;
            var t = UiKit.Label(popRoot, e.text.ToUpperInvariant(), big ? 150 : 46, c, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.At(t.rectTransform, UiKit.C, Vector2.zero, new Vector2(1600, big ? 220 : 70));
            pops.Add(new Pop { text = t, born = Time.unscaledTime, life = big ? 2f : 1.1f, big = big });
        }

        void StartCutin(GameEvent e)
        {
            var v = GameHub.View;
            StyleDef st = null; string nick = "", num = "";
            PlayerSnap p;
            if (v.TryGet(e.playerId, out p))
            {
                st = Styles.Get(p.profile.style);
                nick = string.IsNullOrEmpty(p.profile.nick) ? "Игрок" : p.profile.nick;
                num = p.profile.number.ToString();
            }
            Color c1 = st != null ? st.c1 : UiKit.Orange, c2 = st != null ? st.c2 : UiKit.Gold;
            band.Set(0f, c1, c2);
            cutName.text = (st != null ? st.finisherName : (e.text ?? "")).ToUpperInvariant();
            cutNick.text = st != null ? nick + " · #" + num : "";
            cutQuote.text = st != null ? "«" + st.finisherQuote + "»" : "";
            cutNum.text = num.Length > 0 ? "#" + num : "";
            // readable text on bright gradients: dark outline is already on labels
            cutT = 0f;
            cutRoot.gameObject.SetActive(true);
            Update();
        }

        void StartPoint(GameEvent e)
        {
            int team = Mathf.Clamp(e.intArg, 0, 1);
            pointTeam = team;
            Color tc = TeamLook.Trim[team];
            pointReason.text = string.IsNullOrEmpty(e.text) ? "ОЧКО!" : e.text.ToUpperInvariant();
            pointReason.color = Color.white;
            pointTitle.text = "ОЧКО · " + TeamLook.Names[team];
            pointTitle.color = tc;
            pointAccent.color = tc;
            pointStripe.Set(0f, UiKit.WithA(tc, 0.5f), UiKit.WithA(tc, 0f));
            pointScore.color = tc;
            pointT = 0f;
            pointRoot.gameObject.SetActive(true);
        }

        // ---------------------------------------------------------------- update
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var v = GameHub.View;
            UpdateScoreboard(v, dt);
            UpdateLocal(v);
            UpdatePrompt(v);
            UpdatePops();
            UpdatePoint(dt);
            UpdateCutin(dt);
            UpdateHint(dt);
            UpdateDashLines(v, dt);
        }

        void UpdateScoreboard(MatchView v, float dt)
        {
            for (int t = 0; t < 2; t++)
            {
                int s = v.score != null && v.score.Length > t ? v.score[t] : 0;
                if (s != lastScore[t]) { if (lastScore[t] >= 0) scorePunch[t] = 1f; lastScore[t] = s; scoreText[t].text = s.ToString(); }
                if (scorePunch[t] > 0f) scorePunch[t] = Mathf.Max(0f, scorePunch[t] - dt * 2.5f);
                float k = 1f + 0.5f * scorePunch[t];
                scoreRt[t].localScale = new Vector3(k, k, 1f);
                serveBall[t].gameObject.SetActive(v.servingTeam == t);
            }
            targetText.text = "ДО " + v.targetScore;
            PlayerSnap lp;
            int lt = v.TryGetLocal(out lp) ? lp.team : -1;
            for (int t = 0; t < 2; t++)
                teamName[t].text = t == lt ? TeamLook.Names[t] + "  · ВЫ" : TeamLook.Names[t];
        }

        void UpdateLocal(MatchView v)
        {
            PlayerSnap p;
            if (!v.TryGetLocal(out p)) { localPanel.gameObject.SetActive(false); return; }
            localPanel.gameObject.SetActive(true);
            var st = Styles.Get(p.profile.style);
            localName.text = (string.IsNullOrEmpty(p.profile.nick) ? "Игрок" : p.profile.nick) + "  #" + p.profile.number;
            localStyle.text = st.name.ToUpperInvariant();
            localStyle.color = Color.Lerp(st.c1, Color.white, 0.3f);

            float stam = Mathf.Clamp01(p.stamina / 100f);
            staminaBar.Value = stam;
            staminaBar.color = stam < 0.25f ? UiKit.Danger : UiKit.Cyan;

            float en = Mathf.Clamp01(p.energy / 100f);
            energyBar.Value = en;
            float t = Time.unscaledTime;
            bool full = p.energy >= 99.5f;
            if (full)
            {
                energyBar.color = Color.HSVToRGB(Mathf.Repeat(t * 0.9f, 1f), 0.7f, 1f);
                float pulse = 1f + 0.08f * Mathf.Sin(t * 12f);
                energyRt.localScale = new Vector3(1f, pulse, 1f);
            }
            else
            {
                energyBar.color = Color.Lerp(UiKit.Orange, UiKit.Gold, en);
                energyRt.localScale = Vector3.one;
            }

            if (p.armed)
            {
                localPrompt.text = "ДОБИВАНИЕ ГОТОВО!";
                localPrompt.color = Color.Lerp(UiKit.Gold, Color.white, 0.5f + 0.5f * Mathf.Sin(t * 14f));
            }
            else
            {
                localPrompt.text = st.finisherName;
                localPrompt.color = full ? Color.Lerp(UiKit.Cyan, Color.white, 0.5f + 0.5f * Mathf.Sin(t * 8f)) : new Color(1, 1, 1, 0.55f);
            }
        }

        void UpdatePrompt(MatchView v)
        {
            string s = "";
            RectTransform chip = null;
            Color c = Color.white;
            PlayerSnap lp;
            if (v.TryGetLocal(out lp) && v.plans != null)
            {
                for (int i = 0; i < v.plans.Length; i++)
                {
                    var pl = v.plans[i];
                    if (pl.kind == PlanKind.None || pl.playerId != v.localPlayerId) continue;
                    switch (pl.kind)
                    {
                        case PlanKind.Receive: s = "ПРИЁМ!"; chip = chipLkm; c = UiKit.Cyan; break;
                        case PlanKind.Set: s = "ПАС — В СТОРОНУ ПРИЦЕЛА"; chip = chipLkm; c = UiKit.Gold; break;
                        case PlanKind.Attack:
                            if (lp.air) { s = "БЕЙ!"; chip = chipLkm; c = UiKit.Orange; }
                            else if (pl.timeLeft < 0.55f) { s = "ПРЫГАЙ!"; chip = chipSpace; c = UiKit.Orange; }
                            else { s = "АТАКА!"; c = new Color(1f, 0.8f, 0.5f); }
                            break;
                        case PlanKind.Over: s = "ПЕРЕБРОСЬ!"; c = UiKit.Cyan; break;
                    }
                    if (s.Length > 0) break;
                }
            }
            if (s != promptShown || chip != promptChipShown)
            {
                promptShown = s; promptChipShown = chip; promptText.text = s;
                chipLkm.gameObject.SetActive(chip == chipLkm);
                chipSpace.gameObject.SetActive(chip == chipSpace);
                float tw = promptText.preferredWidth;
                float cw = chip != null ? chip.sizeDelta.x + 18f : 0f;
                float x0 = -(cw + tw) * 0.5f;
                if (chip != null) chip.anchoredPosition = new Vector2(x0, 0f);
                promptText.rectTransform.anchoredPosition = new Vector2(x0 + cw, 0f);
            }
            promptText.color = c;
            promptRoot.gameObject.SetActive(s.Length > 0);
            if (s.Length > 0)
            {
                float k = 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 10f);
                promptRoot.localScale = new Vector3(k, k, 1f);
            }
        }

        void UpdatePops()
        {
            float now = Time.unscaledTime;
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                if (now - pops[i].born >= pops[i].life) { Destroy(pops[i].text.gameObject); pops.RemoveAt(i); }
            }
            int rank = 0;
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var p = pops[i];
                float age = now - p.born;
                var rt = p.text.rectTransform;
                var col = p.text.color;
                if (p.big)
                {
                    float sc = age < 0.2f ? Mathf.Lerp(2.6f, 1f, UiKit.EaseOutBack(age / 0.2f)) : 1f + 0.03f * Mathf.Sin(age * 9f);
                    rt.localScale = new Vector3(sc, sc, 1f);
                    rt.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-12f, -5f, Mathf.Clamp01(age / 0.25f)));
                    rt.anchoredPosition = new Vector2(0, 150f);
                    col.a = age > p.life - 0.4f ? Mathf.Clamp01((p.life - age) / 0.4f) : 1f;
                }
                else
                {
                    float sc = age < 0.12f ? Mathf.Lerp(1.4f, 1f, age / 0.12f) : 1f;
                    rt.localScale = new Vector3(sc, sc, 1f);
                    rt.localRotation = Quaternion.identity;
                    rt.anchoredPosition = new Vector2(0, 290f + rank * 60f);
                    col.a = age > p.life - 0.3f ? Mathf.Clamp01((p.life - age) / 0.3f) : 1f;
                    rank++;
                }
                p.text.color = col;
            }
        }

        void UpdatePoint(float dt)
        {
            if (pointT < 0f) return;
            pointT += dt;
            if (pointT >= PointLen) { pointT = -1f; pointRoot.gameObject.SetActive(false); return; }
            float slide = Mathf.Clamp01(pointT / 0.28f);
            float dir = pointTeam == 0 ? -1f : 1f;
            float x = (1f - UiKit.EaseOutBack(slide)) * 1500f * dir;
            if (pointT > PointLen - 0.3f) x = (1f - (PointLen - pointT) / 0.3f) * 400f * -dir;
            float a = pointT > PointLen - 0.3f ? (PointLen - pointT) / 0.3f : 1f;
            pointRoot.anchoredPosition = new Vector2(x, 300f);
            var cg = pointRoot.GetComponent<CanvasGroup>();
            if (cg == null) cg = pointRoot.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = a;
            var v = GameHub.View;
            int s0 = v.score != null && v.score.Length > 1 ? v.score[0] : 0, s1 = v.score != null && v.score.Length > 1 ? v.score[1] : 0;
            pointScore.text = s0 + " : " + s1;
            float pk = pointT < 0.5f ? 1f + 0.18f * (1f - pointT / 0.5f) : 1f;
            pointScore.rectTransform.localScale = new Vector3(pk, pk, 1f);
        }

        void UpdateCutin(float dt)
        {
            if (cutT < 0f) return;
            cutT += dt;
            float k = cutT / CutinLen;
            if (k >= 1f) { cutT = -1f; cutRoot.gameObject.SetActive(false); return; }
            float lb = Mathf.Clamp01(Mathf.Min(k / 0.1f, (1f - k) / 0.1f));
            barTop.sizeDelta = new Vector2(0, 110f * lb); barBot.sizeDelta = new Vector2(0, 110f * lb);
            band2.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(900f, 0f, Mathf.Clamp01(k / 0.2f)), -14f);
            float slide = k < 0.15f ? 1f - k / 0.15f : 0f;
            bandRt.anchoredPosition = new Vector2(-slide * slide * 2800f, 0f);
            cutDim.color = new Color(0.02f, 0f, 0.06f, 0.6f * Mathf.Clamp01(k / 0.08f));
            float ts = k < 0.3f ? Mathf.Lerp(1.5f, 1f, UiKit.EaseOutBack(k / 0.3f)) : 1f;
            cutName.rectTransform.localScale = new Vector3(ts, ts, 1f);
            for (int i = 0; i < lines.Length; i++)
            {
                float u = Mathf.Repeat(k * lineSpd[i] + linePh[i], 1f);
                float len = 200f + lineLen[i] * 500f;
                var rt = lines[i];
                rt.sizeDelta = new Vector2(len, 3f + lineLen[i] * 6f);
                rt.anchoredPosition = new Vector2(1500f - u * 3000f, (lineY[i] - 0.5f) * 320f);
            }
            float f = k > 0.88f ? (k - 0.88f) / 0.12f : 0f;
            cutFlash.color = new Color(1, 1, 1, f);
        }

        void UpdateHint(float dt)
        {
            hintT += dt;
            if (hintT >= HintLen) { hintRoot.gameObject.SetActive(false); return; }
            hintRoot.gameObject.SetActive(true);
            var cg = hintRoot.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = hintT > HintLen - 2f ? (HintLen - hintT) / 2f : 1f;
        }
    }
}
