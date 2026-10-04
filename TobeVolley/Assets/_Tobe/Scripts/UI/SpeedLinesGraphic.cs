using UnityEngine;
using UnityEngine.UI;

namespace Tobe.UI
{
    /// <summary>Anime speed-line streaks flowing horizontally; deterministic pseudo-random, cheap to rebuild.</summary>
    public class SpeedLinesGraphic : MaskableGraphic
    {
        public int count = 26;
        public float speed = 0.35f, minLen = 0.12f, maxLen = 0.5f, thickness = 3f, tilt = 0.12f;
        public int seed = 7;
        float phase;

        public float Phase
        {
            get { return phase; }
            set { phase = value; SetVerticesDirty(); }
        }

        static float Hash(int i, int k)
        {
            float x = Mathf.Sin(i * 12.9898f + k * 78.233f + 0.5f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }

        void Update()
        {
            if (speed != 0f) Phase = Mathf.Repeat(phase + Time.unscaledDeltaTime * speed, 1000f);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            for (int i = 0; i < count; i++)
            {
                float y = r.yMin + Hash(i, seed) * r.height;
                float len = (minLen + Hash(i, seed + 1) * (maxLen - minLen)) * r.width;
                float sp = 0.5f + Hash(i, seed + 2);
                float u = Mathf.Repeat(Hash(i, seed + 3) + phase * sp, 1f);
                float xHead = r.xMin - len + u * (r.width + len * 2f);
                float th = thickness * (0.5f + Hash(i, seed + 4));
                float a = color.a * (0.35f + 0.65f * Hash(i, seed + 5));
                Color32 head = new Color(color.r, color.g, color.b, a);
                Color32 tail = new Color(color.r, color.g, color.b, 0f);
                float dy = len * tilt;
                int v = vh.currentVertCount;
                vh.AddVert(new Vector3(xHead - len, y + dy), tail, Vector2.zero);
                vh.AddVert(new Vector3(xHead - len, y + dy + th * 0.2f), tail, Vector2.up);
                vh.AddVert(new Vector3(xHead, y + th), head, Vector2.one);
                vh.AddVert(new Vector3(xHead, y), head, Vector2.right);
                vh.AddTriangle(v, v + 1, v + 2);
                vh.AddTriangle(v + 2, v + 3, v);
            }
        }
    }
}
