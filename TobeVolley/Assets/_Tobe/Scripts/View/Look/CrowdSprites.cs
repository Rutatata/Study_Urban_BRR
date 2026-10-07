// Болельщики-спрайты: каждый — квад с аниме-фигуркой из атласа (шейдер Tobe/Crowd). Атлас рисуется кодом при запуске:
// 8 причёсок x 3 позы, каналы-маски (футболка / кожа / волосы) + тёмный контур, поэтому цвета у каждого свои.
// Все фигурки трибуны — один меш и один вызов отрисовки; прыжки и руки анимирует шейдер.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tobe.View
{
    public sealed class CrowdSprites
    {
        const int Cell = 128, Cols = 8, Rows = 3;

        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Color> col = new List<Color>();
        readonly List<Vector4> uv0 = new List<Vector4>(), uv1 = new List<Vector4>(), uv2 = new List<Vector4>();
        readonly List<int> idx = new List<int>();
        Bounds bounds; bool hasBounds;

        public int Count => v.Count / 4;

        /// <summary>Фигурка с опорой в точке p (сиденье). variant = причёска 0..7, phase 0..1, scale ~1.</summary>
        public void Add(Vector3 p, Color shirt, Color skin, Color hair, int variant, float phase, float scale)
        {
            int b = v.Count;
            for (int k = 0; k < 4; k++)
            {
                v.Add(p);
                col.Add(shirt);
                float x = (k == 0 || k == 3) ? -0.5f : 0.5f, y = k >= 2 ? 1f : 0f;
                uv0.Add(new Vector4(x, y, variant, phase));
                uv1.Add(new Vector4(skin.r, skin.g, skin.b, scale));
                uv2.Add(new Vector4(hair.r, hair.g, hair.b, 0f));
            }
            idx.Add(b); idx.Add(b + 2); idx.Add(b + 1);
            idx.Add(b); idx.Add(b + 3); idx.Add(b + 2);
            var bb = new Bounds(p + Vector3.up * 0.5f, new Vector3(1.2f, 1.4f, 1.2f));
            if (hasBounds) bounds.Encapsulate(bb); else { bounds = bb; hasBounds = true; }
        }

        public GameObject Flush(Transform parent, string name)
        {
            if (v.Count == 0) return null;
            var mesh = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(v); mesh.SetColors(col);
            mesh.SetUVs(0, uv0); mesh.SetUVs(1, uv1); mesh.SetUVs(2, uv2);
            mesh.SetTriangles(idx, 0);
            mesh.bounds = bounds;      // квады раскрываются в шейдере, поэтому границы задаём сами
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            v.Clear(); col.Clear(); uv0.Clear(); uv1.Clear(); uv2.Clear(); idx.Clear(); hasBounds = false;
            return go;
        }

        // ------------------------------------------------------------------ материал и атлас
        static Material mat;
        public static Material Material
        {
            get
            {
                if (mat != null) return mat;
                var sh = Resources.Load<Shader>("Shaders/TobeCrowd");
                if (sh == null) sh = Shader.Find("Tobe/Crowd");
                mat = new Material(sh) { name = "tobe_crowd" };
                mat.SetTexture("_MainTex", BuildAtlas());
                mat.SetColor("_Tint", new Color(0.93f, 0.93f, 1f));
                return mat;
            }
        }

        // метки пикселей ячейки: 0 пусто, 1 футболка, 2 кожа, 3 волосы, 4 тёмный (глаза / контур)
        static int[] lab;

        static Texture2D BuildAtlas()
        {
            var tex = new Texture2D(Cell * Cols, Cell * Rows, TextureFormat.RGBA32, true) { name = "crowd_atlas", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[tex.width * tex.height];
            lab = new int[Cell * Cell];
            for (int pose = 0; pose < Rows; pose++)
                for (int hv = 0; hv < Cols; hv++)
                {
                    System.Array.Clear(lab, 0, lab.Length);
                    DrawFigure(hv, pose);
                    Outline();
                    for (int y = 0; y < Cell; y++)
                        for (int x = 0; x < Cell; x++)
                        {
                            int l = lab[y * Cell + x];
                            Color32 c = l == 1 ? new Color32(255, 0, 0, 255) : l == 2 ? new Color32(0, 255, 0, 255) : l == 3 ? new Color32(0, 0, 255, 255)
                                      : l == 4 ? new Color32(0, 0, 0, 255) : new Color32(0, 0, 0, 0);
                            px[(pose * Cell + y) * tex.width + hv * Cell + x] = c;
                        }
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        static void DrawFigure(int hv, int pose)
        {
            // волосы за спиной (длинные / каре / хвост) рисуются первыми, всё остальное — поверх
            if (hv == 4) { Rect(42, 34, 86, 84, 3); Ellipse(64, 84, 24, 22, 3); }
            if (hv == 1) Ellipse(64, 82, 23, 23, 3);
            if (hv == 3) Ellipse(87, 80, 9, 17, 3);

            // руки за телом не видны; поднятые руки — рукав (футболка) + кисть
            if (pose <= 1) Capsule(37, 54, 31, 18, 7, 1);    // левая рука опущена
            if (pose == 0) Capsule(91, 54, 97, 18, 7, 1);    // правая рука опущена
            // туловище: плечи скруглены, к сиденью шире
            Ellipse(64, 30, 31, 33, 1, -1, 58);
            Rect(33, 0, 95, 30, 1);
            Rect(58, 52, 70, 66, 2);                       // шея
            Ellipse(64, 80, 17, 19, 2);                    // голова
            Circle(57, 78, 2.6f, 4); Circle(71, 78, 2.6f, 4);   // глаза

            switch (hv)
            {
                case 0: Cap(84); Ellipse(59, 93, 14, 6, 3); break;                       // короткая с чёлкой
                case 1: Cap(86); break;                                                // каре
                case 2: Cap(84); for (int i = 0; i < 5; i++) Tri(45 + i * 9.5f, 94, 55 + i * 9.5f, 94, 50 + i * 9.5f, 113, 3); break;   // ёжик-пики
                case 3: Cap(85); break;                                                // хвост
                case 4: Cap(86); break;                                                // длинные
                case 5: Cap(85); Circle(46, 101, 9, 3); Circle(82, 101, 9, 3); break;  // два пучка
                case 6: Ellipse(64, 90, 20, 13, 1, 86, 999); Rect(44, 84, 92, 89, 1); break;  // кепка цвета футболки
                default: Ellipse(64, 86, 18, 15, 3, 88, 999); break;                   // короткий ёжик
            }

            if (pose >= 1) { Capsule(91, 54, 104, 94, 7, 1); Circle(106, 103, 7.5f, 2); }
            if (pose == 2) { Capsule(37, 54, 24, 94, 7, 1); Circle(22, 103, 7.5f, 2); }
        }

        static void Cap(float clipY) => Ellipse(64, 88, 20, 15, 3, clipY, 999);

        static void Put(int x, int y, int l) { if (x >= 1 && y >= 0 && x < Cell - 1 && y < Cell - 2) lab[y * Cell + x] = l; }

        static void Ellipse(float cx, float cy, float rx, float ry, int l, float yMin = -999, float yMax = 999)
        {
            for (int y = (int)(cy - ry); y <= (int)(cy + ry) + 1; y++)
                for (int x = (int)(cx - rx); x <= (int)(cx + rx) + 1; x++)
                {
                    float dx = (x + .5f - cx) / rx, dy = (y + .5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f && y >= yMin && y <= yMax) Put(x, y, l);
                }
        }
        static void Circle(float cx, float cy, float r, int l) => Ellipse(cx, cy, r, r, l);
        static void Rect(int x0, int y0, int x1, int y1, int l) { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Put(x, y, l); }

        static void Capsule(float ax, float ay, float bx, float by, float r, int l)
        {
            float vx = bx - ax, vy = by - ay, len2 = vx * vx + vy * vy;
            for (int y = (int)(Mathf.Min(ay, by) - r); y <= (int)(Mathf.Max(ay, by) + r) + 1; y++)
                for (int x = (int)(Mathf.Min(ax, bx) - r); x <= (int)(Mathf.Max(ax, bx) + r) + 1; x++)
                {
                    float px = x + .5f - ax, py = y + .5f - ay;
                    float t = Mathf.Clamp01((px * vx + py * vy) / len2);
                    float dx = px - vx * t, dy = py - vy * t;
                    if (dx * dx + dy * dy <= r * r) Put(x, y, l);
                }
        }

        static void Tri(float ax, float ay, float bx, float by, float cx, float cy, int l)
        {
            for (int y = (int)Mathf.Min(ay, Mathf.Min(by, cy)); y <= (int)Mathf.Max(ay, Mathf.Max(by, cy)) + 1; y++)
                for (int x = (int)Mathf.Min(ax, Mathf.Min(bx, cx)); x <= (int)Mathf.Max(ax, Mathf.Max(bx, cx)) + 1; x++)
                {
                    float px = x + .5f, py = y + .5f;
                    float d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
                    float d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
                    float d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
                    bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
                    if (!(neg && pos)) Put(x, y, l);
                }
        }

        /// <summary>Тёмный контур толщиной 2 пикселя вокруг фигурки — аниме-обводка.</summary>
        static void Outline()
        {
            var src = (int[])lab.Clone();
            for (int y = 0; y < Cell; y++)
                for (int x = 0; x < Cell; x++)
                {
                    if (src[y * Cell + x] != 0) continue;
                    bool near = false;
                    for (int dy = -2; dy <= 2 && !near; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= Cell || yy >= Cell || dx * dx + dy * dy > 5) continue;
                            int s = src[yy * Cell + xx];
                            if (s != 0 && s != 4) { near = true; break; }
                        }
                    if (near) lab[y * Cell + x] = 4;
                }
        }
    }
}
