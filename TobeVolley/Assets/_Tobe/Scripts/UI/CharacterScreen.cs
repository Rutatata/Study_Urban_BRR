using UnityEngine;
using UnityEngine.UI;

namespace Tobe.UI
{
    public sealed class CharacterScreen : UiScreen
    {
        PlayerProfile draft;
        InputField nick;
        Text modelText, numberText, savedText;
        SlantGraphic[] cardBg;
        Outline[] cardOutline;
        RectTransform[] cardRt;
        RectTransform[] swatchRt;
        Outline[] swatchOutline;
        float savedT = -10f;

        static readonly string[] StatLabels = { "Атака", "Прыжок", "Приём", "Пас", "Блок", "Подача", "Скорость" };

        static float[] StatValues(Stats s) { return new[] { s.spike, s.jump, s.receive, s.set, s.block, s.serve, s.speed }; }

        protected override void Build()
        {
            var bg = UiKit.Box(Rt, "Bg", new Color(0.03f, 0.02f, 0.09f, 0.97f), true);
            UiKit.Stretch(bg.rectTransform);
            var stripe = UiKit.Slant(Rt, "Stripe", new Color(UiKit.Orange.r, UiKit.Orange.g, UiKit.Orange.b, 0.18f), 200f);
            UiKit.At(stripe.rectTransform, UiKit.TL, new Vector2(-100, 0), new Vector2(700, 140));

            var title = UiKit.Label(Rt, "ПЕРСОНАЖ", 64, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic, true);
            UiKit.At(title.rectTransform, UiKit.TL, new Vector2(80, -20), new Vector2(900, 100));

            int n = Styles.All.Length;
            cardBg = new SlantGraphic[n]; cardOutline = new Outline[n]; cardRt = new RectTransform[n];
            float cw = 330, gap = 18, x0 = (1920 - (n * cw + (n - 1) * gap)) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                BuildCard(Styles.All[i], idx, x0 + i * (cw + gap), -130f, cw, 590f);
            }

            // customization row
            float cy = -745f;
            var panel = UiKit.MakePanel(Rt, "CustomPanel", new Vector2(1780, 200), 30f, UiKit.Panel, UiKit.Cyan);
            UiKit.At(panel.rectTransform, UiKit.TC, new Vector2(0, cy), new Vector2(1780, 200));

            var nl = UiKit.Label(panel.transform, "НИК", 22, UiKit.Cyan, TextAnchor.UpperLeft);
            UiKit.At(nl.rectTransform, UiKit.TL, new Vector2(60, -16), new Vector2(300, 30));
            nick = UiKit.MakeInput(panel.transform, "Ник", new Vector2(380, 64), 32, 14);
            UiKit.At(nick.GetComponent<RectTransform>(), UiKit.TL, new Vector2(60, -50), new Vector2(380, 64));
            nick.onValueChanged.AddListener(s => draft.nick = s);

            var ml = UiKit.Label(panel.transform, "МОДЕЛЬ", 22, UiKit.Cyan, TextAnchor.UpperLeft);
            UiKit.At(ml.rectTransform, UiKit.TL, new Vector2(500, -16), new Vector2(300, 30));
            var mPrev = UiKit.MakeButton(panel.transform, "<", new Vector2(60, 64), UiKit.Purple, () => ChangeModel(-1), 32);
            UiKit.At(mPrev.GetComponent<RectTransform>(), UiKit.TL, new Vector2(500, -50), new Vector2(60, 64));
            modelText = UiKit.Label(panel.transform, "", 28, Color.white, TextAnchor.MiddleCenter);
            UiKit.At(modelText.rectTransform, UiKit.TL, new Vector2(565, -50), new Vector2(260, 64));
            var mNext = UiKit.MakeButton(panel.transform, ">", new Vector2(60, 64), UiKit.Purple, () => ChangeModel(1), 32);
            UiKit.At(mNext.GetComponent<RectTransform>(), UiKit.TL, new Vector2(830, -50), new Vector2(60, 64));

