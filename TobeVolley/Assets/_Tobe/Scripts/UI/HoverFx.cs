using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tobe.UI
{
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
}
