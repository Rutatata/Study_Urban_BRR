using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tobe.UI
{
    /// <summary>Button hover: scale pop, accent stripe growth and a light sweep across the face.</summary>
    public sealed class HoverFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public float hoverScale = 1.03f;
        public RectTransform sweep, accent;
        public float accentMin = 8f, accentMax = 22f;
        bool hover;
        float k, sweepT = 1f;

        public void OnPointerEnter(PointerEventData e) { hover = true; sweepT = 0f; }
        public void OnPointerExit(PointerEventData e) { hover = false; }
        void OnDisable() { hover = false; k = 0f; transform.localScale = Vector3.one; if (sweep != null) sweep.gameObject.SetActive(false); }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            k = Mathf.MoveTowards(k, hover ? 1f : 0f, dt * 7f);
            float s = Mathf.Lerp(1f, hoverScale, k);
            transform.localScale = new Vector3(s, s, 1f);
            if (accent != null) accent.sizeDelta = new Vector2(Mathf.Lerp(accentMin, accentMax, k), accent.sizeDelta.y);
            if (sweep != null)
            {
                bool run = sweepT < 1f;
                if (run != sweep.gameObject.activeSelf) sweep.gameObject.SetActive(run);
                if (run)
                {
                    sweepT = Mathf.Min(1f, sweepT + dt * 2.6f);
                    var par = (RectTransform)transform;
                    float w = par.rect.width;
                    float x = Mathf.Lerp(-0.2f * w, 1.15f * w, sweepT);
                    sweep.anchoredPosition = new Vector2(x, 0f);
                }
            }
        }
    }
}
