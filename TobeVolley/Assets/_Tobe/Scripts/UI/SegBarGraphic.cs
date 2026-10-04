using UnityEngine;
using UnityEngine.UI;

namespace Tobe.UI
{
    /// <summary>Slanted segmented bar (Rematch-style). Value 0..1 fills segments left to right, the last one partially.</summary>
    public class SegBarGraphic : MaskableGraphic
    {
        public int segments = 10;
        public float slant = 10f, gap = 4f;
        public Color emptyColor = new Color(1f, 1f, 1f, 0.12f);
        float value = 1f;

        public float Value
        {
            get { return value; }
            set { value = Mathf.Clamp01(value); SetVerticesDirty(); }
        }

        public void Configure(int seg, float slantPx, float gapPx)
        {
            segments = Mathf.Max(1, seg); slant = slantPx; gap = gapPx; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            int n = Mathf.Max(1, segments);
            float totalGap = gap * (n - 1);
            float w = (r.width - totalGap - Mathf.Abs(slant)) / n;
            if (w <= 0.5f) return;
            for (int i = 0; i < n; i++)
            {
                float x0 = r.xMin + i * (w + gap);
                float f = Mathf.Clamp01(value * n - i);
                AddSeg(vh, x0, r.yMin, w, r.height, emptyColor * new Color(1, 1, 1, color.a), 1f);
                if (f > 0.001f) AddSeg(vh, x0, r.yMin, w, r.height, color, f);
            }
        }

        void AddSeg(VertexHelper vh, float x0, float y0, float w, float h, Color c, float fill)
        {
            float s = slant;
            float bl = x0 + (s < 0 ? -s : 0f), tl = x0 + (s > 0 ? s : 0f);
            float fw = w * fill;
            int i = vh.currentVertCount;
            Color32 cc = c;
            vh.AddVert(new Vector3(bl, y0), cc, Vector2.zero);
            vh.AddVert(new Vector3(tl, y0 + h), cc, Vector2.up);
            vh.AddVert(new Vector3(tl + fw, y0 + h), cc, Vector2.one);
            vh.AddVert(new Vector3(bl + fw, y0), cc, Vector2.right);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }
}
