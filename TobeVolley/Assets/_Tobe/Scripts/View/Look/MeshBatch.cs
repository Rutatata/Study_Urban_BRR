// Static geometry batcher: collects boxes / beams / cylinders / spheres / quads per material and flushes them as one mesh per material.
// Windings are validated against the requested normal (Unity: front face = clockwise = Cross(p1-p0, p2-p0) points at the viewer).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tobe.View
{
    public sealed class MeshBatch
    {
        sealed class Data
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<Color32> c = new List<Color32>();
            public readonly List<int> t = new List<int>();
        }

        readonly Dictionary<Material, Data> data = new Dictionary<Material, Data>();
        readonly List<Material> order = new List<Material>();
        public readonly string Name;
        public bool CastShadows;
        public bool ReceiveShadows;
        /// <summary>UV density (texture repeats per meter) used by Box / Beam faces.</summary>
        public float UvPerMeter = 1f;

        public MeshBatch(string name) { Name = name; }

        Data D(Material m)
        {
            if (!data.TryGetValue(m, out var d)) { d = new Data(); data[m] = d; order.Add(m); }
            return d;
        }

        public int VertexCount(Material m) => data.TryGetValue(m, out var d) ? d.v.Count : 0;

        // ------------------------------------------------------------------ primitives
        static readonly float[] faceShade = { 0.80f, 0.62f, 1.0f, 0.42f, 0.90f, 0.70f };

        /// <summary>Baked cartoon face shading for unlit vertex-colored materials (top bright, bottom dark).</summary>
        public static Color Shade(Vector3 n, Color c)
        {
            float s = n.y > 0.5f ? 1f : n.y < -0.5f ? 0.42f : (n.x * 0.5f + n.z * 0.3f > 0f ? 0.84f : 0.66f);
            return new Color(c.r * s, c.g * s, c.b * s, c.a);
        }

        void AddQuadRaw(Data d, Vector3 a, Vector3 b, Vector3 c, Vector3 e, Vector3 n, Color32 col, Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD, bool flip)
        {
            int i = d.v.Count;
            d.v.Add(a); d.v.Add(b); d.v.Add(c); d.v.Add(e);
            Vector3 nn = flip ? -n : n;
            d.n.Add(nn); d.n.Add(nn); d.n.Add(nn); d.n.Add(nn);
            d.uv.Add(uvA); d.uv.Add(uvB); d.uv.Add(uvC); d.uv.Add(uvD);
            d.c.Add(col); d.c.Add(col); d.c.Add(col); d.c.Add(col);
            // want Cross(b-a, c-a) . n > 0
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), nn) >= 0f) { d.t.Add(i); d.t.Add(i + 1); d.t.Add(i + 2); d.t.Add(i); d.t.Add(i + 2); d.t.Add(i + 3); }
            else { d.t.Add(i); d.t.Add(i + 2); d.t.Add(i + 1); d.t.Add(i); d.t.Add(i + 3); d.t.Add(i + 2); }
        }

        /// <summary>Quad a,b,c,d (in loop order). Normal is derived from the points unless given. uv in 0..1 (scaled by uvScale).</summary>
        public void Quad(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, bool bothSides = false, Vector2? uvScale = null, bool bake = false)
        {
            var dat = D(m);
            Vector3 n = Vector3.Cross(b - a, d - a).normalized;       // consistent for planar quads
            if (n.sqrMagnitude < 1e-8f) n = Vector3.up;
            Vector2 s = uvScale ?? Vector2.one;
            Color cc = bake ? Shade(n, col) : col;
            AddQuadRaw(dat, a, b, c, d, n, cc, new Vector2(0, 0), new Vector2(s.x, 0), new Vector2(s.x, s.y), new Vector2(0, s.y), false);
            if (bothSides) AddQuadRaw(dat, a, b, c, d, n, bake ? Shade(-n, col) : (Color32)col, new Vector2(0, 0), new Vector2(s.x, 0), new Vector2(s.x, s.y), new Vector2(0, s.y), true);
        }

        /// <summary>Rectangle from center, right/up axes and size. Faces along Cross(right, up)... use <paramref name="facing"/> to choose the visible side.</summary>
        public void Panel(Material m, Vector3 center, Vector3 facing, Vector3 up, float w, float h, Color col, bool bothSides = false, Vector2? uvScale = null, bool bake = false)
        {
            facing.Normalize();
            Vector3 right = Vector3.Cross(facing, up).normalized;      // looking at the face from the front: +right is on the viewer's right
            Vector3 u = Vector3.Cross(right, facing).normalized;
            Vector3 a = center - right * w * .5f - u * h * .5f, b = center + right * w * .5f - u * h * .5f;
            Vector3 c = center + right * w * .5f + u * h * .5f, e = center - right * w * .5f + u * h * .5f;
            var dat = D(m);
            Vector2 s = uvScale ?? Vector2.one;
            AddQuadRaw(dat, a, b, c, e, facing, bake ? Shade(facing, col) : (Color32)col, new Vector2(0, 0), new Vector2(s.x, 0), new Vector2(s.x, s.y), new Vector2(0, s.y), false);
            if (bothSides) AddQuadRaw(dat, a, b, c, e, facing, bake ? Shade(-facing, col) : (Color32)col, new Vector2(0, 0), new Vector2(s.x, 0), new Vector2(s.x, s.y), new Vector2(0, s.y), true);
        }

        public void Tri(Material m, Vector3 a, Vector3 b, Vector3 c, Color col, bool bothSides = true)
        {
            var d = D(m);
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            if (n.sqrMagnitude < 1e-8f) return;
            int i = d.v.Count;
            for (int k = 0; k < 2; k++)
            {
                if (k == 1 && !bothSides) break;
                int bi = d.v.Count;
                Vector3 nn = k == 0 ? n : -n;
                d.v.Add(a); d.v.Add(b); d.v.Add(c);
                d.n.Add(nn); d.n.Add(nn); d.n.Add(nn);
                d.uv.Add(new Vector2(0, 0)); d.uv.Add(new Vector2(1, 0)); d.uv.Add(new Vector2(.5f, 1));
                Color32 cc = col; d.c.Add(cc); d.c.Add(cc); d.c.Add(cc);
                if (k == 0) { d.t.Add(bi); d.t.Add(bi + 1); d.t.Add(bi + 2); } else { d.t.Add(bi); d.t.Add(bi + 2); d.t.Add(bi + 1); }
            }
            _ = i;
        }

        static readonly Vector3[] bn = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        /// <summary>Box with center, size and optional rotation. bake = bake cartoon shading into vertex colors (for unlit vertex-color materials).</summary>
        public void Box(Material m, Vector3 center, Vector3 size, Color? color = null, Quaternion? rot = null, bool bake = false, bool skipBottom = false)
        {
            var d = D(m);
            Quaternion q = rot ?? Quaternion.identity;
            Vector3 h = size * .5f;
            Color col = color ?? Color.white;
            for (int f = 0; f < 6; f++)
            {
                if (skipBottom && f == 3) continue;
                Vector3 n = bn[f];
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 w = Vector3.Cross(n, u);
                float hn = Mathf.Abs(n.x) * h.x + Mathf.Abs(n.y) * h.y + Mathf.Abs(n.z) * h.z;
                float hu = Mathf.Abs(u.x) * h.x + Mathf.Abs(u.y) * h.y + Mathf.Abs(u.z) * h.z;
                float hw = Mathf.Abs(w.x) * h.x + Mathf.Abs(w.y) * h.y + Mathf.Abs(w.z) * h.z;
                Vector3 cc = n * hn;
                Vector3 p0 = center + q * (cc - u * hu - w * hw), p1 = center + q * (cc + u * hu - w * hw);
                Vector3 p2 = center + q * (cc + u * hu + w * hw), p3 = center + q * (cc - u * hu + w * hw);
                Vector3 wn = q * n;
                Color fc = bake ? Shade(wn, col) : col;
                float su = hu * 2f * UvPerMeter, sw = hw * 2f * UvPerMeter;
                AddQuadRaw(d, p0, p1, p2, p3, wn, fc, new Vector2(0, 0), new Vector2(su, 0), new Vector2(su, sw), new Vector2(0, sw), false);
            }
        }

        /// <summary>Square-section beam between two points.</summary>
        public void Beam(Material m, Vector3 a, Vector3 b, float thick, Color? color = null, bool bake = false, float thick2 = -1f)
        {
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 1e-4f) return;
            Vector3 up = Mathf.Abs(dir.normalized.y) > 0.95f ? Vector3.forward : Vector3.up;
            var q = Quaternion.LookRotation(dir / len, up);
            Box(m, (a + b) * .5f, new Vector3(thick, thick2 > 0f ? thick2 : thick, len), color, q, bake);
        }

        /// <summary>Cylinder / cone frustum from p0 (radius r0) to p1 (radius r1). Smooth radial normals.</summary>
        public void Cyl(Material m, Vector3 p0, Vector3 p1, float r0, float r1, int seg = 12, Color? color = null, bool caps = true, bool bake = false, bool inside = false)
        {
            var d = D(m);
            Vector3 axis = p1 - p0;
            float len = axis.magnitude;
            if (len < 1e-5f) return;
            Vector3 up = axis / len;
            Vector3 side = Mathf.Abs(up.y) > 0.95f ? Vector3.right : Vector3.up;
            Vector3 ex = Vector3.Cross(up, side).normalized, ey = Vector3.Cross(up, ex).normalized;
            Color col = color ?? Color.white;
            int b0 = d.v.Count;
            float slope = (r0 - r1) / len;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 rad = ex * Mathf.Cos(a) + ey * Mathf.Sin(a);
                Vector3 nrm = (rad + up * slope).normalized;
                if (inside) nrm = -nrm;
                Color c = bake ? Shade(nrm, col) : col;
                d.v.Add(p0 + rad * r0); d.n.Add(nrm); d.uv.Add(new Vector2(i / (float)seg, 0)); d.c.Add(c);
                d.v.Add(p1 + rad * r1); d.n.Add(nrm); d.uv.Add(new Vector2(i / (float)seg, 1)); d.c.Add(c);
            }
            for (int i = 0; i < seg; i++)
            {
                int a0 = b0 + i * 2, a1 = a0 + 1, a2 = a0 + 2, a3 = a0 + 3;
                // verify winding against normal of first vertex
                Vector3 cr = Vector3.Cross(d.v[a1] - d.v[a0], d.v[a2] - d.v[a0]);
                if (Vector3.Dot(cr, d.n[a0]) >= 0f) { d.t.Add(a0); d.t.Add(a1); d.t.Add(a2); d.t.Add(a2); d.t.Add(a1); d.t.Add(a3); }
                else { d.t.Add(a0); d.t.Add(a2); d.t.Add(a1); d.t.Add(a2); d.t.Add(a3); d.t.Add(a1); }
            }
            if (!caps) return;
            for (int k = 0; k < 2; k++)
            {
                float r = k == 0 ? r0 : r1;
                if (r <= 1e-4f) continue;
                Vector3 ctr = k == 0 ? p0 : p1;
                Vector3 nrm = k == 0 ? -up : up;
                Color c = bake ? Shade(nrm, col) : col;
                int cb = d.v.Count;
                d.v.Add(ctr); d.n.Add(nrm); d.uv.Add(new Vector2(.5f, .5f)); d.c.Add(c);
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    Vector3 rad = ex * Mathf.Cos(a) + ey * Mathf.Sin(a);
                    d.v.Add(ctr + rad * r); d.n.Add(nrm); d.uv.Add(new Vector2(.5f + Mathf.Cos(a) * .5f, .5f + Mathf.Sin(a) * .5f)); d.c.Add(c);
                }
                for (int i = 0; i < seg; i++)
                {
                    int a0 = cb, a1 = cb + 1 + i, a2 = cb + 2 + i;
                    if (Vector3.Dot(Vector3.Cross(d.v[a1] - d.v[a0], d.v[a2] - d.v[a0]), nrm) >= 0f) { d.t.Add(a0); d.t.Add(a1); d.t.Add(a2); }
                    else { d.t.Add(a0); d.t.Add(a2); d.t.Add(a1); }
                }
            }
        }

        /// <summary>Flat ring band (like a headband / drum rim): vertical cylinder wall, two-sided.</summary>
        public void Band(Material m, Vector3 center, Vector3 axis, float radius, float height, int seg = 20, Color? color = null)
        {
            Vector3 a = axis.normalized * height * .5f;
            Cyl(m, center - a, center + a, radius, radius, seg, color, false);
            Cyl(m, center - a, center + a, radius, radius, seg, color, false, false, true);   // inner face
        }

        /// <summary>UV sphere (smooth normals).</summary>
        public void Sphere(Material m, Vector3 center, float radius, int lon = 10, int lat = 7, Color? color = null, Vector3? stretch = null, bool bake = false)
        {
            var d = D(m);
            Vector3 st = stretch ?? Vector3.one;
            Color col = color ?? Color.white;
            int b0 = d.v.Count;
            for (int y = 0; y <= lat; y++)
            {
                float vy = y / (float)lat;
                float phi = vy * Mathf.PI;
                for (int x = 0; x <= lon; x++)
                {
                    float th = x / (float)lon * Mathf.PI * 2f;
                    Vector3 nrm = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                    d.v.Add(center + Vector3.Scale(nrm, st) * radius);
                    d.n.Add(nrm);
                    d.uv.Add(new Vector2(x / (float)lon, 1f - vy));
                    d.c.Add(bake ? Shade(nrm, col) : col);
                }
            }
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int a = b0 + y * (lon + 1) + x, b = a + 1, c = a + lon + 1, e = c + 1;
                    if (Vector3.Dot(Vector3.Cross(d.v[c] - d.v[a], d.v[b] - d.v[a]), d.n[a]) >= 0f) { d.t.Add(a); d.t.Add(c); d.t.Add(b); d.t.Add(b); d.t.Add(c); d.t.Add(e); }
                    else { d.t.Add(a); d.t.Add(b); d.t.Add(c); d.t.Add(b); d.t.Add(e); d.t.Add(c); }
                }
        }

        /// <summary>Open prism between two rings of points (light shafts). Vertex alpha fades from topCol to botCol. Two-sided.</summary>
        public void Volume(Material m, IList<Vector3> top, IList<Vector3> bottom, Color topCol, Color botCol)
        {
            var d = D(m);
            int n = top.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int b = d.v.Count;
                d.v.Add(top[i]); d.v.Add(top[j]); d.v.Add(bottom[j]); d.v.Add(bottom[i]);
                for (int k = 0; k < 4; k++) { d.n.Add(Vector3.up); }
                d.uv.Add(new Vector2(0, 1)); d.uv.Add(new Vector2(1, 1)); d.uv.Add(new Vector2(1, 0)); d.uv.Add(new Vector2(0, 0));
                Color32 ct = topCol, cb = botCol;
                d.c.Add(ct); d.c.Add(ct); d.c.Add(cb); d.c.Add(cb);
                d.t.Add(b); d.t.Add(b + 1); d.t.Add(b + 2); d.t.Add(b); d.t.Add(b + 2); d.t.Add(b + 3);
                d.t.Add(b); d.t.Add(b + 2); d.t.Add(b + 1); d.t.Add(b); d.t.Add(b + 3); d.t.Add(b + 2);
            }
        }

        public void Cone(Material m, Vector3 apex, Vector3 down, float length, float topR, float botR, int seg, Color topCol, Color botCol)
        {
            down.Normalize();
            Vector3 side = Mathf.Abs(down.y) > 0.95f ? Vector3.right : Vector3.up;
            Vector3 ex = Vector3.Cross(down, side).normalized, ey = Vector3.Cross(down, ex).normalized;
            var t = new List<Vector3>(seg); var b = new List<Vector3>(seg);
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 r = ex * Mathf.Cos(a) + ey * Mathf.Sin(a);
                t.Add(apex + r * topR);
                b.Add(apex + down * length + r * botR);
            }
            Volume(m, t, b, topCol, botCol);
        }

        // ------------------------------------------------------------------ output
        /// <summary>Creates one GameObject per material under parent. Returns the container.</summary>
        public Transform Flush(Transform parent, bool makeStatic = true)
        {
            var root = new GameObject(Name).transform;
            root.SetParent(parent, false);
            foreach (var m in order)
            {
                var d = data[m];
                if (d.v.Count == 0) continue;
                var mesh = new Mesh { name = Name + "_" + m.name };
                if (d.v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(d.v);
                mesh.SetNormals(d.n);
                mesh.SetUVs(0, d.uv);
                mesh.SetColors(d.c);
                mesh.SetTriangles(d.t, 0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                var go = new GameObject(m.name);
                go.transform.SetParent(root, false);
                if (makeStatic) go.isStatic = true;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = ReceiveShadows;
            }
            data.Clear(); order.Clear();
            return root;
        }

        /// <summary>Single combined mesh with all submeshes merged into one list (all materials must be identical for this), returns mesh for the given material.</summary>
        public Mesh BuildMesh(Material m, string meshName)
        {
            if (!data.TryGetValue(m, out var d) || d.v.Count == 0) return null;
            var mesh = new Mesh { name = meshName };
            if (d.v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(d.v); mesh.SetNormals(d.n); mesh.SetUVs(0, d.uv); mesh.SetColors(d.c); mesh.SetTriangles(d.t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public void Clear() { data.Clear(); order.Clear(); }
    }
}