            var hl = UiKit.Label(panel.transform, "ЦВЕТ ВОЛОС", 22, UiKit.Cyan, TextAnchor.UpperLeft);
            UiKit.At(hl.rectTransform, UiKit.TL, new Vector2(960, -16), new Vector2(300, 30));
            int hn = TeamLook.HairPresets.Length;
            swatchRt = new RectTransform[hn]; swatchOutline = new Outline[hn];
            for (int i = 0; i < hn; i++)
            {
                int idx = i;
                var sw = UiKit.Slant(panel.transform, "Hair" + i, TeamLook.HairPresets[i], 10f, true);
                UiKit.At(sw.rectTransform, UiKit.TL, new Vector2(960 + i * 62, -52), new Vector2(54, 54));
                var o = sw.gameObject.AddComponent<Outline>(); o.effectColor = Color.white; o.effectDistance = new Vector2(3, -3); o.enabled = false;
                var b = sw.gameObject.AddComponent<Button>(); b.targetGraphic = sw;
                var cb = b.colors; cb.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f); cb.highlightedColor = Color.white; cb.selectedColor = new Color(0.9f, 0.9f, 0.9f, 1f); b.colors = cb;
                var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
                b.onClick.AddListener(() => { if (AudioManager.Instance != null) AudioManager.Instance.PlayClick(); draft.hair = (byte)idx; Refresh(); });
                swatchRt[i] = sw.rectTransform; swatchOutline[i] = o;
            }

            var nul = UiKit.Label(panel.transform, "НОМЕР", 22, UiKit.Cyan, TextAnchor.UpperLeft);
            UiKit.At(nul.rectTransform, UiKit.TL, new Vector2(1500, -16), new Vector2(200, 30));
            var nm = UiKit.MakeButton(panel.transform, "−", new Vector2(56, 64), UiKit.Purple, () => ChangeNumber(-1), 32);
            UiKit.At(nm.GetComponent<RectTransform>(), UiKit.TL, new Vector2(1500, -50), new Vector2(56, 64));
            numberText = UiKit.Label(panel.transform, "", 40, UiKit.Gold, TextAnchor.MiddleCenter);
            UiKit.At(numberText.rectTransform, UiKit.TL, new Vector2(1560, -50), new Vector2(90, 64));
            var np = UiKit.MakeButton(panel.transform, "+", new Vector2(56, 64), UiKit.Purple, () => ChangeNumber(1), 32);
            UiKit.At(np.GetComponent<RectTransform>(), UiKit.TL, new Vector2(1655, -50), new Vector2(56, 64));

