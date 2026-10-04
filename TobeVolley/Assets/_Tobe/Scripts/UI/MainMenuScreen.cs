using UnityEngine;
using UnityEngine.UI;

namespace Tobe.UI
{
    public sealed class MainMenuScreen : UiScreen
    {
        int teamSize = 6;
        Button btn3, btn6, btnFind;
        InputField codeInput;
        Text statusText, errorText, profileText, privateCodeText;
        RectTransform profileCard, profileIcon, spinner, privatePanel, controlsPanel, bgStripe1, bgStripe2;
        string errorMsg = "";

        protected override void Build()
        {
            try { teamSize = PlayerPrefs.GetInt("tobe.size", 6) == 3 ? 3 : 6; } catch (System.Exception) { }

            // background: keep the live arena visible, only shade the left column + top/bottom edges
            var shade = UiKit.Slant(Rt, "Shade", new Color(0.02f, 0.025f, 0.04f, 0.93f), 0f);
            shade.Set(0f, new Color(1, 1, 1, 1f), new Color(1, 1, 1, 0f));
            shade.rectTransform.anchorMin = new Vector2(0, 0); shade.rectTransform.anchorMax = new Vector2(0, 1);
            shade.rectTransform.pivot = new Vector2(0, 0.5f); shade.rectTransform.anchoredPosition = Vector2.zero; shade.rectTransform.sizeDelta = new Vector2(1250, 0);
            var top = UiKit.Slant(Rt, "Top", new Color(0.02f, 0.025f, 0.04f, 0.75f), 0f);
            top.SetV(Color.white, new Color(1, 1, 1, 0f));
            top.rectTransform.anchorMin = new Vector2(0, 1); top.rectTransform.anchorMax = new Vector2(1, 1); top.rectTransform.pivot = new Vector2(0.5f, 1);
            top.rectTransform.anchoredPosition = Vector2.zero; top.rectTransform.sizeDelta = new Vector2(0, 200);
            var bot = UiKit.Slant(Rt, "Bottom", new Color(0.02f, 0.025f, 0.04f, 0.8f), 0f);
            bot.SetV(new Color(1, 1, 1, 0f), Color.white);
            bot.rectTransform.anchorMin = new Vector2(0, 0); bot.rectTransform.anchorMax = new Vector2(1, 0); bot.rectTransform.pivot = new Vector2(0.5f, 0);
            bot.rectTransform.anchoredPosition = Vector2.zero; bot.rectTransform.sizeDelta = new Vector2(0, 220);
            var lines = UiKit.SpeedLines(Rt, "Speed", new Color(1f, 0.6f, 0.2f, 0.22f), 20, 0.18f);
            lines.rectTransform.anchorMin = new Vector2(0, 0); lines.rectTransform.anchorMax = new Vector2(0, 1);
            lines.rectTransform.pivot = new Vector2(0, 0.5f); lines.rectTransform.anchoredPosition = Vector2.zero; lines.rectTransform.sizeDelta = new Vector2(1100, 0);
            var s2 = UiKit.Slant(Rt, "Stripe2", UiKit.Orange, 0f);
            UiKit.At(s2.rectTransform, UiKit.ML, new Vector2(790, 0), new Vector2(22, 1800));
            s2.rectTransform.localRotation = Quaternion.Euler(0, 0, 7f);
            bgStripe2 = s2.rectTransform;
            var s3 = UiKit.Slant(Rt, "Stripe3", UiKit.Cyan, 0f);
            UiKit.At(s3.rectTransform, UiKit.ML, new Vector2(836, 0), new Vector2(8, 1800));
            s3.rectTransform.localRotation = Quaternion.Euler(0, 0, 7f);
            bgStripe1 = s3.rectTransform;

            // logo
            var kanji = UiKit.Label(Rt, "飛べ!", 190, UiKit.Gold, TextAnchor.UpperLeft, FontStyle.Bold, true, true);
            UiKit.At(kanji.rectTransform, UiKit.TL, new Vector2(110, -40), new Vector2(900, 230));
            kanji.horizontalOverflow = HorizontalWrapMode.Overflow;
            var title = UiKit.Label(Rt, "TOBE VOLLEY", 92, Color.white, TextAnchor.UpperLeft, FontStyle.BoldAndItalic, true, true);
            UiKit.At(title.rectTransform, UiKit.TL, new Vector2(118, -250), new Vector2(900, 100));
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            var sub = UiKit.Label(Rt, "аниме-волейбол · онлайн-матчи 3×3 и 6×6", 28, UiKit.Cyan, TextAnchor.UpperLeft, FontStyle.BoldAndItalic);
            UiKit.At(sub.rectTransform, UiKit.TL, new Vector2(122, -345), new Vector2(900, 44));
            sub.horizontalOverflow = HorizontalWrapMode.Overflow;

            // buttons
            float x = 110, y = -410, h = 64, gap = 10;
            btnFind = UiKit.MakeButton(Rt, "НАЙТИ МАТЧ", new Vector2(400, h), UiKit.Orange, OnFind, 36);
            UiKit.At(btnFind.GetComponent<RectTransform>(), UiKit.TL, new Vector2(x, y), new Vector2(400, h));
            btn3 = UiKit.MakeButton(Rt, "3×3", new Vector2(80, h), UiKit.Purple, () => SetSize(3), 28);
            UiKit.At(btn3.GetComponent<RectTransform>(), UiKit.TL, new Vector2(x + 410, y), new Vector2(80, h));
            btn6 = UiKit.MakeButton(Rt, "6×6", new Vector2(80, h), UiKit.Purple, () => SetSize(6), 28);
            UiKit.At(btn6.GetComponent<RectTransform>(), UiKit.TL, new Vector2(x + 495, y), new Vector2(80, h));
            RefreshToggle();
            y -= h + gap;

            var bp = UiKit.MakeButton(Rt, "ПРИВАТНЫЙ МАТЧ", new Vector2(575, h), UiKit.Purple, OnPrivate, 30);
            UiKit.At(bp.GetComponent<RectTransform>(), UiKit.TL, new Vector2(x, y), new Vector2(575, h));
            y -= h + gap;

            codeInput = UiKit.MakeInput(Rt, "КОД", new Vector2(190, h), 30, 8);
            UiKit.At(codeInput.GetComponent<RectTransform>(), UiKit.TL, new Vector2(x, y), new Vector2(190, h));
            codeInput.onValueChanged.AddListener(s => { string u = s.ToUpperInvariant(); if (u != s) codeInput.text = u; });
            var bj = UiKit.MakeButton(Rt, "ВОЙТИ ПО КОДУ", new Vector2(375, h), UiKit.Purple, OnJoin, 28);
            UiKit.At(bj.GetComponent<RectTransform>(), UiKit.TL, new Vector2(x + 200, y), new Vector2(375, h));
            y -= h + gap;

            var bt = UiKit.MakeButton(Rt, "ТРЕНИРОВКА С БОТАМИ", new Vector2(575, h), UiKit.Purple, OnPractice, 30);
            UiKit.At(bt.GetComponent<RectTransform>(), UiKit.TL, new Vector2(x, y), new Vector2(575, h));
            y -= h + gap;

            var bc = UiKit.MakeButton(Rt, "ПЕРСОНАЖ", new Vector2(575, h), UiKit.Purple, () => UiRoot.Instance.OpenCharacter(), 30);
            UiKit.At(bc.GetComponent<RectTransform>(), UiKit.TL, new Vector2(x, y), new Vector2(575, h));
            y -= h + gap;

            var bk = UiKit.MakeButton(Rt, "УПРАВЛЕНИЕ", new Vector2(575, h), UiKit.Purple, () => controlsPanel.gameObject.SetActive(true), 30);
            UiKit.At(bk.GetComponent<RectTransform>(), UiKit.TL, new Vector2(x, y), new Vector2(575, h));
            y -= h + gap;

            var bq = UiKit.MakeButton(Rt, "ВЫХОД", new Vector2(575, h), new Color(0.55f, 0.15f, 0.25f), OnQuit, 30);
            UiKit.At(bq.GetComponent<RectTransform>(), UiKit.TL, new Vector2(x, y), new Vector2(575, h));

            // status line
            var sp = UiKit.Slant(Rt, "Spinner", UiKit.Gold, 0f);
            spinner = sp.rectTransform;
            UiKit.At(spinner, UiKit.BL, new Vector2(124, 60), new Vector2(26, 26));
            statusText = UiKit.Label(Rt, "", 28, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.At(statusText.rectTransform, UiKit.BL, new Vector2(165, 40), new Vector2(1000, 44));
            errorText = UiKit.Label(Rt, "", 24, UiKit.Danger, TextAnchor.LowerLeft, FontStyle.Bold);
            UiKit.At(errorText.rectTransform, UiKit.BL, new Vector2(110, 95), new Vector2(1000, 80));

            // profile card (right): glass, home-team accent
            var card = UiKit.MakePanel(Rt, "ProfileCard", new Vector2(600, 230), 30f, UiKit.Panel, UiKit.Orange);
            UiKit.At(card.rectTransform, UiKit.MR, new Vector2(-110, -40), new Vector2(600, 230));
            profileCard = card.rectTransform;
            var cap = UiKit.Label(card.transform, "ТВОЙ ИГРОК", 22, UiKit.Cyan, TextAnchor.UpperLeft, FontStyle.BoldAndItalic);
            UiKit.Pad(cap.rectTransform, 60, 20, 20, 0);
            profileText = UiKit.Label(card.transform, "", 44, Color.white, TextAnchor.MiddleLeft, FontStyle.BoldAndItalic, true);
            UiKit.Pad(profileText.rectTransform, 170, 55, 20, 70);
            profileText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var edit = UiKit.MakeButton(card.transform, "ИЗМЕНИТЬ ПЕРСОНАЖА", new Vector2(360, 50), UiKit.Purple, () => UiRoot.Instance.OpenCharacter(), 22, 14f);
            UiKit.At(edit.GetComponent<RectTransform>(), UiKit.BL, new Vector2(60, 22), new Vector2(360, 50));

            // private code panel (right, hidden)
            var pp = UiKit.MakePanel(Rt, "PrivatePanel", new Vector2(600, 260), 30f, UiKit.Panel, UiKit.Gold);
            privatePanel = pp.rectTransform;
            UiKit.At(privatePanel, UiKit.MR, new Vector2(-150, 280), new Vector2(600, 260));
            var pc = UiKit.Label(pp.transform, "КОД ПРИВАТНОГО МАТЧА", 24, UiKit.Gold, TextAnchor.UpperLeft);
            UiKit.Pad(pc.rectTransform, 50, 20, 20, 0);
            privateCodeText = UiKit.Label(pp.transform, "", 90, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold, true);
            UiKit.Pad(privateCodeText.rectTransform, 20, 50, 20, 70);
            var cp = UiKit.MakeButton(pp.transform, "СКОПИРОВАТЬ", new Vector2(260, 52), UiKit.Cyan, () => GUIUtility.systemCopyBuffer = privateCodeText.text, 24);
            UiKit.At(cp.GetComponent<RectTransform>(), UiKit.BC, new Vector2(0, 14), new Vector2(260, 52));
            UiKit.SetButtonColor(cp, UiKit.Cyan, UiKit.Navy);
            privatePanel.gameObject.SetActive(false);

            // controls overlay
            var ov = UiKit.Box(Rt, "ControlsOverlay", new Color(0, 0, 0, 0.72f), true);
            UiKit.Stretch(ov.rectTransform);
            controlsPanel = ov.rectTransform;
            var cpn = UiKit.MakePanel(ov.transform, "Panel", new Vector2(1100, 760), 30f, UiKit.Panel, UiKit.Cyan);
            UiKit.At(cpn.rectTransform, UiKit.C, Vector2.zero, new Vector2(1100, 760));
            var ct = UiKit.Label(cpn.transform, "УПРАВЛЕНИЕ", 52, UiKit.Gold, TextAnchor.UpperCenter, FontStyle.Bold, true);
            UiKit.Pad(ct.rectTransform, 0, 30, 0, 0);
            var tbl = UiKit.NewRect("Table", cpn.transform);
            UiKit.At(tbl, UiKit.TL, new Vector2(80, -120), new Vector2(940, 500));
            UiKit.BuildControlsTable(tbl, 940, 30);
            var cl = UiKit.MakeButton(cpn.transform, "ЗАКРЫТЬ", new Vector2(300, 60), UiKit.Orange, () => controlsPanel.gameObject.SetActive(false), 30);
            UiKit.At(cl.GetComponent<RectTransform>(), UiKit.BC, new Vector2(0, 28), new Vector2(300, 60));
            controlsPanel.gameObject.SetActive(false);
        }

        protected override void OnShown()
        {
            RefreshProfile();
            if (privatePanel != null) privatePanel.gameObject.SetActive(false);
            if (controlsPanel != null) controlsPanel.gameObject.SetActive(false);
            errorMsg = ""; errorText.text = "";
        }

        void SetSize(int n)
        {
            teamSize = n;
            try { PlayerPrefs.SetInt("tobe.size", n); } catch (System.Exception) { }
            RefreshToggle();
        }

        void RefreshToggle()
        {
            UiKit.SetButtonColor(btn3, teamSize == 3 ? UiKit.Gold : UiKit.Purple, teamSize == 3 ? UiKit.Navy : Color.white);
            UiKit.SetButtonColor(btn6, teamSize == 6 ? UiKit.Gold : UiKit.Purple, teamSize == 6 ? UiKit.Navy : Color.white);
        }

        void RefreshProfile()
        {
            var p = GameHub.LocalProfile;
            var st = Styles.Get(p.style);
            profileText.text = p.nick + "  #" + p.number + "\n<size=28><color=#FFD23F>" + st.name.ToUpperInvariant() + "</color></size>";
            if (profileIcon != null) Destroy(profileIcon.gameObject);
            profileIcon = UiKit.StyleIcon(profileCard, p.style, 96);
            UiKit.At(profileIcon, UiKit.TL, new Vector2(56, -76), new Vector2(96, 96));
        }

        void SetError(string m) { errorMsg = m; }

        void OnFind()
        {
            if (NetApi.Busy) return;
            errorMsg = "";
            int n = teamSize;
            UiKit.RunNet(() => NetApi.QuickMatch(n), SetError);
        }

        void OnPrivate()
        {
            if (NetApi.Busy) return;
            errorMsg = "";
            int n = teamSize;
            UiKit.RunNet(async () =>
            {
                string code = await NetApi.CreatePrivate(n);
                if (this != null && privateCodeText != null)
                {
                    privateCodeText.text = code ?? "";
                    privatePanel.gameObject.SetActive(!string.IsNullOrEmpty(code));
                }
            }, SetError);
        }

        void OnJoin()
        {
            if (NetApi.Busy) return;
            string code = (codeInput.text ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0) { SetError("Введи код приватного матча."); return; }
            errorMsg = "";
            UiKit.RunNet(() => NetApi.JoinByCode(code), SetError);
        }

        void OnPractice()
        {
            errorMsg = "";
            try
            {
                if (NetApi.StartPractice == null) throw new System.InvalidOperationException("no practice");
                NetApi.StartPractice(teamSize);
            }
            catch (System.Exception e) { SetError("Не удалось начать тренировку: " + e.Message); }
        }

        void OnQuit()
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        void Update()
        {
            string st = NetApi.Status ?? "";
            statusText.text = st;
            bool busy = NetApi.Busy;
            spinner.gameObject.SetActive(busy);
            if (busy) spinner.localRotation = Quaternion.Euler(0, 0, -Time.unscaledTime * 360f);
            errorText.text = errorMsg;
            if (btnFind != null) btnFind.interactable = !busy;
            float t = Time.unscaledTime;
            bgStripe1.anchoredPosition = new Vector2(836 + Mathf.Sin(t * 0.3f) * 12f, 0);
            bgStripe2.anchoredPosition = new Vector2(790 + Mathf.Sin(t * 0.4f + 1f) * 14f, 0);
        }
    }
}
