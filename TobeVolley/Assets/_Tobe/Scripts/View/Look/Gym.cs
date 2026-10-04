// The hall itself: walls, ceiling, steel trusses, catwalks, stadium lights + light shafts, tall windows with warm sun,
// banners, pennant strings, hanging school flags and the giant scoreboard cube.
using System.Collections.Generic;
using UnityEngine;

namespace Tobe.View
{
    public static class Gym
    {
        const float TrussBottom = 13.2f, TrussTop = 14.9f;
        static readonly float[] LampX = { -1.5f, 3f, 6f, 10.5f };
        static readonly float[] LampZ = { -3f, 3f, 9f, 15f, 21f };
        const float LampY = 12.0f;

        public static void Build(Transform root, ArenaLive live)
        {
            var hall = new GameObject("Hall").transform;
            hall.SetParent(root, false);
            Shell(hall);
            Structure(hall);
            Lamps(hall);
            Windows(hall);
            WallBanners(hall, live);
            Pennants(hall, live);
            HangingFlags(hall, live);
            Scoreboard(hall, live);
            Dust(hall);
        }

        // ------------------------------------------------------------------ shell
        static void Shell(Transform parent)
        {
            var b = new MeshBatch("Shell");
            float cx = (Hall.X0 + Hall.X1) * .5f, cz = (Hall.Z0 + Hall.Z1) * .5f;
            float lx = Hall.X1 - Hall.X0, lz = Hall.Z1 - Hall.Z0;
            var wall = Env.Wall;
            var uvW = new Vector2(1, 1);
            b.Panel(wall, new Vector3(Hall.X0, Hall.H * .5f, cz), Vector3.right, Vector3.up, lz, Hall.H, Color.white, false, uvW);
            b.Panel(wall, new Vector3(Hall.X1, Hall.H * .5f, cz), Vector3.left, Vector3.up, lz, Hall.H, Color.white, false, uvW);
            b.Panel(wall, new Vector3(cx, Hall.H * .5f, Hall.Z0), Vector3.forward, Vector3.up, lx, Hall.H, Color.white, false, uvW);
            b.Panel(wall, new Vector3(cx, Hall.H * .5f, Hall.Z1), Vector3.back, Vector3.up, lx, Hall.H, Color.white, false, uvW);
            b.Panel(Env.Ceiling, new Vector3(cx, Hall.H, cz), Vector3.down, Vector3.forward, lx, lz, Color.white, false);
            b.Panel(Env.OuterFloor, new Vector3(cx, -0.03f, cz), Vector3.up, Vector3.forward, lx, lz, Color.white, false);
            // deep-blue lower wall band (behind the stands) and accent stripes
            float wy = 6.6f;
            b.Box(Env.WallDeep, new Vector3(Hall.X0 + 0.05f, wy * .5f + 3f, cz), new Vector3(0.1f, wy, lz));
            b.Box(Env.WallDeep, new Vector3(Hall.X1 - 0.05f, wy * .5f + 3f, cz), new Vector3(0.1f, wy, lz));
            b.Box(Env.WallDeep, new Vector3(cx, wy * .5f + 3f, Hall.Z0 + 0.05f), new Vector3(lx, wy, 0.1f));
            b.Box(Env.WallDeep, new Vector3(cx, wy * .5f + 3f, Hall.Z1 - 0.05f), new Vector3(lx, wy, 0.1f));
            // glowing LED ribbons: orange on team 0 half, teal on team 1 half
            foreach (float y in new[] { 6.7f, 14.4f })
            {
                b.Box(Env.LedOrange, new Vector3(Hall.X0 + 0.08f, y, (Hall.Z0 + Hall.CZ) * .5f), new Vector3(0.12f, 0.16f, Hall.CZ - Hall.Z0));
                b.Box(Env.LedTeal, new Vector3(Hall.X0 + 0.08f, y, (Hall.CZ + Hall.Z1) * .5f), new Vector3(0.12f, 0.16f, Hall.Z1 - Hall.CZ));
                b.Box(Env.LedOrange, new Vector3(Hall.X1 - 0.08f, y, (Hall.Z0 + Hall.CZ) * .5f), new Vector3(0.12f, 0.16f, Hall.CZ - Hall.Z0));
                b.Box(Env.LedTeal, new Vector3(Hall.X1 - 0.08f, y, (Hall.CZ + Hall.Z1) * .5f), new Vector3(0.12f, 0.16f, Hall.Z1 - Hall.CZ));
                b.Box(Env.LedOrange, new Vector3(cx, y, Hall.Z0 + 0.08f), new Vector3(lx, 0.16f, 0.12f));
                b.Box(Env.LedTeal, new Vector3(cx, y, Hall.Z1 - 0.08f), new Vector3(lx, 0.16f, 0.12f));
            }
            // ceiling skylight strips (bright)
            var lamp = Env.Lamp;
            foreach (float x in new[] { -8f, 4.5f, 17f })
                b.Panel(lamp, new Vector3(x, Hall.H - 0.04f, cz), Vector3.down, Vector3.forward, 2.2f, lz - 8f, Color.white, true);
            b.Flush(parent);
        }

