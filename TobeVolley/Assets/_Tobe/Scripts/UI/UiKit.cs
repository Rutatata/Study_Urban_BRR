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

    /// <summary>Parallelogram / gradient quad. slant &gt; 0 shifts the top edge to the right.</summary>
    public class SlantGraphic : MaskableGraphic
    {
        public float slant;
        public Color colorL = Color.white, colorR = Color.white;

        public void Set(float slantPx, Color l, Color r)
        {
            slant = slantPx; colorL = l; colorR = r; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float s = slant;
            float a = Mathf.Min(Mathf.Abs(s), r.width * 0.5f);
            float sp = s > 0 ? a : 0f, sn = s < 0 ? a : 0f;
            Vector3 topL = new Vector3(r.xMin + sp, r.yMax), topR = new Vector3(r.xMax - sn, r.yMax);
            Vector3 botL = new Vector3(r.xMin + sn, r.yMin), botR = new Vector3(r.xMax - sp, r.yMin);
            Color32 cl = colorL * color, cr = colorR * color;
            vh.AddVert(botL, cl, Vector2.zero);
            vh.AddVert(topL, cl, Vector2.up);
            vh.AddVert(topR, cr, Vector2.one);
            vh.AddVert(botR, cr, Vector2.right);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }

    public class CircleGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            Vector2 c = r.center; float rx = r.width * 0.5f, ry = r.height * 0.5f;
            const int N = 28;
            Color32 col = color;
            vh.AddVert(new Vector3(c.x, c.y), col, new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2f;
                vh.AddVert(new Vector3(c.x + Mathf.Cos(a) * rx, c.y + Mathf.Sin(a) * ry), col, new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }
            for (int i = 1; i <= N; i++) vh.AddTriangle(0, i + 1, i);
        }
    }

    public sealed class HoverFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public float hoverScale = 1.04f;
        bool hover;
        public void OnPointerEnter(PointerEventData e) { hover = true; }
        public void OnPointerExit(PointerEventData e) { hover = false; }
        void OnDisable() { hover = false; transform.localScale = Vector3.one; }
        void Update()
        {
            float t = hover ? hoverScale : 1f;
            float s = Mathf.MoveTowards(transform.localScale.x, t, Time.unscaledDeltaTime * 0.8f);
            transform.localScale = new Vector3(s, s, 1f);
        }
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

    public abstract class UiScreen : MonoBehaviour
    {
        public RectTransform Rt { get; private set; }
        public bool Visible { get; private set; }

        public static T Create<T>(Transform parent, string name) where T : UiScreen
        {
            var rt = UiKit.NewRect(name, parent);
            UiKit.Stretch(rt);
            var s = rt.gameObject.AddComponent<T>();
            s.Rt = rt;
            s.Build();
            s.Visible = false;
            rt.gameObject.SetActive(false);
            return s;
        }

        protected abstract void Build();
        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }

        public void Show(bool on)
        {
            if (on == Visible) return;
            Visible = on;
            gameObject.SetActive(on);
            if (on) OnShown(); else OnHidden();
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
                PlayerPrefs.Save();
            }
            catch (Exception) { }
            GameHub.LocalProfile = p;
        }
    }

    public static class UiKit
    {
        public static readonly Color Navy = new Color(0.04f, 0.03f, 0.11f, 1f);
        public static readonly Color Panel = new Color(0.09f, 0.07f, 0.21f, 0.94f);
        public static readonly Color PanelLight = new Color(0.16f, 0.11f, 0.33f, 0.95f);
        public static readonly Color Purple = new Color(0.34f, 0.2f, 0.66f, 1f);
        public static readonly Color Orange = new Color32(0xFF, 0x8A, 0x1A, 255);
        public static readonly Color Gold = new Color32(0xFF, 0xD2, 0x3F, 255);
        public static readonly Color Cyan = new Color32(0x29, 0xE6, 0xFF, 255);
        public static readonly Color Dim = new Color(1f, 1f, 1f, 0.6f);
        public static readonly Color Danger = new Color(1f, 0.35f, 0.35f, 1f);

        public static readonly Vector2 TL = new Vector2(0, 1), TC = new Vector2(.5f, 1), TR = new Vector2(1, 1),
            ML = new Vector2(0, .5f), C = new Vector2(.5f, .5f), MR = new Vector2(1, .5f),
            BL = new Vector2(0, 0), BC = new Vector2(.5f, 0), BR = new Vector2(1, 0);

        public const string OfflineMsg = "Онлайн недоступен: привяжи проект к Unity Gaming Services (см. README). Тренировка работает офлайн.";

        static Font _font;
        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    try { _font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Tahoma", "Verdana", "DejaVu Sans", "Yu Gothic", "Meiryo", "MS Gothic", "Noto Sans CJK JP" }, 32); }
                    catch (Exception) { _font = null; }
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _font;
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

        /// <summary>Dark slanted panel with a soft drop shadow and an optional accent outline.</summary>
        public static SlantGraphic MakePanel(Transform parent, string name, Vector2 size, float slant = 24f, Color? fill = null, Color? accent = null)
        {
            var g = Slant(parent, name, fill ?? Panel, slant);
            g.rectTransform.sizeDelta = size;
            AddShadow(g.gameObject, 8f, 0.5f);
            if (accent.HasValue) AddOutline(g.gameObject, accent.Value, 2.5f);
            return g;
        }

        public static Text Label(Transform parent, string text, int size, Color c, TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle fs = FontStyle.Bold, bool outline = false)
        {
            var rt = NewRect("Text", parent);
            Stretch(rt);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.fontSize = size; t.color = c; t.alignment = anchor; t.fontStyle = fs;
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
            AddShadow(g.gameObject, 5f, 0.5f);
            var b = g.gameObject.AddComponent<Button>();
            b.targetGraphic = g;
            b.transition = Selectable.Transition.ColorTint;
            var cb = b.colors;
            cb.normalColor = new Color(0.86f, 0.86f, 0.9f, 1f);
            cb.highlightedColor = Color.white;
            cb.selectedColor = new Color(0.86f, 0.86f, 0.9f, 1f);
            cb.pressedColor = new Color(0.65f, 0.65f, 0.7f, 1f);
            cb.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            cb.colorMultiplier = 1f; cb.fadeDuration = 0.08f;
            b.colors = cb;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            var lab = Label(g.transform, text, fontSize, Color.white, TextAnchor.MiddleCenter);
            lab.horizontalOverflow = HorizontalWrapMode.Overflow;
            g.gameObject.AddComponent<HoverFx>();
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
            var bg = Slant(parent, "Input", new Color(0.03f, 0.02f, 0.09f, 0.95f), 10f, true);
            bg.rectTransform.sizeDelta = size;
            AddOutline(bg.gameObject, new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f), 2f);
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
                var chip = Slant(parent, "Key", new Color(0.14f, 0.1f, 0.3f, 1f), 10f);
                At(chip.rectTransform, TL, new Vector2(0, -y), new Vector2(170, fontSize * 1.4f));
                AddOutline(chip.gameObject, new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f), 1.5f);
                var kt = Label(chip.transform, key, fontSize, Gold, TextAnchor.MiddleCenter);
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
