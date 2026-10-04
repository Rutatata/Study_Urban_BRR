using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tobe.UI
{
    public sealed class HudScreen : UiScreen
    {
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
        Bar staminaBar, energyBar;

        // prompt
        Text promptText;
        string promptShown = "";

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
        Text pointTitle, pointReason;
        SlantGraphic pointAccent;
        float pointT = -1f;

        // hint
        RectTransform hintRoot;
        Text hintText;
        float hintT;

        protected override void Build()
        {
            BuildScoreboard();
            BuildLocalPanel();
            BuildCrosshair();

            var pr = UiKit.Label(Rt, "", 60, Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true);
            UiKit.At(pr.rectTransform, UiKit.BC, new Vector2(0, 230), new Vector2(1200, 100));
            pr.horizontalOverflow = HorizontalWrapMode.Overflow;
            promptText = pr;

            popRoot = UiKit.NewRect("Popups", Rt);
            UiKit.Stretch(popRoot);

            BuildPointBanner();
            BuildCutin();
            BuildHint();
        }

        void BuildScoreboard()
        {
            for (int t = 0; t < 2; t++)
            {
                float sign = t == 0 ? -1f : 1f;
                Color tc = TeamLook.Trim[t];
                var np = UiKit.MakePanel(Rt, "TeamPanel" + t, new Vector2(300, 56), t == 0 ? 22f : -22f, new Color(0.06f, 0.04f, 0.16f, 0.92f), tc);
                UiKit.AtP(np.rectTransform, UiKit.TC, UiKit.C, new Vector2(sign * 270f, -48f), new Vector2(300, 56));
                teamName[t] = UiKit.Label(np.transform, TeamLook.Names[t], 32, tc, TextAnchor.MiddleCenter, FontStyle.Bold);

                var sp = UiKit.MakePanel(Rt, "ScorePanel" + t, new Vector2(112, 92), t == 0 ? 22f : -22f, new Color(0.02f, 0.02f, 0.08f, 0.95f), tc);
                UiKit.AtP(sp.rectTransform, UiKit.TC, UiKit.C, new Vector2(sign * 60f, -56f), new Vector2(112, 92));
                scoreRt[t] = sp.rectTransform;
                scoreText[t] = UiKit.Label(sp.transform, "0", 70, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);

                var ball = UiKit.Circle(Rt, "Serve" + t, UiKit.Gold);
                UiKit.AtP(ball.rectTransform, UiKit.TC, UiKit.C, new Vector2(sign * 450f, -48f), new Vector2(28, 28));
                UiKit.AddOutline(ball.gameObject, Color.black, 2f);
                serveBall[t] = ball;
            }
            targetText = UiKit.Label(Rt, "до 15", 24, UiKit.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.AtP(targetText.rectTransform, UiKit.TC, UiKit.TC, new Vector2(0, -104), new Vector2(200, 30));
        }

        void BuildLocalPanel()
        {
            var p = UiKit.MakePanel(Rt, "LocalPanel", new Vector2(560, 190), 26f, new Color(0.06f, 0.04f, 0.16f, 0.9f), UiKit.Orange);
            UiKit.At(p.rectTransform, UiKit.BL, new Vector2(40, 40), new Vector2(560, 190));
            localPanel = p.rectTransform;
            localName = UiKit.Label(p.transform, "", 34, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.At(localName.rectTransform, UiKit.TL, new Vector2(40, -10), new Vector2(330, 44));
            localName.horizontalOverflow = HorizontalWrapMode.Overflow;
            localStyle = UiKit.Label(p.transform, "", 24, UiKit.Gold, TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.At(localStyle.rectTransform, UiKit.TR, new Vector2(-40, -14), new Vector2(190, 36));
            localStyle.horizontalOverflow = HorizontalWrapMode.Overflow;

            var sl = UiKit.Label(p.transform, "ВЫНОСЛИВОСТЬ", 16, UiKit.Dim, TextAnchor.MiddleLeft);
            UiKit.At(sl.rectTransform, UiKit.TL, new Vector2(40, -62), new Vector2(200, 20));
            staminaBar = UiKit.MakeBar(p.transform, new Vector2(470, 20), UiKit.Cyan, 8f);
            UiKit.At(staminaBar.Root, UiKit.TL, new Vector2(40, -82), new Vector2(470, 20));
            var el = UiKit.Label(p.transform, "ЭНЕРГИЯ", 16, UiKit.Dim, TextAnchor.MiddleLeft);
            UiKit.At(el.rectTransform, UiKit.TL, new Vector2(40, -108), new Vector2(200, 20));
            energyBar = UiKit.MakeBar(p.transform, new Vector2(470, 24), UiKit.Orange, 8f);
            UiKit.At(energyBar.Root, UiKit.TL, new Vector2(40, -128), new Vector2(470, 24));
            localPrompt = UiKit.Label(p.transform, "", 24, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.At(localPrompt.rectTransform, UiKit.BL, new Vector2(40, 6), new Vector2(480, 30));
            localPrompt.horizontalOverflow = HorizontalWrapMode.Overflow;
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
            UiKit.At(pointRoot, UiKit.C, new Vector2(0, 300), new Vector2(1200, 170));
            var bg = UiKit.MakePanel(pointRoot, "Bg", new Vector2(1200, 170), 40f, new Color(0.03f, 0.02f, 0.1f, 0.92f), Color.white);
            UiKit.Stretch(bg.rectTransform);
            pointAccent = UiKit.Slant(pointRoot, "Accent", UiKit.Orange, 40f);
            UiKit.At(pointAccent.rectTransform, UiKit.ML, new Vector2(0, 0), new Vector2(40, 170));
            pointTitle = UiKit.Label(pointRoot, "ОЧКО", 30, UiKit.Gold, TextAnchor.UpperCenter, FontStyle.Bold);
            UiKit.Pad(pointTitle.rectTransform, 0, 12, 0, 0);
            pointReason = UiKit.Label(pointRoot, "", 84, Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true);
            UiKit.Pad(pointReason.rectTransform, 60, 30, 60, 0);
            pointReason.resizeTextForBestFit = true; pointReason.resizeTextMinSize = 36; pointReason.resizeTextMaxSize = 84;
            pointRoot.gameObject.SetActive(false);
        }

        void BuildCutin()
        {
            cutRoot = UiKit.NewRect("Cutin", Rt);
            UiKit.Stretch(cutRoot);
            cutDim = UiKit.Box(cutRoot, "Dim", new Color(0.02f, 0f, 0.06f, 0.6f));
            UiKit.Stretch(cutDim.rectTransform);

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
            var p = UiKit.MakePanel(Rt, "Hint", new Vector2(700, 150), 20f, new Color(0.03f, 0.02f, 0.1f, 0.8f), UiKit.Cyan);
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
            Color tc = TeamLook.Trim[team];
            pointReason.text = string.IsNullOrEmpty(e.text) ? "ОЧКО!" : e.text.ToUpperInvariant();
            pointReason.color = tc;
            pointTitle.text = "ОЧКО · " + TeamLook.Names[team];
            pointAccent.color = tc;
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
            targetText.text = "до " + v.targetScore;
            PlayerSnap lp;
            int lt = v.TryGetLocal(out lp) ? lp.team : -1;
            for (int t = 0; t < 2; t++)
                teamName[t].text = t == lt ? TeamLook.Names[t] + " ★" : TeamLook.Names[t];
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
            staminaBar.Set(stam);
            staminaBar.Fill.color = stam < 0.25f ? UiKit.Danger : UiKit.Cyan;

            float en = Mathf.Clamp01(p.energy / 100f);
            energyBar.Set(en);
            float t = Time.unscaledTime;
            bool full = p.energy >= 99.5f;
            if (full)
            {
                energyBar.Fill.color = Color.HSVToRGB(Mathf.Repeat(t * 0.9f, 1f), 0.75f, 1f);
                float pulse = 1f + 0.06f * Mathf.Sin(t * 12f);
                energyBar.Root.localScale = new Vector3(1f, pulse, 1f);
            }
            else
            {
                energyBar.Fill.color = Color.Lerp(UiKit.Orange, UiKit.Gold, en);
                energyBar.Root.localScale = Vector3.one;
            }

            if (p.armed)
            {
                localPrompt.text = "ДОБИВАНИЕ ГОТОВО!";
                localPrompt.color = Color.Lerp(UiKit.Gold, Color.white, 0.5f + 0.5f * Mathf.Sin(t * 14f));
            }
            else
            {
                localPrompt.text = "Q — " + st.finisherName;
                localPrompt.color = full ? Color.Lerp(UiKit.Cyan, Color.white, 0.5f + 0.5f * Mathf.Sin(t * 8f)) : new Color(1, 1, 1, 0.5f);
            }
        }

        void UpdatePrompt(MatchView v)
        {
            string s = "";
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
                        case PlanKind.Receive: s = "ПРИЁМ! (ЛКМ)"; c = UiKit.Cyan; break;
                        case PlanKind.Set: s = "ПАС! (ЛКМ — в сторону прицела)"; c = UiKit.Gold; break;
                        case PlanKind.Attack:
                            if (lp.air) { s = "БЕЙ! (ЛКМ)"; c = UiKit.Orange; }
                            else if (pl.timeLeft < 0.55f) { s = "ПРЫГАЙ! (Пробел)"; c = UiKit.Orange; }
                            else { s = "АТАКА!"; c = new Color(1f, 0.8f, 0.5f); }
                            break;
                        case PlanKind.Over: s = "ПЕРЕБРОСЬ!"; c = UiKit.Cyan; break;
                    }
                    if (s.Length > 0) break;
                }
            }
            if (s != promptShown) { promptShown = s; promptText.text = s; }
            promptText.color = c;
            promptText.gameObject.SetActive(s.Length > 0);
            if (s.Length > 0)
            {
                float k = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 10f);
                promptText.rectTransform.localScale = new Vector3(k, k, 1f);
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
            float slide = Mathf.Clamp01(pointT / 0.25f);
            float x = (1f - UiKit.EaseOutBack(slide)) * 1400f;
            float a = pointT > PointLen - 0.3f ? (PointLen - pointT) / 0.3f : 1f;
            pointRoot.anchoredPosition = new Vector2(x, 300f);
            var cg = pointRoot.GetComponent<CanvasGroup>();
            if (cg == null) cg = pointRoot.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = a;
        }

        void UpdateCutin(float dt)
        {
            if (cutT < 0f) return;
            cutT += dt;
            float k = cutT / CutinLen;
            if (k >= 1f) { cutT = -1f; cutRoot.gameObject.SetActive(false); return; }
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
