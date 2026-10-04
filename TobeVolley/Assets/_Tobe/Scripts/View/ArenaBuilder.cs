// Builds the stylized indoor arena: floor, net, stands + crowd, walls, neon, banners, lights, post-processing.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tobe.View
{
    public static class ArenaBuilder
    {
        public static ColorAdjustments Adjust;
        public static Bloom BloomFx;
        static float excite;

        // Hall layout (meters). Floor 27 x 34 centered on the court.
        const float FloorX0 = -9f, FloorZ0 = -8f, FloorW = 27f, FloorL = 34f;
        const float WallX0 = -14.5f, WallX1 = 23.5f, WallZ0 = -13.5f, WallZ1 = 31.5f, WallH = 14f;

        public static void Cheer(float amount) { excite = Mathf.Clamp(excite + amount, 0f, 1.6f); }
        public static float Excitement => excite;

        public static void Build(Transform parent)
        {
            var root = new GameObject("Arena").transform;
            root.SetParent(parent, false);

            BuildAtmosphere(root);
            BuildFloor(root);
            BuildNet(root);
            BuildWalls(root);
            BuildNeon(root);
            BuildBanners(root);
            BuildStandsAndCrowd(root);
            BuildLights(root);
            BuildVolume(root);
        }

        // ------------------------------------------------------------------ atmosphere / post
        static void BuildAtmosphere(Transform root)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.38f, 0.30f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.32f, 0.30f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.50f, 0.34f, 0.24f);
            RenderSettings.fog = false;
        }

        static void BuildVolume(Transform root)
        {
            var go = new GameObject("PostFX");
            go.transform.SetParent(root, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = prof;

            var bloom = prof.Add<Bloom>(true);
            bloom.intensity.Override(1.0f);
            bloom.threshold.Override(0.9f);
            bloom.scatter.Override(0.65f);
            BloomFx = bloom;

            var tm = prof.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);

            var ca = prof.Add<ColorAdjustments>(true);
            ca.saturation.Override(15f);
            ca.contrast.Override(10f);
            ca.postExposure.Override(0f);
            Adjust = ca;

            var vg = prof.Add<Vignette>(true);
            vg.intensity.Override(0.25f);
            vg.smoothness.Override(0.4f);
        }

        // ------------------------------------------------------------------ floor
        static float Hash(int i)
        {
            float s = Mathf.Sin(i * 12.9898f + 4.1414f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }

        static Texture2D MakeFloorTexture()
        {
            Color court = new Color(0.93f, 0.46f, 0.16f);
            Color courtFront = new Color(0.86f, 0.38f, 0.12f);
            Color freeIn = new Color(0.10f, 0.52f, 0.56f);
            Color freeOut = new Color(0.07f, 0.32f, 0.38f);
            const float plank = 0.2f;
            return Mats.MakeTex("floor", 1024, 1280, (u, v) =>
            {
                float wx = FloorX0 + u * FloorW, wz = FloorZ0 + v * FloorL;
                bool inCourt = wx >= 0f && wx <= Court.Width && wz >= 0f && wz <= Court.Length;
                bool inFree = wx >= -3f && wx <= Court.Width + 3f && wz >= -3f && wz <= Court.Length + 3f;
                Color c = inCourt ? ((wz > 6f && wz < 12f) ? courtFront : court) : (inFree ? freeIn : freeOut);

                int pi = Mathf.FloorToInt(wx / plank);
                float fx = wx / plank - pi;
                float joint = Mathf.Floor((wz + Hash(pi) * 2.4f) / 2.4f);
                float tone = 0.9f + 0.1f * Hash(pi * 31 + (int)joint);
                float grain = 0.97f + 0.03f * Mathf.Sin(wz * 40f + Hash(pi) * 20f);
                c *= tone * grain;
                if (fx < 0.05f || fx > 0.97f) c *= 0.72f;
                float fz = (wz + Hash(pi) * 2.4f) / 2.4f - joint;
                if (fz < 0.008f) c *= 0.75f;

                // white lines
                float d = 9f;
                float lx = Mathf.Min(Mathf.Abs(wx), Mathf.Abs(wx - Court.Width));
                if (wz >= -0.05f && wz <= Court.Length + 0.05f) d = Mathf.Min(d, lx);
                if (wx >= -0.05f && wx <= Court.Width + 0.05f)
                {
                    d = Mathf.Min(d, Mathf.Abs(wz));
                    d = Mathf.Min(d, Mathf.Abs(wz - Court.Length));
                    d = Mathf.Min(d, Mathf.Abs(wz - 6f));
                    d = Mathf.Min(d, Mathf.Abs(wz - 12f));
                    d = Mathf.Min(d, Mathf.Abs(wz - Court.NetZ));
                }
                float la = Mathf.Clamp01((0.05f - d) / 0.02f);
                if (la > 0f) c = Color.Lerp(c, new Color(0.97f, 0.97f, 0.95f), la);
                c.a = 1f;
                return c;
            }, true, TextureWrapMode.Clamp, FilterMode.Trilinear);
        }

        static void BuildFloor(Transform root)
        {
            var tex = MakeFloorTexture();
            tex.anisoLevel = 8;
            var mat = Mats.Lit("floor", Color.white, 0.55f, 0f, null, tex);
            var go = Mats.FloorQuad("Floor", root, new Vector3(FloorX0 + FloorW * .5f, 0f, FloorZ0 + FloorL * .5f), FloorW, FloorL, mat);
            var r = go.GetComponent<MeshRenderer>();
            r.receiveShadows = true;
        }

        // ------------------------------------------------------------------ net
        static void BuildNet(Transform root)
        {
            var net = new GameObject("Net").transform;
            net.SetParent(root, false);
            var postMat = Mats.Lit("post", new Color(0.12f, 0.12f, 0.14f), 0.6f, 0.6f);
            float left = -0.7f, right = Court.Width + 0.7f, w = right - left;
            foreach (float x in new[] { left, right })
            {
                Mats.Prim(PrimitiveType.Cylinder, net, "Post", new Vector3(x, 1.3f, Court.NetZ), new Vector3(0.12f, 1.3f, 0.12f), postMat, true);
                Mats.Prim(PrimitiveType.Cube, net, "PostBase", new Vector3(x, 0.05f, Court.NetZ), new Vector3(0.5f, 0.1f, 0.5f), postMat, true);
            }

            var gridTex = Mats.MakeTex("netgrid", 32, 32, (u, v) =>
            {
                float a = Mathf.Min(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)); // cross lines through the middle
                float line = Mathf.Clamp01((0.06f - a) / 0.03f);
                return new Color(0.95f, 0.95f, 0.95f, line * 0.9f);
            }, true, TextureWrapMode.Repeat);
            var netMat = Mats.Unlit("netband", new Color(0.9f, 0.9f, 0.95f, 1f), gridTex, true, false, true);
            netMat.mainTextureScale = new Vector2(w / 0.1f, (Court.NetTop - Court.NetBottom) / 0.1f);
            // back plane tint so the net reads as a surface
            var shade = Mats.Unlit("netshade", new Color(0f, 0f, 0f, 0.12f), null, true, false, true);
            float h = Court.NetTop - Court.NetBottom, cy = (Court.NetTop + Court.NetBottom) * .5f;
            Mats.Quad("NetShade", net, new Vector3((left + right) * .5f, cy, Court.NetZ), Vector3.zero, new Vector3(w, h, 1), shade);
            Mats.Quad("NetBand", net, new Vector3((left + right) * .5f, cy, Court.NetZ + 0.002f), Vector3.zero, new Vector3(w, h, 1), netMat);

            var white = Mats.Lit("tapeW", new Color(0.97f, 0.97f, 0.97f), 0.3f);
            var black = Mats.Lit("tapeB", new Color(0.06f, 0.06f, 0.07f), 0.3f);
            Mats.Prim(PrimitiveType.Cube, net, "TopTape", new Vector3((left + right) * .5f, Court.NetTop - 0.04f, Court.NetZ), new Vector3(w, 0.08f, 0.03f), white, true);
            Mats.Prim(PrimitiveType.Cube, net, "BottomTape", new Vector3((left + right) * .5f, Court.NetBottom + 0.02f, Court.NetZ), new Vector3(w, 0.04f, 0.03f), black, true);

            var red = Mats.Lit("antR", new Color(0.9f, 0.1f, 0.1f), 0.4f);
            foreach (float x in new[] { 0f, Court.Width })
            {
                float y0 = Court.NetBottom;
                const float seg = 0.3f;
                int n = Mathf.RoundToInt((3.23f - y0) / seg);
                for (int i = 0; i < n; i++)
                    Mats.Prim(PrimitiveType.Cylinder, net, "Antenna", new Vector3(x, y0 + seg * (i + .5f), Court.NetZ),
                        new Vector3(0.025f, seg * .5f, 0.025f), (i & 1) == 0 ? red : white, false);
            }
        }

        // ------------------------------------------------------------------ walls
        static void BuildWalls(Transform root)
        {
            var wall = Mats.Lit("wall", new Color(0.05f, 0.05f, 0.09f), 0.2f);
            float cx = (WallX0 + WallX1) * .5f, cz = (WallZ0 + WallZ1) * .5f;
            float lx = WallX1 - WallX0, lz = WallZ1 - WallZ0;
            Mats.Quad("WallL", root, new Vector3(WallX0, WallH * .5f, cz), new Vector3(0, -90, 0), new Vector3(lz, WallH, 1), wall);
            Mats.Quad("WallR", root, new Vector3(WallX1, WallH * .5f, cz), new Vector3(0, 90, 0), new Vector3(lz, WallH, 1), wall);
            Mats.Quad("WallB", root, new Vector3(cx, WallH * .5f, WallZ0), new Vector3(0, 180, 0), new Vector3(lx, WallH, 1), wall);
            Mats.Quad("WallF", root, new Vector3(cx, WallH * .5f, WallZ1), Vector3.zero, new Vector3(lx, WallH, 1), wall);
            var ceil = Mats.Lit("ceiling", new Color(0.04f, 0.04f, 0.07f), 0.1f);
            Mats.Quad("Ceiling", root, new Vector3(cx, WallH, cz), new Vector3(-90, 0, 0), new Vector3(lx, lz, 1), ceil);
            // outer ground beyond floor (under stands)
            var dark = Mats.Lit("outerfloor", new Color(0.03f, 0.03f, 0.05f), 0.1f);
            Mats.FloorQuad("OuterFloor", root, new Vector3(cx, -0.02f, cz), lx, lz, dark);
        }

        static Material Neon(string key, Color c, float intensity)
            => Mats.Lit(key, new Color(0.02f, 0.02f, 0.03f), 0.2f, 0f, c * intensity);

        static void BuildNeon(Transform root)
        {
            var cyan = Neon("neonC", new Color(0f, 0.9f, 1f), 2.6f);
            var mag = Neon("neonM", new Color(1f, 0.1f, 0.75f), 2.6f);
            var gold = Neon("neonG", new Color(1f, 0.72f, 0.1f), 2.6f);
            float cx = (WallX0 + WallX1) * .5f, cz = (WallZ0 + WallZ1) * .5f;
            float lx = WallX1 - WallX0, lz = WallZ1 - WallZ0;
            var rows = new[] { (9.0f, cyan), (10.2f, mag), (11.4f, gold) };
            foreach (var (y, m) in rows)
            {
                Mats.Prim(PrimitiveType.Cube, root, "NeonL", new Vector3(WallX0 + 0.1f, y, cz), new Vector3(0.1f, 0.12f, lz), m);
                Mats.Prim(PrimitiveType.Cube, root, "NeonR", new Vector3(WallX1 - 0.1f, y, cz), new Vector3(0.1f, 0.12f, lz), m);
                Mats.Prim(PrimitiveType.Cube, root, "NeonB", new Vector3(cx, y, WallZ0 + 0.1f), new Vector3(lx, 0.12f, 0.1f), m);
                Mats.Prim(PrimitiveType.Cube, root, "NeonF", new Vector3(cx, y, WallZ1 - 0.1f), new Vector3(lx, 0.12f, 0.1f), m);
            }
            // truss frame above the court
            float y2 = 12.6f;
            Mats.Prim(PrimitiveType.Cube, root, "TrussA", new Vector3(-2f, y2, 9f), new Vector3(0.15f, 0.15f, 22f), gold);
            Mats.Prim(PrimitiveType.Cube, root, "TrussB", new Vector3(11f, y2, 9f), new Vector3(0.15f, 0.15f, 22f), gold);
            Mats.Prim(PrimitiveType.Cube, root, "TrussC", new Vector3(4.5f, y2, -2f), new Vector3(13f, 0.15f, 0.15f), cyan);
            Mats.Prim(PrimitiveType.Cube, root, "TrussD", new Vector3(4.5f, y2, 20f), new Vector3(13f, 0.15f, 0.15f), mag);
            // ceiling light panels
            var panel = Mats.Lit("panel", new Color(0.1f, 0.1f, 0.1f), 0.2f, 0f, new Color(1f, 0.95f, 0.85f) * 2.2f);
            foreach (float x in new[] { 1.5f, 7.5f })
                foreach (float z in new[] { 2f, 7f, 11f, 16f })
                    Mats.Quad("Panel", root, new Vector3(x, WallH - 0.05f, z), new Vector3(-90, 0, 0), new Vector3(2.2f, 1.2f, 1), panel);
        }

        // ------------------------------------------------------------------ banners
        static void BuildBanners(Transform root)
        {
            var clothRed = Mats.Lit("bannerRed", new Color(0.55f, 0.04f, 0.1f), 0.2f, 0f, null, null, false, true);
            var clothBlue = Mats.Lit("bannerBlue", new Color(0.04f, 0.1f, 0.4f), 0.2f, 0f, null, null, false, true);
            var trim = Mats.Lit("bannerTrim", new Color(0.1f, 0.08f, 0.02f), 0.2f, 0f, new Color(1f, 0.75f, 0.2f) * 1.6f);
            // back wall (z low) faces +Z, front wall faces -Z
            Banner(root, new Vector3(1.5f, 9.5f, WallZ0 + 0.1f), 180f, "飛\nべ", clothRed, trim, new Color(1f, 0.85f, 0.3f));
            Banner(root, new Vector3(7.5f, 9.5f, WallZ0 + 0.1f), 180f, "繋\nげ", clothBlue, trim, new Color(0.5f, 1f, 1f));
            Banner(root, new Vector3(1.5f, 9.5f, WallZ1 - 0.1f), 0f, "繋\nげ", clothBlue, trim, new Color(0.5f, 1f, 1f));
            Banner(root, new Vector3(7.5f, 9.5f, WallZ1 - 0.1f), 0f, "飛\nべ", clothRed, trim, new Color(1f, 0.85f, 0.3f));
        }

        static void Banner(Transform root, Vector3 pos, float yaw, string text, Material cloth, Material trim, Color textColor)
        {
            var b = new GameObject("Banner").transform;
            b.SetParent(root, false);
            b.localPosition = pos;
            b.localEulerAngles = new Vector3(0, yaw, 0);
            Mats.Quad("Cloth", b, Vector3.zero, Vector3.zero, new Vector3(3.6f, 7.2f, 1), cloth);
            Mats.Quad("TrimT", b, new Vector3(0, 3.55f, -0.01f), Vector3.zero, new Vector3(3.8f, 0.15f, 1), trim);
            Mats.Quad("TrimB", b, new Vector3(0, -3.55f, -0.01f), Vector3.zero, new Vector3(3.8f, 0.15f, 1), trim);
            Mats.Quad("TrimL", b, new Vector3(-1.8f, 0, -0.01f), Vector3.zero, new Vector3(0.12f, 7.2f, 1), trim);
            Mats.Quad("TrimR", b, new Vector3(1.8f, 0, -0.01f), Vector3.zero, new Vector3(0.12f, 7.2f, 1), trim);
            var tm = Mats.Text(b, text, 0.28f, 128, textColor);
            tm.transform.localPosition = new Vector3(0, 0, -0.03f);
        }

        // ------------------------------------------------------------------ stands + crowd
        static void BuildStandsAndCrowd(Transform root)
        {
            var standMat = Mats.Lit("stand", new Color(0.07f, 0.07f, 0.12f), 0.15f);
            var crowdMat = Mats.VertexColorOpaque("crowd", new Color(0.85f, 0.85f, 0.9f));
            var holder = new GameObject("Crowd");
            holder.transform.SetParent(root, false);
            var anim = holder.AddComponent<CrowdAnimator>();
            var rng = new System.Random(1234);

            const float x0 = -5f, x1 = 14f, z0 = -4f, z1 = 22f;
            const int rows = 10;
            float depth = rows * 0.9f;
            // left / right stands run along Z (include corners), end stands along X.
            BuildStand(holder.transform, anim, standMat, crowdMat, rng, new Vector3(x0, 0, (z0 + z1) * .5f), new Vector3(-1, 0, 0), new Vector3(0, 0, 1), (z1 - z0) + 2f * depth, rows);
            BuildStand(holder.transform, anim, standMat, crowdMat, rng, new Vector3(x1, 0, (z0 + z1) * .5f), new Vector3(1, 0, 0), new Vector3(0, 0, 1), (z1 - z0) + 2f * depth, rows);
            BuildStand(holder.transform, anim, standMat, crowdMat, rng, new Vector3((x0 + x1) * .5f, 0, z0), new Vector3(0, 0, -1), new Vector3(1, 0, 0), x1 - x0, rows);
            BuildStand(holder.transform, anim, standMat, crowdMat, rng, new Vector3((x0 + x1) * .5f, 0, z1), new Vector3(0, 0, 1), new Vector3(1, 0, 0), x1 - x0, rows);
        }

        static readonly Color[] ShirtPalette =
        {
            new Color(0.9f,0.15f,0.15f), new Color(0.15f,0.4f,0.95f), new Color(0.95f,0.8f,0.15f), new Color(0.95f,0.95f,0.95f),
            new Color(0.15f,0.8f,0.4f), new Color(0.9f,0.4f,0.9f), new Color(1f,0.5f,0.1f), new Color(0.1f,0.1f,0.15f), new Color(0.2f,0.85f,0.9f),
        };
        static readonly Color[] SkinPalette =
        {
            new Color(0.96f,0.8f,0.66f), new Color(0.85f,0.65f,0.5f), new Color(0.65f,0.45f,0.32f), new Color(0.45f,0.3f,0.2f), new Color(0.98f,0.86f,0.75f),
        };

        static void BuildStand(Transform parent, CrowdAnimator anim, Material standMat, Material crowdMat, System.Random rng,
            Vector3 inner, Vector3 outDir, Vector3 along, float length, int rows)
        {
            const float rowD = 0.9f, rowH = 0.6f, spacing = 0.55f;
            const int perChunk = 22;
            Vector3 absOut = new Vector3(Mathf.Abs(outDir.x), 0, Mathf.Abs(outDir.z));
            Vector3 absAlong = new Vector3(Mathf.Abs(along.x), 0, Mathf.Abs(along.z));
            for (int i = 0; i < rows; i++)
            {
                float top = (i + 1) * rowH;
                Vector3 c = inner + outDir * (i * rowD + rowD * .5f) + Vector3.up * (top * .5f);
                Vector3 s = absOut * rowD + absAlong * length + Vector3.up * top;
                Mats.Prim(PrimitiveType.Cube, parent, "Tier", c, s, standMat);

                float rowBase = top;
                Vector3 rowPos = inner + outDir * (i * rowD + rowD * .5f);
                int count = Mathf.FloorToInt(length / spacing);
                var verts = new List<Vector3>(); var cols = new List<Color32>(); var tris = new List<int>();
                int inChunk = 0;
                for (int k = 0; k < count; k++)
                {
                    if (rng.NextDouble() < 0.08) continue; // empty seat
                    float sPos = -length * .5f + (k + 0.5f) * spacing + ((float)rng.NextDouble() - .5f) * 0.12f;
                    Vector3 p = rowPos + along * sPos + new Vector3(0, rowBase, 0);
                    float hh = 0.9f + (float)rng.NextDouble() * 0.2f;
                    Vector3 bodySize = absAlong * 0.40f + absOut * 0.30f + Vector3.up * 0.58f * hh;
                    Vector3 headSize = new Vector3(0.24f, 0.24f, 0.24f);
                    Color shirt = ShirtPalette[rng.Next(ShirtPalette.Length)];
                    Color skin = SkinPalette[rng.Next(SkinPalette.Length)];
                    AddBox(verts, cols, tris, p + Vector3.up * (0.29f * hh), bodySize, shirt);
                    AddBox(verts, cols, tris, p + Vector3.up * (0.58f * hh + 0.15f), headSize, skin);
                    if (rng.NextDouble() < 0.25) // hair / cap on top
                        AddBox(verts, cols, tris, p + Vector3.up * (0.58f * hh + 0.29f), new Vector3(0.26f, 0.08f, 0.26f), ShirtPalette[rng.Next(ShirtPalette.Length)] * 0.7f);
                    if (++inChunk >= perChunk) { Flush(parent, anim, crowdMat, verts, cols, tris, rng, i); inChunk = 0; }
                }
                if (inChunk > 0) Flush(parent, anim, crowdMat, verts, cols, tris, rng, i);
            }
        }

        static void Flush(Transform parent, CrowdAnimator anim, Material mat, List<Vector3> v, List<Color32> c, List<int> t, System.Random rng, int row)
        {
            var mesh = new Mesh { name = "CrowdChunk" };
            if (v.Count > 60000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            var go = new GameObject("Chunk");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            anim.Register(go.transform, (float)rng.NextDouble() * 6.28f, 0.7f + (float)rng.NextDouble() * 0.6f);
            v.Clear(); c.Clear(); t.Clear();
        }

        static readonly float[] faceShade = { 0.78f, 0.62f, 1.0f, 0.4f, 0.88f, 0.7f };
        static readonly Vector3[] faceN = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        static void AddBox(List<Vector3> v, List<Color32> c, List<int> t, Vector3 ctr, Vector3 size, Color col)
        {
            Vector3 h = size * .5f;
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = faceN[f];
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 w = -Vector3.Cross(n, u);
                float hn = Mathf.Abs(n.x) * h.x + Mathf.Abs(n.y) * h.y + Mathf.Abs(n.z) * h.z;
                float hu = Mathf.Abs(u.x) * h.x + Mathf.Abs(u.y) * h.y + Mathf.Abs(u.z) * h.z;
                float hw = Mathf.Abs(w.x) * h.x + Mathf.Abs(w.y) * h.y + Mathf.Abs(w.z) * h.z;
                Vector3 cc = ctr + n * hn;
                int b = v.Count;
                v.Add(cc - u * hu - w * hw); v.Add(cc + u * hu - w * hw); v.Add(cc + u * hu + w * hw); v.Add(cc - u * hu + w * hw);
                Color sc = col * faceShade[f]; sc.a = 1f;
                Color32 c32 = sc;
                c.Add(c32); c.Add(c32); c.Add(c32); c.Add(c32);
                t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b); t.Add(b + 2); t.Add(b + 3);
            }
        }

        // ------------------------------------------------------------------ lights
        static void BuildLights(Transform root)
        {
            var sun = new GameObject("MainLight").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.86f);
            sun.intensity = 0.9f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.transform.rotation = Quaternion.Euler(58f, -28f, 0f);
            RenderSettings.sun = sun;

            Color[] tints = { new Color(1f, 0.9f, 0.8f), new Color(0.8f, 0.9f, 1f) };
            int n = 0;
            foreach (var p in new[] { new Vector3(2f, 13f, 3f), new Vector3(7f, 13f, 3f), new Vector3(2f, 13f, 15f), new Vector3(7f, 13f, 15f), new Vector3(4.5f, 13f, 9f) })
            {
                var l = new GameObject("Spot" + n).AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.position = p;
                l.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                l.type = LightType.Spot;
                l.spotAngle = 85f;
                l.innerSpotAngle = 40f;
                l.range = 30f;
                l.intensity = 2.2f;
                l.color = tints[n++ % 2];
                l.shadows = LightShadows.None;
            }
        }
    }

}
