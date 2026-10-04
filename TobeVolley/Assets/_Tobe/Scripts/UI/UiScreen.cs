using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tobe.UI
{
    public abstract class UiScreen : MonoBehaviour
    {
        public RectTransform Rt { get; private set; }
        public bool Visible { get; private set; }
        ScreenFx fx;

        /// <summary>Slide-in offset in canvas pixels (zero = pure fade).</summary>
        protected virtual Vector2 SlideIn { get { return new Vector2(-56f, 0f); } }

        public static T Create<T>(Transform parent, string name) where T : UiScreen
        {
            var rt = UiKit.NewRect(name, parent);
            UiKit.Stretch(rt);
            var s = rt.gameObject.AddComponent<T>();
            s.Rt = rt;
            s.fx = rt.gameObject.AddComponent<ScreenFx>();
            s.fx.slide = s.SlideIn;
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
            if (on)
            {
                gameObject.SetActive(true);
                if (fx != null) fx.PlayIn();
                OnShown();
            }
            else
            {
                OnHidden();
                if (fx != null && gameObject.activeInHierarchy) fx.PlayOut(() => { if (this != null && !Visible) gameObject.SetActive(false); });
                else gameObject.SetActive(false);
            }
        }
    }
}