        // ------------------------------------------------------------------ steel trusses, purlins, catwalks
        static void Truss(MeshBatch b, Material m, float z, float x0, float x1, float yb, float yt)
        {
            b.Beam(m, new Vector3(x0, yb, z), new Vector3(x1, yb, z), 0.18f);
            b.Beam(m, new Vector3(x0, yt, z), new Vector3(x1, yt, z), 0.18f);
            const float seg = 1.9f;
            int n = Mathf.FloorToInt((x1 - x0) / seg);
            for (int i = 0; i < n; i++)
            {
                float xa = x0 + i * seg, xb = xa + seg;
                if ((i & 1) == 0) b.Beam(m, new Vector3(xa, yb, z), new Vector3(xb, yt, z), 0.09f);
                else b.Beam(m, new Vector3(xa, yt, z), new Vector3(xb, yb, z), 0.09f);
                b.Beam(m, new Vector3(xa, yb, z), new Vector3(xa, yt, z), 0.07f);
            }
        }

        static void Structure(Transform parent)
        {
            var b = new MeshBatch("Steel");
            var steel = Env.Steel; var dark = Env.SteelDark;
            for (float z = -12f; z <= 30.1f; z += 6f) Truss(b, steel, z, Hall.X0 + 0.2f, Hall.X1 - 0.2f, TrussBottom, TrussTop);
            // purlins along Z
            foreach (float x in new[] { -12f, -6f, 0f, 4.5f, 9f, 15f, 21f })
            {
                b.Beam(dark, new Vector3(x, TrussTop, Hall.Z0 + 0.3f), new Vector3(x, TrussTop, Hall.Z1 - 0.3f), 0.14f);
                b.Beam(dark, new Vector3(x, TrussBottom, Hall.Z0 + 0.3f), new Vector3(x, TrussBottom, Hall.Z1 - 0.3f), 0.12f);
            }
            // two catwalks along Z over the court sides
            foreach (float x in new[] { -4.2f, 13.2f })
            {
                float y = 11.5f;
                b.Box(dark, new Vector3(x, y, Hall.CZ), new Vector3(1.2f, 0.08f, Hall.Z1 - Hall.Z0 - 2f));
                for (float rx = -0.55f; rx <= 0.56f; rx += 1.1f)
                {
                    b.Beam(steel, new Vector3(x + rx, y + 1.05f, Hall.Z0 + 1f), new Vector3(x + rx, y + 1.05f, Hall.Z1 - 1f), 0.06f);
                    b.Beam(steel, new Vector3(x + rx, y + 0.55f, Hall.Z0 + 1f), new Vector3(x + rx, y + 0.55f, Hall.Z1 - 1f), 0.04f);
                    for (float z = Hall.Z0 + 1f; z <= Hall.Z1 - 1f; z += 2f)
                        b.Beam(steel, new Vector3(x + rx, y, z), new Vector3(x + rx, y + 1.05f, z), 0.05f);
                }
                for (float z = -12f; z <= 30.1f; z += 6f)
                    b.Beam(steel, new Vector3(x, y, z), new Vector3(x, TrussBottom, z), 0.07f);
            }
            // cross braces in the roof plane
            for (float z = -12f; z < 30f; z += 6f)
            {
                b.Beam(dark, new Vector3(Hall.X0 + 0.2f, TrussTop, z), new Vector3(0f, TrussTop, z + 6f), 0.06f);
                b.Beam(dark, new Vector3(Hall.X1 - 0.2f, TrussTop, z), new Vector3(9f, TrussTop, z + 6f), 0.06f);
            }
            b.Flush(parent);
        }

