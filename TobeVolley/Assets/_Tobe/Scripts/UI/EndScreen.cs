using UnityEngine;
using UnityEngine.UI;

namespace Tobe.UI
{
    public sealed class EndScreen : UiScreen
    {
        Text title, scoreText, subText, errorText;
        SlantGraphic band;
        int sizeAtEnd = 6;
        string error = "";

        protected override void Build()
        {
            var dim = UiKit.Box(Rt, "Dim", new Color(0.01f, 0f, 0.05f, 0.82f), true);
            UiKit.Stretch(dim.rectTransform);

            band = UiKit.Slant(Rt, "Band", UiKit.Gold, 0f);
            UiKit.At(band.rectTransform, UiKit.C, new Vector2(0, 120), new Vector2(2600, 300));
            band.rectTransform.localRotation = Quaternion.Euler(0, 0, 3.5f);
            UiKit.AddOutline(band.gameObject, Color.black, 6f);

            title = UiKit.Label(Rt, "ПОБЕДА!", 190, Color.white, TextAnchor.MiddleCenter, FontStyle.BoldAndItalic, true);
            UiKit.At(title.rectTransform, UiKit.C, new Vector2(0, 130), new Vector2(1600, 260));
            title.horizontalOverflow = HorizontalWrapMode.Overflow;

            subText = UiKit.Label(Rt, "", 36, UiKit.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.At(subText.rectTransform, UiKit.C, new Vector2(0, -50), new Vector2(1200, 50));

            scoreText = UiKit.Label(Rt, "", 130, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UiKit.At(scoreText.rectTransform, UiKit.C, new Vector2(0, -150), new Vector2(1200, 170));

            var again = UiKit.MakeButton(Rt, "ЕЩЁ МАТЧ", new Vector2(420, 80), UiKit.Orange, OnAgain, 36);
            UiKit.At(again.GetComponent<RectTransform>(), UiKit.C, new Vector2(-240, -300), new Vector2(420, 80));
            var menu = UiKit.MakeButton(Rt, "В МЕНЮ", new Vector2(420, 80), UiKit.Purple, OnMenu, 36);
            UiKit.At(menu.GetComponent<RectTransform>(), UiKit.C, new Vector2(240, -300), new Vector2(420, 80));

            errorText = UiKit.Label(Rt, "", 24, UiKit.Danger, TextAnchor.UpperCenter, FontStyle.Bold);
            UiKit.At(errorText.rectTransform, UiKit.C, new Vector2(0, -360), new Vector2(1300, 90));
        }

        protected override void OnShown()
        {
            error = "";
            var v = GameHub.View;
            sizeAtEnd = v.teamSize == 3 ? 3 : 6;
            PlayerSnap lp;
            bool haveLocal = v.TryGetLocal(out lp);
            if (haveLocal)
            {
                bool win = v.winner == lp.team;
                title.text = win ? "ПОБЕДА!" : "ПОРАЖЕНИЕ";
                band.Set(0f, win ? UiKit.Orange : new Color(0.25f, 0.2f, 0.6f), win ? UiKit.Gold : new Color(0.1f, 0.5f, 0.9f));
                subText.text = TeamLook.Names[Mathf.Clamp(lp.team, 0, 1)];
            }
            else
            {
                int w = Mathf.Clamp(v.winner, 0, 1);
                title.text = v.winner < 0 ? "МАТЧ ОКОНЧЕН" : "ПОБЕДА: " + TeamLook.Names[w];
                band.Set(0f, UiKit.Purple, UiKit.Cyan);
                subText.text = "";
            }
            int s0 = v.score != null && v.score.Length > 1 ? v.score[0] : 0, s1 = v.score != null && v.score.Length > 1 ? v.score[1] : 0;
            scoreText.text = s0 + " : " + s1;
        }

        void OnAgain()
        {
            error = "";
            int n = sizeAtEnd;
            if (UiRoot.Instance != null) UiRoot.Instance.EndDismissed = true;
            if (NetApi.Leave != null) NetApi.Leave();
            UiKit.RunNet(() => NetApi.QuickMatch(n), m =>
            {
                // QuickMatch failed: the main menu is visible now, show the reason there via status
                NetApi.SetStatus(m, false);
            });
        }

        void OnMenu()
        {
            if (UiRoot.Instance != null) UiRoot.Instance.EndDismissed = true;
            if (NetApi.Leave != null) NetApi.Leave();
        }

        void Update()
        {
            errorText.text = error;
            float t = Time.unscaledTime;
            title.rectTransform.localScale = Vector3.one * (1f + 0.02f * Mathf.Sin(t * 3f));
        }
    }
}
