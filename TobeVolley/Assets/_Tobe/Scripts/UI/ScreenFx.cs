using UnityEngine;

namespace Tobe.UI
{
    /// <summary>Fade + slide transition driver for a UiScreen root (kept separate so screens can have their own Update).</summary>
    public sealed class ScreenFx : MonoBehaviour
    {
        public Vector2 slide = new Vector2(-56f, 0f);
        public float inTime = 0.32f, outTime = 0.18f;

        RectTransform rt;
        CanvasGroup cg;
        float t = 1f;
        bool goingOut;
        System.Action onOutDone;

        void Ensure()
        {
            if (rt == null) rt = (RectTransform)transform;
            if (cg == null) { cg = GetComponent<CanvasGroup>(); if (cg == null) cg = gameObject.AddComponent<CanvasGroup>(); }
        }

        public void PlayIn()
        {
            Ensure();
            goingOut = false; onOutDone = null; t = 0f;
            Apply(0f);
        }

        public void PlayOut(System.Action done)
        {
            Ensure();
            goingOut = true; onOutDone = done;
            if (!gameObject.activeInHierarchy) { Finish(); return; }
            cg.blocksRaycasts = false;
        }

        void Finish()
        {
            var d = onOutDone; onOutDone = null; goingOut = false;
            if (d != null) d();
        }

        void Apply(float k)
        {
            float e = 1f - (1f - k) * (1f - k) * (1f - k);
            cg.alpha = k;
            if (rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one)
            {
                var o = slide * (1f - e);
                rt.offsetMin = o; rt.offsetMax = o;
            }
            cg.blocksRaycasts = k > 0.6f;
        }

        void Update()
        {
            if (cg == null) return;
            float dt = Time.unscaledDeltaTime;
            if (goingOut)
            {
                t -= dt / Mathf.Max(0.01f, outTime);
                if (t <= 0f) { t = 0f; Apply(0f); Finish(); return; }
                Apply(t);
            }
            else if (t < 1f)
            {
                t = Mathf.Min(1f, t + dt / Mathf.Max(0.01f, inTime));
                Apply(t);
            }
        }
    }
}