        // ------------------------------------------------------------------ stadium lights, shafts, floor pools
        static void Lamps(Transform parent)
        {
            var b = new MeshBatch("Lamps");
            var shafts = new MeshBatch("Shafts");
            var pools = new MeshBatch("LightPools");
            var housing = Env.SteelDark; var panel = Env.Lamp; var steel = Env.Steel;
            Color warm = new Color(1f, 0.92f, 0.76f, 1f);
            foreach (float x in LampX)
                foreach (float z in LampZ)
                {
                    b.Box(housing, new Vector3(x, LampY + 0.12f, z), new Vector3(2.5f, 0.22f, 1.15f));
                    b.Panel(panel, new Vector3(x, LampY, z), Vector3.down, Vector3.forward, 2.3f, 0.95f, Color.white);
                    foreach (float cx in new[] { -1.1f, 1.1f })
                        foreach (float cz in new[] { -0.4f, 0.4f })
                            b.Beam(steel, new Vector3(x + cx, LampY + 0.2f, z + cz), new Vector3(x + cx, TrussBottom, z + cz), 0.03f);
                    shafts.Cone(Env.Shaft, new Vector3(x, LampY - 0.05f, z), Vector3.down, LampY - 0.05f, 0.7f, 3.4f, 14,
                        new Color(warm.r, warm.g, warm.b, 0.22f), new Color(warm.r, warm.g, warm.b, 0.015f));
                    pools.Panel(Env.Pool, new Vector3(x, 0.014f, z), Vector3.up, Vector3.forward, 7.5f, 7.5f, new Color(1f, 0.9f, 0.7f, 0.11f));
                }
            b.Flush(parent);
            shafts.Flush(parent);
            pools.Flush(parent);
        }

        // ------------------------------------------------------------------ windows + sun
        static void Windows(Transform parent)
        {
            var b = new MeshBatch("Windows");
            var sun = new MeshBatch("SunShafts");
            var glass = Env.Glass; var frame = Env.SteelDark;
            float[] zs = { -9f, -3f, 3f, 9f, 15f, 21f, 27f };
            float y0 = 8.4f, y1 = 13.6f, w = 3.4f;
            foreach (int s in new[] { 1, -1 })
            {
                float wx = s > 0 ? Hall.X0 : Hall.X1;
                foreach (float z in zs)
                {
                    float xi = wx + s * 0.06f;
                    b.Panel(glass, new Vector3(xi, (y0 + y1) * .5f, z), new Vector3(s, 0, 0), Vector3.up, w, y1 - y0, Color.white);
                    float xf = wx + s * 0.12f;
                    b.Box(frame, new Vector3(xf, (y0 + y1) * .5f, z - w * .5f), new Vector3(0.2f, y1 - y0 + 0.2f, 0.16f));
                    b.Box(frame, new Vector3(xf, (y0 + y1) * .5f, z + w * .5f), new Vector3(0.2f, y1 - y0 + 0.2f, 0.16f));
                    b.Box(frame, new Vector3(xf, y0, z), new Vector3(0.2f, 0.16f, w + 0.2f));
                    b.Box(frame, new Vector3(xf, y1, z), new Vector3(0.2f, 0.16f, w + 0.2f));
                    b.Box(frame, new Vector3(xf, (y0 + y1) * .5f, z), new Vector3(0.14f, y1 - y0, 0.08f));
                    b.Box(frame, new Vector3(xf, y0 + (y1 - y0) * 0.38f, z), new Vector3(0.14f, 0.07f, w));
                    b.Box(frame, new Vector3(xf, y0 + (y1 - y0) * 0.72f, z), new Vector3(0.14f, 0.07f, w));
                    // warm sun volume leaning into the hall
                    Vector3 dir = new Vector3(s * 0.62f, -0.78f, 0.1f) * 11f;
                    var top = new List<Vector3>
                    {
                        new Vector3(xi, y0, z - w * .5f), new Vector3(xi, y0, z + w * .5f), new Vector3(xi, y1, z + w * .5f), new Vector3(xi, y1, z - w * .5f),
                    };
                    var bot = new List<Vector3>();
                    foreach (var p in top) bot.Add(p + dir + new Vector3(0, 0, (p.z - z) * 0.25f));
                    sun.Volume(Env.Shaft, top, bot, new Color(1f, 0.82f, 0.5f, 0.2f), new Color(1f, 0.8f, 0.5f, 0f));
                }
            }
            b.Flush(parent);
            sun.Flush(parent);
        }

