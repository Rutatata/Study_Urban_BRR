// Small uGUI kit: slanted panels, buttons, labels, bars, inputs. Everything is built from code.
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tobe.UI
{
    public static class ControlsHelp
    {
        public const string Text =
            "WASD — бег (относительно камеры)\n" +
            "Shift — рывок\n" +
            "Мышь — камера и прицел\n" +
            "ЛКМ — касание мяча (приём/пас/удар в прыжке; держи на подаче — сила)\n" +
            "Пробел — прыжок (атака/блок)\n" +
            "E — нырок\n" +
            "C — «Дай мне!»\n" +
            "Q — зарядить добивание\n" +
            "Esc — меню";

        public static readonly string[,] Rows =
        {
            { "WASD", "бег (относительно камеры)" },
            { "Shift", "рывок" },
            { "Мышь", "камера и прицел" },
            { "ЛКМ", "касание мяча (приём/пас/удар в прыжке; держи на подаче — сила)" },
            { "Пробел", "прыжок (атака/блок)" },
            { "E", "нырок" },
            { "C", "«Дай мне!»" },
            { "Q", "зарядить добивание" },
            { "Esc", "меню" },
        };
    }

    public sealed class Bar
    {
        public RectTransform Root;
        public SlantGraphic Back, Fill;
        public void Set(float t)
        {
            t = Mathf.Clamp01(t);
            Fill.gameObject.SetActive(t > 0.015f);
            var rt = Fill.rectTransform;
            rt.anchorMax = new Vector2(t, 1f);
        }
    }

    public static class ProfileStore
    {
        public static void Load()
        {
            var p = GameHub.LocalProfile;
            try
            {
                if (PlayerPrefs.HasKey("tobe.nick"))
                {
                    p.nick = PlayerPrefs.GetString("tobe.nick", p.nick);
                    p.style = (PlayStyle)Mathf.Clamp(PlayerPrefs.GetInt("tobe.style", (int)p.style), 0, Styles.All.Length - 1);
                    p.model = (byte)Mathf.Clamp(PlayerPrefs.GetInt("tobe.model", p.model), 0, 255);
                    p.hair = (byte)Mathf.Clamp(PlayerPrefs.GetInt("tobe.hair", p.hair), 0, TeamLook.HairPresets.Length - 1);
                    p.number = (byte)Mathf.Clamp(PlayerPrefs.GetInt("tobe.number", p.number), 1, 99);
                    p.skin = (byte)Mathf.Clamp(PlayerPrefs.GetInt("tobe.skin", p.skin), 0, TeamLook.SkinTones.Length - 1);
                    p.height = (byte)Mathf.Clamp(PlayerPrefs.GetInt("tobe.height", p.height), 0, 255);
                    p.build = (byte)Mathf.Clamp(PlayerPrefs.GetInt("tobe.build", p.build), 0, 255);
                    p.eyes = (byte)Mathf.Clamp(PlayerPrefs.GetInt("tobe.eyes", p.eyes), 0, TeamLook.EyeColors.Length - 1);
                    p.gear = (byte)Mathf.Clamp(PlayerPrefs.GetInt("tobe.gear", p.gear), 0, 255);
                }
            }
            catch (Exception) { }
            GameHub.LocalProfile = p;
        }

        public static void Save(PlayerProfile p)
        {
            try
            {
                PlayerPrefs.SetString("tobe.nick", p.nick ?? "");
                PlayerPrefs.SetInt("tobe.style", (int)p.style);
                PlayerPrefs.SetInt("tobe.model", p.model);
                PlayerPrefs.SetInt("tobe.hair", p.hair);
                PlayerPrefs.SetInt("tobe.number", p.number);
                PlayerPrefs.SetInt("tobe.skin", p.skin);
                PlayerPrefs.SetInt("tobe.height", p.height);
                PlayerPrefs.SetInt("tobe.build", p.build);
                PlayerPrefs.SetInt("tobe.eyes", p.eyes);
                PlayerPrefs.SetInt("tobe.gear", p.gear);
                PlayerPrefs.Save();
            }
            catch (Exception) { }
            GameHub.LocalProfile = p;
        }
    }

    public static class UiKit
    {
        // Dark graphite glass + team accents (home = orange/black, away = white/teal)
        public static readonly Color Navy = new Color(0.03f, 0.035f, 0.055f, 1f);
        public static readonly Color Panel = new Color(0.055f, 0.065f, 0.095f, 0.88f);
        public static readonly Color PanelLight = new Color(0.12f, 0.14f, 0.2f, 0.92f);
        public static readonly Color Purple = new Color(0.17f, 0.2f, 0.29f, 1f);      // secondary button slate (name kept for compatibility)
        public static readonly Color Orange = new Color32(0xFF, 0x7A, 0x12, 255);
        public static readonly Color Gold = new Color32(0xFF, 0xD2, 0x3F, 255);
        public static readonly Color Cyan = new Color32(0x22, 0xDD, 0xCB, 255);       // teal accent
        public static readonly Color Dim = new Color(1f, 1f, 1f, 0.6f);
        public static readonly Color Danger = new Color(1f, 0.35f, 0.35f, 1f);
        public static readonly Color Glass = new Color(0.04f, 0.05f, 0.08f, 0.8f);

        public static Color WithA(Color c, float a) { return new Color(c.r, c.g, c.b, a); }

        public static readonly Vector2 TL = new Vector2(0, 1), TC = new Vector2(.5f, 1), TR = new Vector2(1, 1),
            ML = new Vector2(0, .5f), C = new Vector2(.5f, .5f), MR = new Vector2(1, .5f),
            BL = new Vector2(0, 0), BC = new Vector2(.5f, 0), BR = new Vector2(1, 0);

        public const string OfflineMsg = "Онлайн недоступен: привяжи проект к Unity Gaming Services (см. README). Тренировка работает офлайн.";

        static Font _font, _head;
        static readonly string[] CjkFonts = { "Yu Gothic", "Meiryo", "MS Gothic", "Noto Sans CJK JP" };

        static Font MakeOsFont(string[] names)
        {
            Font f = null;
            try { f = Font.CreateDynamicFontFromOSFont(names, 32); } catch (Exception) { f = null; }
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f;
        }

        /// <summary>Body font (Cyrillic + CJK fallbacks).</summary>
        public static Font Font
        {
            get
            {
                if (_font == null) _font = MakeOsFont(new[] { "Segoe UI", "Arial", "Tahoma", "Verdana", "DejaVu Sans", "Yu Gothic", "Meiryo", "MS Gothic", "Noto Sans CJK JP" });
                return _font;
            }
        }

        /// <summary>Heading font: bold sporty faces with Cyrillic support, then the body chain.</summary>
        public static Font HeadFont
        {
            get
            {
                if (_head == null) _head = MakeOsFont(new[] { "Bahnschrift", "Impact", "Arial Black", "Segoe UI Black", "Segoe UI", "Arial", "DejaVu Sans", CjkFonts[0], CjkFonts[1], CjkFonts[2], CjkFonts[3] });
                return _head;
            }
        }

        // ---------- layout ----------
        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        /// <summary>Anchor and pivot at the same point; pos is the offset of that point from the parent's anchor.</summary>
        public static void At(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        public static void AtP(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        public static void Pad(RectTransform rt, float l, float t, float r, float b)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        // ---------- graphics ----------
        public static SlantGraphic Slant(Transform parent, string name, Color c, float slant, bool raycast = false)
        {
            var rt = NewRect(name, parent);
            rt.gameObject.AddComponent<CanvasRenderer>();
            var g = rt.gameObject.AddComponent<SlantGraphic>();
            g.color = c; g.slant = slant; g.raycastTarget = raycast;
            return g;
        }

        public static Image Box(Transform parent, string name, Color c, bool raycast = false)
        {
            var rt = NewRect(name, parent);
            var im = rt.gameObject.AddComponent<Image>();
            im.color = c; im.raycastTarget = raycast;
            return im;
        }

        public static CircleGraphic Circle(Transform parent, string name, Color c)
        {
            var rt = NewRect(name, parent);
            rt.gameObject.AddComponent<CanvasRenderer>();
            var g = rt.gameObject.AddComponent<CircleGraphic>();
            g.color = c; g.raycastTarget = false;
            return g;
        }

        public static void AddShadow(GameObject go, float d = 4f, float a = 0.55f)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, a);
            s.effectDistance = new Vector2(d, -d);
        }

        public static void AddOutline(GameObject go, Color c, float d = 3f)
        {
            var o = go.AddComponent<Outline>();
            o.effectColor = c;
            o.effectDistance = new Vector2(d, -d);
        }

        /// <summary>Dark glass slanted panel (soft shadow, top sheen) with an optional colored accent stripe on the left edge.</summary>
        public static SlantGraphic MakePanel(Transform parent, string name, Vector2 size, float slant = 24f, Color? fill = null, Color? accent = null)
        {
            var g = Slant(parent, name, fill ?? Panel, slant);
            g.rectTransform.sizeDelta = size;
            g.SetV(new Color(1.35f, 1.35f, 1.4f, 1f), new Color(0.8f, 0.8f, 0.85f, 1f));
            AddShadow(g.gameObject, 8f, 0.45f);
            if (accent.HasValue) AddAccent(g.transform, accent.Value, slant, 7f);
            return g;
        }

        /// <summary>Slanted accent stripe along the left edge of a panel (same slant as the panel).</summary>
        public static SlantGraphic AddAccent(Transform panel, Color c, float slant, float width)
        {
            var st = Slant(panel, "Accent", c, slant);
            st.slantClamp = 0.99f;
            var rt = st.rectTransform;
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(Mathf.Abs(slant) + width, 0);
            return st;
        }

        /// <summary>Thin horizontal accent line (gradient from color to transparent).</summary>
        public static SlantGraphic AccentLine(Transform parent, Color c, float width, float height = 3f)
        {
            var g = Slant(parent, "Line", c, 0f);
            g.Set(0f, c, new Color(c.r, c.g, c.b, 0f));
            g.rectTransform.sizeDelta = new Vector2(width, height);
            return g;
        }

        public static SpeedLinesGraphic SpeedLines(Transform parent, string name, Color c, int count = 24, float speed = 0.3f)
        {
            var rt = NewRect(name, parent);
            rt.gameObject.AddComponent<CanvasRenderer>();
            var g = rt.gameObject.AddComponent<SpeedLinesGraphic>();
            g.color = c; g.count = count; g.speed = speed; g.raycastTarget = false;
            Stretch(rt);
            return g;
        }

        public static SegBarGraphic SegBar(Transform parent, Vector2 size, Color fill, int segments = 10, float slant = 10f, float gap = 4f)
        {
            var rt = NewRect("SegBar", parent);
            rt.gameObject.AddComponent<CanvasRenderer>();
            var g = rt.gameObject.AddComponent<SegBarGraphic>();
            g.color = fill; g.segments = segments; g.slant = slant; g.gap = gap; g.raycastTarget = false;
            rt.sizeDelta = size;
            return g;
        }

        public static string StyleCode(PlayStyle s)
        {
            switch (s)
            {
                case PlayStyle.Setter: return "СВ";
                case PlayStyle.Outside: return "ДО";
                case PlayStyle.Middle: return "ЦН";
                case PlayStyle.Opposite: return "ДГ";
                default: return "ЛБ";
            }
        }

        /// <summary>Round style badge with a two-letter code in the style's colors.</summary>
        public static RectTransform StyleIcon(Transform parent, PlayStyle style, float size)
        {
            var def = Styles.Get(style);
            var root = NewRect("StyleIcon", parent);
            root.sizeDelta = new Vector2(size, size);
            var ring = Circle(root, "Ring", def.c2); Stretch(ring.rectTransform);
            var core = Circle(root, "Core", Color.Lerp(def.c1, Color.black, 0.35f)); Stretch(core.rectTransform);
            Pad(core.rectTransform, size * 0.08f, size * 0.08f, size * 0.08f, size * 0.08f);
            var t = Label(root, StyleCode(style), Mathf.RoundToInt(size * 0.4f), Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return root;
        }

        /// <summary>Keyboard/mouse key chip, e.g. "ЛКМ" or "ПРОБЕЛ".</summary>
        public static RectTransform KeyChip(Transform parent, string key, float height, Color? tint = null)
        {
            Color c = tint ?? Gold;
            int fs = Mathf.RoundToInt(height * 0.55f);
            float w = Mathf.Max(height * 1.1f, key.Length * fs * 0.72f + height * 0.7f);
            var chip = Slant(parent, "Key_" + key, new Color(0.96f, 0.97f, 1f, 0.95f), height * 0.2f);
            chip.rectTransform.sizeDelta = new Vector2(w, height);
            chip.SetV(Color.white, new Color(0.78f, 0.8f, 0.88f, 1f));
            AddShadow(chip.gameObject, 3f, 0.6f);
            var under = Slant(chip.transform, "Edge", c, 0f);
            under.rectTransform.anchorMin = new Vector2(0, 0); under.rectTransform.anchorMax = new Vector2(1, 0);
            under.rectTransform.pivot = new Vector2(0.5f, 0); under.rectTransform.sizeDelta = new Vector2(0, Mathf.Max(3f, height * 0.1f));
            var t = Label(chip.transform, key, fs, new Color(0.08f, 0.09f, 0.13f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            foreach (var sh in t.GetComponents<Shadow>()) UnityEngine.Object.Destroy(sh);
            return chip.rectTransform;
        }

        public static Text Label(Transform parent, string text, int size, Color c, TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle fs = FontStyle.Bold, bool outline = false, bool head = false)
        {
            var rt = NewRect("Text", parent);
            Stretch(rt);
            var t = rt.gameObject.AddComponent<Text>();
            if (!head && size >= 36 && (fs == FontStyle.Bold || fs == FontStyle.BoldAndItalic)) head = true;   // big bold text uses the sporty heading font
            t.font = head ? HeadFont : Font; t.fontSize = size; t.color = c; t.alignment = anchor; t.fontStyle = fs;
            t.text = text; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            if (outline) { AddOutline(rt.gameObject, new Color(0, 0, 0, 0.9f), Mathf.Max(2f, size * 0.04f)); AddShadow(rt.gameObject, Mathf.Max(3f, size * 0.06f), 0.6f); }
            else AddShadow(rt.gameObject, 2f, 0.5f);
            return t;
        }

        public static Bar MakeBar(Transform parent, Vector2 size, Color fill, float slant = 8f)
        {
            var b = new Bar();
            var back = Slant(parent, "Bar", new Color(0f, 0f, 0f, 0.55f), slant);
            back.rectTransform.sizeDelta = size;
            var f = Slant(back.transform, "Fill", fill, slant);
            var rt = f.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            b.Root = back.rectTransform; b.Back = back; b.Fill = f;
            return b;
        }

        // ---------- widgets ----------
        public static Button MakeButton(Transform parent, string text, Vector2 size, Color color, Action onClick, int fontSize = 30, float slant = 18f)
        {
            var g = Slant(parent, "Btn_" + text, color, slant, true);
            g.rectTransform.sizeDelta = size;
            g.SetV(new Color(1.18f, 1.18f, 1.18f, 1f), new Color(0.78f, 0.78f, 0.8f, 1f));
            AddShadow(g.gameObject, 5f, 0.5f);
            var b = g.gameObject.AddComponent<Button>();
            b.targetGraphic = g;
            b.transition = Selectable.Transition.ColorTint;
            var cb = b.colors;
            cb.normalColor = new Color(0.9f, 0.9f, 0.94f, 1f);
            cb.highlightedColor = Color.white;
            cb.selectedColor = new Color(0.9f, 0.9f, 0.94f, 1f);
            cb.pressedColor = new Color(0.65f, 0.65f, 0.7f, 1f);
            cb.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            cb.colorMultiplier = 1f; cb.fadeDuration = 0.08f;
            b.colors = cb;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            g.gameObject.AddComponent<RectMask2D>();

            bool bright = (color.r * 0.3f + color.g * 0.6f + color.b * 0.1f) > 0.55f;
            var acc = Slant(g.transform, "Accent", bright ? new Color(0.08f, 0.09f, 0.13f, 0.9f) : Orange, slant);
            acc.slantClamp = 0.99f;
            var art = acc.rectTransform;
            art.anchorMin = new Vector2(0, 0); art.anchorMax = new Vector2(0, 1); art.pivot = new Vector2(0, 0.5f);
            art.anchoredPosition = Vector2.zero; art.sizeDelta = new Vector2(slant + 8f, 0);

            var lab = Label(g.transform, text, fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, false, true);
            lab.horizontalOverflow = HorizontalWrapMode.Overflow;
            Pad(lab.rectTransform, slant * 0.5f + 8f, 0, 4, 0);

            var sw = Slant(g.transform, "Sweep", new Color(1, 1, 1, 0.28f), 26f);
            sw.slantClamp = 0.99f;
            var srt = sw.rectTransform;
            srt.anchorMin = new Vector2(0, 0); srt.anchorMax = new Vector2(0, 1); srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(70, 0);
            sw.gameObject.SetActive(false);

            var hv = g.gameObject.AddComponent<HoverFx>();
            hv.sweep = srt; hv.accent = art; hv.accentMin = slant + 8f; hv.accentMax = slant + 22f;
            b.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlayClick();
                if (onClick != null) onClick();
            });
            return b;
        }

        public static void SetButtonColor(Button b, Color c, Color? textColor = null)
        {
            if (b == null) return;
            if (b.targetGraphic != null) b.targetGraphic.color = c;
            if (textColor.HasValue) { var t = b.GetComponentInChildren<Text>(); if (t != null) t.color = textColor.Value; }
        }

        public static void SetButtonText(Button b, string s)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = s;
        }

        public static InputField MakeInput(Transform parent, string placeholder, Vector2 size, int fontSize, int maxLen)
        {
            var bg = Slant(parent, "Input", new Color(0.02f, 0.025f, 0.045f, 0.95f), 10f, true);
            bg.rectTransform.sizeDelta = size;
            var line = Slant(bg.transform, "Line", Cyan, 0f);
            line.rectTransform.anchorMin = new Vector2(0, 0); line.rectTransform.anchorMax = new Vector2(1, 0);
            line.rectTransform.pivot = new Vector2(0.5f, 0); line.rectTransform.sizeDelta = new Vector2(0, 3);
            var txt = Label(bg.transform, "", fontSize, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            Pad(txt.rectTransform, 18, 0, 18, 0);
            txt.supportRichText = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            var ph = Label(bg.transform, placeholder, fontSize, new Color(1, 1, 1, 0.35f), TextAnchor.MiddleLeft, FontStyle.Italic);
            Pad(ph.rectTransform, 18, 0, 18, 0);
            ph.horizontalOverflow = HorizontalWrapMode.Overflow;
            var f = bg.gameObject.AddComponent<InputField>();
            f.targetGraphic = bg;
            f.transition = Selectable.Transition.None;
            f.textComponent = txt;
            f.placeholder = ph;
            f.characterLimit = maxLen;
            f.lineType = InputField.LineType.SingleLine;
            return f;
        }

        /// <summary>Slanted slider (value range min..max). Returns the Slider; fill is colored with <paramref name="fill"/>.</summary>
        public static Slider MakeSlider(Transform parent, Vector2 size, Color fill, float min, float max, bool whole, Action<float> onChanged)
        {
            var back = Slant(parent, "Slider", new Color(0f, 0f, 0f, 0.55f), 8f, true);
            back.rectTransform.sizeDelta = size;
            var area = NewRect("FillArea", back.transform); Pad(area, 6, size.y * 0.3f, 6, size.y * 0.3f);
            var fillG = Slant(area, "Fill", fill, 6f);
            fillG.rectTransform.anchorMin = Vector2.zero; fillG.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            fillG.rectTransform.offsetMin = Vector2.zero; fillG.rectTransform.offsetMax = Vector2.zero;
            var harea = NewRect("HandleArea", back.transform); Pad(harea, 14, 0, 14, 0);
            var hg = Slant(harea, "Handle", Color.white, 8f, true);
            hg.rectTransform.sizeDelta = new Vector2(22, size.y + 12);
            hg.rectTransform.anchorMin = new Vector2(0.5f, 0); hg.rectTransform.anchorMax = new Vector2(0.5f, 1);
            hg.rectTransform.offsetMin = new Vector2(-11, -6); hg.rectTransform.offsetMax = new Vector2(11, 6);
            AddShadow(hg.gameObject, 3f, 0.6f);
            var sl = back.gameObject.AddComponent<Slider>();
            sl.fillRect = fillG.rectTransform; sl.handleRect = hg.rectTransform; sl.targetGraphic = hg;
            sl.direction = Slider.Direction.LeftToRight;
            sl.minValue = min; sl.maxValue = max; sl.wholeNumbers = whole; sl.value = min;
            var nav = sl.navigation; nav.mode = Navigation.Mode.None; sl.navigation = nav;
            if (onChanged != null) sl.onValueChanged.AddListener(v => onChanged(v));
            return sl;
        }

        /// <summary>Square color swatch button with a selection frame (Outline) you toggle through the returned Outline.</summary>
        public static Button MakeSwatch(Transform parent, Color c, float size, Action onClick, out Outline frame)
        {
            var sw = Slant(parent, "Swatch", c, 8f, true);
            sw.rectTransform.sizeDelta = new Vector2(size, size);
            sw.SetV(Color.white, new Color(0.8f, 0.8f, 0.8f, 1f));
            AddShadow(sw.gameObject, 3f, 0.5f);
            frame = sw.gameObject.AddComponent<Outline>();
            frame.effectColor = Color.white; frame.effectDistance = new Vector2(3, -3); frame.enabled = false;
            var b = sw.gameObject.AddComponent<Button>(); b.targetGraphic = sw;
            var cb = b.colors; cb.normalColor = new Color(0.92f, 0.92f, 0.92f, 1f); cb.highlightedColor = Color.white; cb.selectedColor = new Color(0.92f, 0.92f, 0.92f, 1f); b.colors = cb;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            sw.gameObject.AddComponent<HoverFx>().hoverScale = 1.1f;
            b.onClick.AddListener(() => { if (AudioManager.Instance != null) AudioManager.Instance.PlayClick(); if (onClick != null) onClick(); });
            return b;
        }

        public static VerticalLayoutGroup VLayout(RectTransform rt, float spacing, TextAnchor align = TextAnchor.UpperLeft)
        {
            var l = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            l.spacing = spacing; l.childAlignment = align;
            l.childControlWidth = false; l.childControlHeight = false;
            l.childForceExpandWidth = false; l.childForceExpandHeight = false;
            return l;
        }

        public static HorizontalLayoutGroup HLayout(RectTransform rt, float spacing, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var l = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            l.spacing = spacing; l.childAlignment = align;
            l.childControlWidth = false; l.childControlHeight = false;
            l.childForceExpandWidth = false; l.childForceExpandHeight = false;
            return l;
        }

        /// <summary>Builds the controls table at the top-left of <paramref name="parent"/>; returns the height used.</summary>
        public static float BuildControlsTable(Transform parent, float width, int fontSize)
        {
            float y = 0f;
            int n = ControlsHelp.Rows.GetLength(0);
            for (int i = 0; i < n; i++)
            {
                string key = ControlsHelp.Rows[i, 0], desc = ControlsHelp.Rows[i, 1];
                float h = desc.Length > 40 ? fontSize * 2.3f : fontSize * 1.6f;
                var chip = KeyChip(parent, key, fontSize * 1.4f, Orange);
                AtP(chip, TL, TL, new Vector2(0, -y), chip.sizeDelta);
                var dt = Label(parent, desc, fontSize, Color.white, TextAnchor.UpperLeft, FontStyle.Normal);
                At(dt.rectTransform, TL, new Vector2(190, -y + 2), new Vector2(width - 190, h));
                dt.horizontalOverflow = HorizontalWrapMode.Wrap;
                y += h + 6f;
            }
            return y;
        }

        // ---------- async helper ----------
        public static async void RunNet(Func<Task> f, Action<string> onError)
        {
            try
            {
                if (f == null) throw new InvalidOperationException("NetApi not ready");
                await f();
            }
            catch (Exception e)
            {
                string m = OfflineMsg;
                if (!(e is NullReferenceException) && !(e is InvalidOperationException) && !string.IsNullOrEmpty(e.Message))
                {
                    string s = e.Message; if (s.Length > 120) s = s.Substring(0, 120);
                    m += "\n[" + s + "]";
                }
                Debug.LogWarning("[TobeUI] net action failed: " + e);
                if (onError != null) onError(m);
            }
        }

        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t) - 1f;
            const float s = 1.70158f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }
    }
}
