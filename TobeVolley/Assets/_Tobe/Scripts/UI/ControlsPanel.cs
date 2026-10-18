// Экран переназначения управления: вкладки «клавиатура и мышь» / «геймпад», строка на действие — нажми строку, затем новую кнопку.
using UnityEngine;
using UnityEngine.UI;

namespace Tobe.UI
{
    public sealed class ControlsPanel : MonoBehaviour
    {
        bool pad; int waiting = -1; float waitT;
        Button tabK, tabP; Text hint, sensText;
        readonly Button[] rows = new Button[Controls.Names.Length];
        Slider sens;

        public static ControlsPanel Create(RectTransform parent, float width)
        {
            var cp = parent.gameObject.AddComponent<ControlsPanel>();
            cp.Build(parent, width);
            return cp;
        }

        void Build(RectTransform root, float width)
        {
            tabK = UiKit.MakeButton(root, "КЛАВИАТУРА И МЫШЬ", new Vector2(440, 56), UiKit.Orange, () => { pad = false; waiting = -1; Refresh(); }, 24, 14f);
            UiKit.At(tabK.GetComponent<RectTransform>(), UiKit.TL, new Vector2(0, 0), new Vector2(440, 56));
            tabP = UiKit.MakeButton(root, "ГЕЙМПАД", new Vector2(300, 56), UiKit.Purple, () => { pad = true; waiting = -1; Refresh(); }, 24, 14f);
            UiKit.At(tabP.GetComponent<RectTransform>(), UiKit.TL, new Vector2(452, 0), new Vector2(300, 56));
            var rs = UiKit.MakeButton(root, "СБРОС", new Vector2(170, 56), UiKit.Purple, () => { Controls.ResetDefaults(pad); waiting = -1; Refresh(); }, 22, 14f);
            UiKit.At(rs.GetComponent<RectTransform>(), UiKit.TL, new Vector2(width - 170, 0), new Vector2(170, 56));

            float y = -72f, colW = (width - 20f) * 0.5f;
            for (int i = 0; i < rows.Length; i++)
            {
                int idx = i;
                float x = (i % 2) * (colW + 20f), yy = y - (i / 2) * 50f;
                var name = UiKit.Label(root, Controls.Names[i], 20, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
                UiKit.At(name.rectTransform, UiKit.TL, new Vector2(x, yy), new Vector2(colW - 150f, 44));
                var b = UiKit.MakeButton(root, "", new Vector2(140, 44), UiKit.Purple, () => { waiting = idx; waitT = 0f; Refresh(); }, 20, 10f);
                UiKit.At(b.GetComponent<RectTransform>(), UiKit.TL, new Vector2(x + colW - 140f, yy), new Vector2(140, 44));
                rows[i] = b;
            }
            float yb = y - ((rows.Length + 1) / 2) * 50f - 10f;
            var fixedL = UiKit.Label(root, "Бег — WASD / стрелки или левый стик.  Камера и прицел — мышь или правый стик.  Тип подачи — 1/2/3 на клавиатуре.", 18, UiKit.Dim, TextAnchor.UpperLeft, FontStyle.Italic);
            UiKit.At(fixedL.rectTransform, UiKit.TL, new Vector2(0, yb), new Vector2(width, 50));
            sensText = UiKit.Label(root, "", 20, UiKit.Cyan, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.At(sensText.rectTransform, UiKit.TL, new Vector2(0, yb - 52), new Vector2(420, 34));
            sens = UiKit.MakeSlider(root, new Vector2(420, 24), UiKit.Orange, 0.3f, 3f, false, v => { if (pad) Controls.PadSens = 170f * v; else Controls.MouseSens = 0.1f * v; Save(); Refresh(); });
            UiKit.At(sens.GetComponent<RectTransform>(), UiKit.TL, new Vector2(440, yb - 56), new Vector2(420, 24));
            hint = UiKit.Label(root, "", 22, UiKit.Gold, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic);
            UiKit.At(hint.rectTransform, UiKit.TL, new Vector2(0, yb - 100), new Vector2(width, 36));
            try { Controls.MouseSens = PlayerPrefs.GetFloat("tobe.sens.mouse", 0.1f); Controls.PadSens = PlayerPrefs.GetFloat("tobe.sens.pad", 170f); } catch (System.Exception) { }
            Refresh();
        }

        static void Save() { try { PlayerPrefs.SetFloat("tobe.sens.mouse", Controls.MouseSens); PlayerPrefs.SetFloat("tobe.sens.pad", Controls.PadSens); PlayerPrefs.Save(); } catch (System.Exception) { } }

        void Refresh()
        {
            UiKit.SetButtonColor(tabK, pad ? UiKit.Purple : UiKit.Orange);
            UiKit.SetButtonColor(tabP, pad ? UiKit.Orange : UiKit.Purple);
            for (int i = 0; i < rows.Length; i++)
            {
                var t = rows[i].GetComponentInChildren<Text>();
                if (t != null) t.text = waiting == i ? "…" : Controls.Label(Controls.Binding((Act)i, pad));
                UiKit.SetButtonColor(rows[i], waiting == i ? UiKit.Orange : UiKit.Purple);
            }
            float v = pad ? Controls.PadSens / 170f : Controls.MouseSens / 0.1f;
            sensText.text = (pad ? "ЧУВСТВИТЕЛЬНОСТЬ СТИКА: " : "ЧУВСТВИТЕЛЬНОСТЬ МЫШИ: ") + v.ToString("0.0");
            if (sens != null && Mathf.Abs(sens.value - v) > 0.01f) sens.SetValueWithoutNotify(v);
            hint.text = waiting >= 0 ? (pad ? "Нажми кнопку на геймпаде для «" : "Нажми клавишу или кнопку мыши для «") + Controls.Names[waiting] + "»" : "Нажми на кнопку действия, чтобы назначить другую.";
        }

        void Update()
        {
            if (waiting < 0) return;
            waitT += Time.unscaledDeltaTime;
            if (waitT < 0.15f) return;   // клик мыши, которым выбрали строку, не должен сразу стать привязкой
            string b = Controls.CaptureNext(pad);
            if (b == null) { if (waitT > 8f) { waiting = -1; Refresh(); } return; }
            Controls.SetBinding((Act)waiting, pad, b);
            waiting = -1; Refresh();
        }
    }
}
