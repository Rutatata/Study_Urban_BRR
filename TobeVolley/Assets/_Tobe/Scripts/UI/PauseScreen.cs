using UnityEngine;
using UnityEngine.UI;

namespace Tobe.UI
{
    /// <summary>Overlay shown on Esc during a match. The match keeps running underneath.</summary>
    public sealed class PauseScreen : UiScreen
    {
        RectTransform tbl, drillRow;
        readonly Button[] drillBtn = new Button[6];
        protected override void OnShown()
        {   // таблица под текущее устройство и привязки
            for (int i = tbl.childCount - 1; i >= 0; i--) Destroy(tbl.GetChild(i).gameObject);
            UiKit.BuildControlsTable(tbl, 940, 28);
            drillRow.gameObject.SetActive(Drill.Active);
            RefreshDrill();
        }
        void RefreshDrill() { for (int k = 1; k <= 5; k++) UiKit.SetButtonColor(drillBtn[k], Drill.Kind == k ? UiKit.Orange : UiKit.Purple); }
        protected override void Build()
        {
            var dim = UiKit.Box(Rt, "Dim", new Color(0.01f, 0f, 0.05f, 0.75f), true);
            UiKit.Stretch(dim.rectTransform);

            var panel = UiKit.MakePanel(Rt, "Panel", new Vector2(1100, 900), 30f, UiKit.Panel, UiKit.Orange);
            UiKit.At(panel.rectTransform, UiKit.C, Vector2.zero, new Vector2(1100, 900));
            var title = UiKit.Label(panel.transform, "ПАУЗА", 72, UiKit.Gold, TextAnchor.UpperCenter, FontStyle.BoldAndItalic, true);
            UiKit.Pad(title.rectTransform, 0, 24, 0, 0);
            var note = UiKit.Label(panel.transform, "Матч продолжается", 22, UiKit.Dim, TextAnchor.UpperCenter, FontStyle.Italic);
            UiKit.Pad(note.rectTransform, 0, 112, 0, 0);

            tbl = UiKit.NewRect("Table", panel.transform);
            UiKit.At(tbl, UiKit.TL, new Vector2(80, -160), new Vector2(940, 520));
            UiKit.BuildControlsTable(tbl, 940, 28);

            // полигон: смена упражнения (счётчик обнуляется)
            drillRow = UiKit.NewRect("Drills", panel.transform);
            UiKit.At(drillRow, UiKit.BC, new Vector2(0, 136), new Vector2(1000, 64));
            for (int k = 1; k <= 5; k++)
            {
                int kk = k;
                var b = UiKit.MakeButton(drillRow, Drill.Names[k], new Vector2(190, 60), UiKit.Purple, () => { Drill.Begin(kk); RefreshDrill(); if (UiRoot.Instance != null) UiRoot.Instance.ClosePause(); }, 24, 12f);
                UiKit.At(b.GetComponent<RectTransform>(), UiKit.TL, new Vector2((k - 1) * 202, 0), new Vector2(190, 60));
                drillBtn[k] = b;
            }

            var resume = UiKit.MakeButton(panel.transform, "ПРОДОЛЖИТЬ", new Vector2(440, 76), UiKit.Orange, () => { if (UiRoot.Instance != null) UiRoot.Instance.ClosePause(); }, 34);
            UiKit.At(resume.GetComponent<RectTransform>(), UiKit.BC, new Vector2(-250, 40), new Vector2(440, 76));
            var leave = UiKit.MakeButton(panel.transform, "ПОКИНУТЬ МАТЧ", new Vector2(440, 76), new Color(0.55f, 0.15f, 0.25f), () =>
            {
                if (UiRoot.Instance != null) UiRoot.Instance.ClosePause();
                Drill.Stop();
                if (NetApi.Leave != null) NetApi.Leave();
            }, 34);
            UiKit.At(leave.GetComponent<RectTransform>(), UiKit.BC, new Vector2(250, 40), new Vector2(440, 76));
        }
    }
}
