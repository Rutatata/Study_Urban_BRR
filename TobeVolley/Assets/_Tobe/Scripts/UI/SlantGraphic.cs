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
        public float slantClamp = 0.5f; // max slant as a fraction of the width
        public Color colorL = Color.white, colorR = Color.white;
        public Color colorTop = Color.white, colorBot = Color.white; // vertical tint (multiplied)

        public void Set(float slantPx, Color l, Color r)
        {
            slant = slantPx; colorL = l; colorR = r; SetVerticesDirty();
        }

        public void SetV(Color top, Color bot)
        {
            colorTop = top; colorBot = bot; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float s = slant;
            float a = Mathf.Min(Mathf.Abs(s), r.width * slantClamp);
            float sp = s > 0 ? a : 0f, sn = s < 0 ? a : 0f;
            Vector3 topL = new Vector3(r.xMin + sp, r.yMax), topR = new Vector3(r.xMax - sn, r.yMax);
            Vector3 botL = new Vector3(r.xMin + sn, r.yMin), botR = new Vector3(r.xMax - sp, r.yMin);
            Color cl = colorL * color, cr = colorR * color;
            vh.AddVert(botL, (Color32)(cl * colorBot), Vector2.zero);
            vh.AddVert(topL, (Color32)(cl * colorTop), Vector2.up);
            vh.AddVert(topR, (Color32)(cr * colorTop), Vector2.one);
            vh.AddVert(botR, (Color32)(cr * colorBot), Vector2.right);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