        // ------------------------------------------------------------------ banners on the walls
        static void Banner(MeshBatch cloth, Transform parent, Vector3 pos, Vector3 facing, float w, float h, Color clothCol, Color trim, Color textCol, string text, float charSize)
        {
            var mat = Mats.Toon("banner_" + ColorUtility.ToHtmlStringRGB(clothCol), clothCol, new Color(clothCol.r * .7f, clothCol.g * .7f, clothCol.b * .75f), true, null, 0.4f);
            var trimMat = Mats.Unlit("banner_trim_" + ColorUtility.ToHtmlStringRGB(trim), trim * 1.5f);
            cloth.Panel(mat, pos, facing, Vector3.up, w, h, Color.white, false);
            cloth.Panel(trimMat, pos + facing * 0.03f + Vector3.up * (h * .5f - 0.08f), facing, Vector3.up, w + 0.2f, 0.16f, Color.white, true);
            cloth.Panel(trimMat, pos + facing * 0.03f - Vector3.up * (h * .5f - 0.08f), facing, Vector3.up, w + 0.2f, 0.16f, Color.white, true);
            cloth.Panel(trimMat, pos + facing * 0.03f + Vector3.Cross(Vector3.up, facing) * (w * .5f - 0.06f), facing, Vector3.up, 0.12f, h, Color.white, true);
            cloth.Panel(trimMat, pos + facing * 0.03f - Vector3.Cross(Vector3.up, facing) * (w * .5f - 0.06f), facing, Vector3.up, 0.12f, h, Color.white, true);
            var tm = Mats.Text(parent, text, charSize, 128, textCol);
            tm.transform.position = pos + facing * 0.08f;
            tm.transform.rotation = Quaternion.LookRotation(-facing, Vector3.up);
        }

