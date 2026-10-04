using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tobe.UI
{
    public class CircleGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            Vector2 c = r.center; float rx = r.width * 0.5f, ry = r.height * 0.5f;
            const int N = 28;
            Color32 col = color;
            vh.AddVert(new Vector3(c.x, c.y), col, new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2f;
                vh.AddVert(new Vector3(c.x + Mathf.Cos(a) * rx, c.y + Mathf.Sin(a) * ry), col, new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }
            for (int i = 1; i <= N; i++) vh.AddTriangle(0, i + 1, i);
        }
    }
}
