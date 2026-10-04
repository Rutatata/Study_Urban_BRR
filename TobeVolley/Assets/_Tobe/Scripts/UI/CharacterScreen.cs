using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tobe.UI
{
    /// <summary>Character customizer: live 3D preview on the left, tabs (player / appearance / gear) on the right.</summary>
    public sealed class CharacterScreen : UiScreen
    {
        sealed class GearDef
        {
            public Gear flag; public string name, desc;
            public Button btn; public SlantGraphic bg, stripe; public Text state;
        }

        static readonly string[] StatLabels = { "АТАКА", "ПРЫЖОК", "ПРИЁМ", "ПАС", "БЛОК", "ПОДАЧА", "СКОРОСТЬ" };
        static readonly string[] TabNames = { "ИГРОК", "ВНЕШНОСТЬ", "ЭКИПИРОВКА" };

        readonly GearDef[] Gears =
        {
            new GearDef { flag = Gear.KneePads, name = "Наколенники", desc = "Для нырков и приёма" },
            new GearDef { flag = Gear.Headband, name = "Повязка", desc = "Держит волосы, добавляет стиля" },
            new GearDef { flag = Gear.Wristbands, name = "Напульсники", desc = "Классика атакующих" },
            new GearDef { flag = Gear.Glasses, name = "Спорт-очки", desc = "Взгляд снайпера" },
            new GearDef { flag = Gear.ArmSleeve, name = "Рукав на руку", desc = "Компрессионный рукав" },
            new GearDef { flag = Gear.AnkleTape, name = "Тейп на лодыжках", desc = "Для резких остановок" },
        };

        PlayerProfile draft;
        CharacterPreview preview;
        RawImage previewImg;
        Text loadingText, plateNick, plateNum, bigNum, savedText, modelText, numberText, heightText, buildText;
        RectTransform plateIcon, plateRoot;
        InputField nick;
        Button[] tabBtn = new Button[3];
        RectTransform[] tabRoot = new RectTransform[3];
        int tab;
        Button[] styleBtn;
        Text styleName, styleDesc, finName, finQuote;
        SegBarGraphic[] statBar = new SegBarGraphic[7];
        Text[] statVal = new Text[7];
        SlantGraphic styleStripe, styleHead;
        Outline[] skinFrame, hairFrame, eyeFrame;
        Slider heightSlider, buildSlider;
        bool building = true, suppress;
        float savedT = -10f;
        PlayerProfile saved;

        protected override Vector2 SlideIn { get { return new Vector2(0f, -40f); } }

        // ------------------------------------------------------------------ build
        protected override void Build()
        {
            var bg = UiKit.Box(Rt, "Bg", new Color(0.02f, 0.025f, 0.04f, 0.94f), true);
            UiKit.Stretch(bg.rectTransform);
            UiKit.SpeedLines(Rt, "Speed", new Color(1f, 0.55f, 0.15f, 0.18f), 22, 0.12f);
            var stripe = UiKit.Slant(Rt, "Stripe", UiKit.WithA(UiKit.Orange, 0.16f), 160f);
            stripe.slantClamp = 0.99f;
            UiKit.At(stripe.rectTransform, UiKit.TL, new Vector2(-60, 0), new Vector2(760, 130));

            var title = UiKit.Label(Rt, "ПЕРСОНАЖ", 66, Color.white, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic, true);
            UiKit.At(title.rectTransform, UiKit.TL, new Vector2(70, -18), new Vector2(700, 80));
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            var sub = UiKit.Label(Rt, "РЕДАКТОР ВНЕШНОСТИ И СТИЛЯ ИГРЫ", 22, UiKit.Cyan, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            UiKit.At(sub.rectTransform, UiKit.TL, new Vector2(74, -94), new Vector2(700, 30));
            sub.horizontalOverflow = HorizontalWrapMode.Overflow;

            BuildPreview();
            BuildRight();

            var save = UiKit.MakeButton(Rt, "СОХРАНИТЬ", new Vector2(420, 70), UiKit.Orange, Save, 34);
            UiKit.At(save.GetComponent<RectTransform>(), UiKit.BR, new Vector2(-390, 30), new Vector2(420, 70));
            var back = UiKit.MakeButton(Rt, "НАЗАД", new Vector2(300, 70), UiKit.Purple, () => Show(false), 34);
            UiKit.At(back.GetComponent<RectTransform>(), UiKit.BR, new Vector2(-70, 30), new Vector2(300, 70));
            var reset = UiKit.MakeButton(Rt, "СБРОС", new Vector2(220, 70), UiKit.Purple, ResetDraft, 28);
            UiKit.At(reset.GetComponent<RectTransform>(), UiKit.BR, new Vector2(-840, 30), new Vector2(220, 70));
            savedText = UiKit.Label(Rt, "", 30, UiKit.Cyan, TextAnchor.MiddleRight, FontStyle.BoldAndItalic);
            UiKit.At(savedText.rectTransform, UiKit.BL, new Vector2(70, 30), new Vector2(500, 70));
            savedText.horizontalOverflow = HorizontalWrapMode.Overflow;
            building = false;
        }

        void BuildPreview()
        {
            preview = CharacterPreview.Create();

            var panel = UiKit.MakePanel(Rt, "PreviewPanel", new Vector2(720, 800), 0f, new Color(0.04f, 0.05f, 0.08f, 0.9f), UiKit.Orange);
            UiKit.At(panel.rectTransform, UiKit.TL, new Vector2(70, -140), new Vector2(720, 800));

            var ri = UiKit.NewRect("Preview", panel.transform);
            UiKit.At(ri, UiKit.TC, new Vector2(4, -8), new Vector2(700, 784));
            previewImg = ri.gameObject.AddComponent<RawImage>();
            previewImg.texture = preview.Texture;
            previewImg.raycastTarget = true;
            var trig = ri.gameObject.AddComponent<EventTrigger>();
            var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener(d => { var pe = d as PointerEventData; if (pe != null) preview.Drag(pe.delta.x); });
            trig.triggers.Add(drag);

            loadingText = UiKit.Label(panel.transform, "ЗАГРУЗКА МОДЕЛИ…", 28, UiKit.Dim, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            UiKit.Stretch(loadingText.rectTransform);

            bigNum = UiKit.Label(panel.transform, "", 230, new Color(1f, 1f, 1f, 0.09f), TextAnchor.UpperLeft, FontStyle.BoldAndItalic, false, true);
            UiKit.At(bigNum.rectTransform, UiKit.TL, new Vector2(34, -14), new Vector2(560, 260));
            bigNum.horizontalOverflow = HorizontalWrapMode.Overflow;

            var hint = UiKit.Label(panel.transform, "ТЯНИ МЫШЬЮ — ПОВЕРНУТЬ", 18, UiKit.Dim, TextAnchor.MiddleRight, FontStyle.BoldAndItalic);
            UiKit.At(hint.rectTransform, UiKit.TR, new Vector2(-24, -18), new Vector2(380, 26));
            hint.horizontalOverflow = HorizontalWrapMode.Overflow;

            // nameplate
            var plate = UiKit.Slant(panel.transform, "Plate", new Color(0.02f, 0.025f, 0.04f, 0.88f), 22f);
            UiKit.At(plate.rectTransform, UiKit.BL, new Vector2(26, 26), new Vector2(520, 96));
            UiKit.AddAccent(plate.transform, UiKit.Orange, 22f, 8f);
            plateRoot = plate.rectTransform;
            plateNick = UiKit.Label(plate.transform, "", 40, Color.white, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic, true);
            UiKit.Pad(plateNick.rectTransform, 112, 6, 120, 28);
            plateNick.horizontalOverflow = HorizontalWrapMode.Overflow;
            plateNum = UiKit.Label(plate.transform, "", 56, UiKit.Orange, TextAnchor.MiddleRight, FontStyle.BoldAndItalic, true);
            UiKit.Pad(plateNum.rectTransform, 300, 0, 34, 0);
            plateNum.horizontalOverflow = HorizontalWrapMode.Overflow;
            var team = UiKit.Label(plate.transform, TeamLook.Names[0], 16, UiKit.Dim, TextAnchor.LowerLeft, FontStyle.BoldAndItalic);
            UiKit.Pad(team.rectTransform, 112, 0, 100, 8);
            plateIcon = UiKit.StyleIcon(plate.transform, PlayStyle.Middle, 64);
            UiKit.At(plateIcon, UiKit.ML, new Vector2(40, 0), new Vector2(64, 64));

            var rnd = UiKit.MakeButton(panel.transform, "СЛУЧАЙНО", new Vector2(190, 56), UiKit.Purple, Randomize, 22, 14f);
            UiKit.At(rnd.GetComponent<RectTransform>(), UiKit.BR, new Vector2(-26, 34), new Vector2(190, 56));
        }

        void BuildRight()
        {
            var panel = UiKit.MakePanel(Rt, "RightPanel", new Vector2(1020, 800), 0f, UiKit.Panel, UiKit.Cyan);
            UiKit.At(panel.rectTransform, UiKit.TL, new Vector2(830, -140), new Vector2(1020, 800));

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var b = UiKit.MakeButton(panel.transform, TabNames[i], new Vector2(310, 62), UiKit.Purple, () => SetTab(idx), 28, 16f);
                UiKit.At(b.GetComponent<RectTransform>(), UiKit.TL, new Vector2(36 + i * 322, -24), new Vector2(310, 62));
                tabBtn[i] = b;
                var root = UiKit.NewRect("Tab" + i, panel.transform);
                UiKit.At(root, UiKit.TL, new Vector2(40, -108), new Vector2(940, 670));
                tabRoot[i] = root;
            }
            BuildTabPlayer(tabRoot[0]);
            BuildTabLook(tabRoot[1]);
            BuildTabGear(tabRoot[2]);
        }

        static Text Cap(Transform p, string text, Vector2 pos, float w = 500)
        {
            var t = UiKit.Label(p, text, 20, UiKit.Cyan, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            UiKit.At(t.rectTransform, UiKit.TL, pos, new Vector2(w, 26));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        // ---------- tab: player
        void BuildTabPlayer(RectTransform root)
        {
            Cap(root, "НИКНЕЙМ", new Vector2(0, 0));
            nick = UiKit.MakeInput(root, "Ник", new Vector2(430, 60), 30, 14);
            UiKit.At(nick.GetComponent<RectTransform>(), UiKit.TL, new Vector2(0, -30), new Vector2(430, 60));
            nick.onValueChanged.AddListener(s => { if (suppress) return; draft.nick = s; Refresh(false); });

            Cap(root, "НОМЕР", new Vector2(480, 0));
            var nm = UiKit.MakeButton(root, "−", new Vector2(56, 60), UiKit.Purple, () => ChangeNumber(-1), 32, 10f);
            UiKit.At(nm.GetComponent<RectTransform>(), UiKit.TL, new Vector2(480, -30), new Vector2(56, 60));
            numberText = UiKit.Label(root, "", 44, UiKit.Gold, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false, true);
            UiKit.At(numberText.rectTransform, UiKit.TL, new Vector2(540, -30), new Vector2(100, 60));
            var np = UiKit.MakeButton(root, "+", new Vector2(56, 60), UiKit.Purple, () => ChangeNumber(1), 32, 10f);
            UiKit.At(np.GetComponent<RectTransform>(), UiKit.TL, new Vector2(644, -30), new Vector2(56, 60));

            Cap(root, "СТИЛЬ ИГРЫ", new Vector2(0, -104));
            int n = Styles.All.Length;
            styleBtn = new Button[n];
            for (int i = 0; i < n; i++)
            {
                int idx = i;
                var def = Styles.All[i];
                var b = UiKit.MakeButton(root, def.name.ToUpperInvariant(), new Vector2(320, 64), UiKit.Purple, () => PickStyle((PlayStyle)idx), 26, 14f);
                UiKit.At(b.GetComponent<RectTransform>(), UiKit.TL, new Vector2(0, -136 - i * 74), new Vector2(320, 64));
                var lab = b.GetComponentInChildren<Text>();
                lab.alignment = TextAnchor.MiddleLeft;
                UiKit.Pad(lab.rectTransform, 84, 0, 8, 0);
                var ic = UiKit.StyleIcon(b.transform, def.style, 46);
                UiKit.At(ic, UiKit.ML, new Vector2(34, 0), new Vector2(46, 46));
                styleBtn[i] = b;
            }

            // detail card
            var card = UiKit.MakePanel(root, "StyleCard", new Vector2(590, 510), 0f, new Color(0.07f, 0.08f, 0.12f, 0.95f));
            UiKit.At(card.rectTransform, UiKit.TL, new Vector2(350, -136), new Vector2(590, 510));
            styleStripe = UiKit.AddAccent(card.transform, UiKit.Orange, 0f, 8f);
            styleHead = UiKit.Slant(card.transform, "Head", Color.white, 18f);
            UiKit.At(styleHead.rectTransform, UiKit.TL, new Vector2(8, 0), new Vector2(582, 66));
            styleName = UiKit.Label(styleHead.transform, "", 40, Color.white, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic, true);
            UiKit.Pad(styleName.rectTransform, 30, 0, 20, 0);
            styleName.horizontalOverflow = HorizontalWrapMode.Overflow;
            styleDesc = UiKit.Label(card.transform, "", 21, new Color(1, 1, 1, 0.85f), TextAnchor.UpperLeft, FontStyle.Normal);
            UiKit.At(styleDesc.rectTransform, UiKit.TL, new Vector2(30, -76), new Vector2(534, 58));
            for (int i = 0; i < 7; i++)
            {
                float y = -142 - i * 34;
                var l = UiKit.Label(card.transform, StatLabels[i], 18, Color.white, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
                UiKit.At(l.rectTransform, UiKit.TL, new Vector2(30, y), new Vector2(120, 26));
                var sb = UiKit.SegBar(card.transform, new Vector2(360, 18), UiKit.Orange, 10, 7f, 3f);
                UiKit.At(sb.rectTransform, UiKit.TL, new Vector2(150, y - 4), new Vector2(360, 18));
                statBar[i] = sb;
                statVal[i] = UiKit.Label(card.transform, "", 22, UiKit.Gold, TextAnchor.MiddleRight, FontStyle.BoldAndItalic);
                UiKit.At(statVal[i].rectTransform, UiKit.TL, new Vector2(515, y), new Vector2(50, 26));
            }
            var fl = UiKit.Label(card.transform, "ФИНИШЕР", 17, UiKit.Cyan, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            UiKit.At(fl.rectTransform, UiKit.TL, new Vector2(30, -392), new Vector2(300, 24));
            finName = UiKit.Label(card.transform, "", 32, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic, false, true);
            UiKit.At(finName.rectTransform, UiKit.TL, new Vector2(30, -416), new Vector2(534, 40));
            finName.horizontalOverflow = HorizontalWrapMode.Overflow;
            finQuote = UiKit.Label(card.transform, "", 20, new Color(1, 1, 1, 0.7f), TextAnchor.UpperLeft, FontStyle.Italic);
            UiKit.At(finQuote.rectTransform, UiKit.TL, new Vector2(30, -458), new Vector2(534, 44));
        }

        // ---------- tab: look
        void BuildTabLook(RectTransform root)
        {
            Cap(root, "МОДЕЛЬ", new Vector2(0, 0));
            var mPrev = UiKit.MakeButton(root, "<", new Vector2(64, 60), UiKit.Purple, () => ChangeModel(-1), 32, 10f);
            UiKit.At(mPrev.GetComponent<RectTransform>(), UiKit.TL, new Vector2(0, -30), new Vector2(64, 60));
            modelText = UiKit.Label(root, "", 30, Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            UiKit.At(modelText.rectTransform, UiKit.TL, new Vector2(72, -30), new Vector2(420, 60));
            var mNext = UiKit.MakeButton(root, ">", new Vector2(64, 60), UiKit.Purple, () => ChangeModel(1), 32, 10f);
            UiKit.At(mNext.GetComponent<RectTransform>(), UiKit.TL, new Vector2(500, -30), new Vector2(64, 60));

            Cap(root, "ТОН КОЖИ", new Vector2(0, -108));
            skinFrame = Swatches(root, TeamLook.SkinTones, -140, i => { draft.skin = (byte)i; Refresh(true); });
            Cap(root, "ЦВЕТ ВОЛОС", new Vector2(0, -214));
            hairFrame = Swatches(root, TeamLook.HairPresets, -246, i => { draft.hair = (byte)i; Refresh(true); });
            Cap(root, "ЦВЕТ ГЛАЗ", new Vector2(0, -320));
            eyeFrame = Swatches(root, TeamLook.EyeColors, -352, i => { draft.eyes = (byte)i; Refresh(true); });

            heightText = Cap(root, "РОСТ", new Vector2(0, -430), 700);
            heightSlider = UiKit.MakeSlider(root, new Vector2(760, 24), UiKit.Orange, 0, 255, true, v => { if (suppress) return; draft.height = (byte)v; Refresh(false); });
            UiKit.At(heightSlider.GetComponent<RectTransform>(), UiKit.TL, new Vector2(10, -472), new Vector2(760, 24));
            buildText = Cap(root, "ТЕЛОСЛОЖЕНИЕ", new Vector2(0, -530), 700);
            buildSlider = UiKit.MakeSlider(root, new Vector2(760, 24), UiKit.Cyan, 0, 255, true, v => { if (suppress) return; draft.build = (byte)v; Refresh(false); });
            UiKit.At(buildSlider.GetComponent<RectTransform>(), UiKit.TL, new Vector2(10, -572), new Vector2(760, 24));
        }

        Outline[] Swatches(RectTransform root, Color[] colors, float y, Action<int> pick)
        {
            var frames = new Outline[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                int idx = i;
                Outline fr;
                var b = UiKit.MakeSwatch(root, colors[i], 56, () => pick(idx), out fr);
                UiKit.At(b.GetComponent<RectTransform>(), UiKit.TL, new Vector2(i * 70, y), new Vector2(56, 56));
                frames[i] = fr;
            }
            return frames;
        }

        // ---------- tab: gear
        void BuildTabGear(RectTransform root)
        {
            Cap(root, "ЭКИПИРОВКА МЕНЯЕТ ТОЛЬКО ВНЕШНИЙ ВИД", new Vector2(0, 0), 800);
            for (int i = 0; i < Gears.Length; i++)
            {
                var g = Gears[i];
                var gg = g;
                var b = UiKit.MakeButton(root, g.name.ToUpperInvariant(), new Vector2(456, 112), UiKit.Purple, () => ToggleGear(gg), 26, 16f);
                UiKit.At(b.GetComponent<RectTransform>(), UiKit.TL, new Vector2((i % 2) * 476, -40 - (i / 2) * 128), new Vector2(456, 112));
                var lab = b.GetComponentInChildren<Text>();
                lab.alignment = TextAnchor.UpperLeft;
                UiKit.Pad(lab.rectTransform, 40, 20, 150, 0);
                var d = UiKit.Label(b.transform, g.desc, 19, new Color(1, 1, 1, 0.7f), TextAnchor.LowerLeft, FontStyle.Normal);
                UiKit.Pad(d.rectTransform, 40, 0, 150, 18);
                var pill = UiKit.Slant(b.transform, "Pill", new Color(0, 0, 0, 0.5f), 10f);
                UiKit.At(pill.rectTransform, UiKit.MR, new Vector2(-24, 0), new Vector2(110, 46));
                var st = UiKit.Label(pill.transform, "ВЫКЛ", 20, Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false, true);
                g.btn = b; g.bg = (SlantGraphic)b.targetGraphic; g.state = st; g.stripe = pill;
            }
            var all = UiKit.MakeButton(root, "ВСЁ", new Vector2(200, 56), UiKit.Purple, () => { draft.gear = 63; Refresh(true); }, 24, 14f);
            UiKit.At(all.GetComponent<RectTransform>(), UiKit.TL, new Vector2(0, -440), new Vector2(200, 56));
            var none = UiKit.MakeButton(root, "СНЯТЬ ВСЁ", new Vector2(240, 56), UiKit.Purple, () => { draft.gear = 0; Refresh(true); }, 24, 14f);
            UiKit.At(none.GetComponent<RectTransform>(), UiKit.TL, new Vector2(216, -440), new Vector2(240, 56));
        }

        // ------------------------------------------------------------------ actions
        void SetTab(int i) { tab = i; RefreshTabs(); }

        void PickStyle(PlayStyle s)
        {
            if (draft.style == s) return;
            draft.style = s;
            preview.Play(PoseId.Celebrate, 1.0f);
            Refresh(false);
        }

        int ModelCount() { return Mathf.Max(1, GameHub.ModelNames != null ? GameHub.ModelNames.Length : 0); }

        void ChangeModel(int d)
        {
            int n = ModelCount();
            draft.model = (byte)(((draft.model + d) % n + n) % n);
            Refresh(true);
        }

        void ChangeNumber(int d)
        {
            int v = draft.number + d;
            if (v < 1) v = 99;
            if (v > 99) v = 1;
            draft.number = (byte)v;
            Refresh(false);
        }

        void ToggleGear(GearDef g)
        {
            draft.gear = (byte)(draft.gear ^ (byte)g.flag);
            Refresh(true);
        }

        void Randomize()
        {
            draft.skin = (byte)UnityEngine.Random.Range(0, TeamLook.SkinTones.Length);
            draft.hair = (byte)UnityEngine.Random.Range(0, TeamLook.HairPresets.Length);
            draft.eyes = (byte)UnityEngine.Random.Range(0, TeamLook.EyeColors.Length);
            draft.height = (byte)UnityEngine.Random.Range(30, 226);
            draft.build = (byte)UnityEngine.Random.Range(30, 226);
            draft.gear = (byte)UnityEngine.Random.Range(0, 64);
            preview.Play(PoseId.Celebrate, 1.2f);
            Refresh(true);
        }

        void ResetDraft()
        {
            draft = saved;
            if (draft.number < 1) draft.number = 1;
            suppress = true; nick.text = draft.nick ?? ""; suppress = false;
            Refresh(true);
        }

        void Save()
        {
            draft.nick = string.IsNullOrWhiteSpace(nick.text) ? "Игрок" : nick.text.Trim();
            ProfileStore.Save(draft);
            saved = draft;
            savedText.text = "СОХРАНЕНО!";
            savedT = Time.unscaledTime;
        }

        // ------------------------------------------------------------------ show / refresh
        protected override void OnShown()
        {
            saved = GameHub.LocalProfile;
            draft = saved;
            if (draft.number < 1) draft.number = 1;
            if (draft.model >= ModelCount()) draft.model = 0;
            suppress = true;
            nick.text = draft.nick ?? "";
            suppress = false;
            savedText.text = "";
            tab = 0;
            preview.SetActive(true);
            Refresh(true, true);
            RefreshTabs();
        }

        protected override void OnHidden()
        {
            if (preview != null) preview.SetActive(false);
        }

        void RefreshTabs()
        {
            for (int i = 0; i < 3; i++)
            {
                tabRoot[i].gameObject.SetActive(i == tab);
                UiKit.SetButtonColor(tabBtn[i], i == tab ? UiKit.Orange : UiKit.Purple);
            }
        }

        static void Frames(Outline[] f, int sel)
        {
            for (int i = 0; i < f.Length; i++)
            {
                f[i].enabled = i == sel;
                f[i].transform.localScale = i == sel ? Vector3.one * 1.12f : Vector3.one;
            }
        }

        void Refresh(bool unused, bool immediate = false)
        {
            if (building) return;
            suppress = true;
            var def = Styles.Get(draft.style);

            // style list + detail
            for (int i = 0; i < styleBtn.Length; i++)
            {
                bool sel = (int)draft.style == i;
                UiKit.SetButtonColor(styleBtn[i], sel ? Color.Lerp(Styles.All[i].c1, Color.black, 0.45f) : UiKit.Purple);
            }
            styleHead.Set(18f, def.c1, def.c2);
            styleStripe.color = def.c1;
            styleName.text = def.name.ToUpperInvariant();
            styleDesc.text = def.desc;
            float[] vals = { def.stats.spike, def.stats.jump, def.stats.receive, def.stats.set, def.stats.block, def.stats.serve, def.stats.speed };
            for (int i = 0; i < 7; i++)
            {
                statBar[i].color = Color.Lerp(def.c1, def.c2, 0.35f);
                statBar[i].Value = vals[i] / 10f;
                statVal[i].text = Mathf.RoundToInt(vals[i]).ToString();
            }
            finName.text = def.finisherName;
            finQuote.text = "«" + def.finisherQuote + "»";
            numberText.text = draft.number.ToString();

            // look
            var names = GameHub.ModelNames;
            modelText.text = (names != null && names.Length > 0 && draft.model < names.Length ? names[draft.model] : "Модель " + (draft.model + 1)).ToUpperInvariant()
                + "   " + (draft.model + 1) + "/" + ModelCount();
            Frames(skinFrame, draft.skin); Frames(hairFrame, draft.hair); Frames(eyeFrame, draft.eyes);
            heightSlider.value = draft.height; buildSlider.value = draft.build;
            int cm = Mathf.RoundToInt(180f * draft.HeightScale);
            heightText.text = "РОСТ   " + cm + " СМ";
            buildText.text = "ТЕЛОСЛОЖЕНИЕ   " + (draft.build < 85 ? "ХУДОЩАВОЕ" : draft.build < 170 ? "СПОРТИВНОЕ" : "АТЛЕТИЧНОЕ");

            // gear
            for (int i = 0; i < Gears.Length; i++)
            {
                var g = Gears[i];
                bool on = (draft.gear & (byte)g.flag) != 0;
                UiKit.SetButtonColor(g.btn, on ? new Color(0.5f, 0.28f, 0.07f, 1f) : UiKit.Purple);
                g.state.text = on ? "ВКЛ" : "ВЫКЛ";
                g.state.color = on ? UiKit.Gold : new Color(1, 1, 1, 0.55f);
                g.stripe.color = on ? new Color(0f, 0f, 0f, 0.55f) : new Color(0f, 0f, 0f, 0.4f);
            }

            // plate
            plateNick.text = string.IsNullOrEmpty(draft.nick) ? "ИГРОК" : draft.nick.ToUpperInvariant();
            plateNum.text = "#" + draft.number;
            bigNum.text = draft.number.ToString();
            ReplaceIcon();
            suppress = false;

            preview.SetProfile(draft, 0, immediate);
        }

        PlayStyle iconStyle = (PlayStyle)255;
        void ReplaceIcon()
        {
            if (iconStyle == draft.style) return;
            iconStyle = draft.style;
            var parent = plateIcon.parent;
            Destroy(plateIcon.gameObject);
            plateIcon = UiKit.StyleIcon(parent, draft.style, 64);
            UiKit.At(plateIcon, UiKit.ML, new Vector2(40, 0), new Vector2(64, 64));
        }

        void Update()
        {
            if (savedText.text.Length > 0 && Time.unscaledTime - savedT > 2f) savedText.text = "";
            if (preview != null) loadingText.gameObject.SetActive(preview.Busy);
        }
    }
}
