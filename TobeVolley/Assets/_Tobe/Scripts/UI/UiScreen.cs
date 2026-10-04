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
}
