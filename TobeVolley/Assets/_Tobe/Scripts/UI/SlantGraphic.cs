using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tobe.UI
{
    /// <summary>Parallelogram / gradient quad. slant &gt; 0 shifts the top edge to the right.</summary>
    public class SlantGraphic : MaskableGraphic
    {
        public float slant;
        public Color colorL = Color.white, colorR = Color.white;

        public void Set(float slantPx, Color l, Color r)
        {
            slant = slantPx; colorL = l; colorR = r; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float s = slant;
            float a = Mathf.Min(Mathf.Abs(s), r.width * 0.5f);
            float sp = s > 0 ? a : 0f, sn = s < 0 ? a : 0f;
            Vector3 topL = new Vector3(r.xMin + sp, r.yMax), topR = new Vector3(r.xMax - sn, r.yMax);
            Vector3 botL = new Vector3(r.xMin + sn, r.yMin), botR = new Vector3(r.xMax - sp, r.yMin);
            Color32 cl = colorL * color, cr = colorR * color;
            vh.AddVert(botL, cl, Vector2.zero);
            vh.AddVert(topL, cl, Vector2.up);
            vh.AddVert(topR, cr, Vector2.one);
            vh.AddVert(botR, cr, Vector2.right);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