        static void WallBanners(Transform parent, ArenaLive live)
        {
            var cloth = new MeshBatch("Banners");
            Color black = new Color(0.07f, 0.07f, 0.09f), white = new Color(0.95f, 0.96f, 0.98f);
            Color orange = Hall.Orange, teal = Hall.Teal, red = new Color(0.72f, 0.07f, 0.14f), navy = new Color(0.07f, 0.12f, 0.4f);
            float yc = 9.8f;
            // team 0 end wall (z low) faces +Z
            float zf0 = Hall.Z0 + 0.12f, zf1 = Hall.Z1 - 0.12f;
            Banner(cloth, parent, new Vector3(4.5f, yc, zf0), Vector3.forward, 4.6f, 8.6f, black, orange, orange, "飛\nべ", 0.32f);
            Banner(cloth, parent, new Vector3(-1.2f, yc, zf0), Vector3.forward, 3.0f, 7.0f, red, orange, new Color(1f, 0.9f, 0.5f), "烏\n野", 0.24f);
            Banner(cloth, parent, new Vector3(10.2f, yc, zf0), Vector3.forward, 3.0f, 7.0f, red, orange, new Color(1f, 0.9f, 0.5f), "繋\nげ", 0.24f);
            // team 1 end wall (z high) faces -Z
            Banner(cloth, parent, new Vector3(4.5f, yc, zf1), Vector3.back, 4.6f, 8.6f, white, teal, new Color(0.05f, 0.45f, 0.5f), "繋\nげ", 0.32f);
            Banner(cloth, parent, new Vector3(-1.2f, yc, zf1), Vector3.back, 3.0f, 7.0f, navy, teal, new Color(0.6f, 1f, 1f), "青\n城", 0.24f);
            Banner(cloth, parent, new Vector3(10.2f, yc, zf1), Vector3.back, 3.0f, 7.0f, navy, teal, new Color(0.6f, 1f, 1f), "必\n勝", 0.24f);
            // side walls between the windows
            string[] words = { "飛べ", "繋げ", "全力", "勝利", "一球", "不屈" };
            float[] zs = { -6f, 0f, 6f, 12f, 18f, 24f };
            for (int i = 0; i < zs.Length; i++)
            {
                bool t0 = zs[i] < Hall.CZ;
                Color c = t0 ? black : white;
                Color tc = t0 ? orange : new Color(0.05f, 0.5f, 0.55f);
                Color tr = t0 ? orange : teal;
                string txt = words[i].Substring(0, 1) + "\n" + words[i].Substring(1, 1);
                Banner(cloth, parent, new Vector3(Hall.X0 + 0.12f, 9.3f, zs[i]), Vector3.right, 2.0f, 5.2f, c, tr, tc, txt, 0.15f);
                Banner(cloth, parent, new Vector3(Hall.X1 - 0.12f, 9.3f, zs[i]), Vector3.left, 2.0f, 5.2f, c, tr, tc, txt, 0.15f);
            }
            cloth.Flush(parent);
            _ = live;
        }

        // ------------------------------------------------------------------ pennant strings across the hall
        static void PennantString(MeshBatch b, Material m, Vector3 a, Vector3 c, float sag, int count, Color c1, Color c2, Color c3)
        {
            Vector3 prev = a;
            const int seg = 28;
            Vector3[] pts = new Vector3[seg + 1];
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg;
                pts[i] = Vector3.Lerp(a, c, t) + Vector3.down * (sag * 4f * t * (1f - t));
            }
            for (int i = 1; i <= seg; i++) b.Beam(Env.Black, pts[i - 1], pts[i], 0.02f);
            _ = prev;
            for (int k = 0; k < count; k++)
            {
                float t = (k + 0.5f) / count;
                int idx = Mathf.Clamp(Mathf.FloorToInt(t * seg), 0, seg - 1);
                float f = t * seg - idx;
                Vector3 p = Vector3.Lerp(pts[idx], pts[idx + 1], f);
                Vector3 dir = (c - a).normalized;
                float w = Mathf.Min(0.55f, (c - a).magnitude / count * 0.8f);
                Color col = (k % 3 == 0) ? c1 : (k % 3 == 1) ? c2 : c3;
                col = Color.Lerp(col, Color.white, 0.0f);
                b.Tri(m, p - dir * w * .5f, p + dir * w * .5f, p + Vector3.down * 0.8f, MeshBatch.Shade(Vector3.up, col), true);
            }
        }

        static void Pennants(Transform parent, ArenaLive live)
        {
            var b = new MeshBatch("Pennants");
            var vc = Env.Vc;
            Color blk = new Color(0.1f, 0.1f, 0.12f), o = Hall.Orange * 1.0f, w = new Color(0.96f, 0.96f, 0.98f), t = Hall.Teal;
            float y = 11.2f;
            // across the court (X direction), over each end
            PennantString(b, vc, new Vector3(-4.2f, y, -2f), new Vector3(13.2f, y, -2f), 1.0f, 26, blk, o, w);
            PennantString(b, vc, new Vector3(-4.2f, y, 20f), new Vector3(13.2f, y, 20f), 1.0f, 26, w, t, new Color(0.1f, 0.25f, 0.4f));
            // along the sides
            PennantString(b, vc, new Vector3(-4.2f, y + 0.4f, -2f), new Vector3(-4.2f, y + 0.4f, 9f), 0.9f, 20, blk, o, w);
            PennantString(b, vc, new Vector3(-4.2f, y + 0.4f, 9f), new Vector3(-4.2f, y + 0.4f, 20f), 0.9f, 20, w, t, new Color(0.1f, 0.25f, 0.4f));
            PennantString(b, vc, new Vector3(13.2f, y + 0.4f, -2f), new Vector3(13.2f, y + 0.4f, 9f), 0.9f, 20, blk, o, w);
            PennantString(b, vc, new Vector3(13.2f, y + 0.4f, 9f), new Vector3(13.2f, y + 0.4f, 20f), 0.9f, 20, w, t, new Color(0.1f, 0.25f, 0.4f));
            var root = b.Flush(parent);
            live.AddSway(root, new Vector3(1, 0, 0), 0.35f, 0.9f, 0f, false);
        }