            // bottom buttons
            var save = UiKit.MakeButton(Rt, "СОХРАНИТЬ", new Vector2(420, 72), UiKit.Orange, Save, 34);
            UiKit.At(save.GetComponent<RectTransform>(), UiKit.BC, new Vector2(-240, 36), new Vector2(420, 72));
            var back = UiKit.MakeButton(Rt, "НАЗАД", new Vector2(320, 72), UiKit.Purple, () => Show(false), 34);
            UiKit.At(back.GetComponent<RectTransform>(), UiKit.BC, new Vector2(150, 36), new Vector2(320, 72));
            savedText = UiKit.Label(Rt, "", 28, UiKit.Cyan, TextAnchor.MiddleLeft);
            UiKit.At(savedText.rectTransform, UiKit.BC, new Vector2(500, 36), new Vector2(400, 72));
            savedText.rectTransform.pivot = UiKit.BC;
        }

        void BuildCard(StyleDef st, int idx, float x, float y, float w, float h)
        {
            var card = UiKit.Slant(Rt, "Card" + idx, UiKit.Panel, 20f, true);
            UiKit.At(card.rectTransform, UiKit.TL, new Vector2(x, y), new Vector2(w, h));
            UiKit.AddShadow(card.gameObject, 8f, 0.5f);
            var o = card.gameObject.AddComponent<Outline>();
            o.effectColor = UiKit.Gold; o.effectDistance = new Vector2(4, -4); o.enabled = false;
            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = card;
            var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
            var cb = btn.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f); cb.selectedColor = Color.white; cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f); btn.colors = cb;
            btn.onClick.AddListener(() => { if (AudioManager.Instance != null) AudioManager.Instance.PlayClick(); draft.style = st.style; Refresh(); });
            card.gameObject.AddComponent<HoverFx>().hoverScale = 1.02f;
            cardBg[idx] = card; cardOutline[idx] = o; cardRt[idx] = card.rectTransform;

            var head = UiKit.Slant(card.transform, "Head", Color.white, 20f);
            head.Set(20f, st.c1, st.c2);
            UiKit.At(head.rectTransform, UiKit.TL, new Vector2(0, 0), new Vector2(w, 74));
            var nm = UiKit.Label(head.transform, st.name.ToUpperInvariant(), 32, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            nm.horizontalOverflow = HorizontalWrapMode.Overflow;

            var desc = UiKit.Label(card.transform, st.desc, 22, new Color(1, 1, 1, 0.85f), TextAnchor.UpperLeft, FontStyle.Normal);
            UiKit.At(desc.rectTransform, UiKit.TL, new Vector2(26, -86), new Vector2(w - 52, 96));

            var vals = StatValues(st.stats);
            for (int i = 0; i < StatLabels.Length; i++)
            {
                float ry = -190f - i * 32f;
                var l = UiKit.Label(card.transform, StatLabels[i], 20, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                UiKit.At(l.rectTransform, UiKit.TL, new Vector2(26, ry), new Vector2(110, 26));
                var bar = UiKit.MakeBar(card.transform, new Vector2(w - 26 - 136 - 36, 16), Color.Lerp(st.c1, st.c2, 0.4f), 6f);
                UiKit.At(bar.Root, UiKit.TL, new Vector2(136, ry - 5), new Vector2(w - 26 - 136 - 36, 16));
                bar.Set(vals[i] / 10f);
            }

            var fl = UiKit.Label(card.transform, "ФИНИШЕР", 18, UiKit.Cyan, TextAnchor.UpperLeft);
            UiKit.At(fl.rectTransform, UiKit.TL, new Vector2(26, -428), new Vector2(w - 52, 24));
            var fn = UiKit.Label(card.transform, st.finisherName, 28, UiKit.Gold, TextAnchor.UpperLeft, FontStyle.Bold);
            UiKit.At(fn.rectTransform, UiKit.TL, new Vector2(26, -452), new Vector2(w - 52, 40));
            var fq = UiKit.Label(card.transform, "«" + st.finisherQuote + "»", 20, new Color(1, 1, 1, 0.75f), TextAnchor.UpperLeft, FontStyle.Italic);
            UiKit.At(fq.rectTransform, UiKit.TL, new Vector2(26, -496), new Vector2(w - 52, 80));
        }

        int ModelCount() { return Mathf.Max(1, GameHub.ModelNames != null ? GameHub.ModelNames.Length : 0); }

        void ChangeModel(int d)
        {
            int n = ModelCount();
            draft.model = (byte)(((draft.model + d) % n + n) % n);
            Refresh();
        }

        void ChangeNumber(int d)
        {
            int v = draft.number + d;
            if (v < 1) v = 99; if (v > 99) v = 1;
            draft.number = (byte)v;
            Refresh();
        }

        protected override void OnShown()
        {
            draft = GameHub.LocalProfile;
            if (draft.number < 1) draft.number = 1;
            if (draft.model >= ModelCount()) draft.model = 0;
            nick.text = draft.nick ?? "";
            savedText.text = "";
            Refresh();
        }

        void Refresh()
        {
            for (int i = 0; i < cardBg.Length; i++)
            {
                bool sel = (int)draft.style == i;
                cardOutline[i].enabled = sel;
                cardBg[i].color = sel ? new Color(0.22f, 0.15f, 0.45f, 1f) : UiKit.Panel;
            }
            for (int i = 0; i < swatchOutline.Length; i++)
            {
                bool sel = draft.hair == i;
                swatchOutline[i].enabled = sel;
                swatchRt[i].localScale = sel ? Vector3.one * 1.15f : Vector3.one;
            }
            var names = GameHub.ModelNames;
            modelText.text = (names != null && names.Length > 0 && draft.model < names.Length) ? names[draft.model] : "Модель " + (draft.model + 1);
            numberText.text = draft.number.ToString();
        }

        void Save()
        {
            draft.nick = string.IsNullOrWhiteSpace(nick.text) ? "Игрок" : nick.text.Trim();
            ProfileStore.Save(draft);
            savedText.text = "Сохранено!";
            savedT = Time.unscaledTime;
        }

        void Update()
        {
            if (savedText.text.Length > 0 && Time.unscaledTime - savedT > 2f) savedText.text = "";
        }
    }
}
