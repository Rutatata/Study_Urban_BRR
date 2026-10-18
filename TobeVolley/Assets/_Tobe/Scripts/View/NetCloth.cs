using UnityEngine;

namespace Tobe.View
{
    /// <summary>
    /// Сетка как ткань: прогиб по Z на решётке NX×NY, волновое уравнение с затуханием и натяжением тросов.
    /// Мяч, коснувшийся полотна, продавливает его — волна расходится и гаснет. Чисто визуально (правила считает сервер).
    /// Вершины пересчитываются, только пока сетка шевелится.
    /// </summary>
    public sealed class NetCloth : MonoBehaviour
    {
        const int NX = 73, NY = 9;
        const float C2 = 9f, Damp = 4.5f, Restore = 4f, TopCable = 120f, BottomCord = 30f, Step = 1f / 120f;   // подобрано: удар 9 м/с — прогиб ~27 см, затихает ~за 1 с
        float left, w, bottom, h, dx, dy;
        readonly float[] d = new float[NX * NY], v = new float[NX * NY];
        Vector3[] verts; Mesh mesh; bool active, wasTouch;

        public static void Build(Transform parent, float left, float right, float bottom, float top, float z, Material shade, Material band)
        {
            var go = new GameObject("NetCloth");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);
            var nc = go.AddComponent<NetCloth>();
            nc.left = left; nc.w = right - left; nc.bottom = bottom; nc.h = top - bottom;
            nc.dx = nc.w / (NX - 1); nc.dy = nc.h / (NY - 1);
            nc.verts = new Vector3[NX * NY];
            var uv = new Vector2[NX * NY];
            for (int j = 0; j < NY; j++)
                for (int i = 0; i < NX; i++)
                {
                    int k = j * NX + i;
                    nc.verts[k] = new Vector3(left + i * nc.dx, bottom + j * nc.dy, 0f);
                    uv[k] = new Vector2(i / (float)(NX - 1), j / (float)(NY - 1));
                }
            var tris = new int[(NX - 1) * (NY - 1) * 6];
            int t = 0;
            for (int j = 0; j < NY - 1; j++)
                for (int i = 0; i < NX - 1; i++)
                {
                    int k = j * NX + i;
                    tris[t++] = k; tris[t++] = k + NX; tris[t++] = k + 1;
                    tris[t++] = k + 1; tris[t++] = k + NX; tris[t++] = k + NX + 1;
                }
            var m = new Mesh { name = "NetCloth" };
            m.MarkDynamic();
            m.vertices = nc.verts; m.uv = uv;
            m.subMeshCount = 2; m.SetTriangles(tris, 0); m.SetTriangles(tris, 1);   // тень полотна, потом сама сетка
            m.bounds = new Bounds(new Vector3(left + nc.w * 0.5f, bottom + nc.h * 0.5f, 0f), new Vector3(nc.w, nc.h, 1.4f));
            nc.mesh = m;
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { shade, band };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        void Update()
        {
            var b = GameHub.View.ball;
            float z0 = transform.position.z;
            float u = (b.pos.x - left) / w, tv = (b.pos.y - bottom) / h;
            bool touch = !b.held && Mathf.Abs(b.pos.z - z0) < Court.BallRadius + 0.04f && u > 0f && u < 1f && tv > -0.05f && tv < 1.05f;
            if (touch && !wasTouch) Poke(b.pos.x, b.pos.y, Mathf.Clamp(b.vel.z, -12f, 12f));
            wasTouch = touch;
            if (!active) return;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / Step));
            float s = dt / steps, ix2 = 1f / (dx * dx), iy2 = 1f / (dy * dy), energy = 0f;
            for (int n = 0; n < steps; n++)
            {
                for (int j = 0; j < NY; j++)
                {
                    int up = j < NY - 1 ? j + 1 : j - 1, dn = j > 0 ? j - 1 : j + 1;   // края полотна — зеркально
                    float k0 = Restore + (j == NY - 1 ? TopCable : j == 0 ? BottomCord : 0f);
                    for (int i = 1; i < NX - 1; i++)   // столбы (i = 0 и последний) неподвижны
                    {
                        int k = j * NX + i;
                        float lap = (d[k - 1] + d[k + 1] - 2f * d[k]) * ix2 + (d[up * NX + i] + d[dn * NX + i] - 2f * d[k]) * iy2;
                        v[k] += (C2 * lap - Damp * v[k] - k0 * d[k]) * s;
                    }
                }
                for (int k = 0; k < d.Length; k++) d[k] += v[k] * s;
            }
            for (int k = 0; k < d.Length; k++) { verts[k].z = d[k]; energy = Mathf.Max(energy, Mathf.Abs(d[k]) + Mathf.Abs(v[k]) * 0.05f); }
            if (energy < 2e-4f)
            {   // успокоилась — выпрямляем и больше не считаем
                System.Array.Clear(d, 0, d.Length); System.Array.Clear(v, 0, v.Length);
                for (int k = 0; k < verts.Length; k++) verts[k].z = 0f;
                active = false;
            }
            mesh.vertices = verts;
        }

        /// <summary>Наибольший прогиб сейчас, м (для проверки в меню Tobe/Dev).</summary>
        public float MaxDisp { get { float m = 0f; foreach (var x in d) m = Mathf.Max(m, Mathf.Abs(x)); return m; } }

        /// <summary>Толчок полотна в точке (x, y) со скоростью vz (знак — в какую сторону прогнётся).</summary>
        public void Poke(float x, float y, float vz)
        {
            const float rad = 0.35f;
            for (int j = 0; j < NY; j++)
                for (int i = 1; i < NX - 1; i++)
                {
                    float ddx = left + i * dx - x, ddy = bottom + j * dy - y, dist = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                    if (dist >= rad) continue;
                    float f = 1f - dist / rad;
                    v[j * NX + i] += vz * 1.3f * f * f;
                }
            active = true;
        }
    }
}