        static void HangingFlags(Transform parent, ArenaLive live)
        {
            float[] xs = { -9f, 18f };
            float[] zs = { -6f, 3f, 15f, 24f };
            int n = 0;
            foreach (float x in xs)
                foreach (float z in zs)
                {
                    int team = z < Hall.CZ ? 0 : 1;
                    var pivot = new GameObject("HangFlag").transform;
                    pivot.SetParent(parent, false);
                    pivot.position = new Vector3(x, TrussBottom - 0.1f, z);
                    pivot.rotation = Quaternion.Euler(0, x < Hall.CX ? 90f : -90f, 0);
                    var mat = Mats.Unlit("hangflag_" + team + "_" + (n & 3), new Color(1.1f, 1.1f, 1.1f), ProcTex.FlagTex(team, n & 3), false, false, true);
                    Mats.Quad("Cloth", pivot, new Vector3(0, -2.2f, 0), Vector3.zero, new Vector3(2.4f, 4.4f, 1f), mat);
                    Mats.Prim(PrimitiveType.Cylinder, pivot, "Rod", new Vector3(0, 0, 0), new Vector3(0.05f, 1.25f, 0.05f), Env.SteelDark).transform.localRotation = Quaternion.Euler(0, 0, 90);
                    live.AddSway(pivot, new Vector3(1, 0, 0), 2.2f, 0.8f + (n % 3) * 0.15f, n * 1.3f, false);
                    n++;
                }
        }

        // ------------------------------------------------------------------ scoreboard cube
        static void Scoreboard(Transform parent, ArenaLive live)
        {
            var root = new GameObject("Scoreboard").transform;
            root.SetParent(parent, false);
            Vector3 c = new Vector3(Hall.CX, 10.4f, Hall.CZ);
            const float sx = 5.8f, sy = 2.8f, sz = 5.8f;
            var b = new MeshBatch("ScoreboardBody");
            b.Box(Env.SteelDark, c, new Vector3(sx, sy, sz));
            foreach (float yy in new[] { c.y + sy * .5f + 0.02f, c.y - sy * .5f - 0.02f })
            {
                b.Box(Env.LedOrange, new Vector3(c.x, yy, c.z - sz * .5f), new Vector3(sx + 0.1f, 0.1f, 0.12f));
                b.Box(Env.LedTeal, new Vector3(c.x, yy, c.z + sz * .5f), new Vector3(sx + 0.1f, 0.1f, 0.12f));
                b.Box(Env.LedWhite, new Vector3(c.x - sx * .5f, yy, c.z), new Vector3(0.12f, 0.1f, sz + 0.1f));
                b.Box(Env.LedWhite, new Vector3(c.x + sx * .5f, yy, c.z), new Vector3(0.12f, 0.1f, sz + 0.1f));
            }
            foreach (var cx in new[] { -1f, 1f })
                foreach (var cz in new[] { -1f, 1f })
                    b.Beam(Env.Steel, new Vector3(c.x + cx * (sx * .5f - 0.2f), c.y + sy * .5f, c.z + cz * (sz * .5f - 0.2f)), new Vector3(c.x + cx * (sx * .5f - 0.2f) * 1.5f, TrussBottom, c.z + cz * (sz * .5f - 0.2f) * 1.5f), 0.07f);
            // underside LED
            var under = Mats.Unlit("sb_under", new Color(1.6f, 1.2f, 0.8f));
            b.Panel(under, new Vector3(c.x, c.y - sy * .5f - 0.04f, c.z), Vector3.down, Vector3.forward, sx - 0.5f, sz - 0.5f, Color.white);
            b.Flush(root);

            var scr = Mats.Unlit("sb_screen", new Color(1.5f, 1.5f, 1.5f), ProcTex.ScreenTex());
            var faces = new[]
            {
                (n: new Vector3(0, 0, -1), w: sx), (n: new Vector3(0, 0, 1), w: sx),
                (n: new Vector3(-1, 0, 0), w: sz), (n: new Vector3(1, 0, 0), w: sz),
            };
            var sb = new MeshBatch("ScoreboardScreens");
            foreach (var f in faces)
            {
                float half = (Mathf.Abs(f.n.x) > 0.5f ? sx : sz) * .5f;
                Vector3 fc = c + f.n * (half + 0.03f);
                sb.Panel(scr, fc, f.n, Vector3.up, f.w - 0.5f, sy - 0.4f, Color.white);
                var face = new ArenaLive.ScoreFace();
                var rot = Quaternion.LookRotation(-f.n, Vector3.up);       // TextMesh reads from the side the viewer stands on
                float fw = f.w - 0.5f;
                face.scoreL = MakeText(root, "0", fc + f.n * 0.04f + rot * new Vector3(-fw * 0.25f, -0.1f, 0), rot, 0.16f, 128, new Color(1f, 0.85f, 0.4f));
                face.scoreR = MakeText(root, "0", fc + f.n * 0.04f + rot * new Vector3(fw * 0.25f, -0.1f, 0), rot, 0.16f, 128, new Color(0.6f, 1f, 1f));
                face.nameL = MakeText(root, TeamLook.Names[0], fc + f.n * 0.04f + rot * new Vector3(-fw * 0.25f, 0.82f, 0), rot, 0.03f, 128, Color.white);
                face.nameR = MakeText(root, TeamLook.Names[1], fc + f.n * 0.04f + rot * new Vector3(fw * 0.25f, 0.82f, 0), rot, 0.03f, 128, Color.white);
                face.status = MakeText(root, "", fc + f.n * 0.04f + rot * new Vector3(0, -1.0f, 0), rot, 0.022f, 128, new Color(1f, 1f, 0.8f));
                var dot = Mats.Quad("Serve", root, fc + f.n * 0.04f + rot * new Vector3(-fw * 0.25f, 0.5f, 0), rot.eulerAngles, new Vector3(0.28f, 0.28f, 1f), Mats.Unlit("sb_dot", new Color(2f, 2f, 1.2f), Mats.DiscTex, true));
                face.dot = dot.transform; face.dotL = rot * new Vector3(-fw * 0.25f, 0.5f, 0); face.dotR = rot * new Vector3(fw * 0.25f, 0.5f, 0); face.dotBase = fc + f.n * 0.04f;
                live.AddFace(face);
            }
            sb.Flush(root);
        }

        static TextMesh MakeText(Transform parent, string s, Vector3 pos, Quaternion rot, float charSize, int fontSize, Color c)
        {
            var tm = Mats.Text(parent, s, charSize, fontSize, c);
            tm.transform.position = pos;
            tm.transform.rotation = rot;
            return tm;
        }

        // ------------------------------------------------------------------ floating dust in the light
        static void Dust(Transform parent)
        {
            var ps = FxManager.MakeSystem(parent, "DustMotes", Mats.Particle("dust_motes", Mats.SoftDot, true), 260, 9f, 0.06f, -0.01f, true, false);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.12f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.7f, 0.5f), new Color(1f, 1f, 1f, 0.25f));
            main.maxParticles = 260;
            var em = ps.emission; em.enabled = true; em.rateOverTime = 36f;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(26f, 9f, 34f);
            ps.transform.position = new Vector3(Hall.CX, 6f, Hall.CZ);
            ps.Play();
        }
    }
}
