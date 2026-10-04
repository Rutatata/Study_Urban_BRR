// Procedural textures for the arena / ball / characters (painterly flat colors with subtle variation). Everything is cached in Mats.
using UnityEngine;

namespace Tobe.View
{
    public static class ProcTex
    {
        // ------------------------------------------------------------------ noise helpers
        static uint Hu(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352dU; x ^= x >> 15; x *= 0x846ca68bU; x ^= x >> 16;
            return x;
        }
        public static float H01(int a, int b = 0)
        {
            unchecked { return (Hu((uint)(a * 73856093) ^ (uint)(b * 19349663) ^ 0x9E3779B9U) & 0xFFFFFF) / 16777216f; }
        }
        public static float Noise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = H01(xi, yi), b = H01(xi + 1, yi), c = H01(xi, yi + 1), d = H01(xi + 1, yi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
        static float Line(float dist, float width, float aa = 0.012f) => Mathf.Clamp01((width * 0.5f - dist) / aa + 0.5f);

        // ------------------------------------------------------------------ floor
        public const float FloorX0 = -9f, FloorZ0 = -8f, FloorW = 27f, FloorL = 34f;
        const float Plank = 0.2f;

        public static readonly Color CourtColor = new Color(0.95f, 0.50f, 0.17f);
        public static readonly Color CourtFront = new Color(0.90f, 0.44f, 0.14f);
        public static readonly Color FreeIn = new Color(0.10f, 0.55f, 0.53f);
        public static readonly Color FreeOut = new Color(0.07f, 0.34f, 0.38f);

        /// <summary>Painted wooden sports floor: orange court, teal free zone, plank variation, faint basketball / badminton lines.
        /// When <paramref name="woodDetail"/> is true the plank grain is left to the tiled detail texture.</summary>
        public static Texture2D FloorBase(bool woodDetail)
        {
            string key = woodDetail ? "floor_base_d" : "floor_base";
            if (Mats.TryGetTex(key, out var cached)) return cached;
            var plankTone = new float[320];
            for (int i = 0; i < plankTone.Length; i++) plankTone[i] = 0.93f + 0.07f * H01(i, 7);
            var t = Mats.MakeTex(key, 1024, 1280, (u, v) =>
            {
                float wx = FloorX0 + u * FloorW, wz = FloorZ0 + v * FloorL;
                bool inCourt = wx >= 0f && wx <= Court.Width && wz >= 0f && wz <= Court.Length;
                bool inFree = wx >= -3f && wx <= Court.Width + 3f && wz >= -3f && wz <= Court.Length + 3f;
                Color c = inCourt ? ((wz > 6f && wz < 12f) ? CourtFront : CourtColor) : (inFree ? FreeIn : FreeOut);
                // free zone fades slightly to the darker outer color
                if (!inCourt && inFree)
                {
                    float dx = Mathf.Max(Mathf.Max(-wx, wx - Court.Width), 0f), dz = Mathf.Max(Mathf.Max(-wz, wz - Court.Length), 0f);
                    c = Color.Lerp(FreeIn, new Color(0.08f, 0.44f, 0.46f), Mathf.Clamp01(Mathf.Max(dx, dz) / 3f));
                }

                int pi = Mathf.FloorToInt((wx + 20f) / Plank);
                float fx = (wx + 20f) / Plank - pi;
                float off = H01(pi, 3) * 2.4f;
                int joint = Mathf.FloorToInt((wz + off) / 2.4f);
                float fz = (wz + off) / 2.4f - joint;
                float tone = plankTone[Mathf.Clamp(pi, 0, 319)] * (0.94f + 0.06f * H01(pi, joint + 100));
                // long painterly brush variation along the planks
                float brush = Noise(wx * 0.9f, wz * 0.07f) * 0.5f + Noise(wx * 3.1f, wz * 0.25f) * 0.5f;
                float k = tone * (0.93f + 0.14f * brush);
                if (!woodDetail)
                {
                    float grain = 0.975f + 0.025f * Mathf.Sin(wx * 150f + Noise(wx * 5f, wz * 0.6f) * 9f);
                    k *= grain;
                }
                c *= k;
                if (fx < 0.045f || fx > 0.975f) c *= woodDetail ? 0.85f : 0.74f;     // plank seam
                if (fz < 0.006f) c *= 0.8f;                                           // butt joint

                // faint lines of other sports (basketball: yellow, badminton: pale cyan)
                float bx = wx - 4.5f, bz = wz - 9f;
                float ax = Mathf.Abs(bx), az = Mathf.Abs(bz);
                float la = 0f;
                // basketball 15 x 28
                const float hx = 7.5f, hz = 14f;
                if (ax < hx + 0.1f && az < hz + 0.1f)
                {
                    la = Mathf.Max(la, Line(Mathf.Abs(Mathf.Max(ax - hx, az - hz)), 0.05f));
                    la = Mathf.Max(la, Line(az, 0.05f) * (ax < hx ? 1f : 0f));
                    la = Mathf.Max(la, Line(Mathf.Abs(Mathf.Sqrt(ax * ax + az * az) - 1.8f), 0.05f));
                    float dzEnd = hz - az;                                              // distance to nearest baseline
                    if (dzEnd < 5.9f && ax < 2.5f) la = Mathf.Max(la, Mathf.Max(Line(Mathf.Abs(ax - 2.45f), 0.05f), Line(Mathf.Abs(dzEnd - 5.8f), 0.05f) * (ax < 2.45f ? 1f : 0f)));
                    float ftd = Mathf.Sqrt(ax * ax + (az - (hz - 5.8f)) * (az - (hz - 5.8f)));
                    if (az < hz - 5.8f) la = Mathf.Max(la, Line(Mathf.Abs(ftd - 1.8f), 0.05f));
                    float arc = Mathf.Sqrt(ax * ax + (az - (hz - 1.575f)) * (az - (hz - 1.575f)));
                    if (ax < 6.6f && az < hz - 1.575f) la = Mathf.Max(la, Line(Mathf.Abs(arc - 6.75f), 0.05f));
                    if (Mathf.Abs(ax - 6.6f) < 0.03f && az > hz - 1.575f - 0.01f) la = Mathf.Max(la, 1f);
                }
                if (la > 0f) c = Color.Lerp(c, new Color(0.96f, 0.88f, 0.42f), la * 0.33f);
                float lb = 0f;
                // badminton 6.1 x 13.4
                if (ax < 3.15f && az < 6.8f)
                {
                    lb = Mathf.Max(lb, Line(Mathf.Abs(Mathf.Max(ax - 3.05f, az - 6.7f)), 0.04f));
                    lb = Mathf.Max(lb, Line(Mathf.Abs(ax - 2.59f), 0.04f) * (az < 6.7f ? 1f : 0f));
                    lb = Mathf.Max(lb, Line(Mathf.Abs(az - 1.98f), 0.04f) * (ax < 3.05f ? 1f : 0f));
                    lb = Mathf.Max(lb, Line(Mathf.Abs(az - 5.94f), 0.04f) * (ax < 3.05f ? 1f : 0f));
                    lb = Mathf.Max(lb, Line(ax, 0.04f) * (az > 1.98f && az < 6.7f ? 1f : 0f));
                }
                if (lb > 0f) c = Color.Lerp(c, new Color(0.75f, 0.97f, 1f), lb * 0.30f);
                c.a = 1f;
                return c;
            }, true, TextureWrapMode.Clamp, FilterMode.Trilinear);
            t.anisoLevel = 8;
            return t;
        }

        /// <summary>Procedural tangent-space normal map: plank grooves + varnished grain.</summary>
        public static Texture2D FloorNormal()
        {
            const string key = "floor_normal";
            if (Mats.TryGetTex(key, out var cached)) return cached;
            const int W = 512, H = 640;
            var hgt = new float[W * H];
            for (int y = 0; y < H; y++)
            {
                float wz = FloorZ0 + (y + 0.5f) / H * FloorL;
                for (int x = 0; x < W; x++)
                {
                    float wx = FloorX0 + (x + 0.5f) / W * FloorW;
                    int pi = Mathf.FloorToInt((wx + 20f) / Plank);
                    float fx = (wx + 20f) / Plank - pi;
                    float off = H01(pi, 3) * 2.4f;
                    float fz = (wz + off) / 2.4f; fz -= Mathf.Floor(fz);
                    float h = 0f;
                    if (fx < 0.08f) h -= (0.08f - fx) * 12f;
                    if (fx > 0.96f) h -= (fx - 0.96f) * 20f;
                    if (fz < 0.012f) h -= (0.012f - fz) * 60f;
                    h += Mathf.Sin(wx * 90f + Noise(wx * 4f, wz * 0.5f) * 7f) * 0.05f;
                    hgt[y * W + x] = h;
                }
            }
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float hl = hgt[y * W + Mathf.Max(x - 1, 0)], hr = hgt[y * W + Mathf.Min(x + 1, W - 1)];
                    float hd = hgt[Mathf.Max(y - 1, 0) * W + x], hu = hgt[Mathf.Min(y + 1, H - 1) * W + x];
                    var n = new Vector3((hl - hr) * 0.9f, (hd - hu) * 0.9f, 1f).normalized;
                    px[y * W + x] = new Color32((byte)((n.x * .5f + .5f) * 255f), (byte)((n.y * .5f + .5f) * 255f), (byte)((n.z * .5f + .5f) * 255f), 255);
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true, true) { name = key, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            tex.SetPixels32(px);
            tex.Apply(true, false);
            Mats.CacheTex(key, tex);
            return tex;
        }

        // ------------------------------------------------------------------ cloth / boards
        /// <summary>Gray padded-wall panel texture (tileable): vertical seams, soft quilting.</summary>
        public static Texture2D PadTex() => Mats.MakeTex("pad_gray", 128, 128, (u, v) =>
        {
            float fx = u * 4f; fx -= Mathf.Floor(fx);
            float fy = v * 2f; fy -= Mathf.Floor(fy);
            float seam = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy));
            float k = Mathf.Lerp(0.62f, 1f, Mathf.Clamp01(seam * 14f));
            k *= 0.94f + 0.06f * Noise(u * 40f, v * 40f);
            return new Color(k, k, k, 1f);
        }, true, TextureWrapMode.Repeat);

        /// <summary>Sports-hall ad board (variant 0..5): flat shapes on two-tone background. Bright, no text.</summary>
        public static Texture2D AdTex(int variant)
        {
            variant = ((variant % 6) + 6) % 6;
            Color[] bgA = { new Color(0.9f, 0.12f, 0.2f), new Color(0.1f, 0.3f, 0.85f), new Color(0.1f, 0.65f, 0.4f), new Color(0.55f, 0.2f, 0.85f), new Color(1f, 0.55f, 0.1f), new Color(0.08f, 0.7f, 0.85f) };
            Color[] bgB = { new Color(1f, 0.92f, 0.95f), new Color(1f, 0.85f, 0.2f), new Color(0.96f, 0.98f, 0.9f), new Color(1f, 0.5f, 0.75f), new Color(0.1f, 0.1f, 0.14f), new Color(0.05f, 0.12f, 0.35f) };
            Color a = bgA[variant], b = bgB[variant];
            return Mats.MakeTex("ad" + variant, 512, 128, (u, v) =>
            {
                float x = u * 4f, y = v;
                Color c = a;
                switch (variant)
                {
                    case 0: { float s = (x * 2f + y * 1.2f) * 3f; s -= Mathf.Floor(s); c = s < 0.5f ? a : b; break; }
                    case 1: { float s = Mathf.Abs(((x * 3f) % 1f) - 0.5f) * 2f; c = (y > s * 0.6f + 0.15f && y < s * 0.6f + 0.55f) ? b : a; break; }
                    case 2: { float dx = (x % 1f) - 0.5f, dy = (y - 0.5f) * 0.25f; c = (dx * dx + dy * dy < 0.045f * 0.25f * 3f) ? b : a; if (((int)(x * 2f)) % 2 == 1) c = Color.Lerp(c, b, 0.35f); break; }
                    case 3: { float dx = Mathf.Abs((x % 0.5f) - 0.25f) * 4f, dy = Mathf.Abs(y - 0.5f) * 2f; c = (Mathf.Sqrt(dx) + Mathf.Sqrt(dy) < 0.9f) ? b : a; break; }
                    case 4: { int cx = Mathf.FloorToInt(x * 8f), cy = Mathf.FloorToInt(y * 4f); c = ((cx + cy) & 1) == 0 ? a : b; break; }
                    default: { float w = y + Mathf.Sin(x * 12f) * 0.12f; c = (w > 0.35f && w < 0.65f) ? b : a; break; }
                }
                float edge = Mathf.Min(Mathf.Min(u, 1f - u) * 4f, Mathf.Min(v, 1f - v) * 4f);
                c *= Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(edge * 3f));
                c.a = 1f;
                return c;
            }, true, TextureWrapMode.Clamp);
        }

        /// <summary>Hanging school flag texture. team 0 = black/orange, team 1 = white/teal, 2 = neutral gold/red.</summary>
        public static Texture2D FlagTex(int team, int variant)
        {
            Color bg, fg, ac;
            if (team == 0) { bg = new Color(0.07f, 0.07f, 0.09f); fg = new Color(1f, 0.52f, 0.06f); ac = Color.white; }
            else if (team == 1) { bg = new Color(0.96f, 0.97f, 0.98f); fg = new Color(0.1f, 0.68f, 0.68f); ac = new Color(0.06f, 0.25f, 0.35f); }
            else { bg = new Color(0.75f, 0.08f, 0.15f); fg = new Color(1f, 0.82f, 0.25f); ac = Color.white; }
            variant &= 3;
            return Mats.MakeTex("flag" + team + "_" + variant, 128, 192, (u, v) =>
            {
                Color c = bg;
                float dx = u - 0.5f, dy = v - 0.5f;
                switch (variant)
                {
                    case 0: if (v > 0.12f && v < 0.22f || v > 0.78f && v < 0.88f) c = fg; if (dx * dx * 1.5f + dy * dy < 0.045f) c = fg; if (dx * dx * 1.5f + dy * dy < 0.016f) c = ac; break;
                    case 1: if (Mathf.Abs(dx + (v - 0.5f) * 0.5f) < 0.09f) c = fg; if (Mathf.Abs(dx + (v - 0.5f) * 0.5f - 0.2f) < 0.025f) c = ac; break;
                    case 2: if (v < 0.3f) c = fg; if (v > 0.3f && v < 0.34f) c = ac; if (Mathf.Abs(dx) < 0.18f && v > 0.5f && v < 0.8f && Mathf.Abs(dx) < (0.8f - v) * 0.6f) c = fg; break;
                    default: { float s = (u + v) * 5f; s -= Mathf.Floor(s); if (s < 0.5f) c = Color.Lerp(bg, fg, 0.85f); break; }
                }
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                if (edge < 0.035f) c = fg * 0.9f;
                c.a = 1f;
                return c;
            }, true, TextureWrapMode.Clamp);
        }

        /// <summary>Scoreboard screen background: orange-dark left half, teal right half, LED dot grid.</summary>
        public static Texture2D ScreenTex() => Mats.MakeTex("screen", 256, 128, (u, v) =>
        {
            Color l = Color.Lerp(new Color(0.55f, 0.18f, 0.02f), new Color(0.12f, 0.05f, 0.04f), u * 1.6f);
            Color r = Color.Lerp(new Color(0.02f, 0.2f, 0.28f), new Color(0.04f, 0.5f, 0.55f), (u - 0.5f) * 2f);
            Color c = u < 0.5f ? l : r;
            if (Mathf.Abs(u - 0.5f) < 0.006f) c = new Color(1f, 1f, 1f);
            c *= 0.75f + 0.25f * v;
            float gx = (u * 256f) % 2f, gy = (v * 128f) % 2f;
            if (gx < 0.7f || gy < 0.7f) c *= 0.68f;
            c.a = 1f;
            return c;
        }, true, TextureWrapMode.Clamp, FilterMode.Bilinear);

        public static Texture2D WindowTex() => Mats.MakeTex("window", 8, 64, (u, v) =>
        {
            Color top = new Color(0.7f, 0.85f, 1f), bot = new Color(1f, 0.93f, 0.75f);
            Color c = Color.Lerp(bot, top, v);
            c.a = 1f;
            return c;
        });

        /// <summary>Soft vertical streak used for sun shafts (alpha fades at the sides).</summary>
        public static Texture2D StreakTex() => Mats.MakeTex("streak", 64, 4, (u, v) =>
        {
            float a = Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) * 2f);
            return new Color(1, 1, 1, a * a);
        });

        // ------------------------------------------------------------------ fx
        /// <summary>Radial anime speed lines with a clear center (alpha mask, white).</summary>
        public static Texture2D SpeedLinesTex => Mats.MakeTex("speedlines", 256, 256, (u, v) =>
        {
            float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            if (r > 1f) return new Color(1, 1, 1, 0);
            float ang = Mathf.Atan2(dy, dx);
            int bucket = Mathf.FloorToInt((ang + Mathf.PI) / (Mathf.PI * 2f) * 72f);
            float len = 0.35f + 0.65f * H01(bucket, 11);          // each spoke has its own length
            float within = ((ang + Mathf.PI) / (Mathf.PI * 2f) * 72f) - bucket;
            float spoke = Mathf.Clamp01(1f - Mathf.Abs(within - 0.5f) * 2.4f) * (H01(bucket, 5) > 0.35f ? 1f : 0f);
            float radial = Mathf.Clamp01((r - 0.18f) / 0.12f) * Mathf.Clamp01((len - (r - 0.18f)) / 0.2f) * Mathf.Clamp01((1f - r) * 3f);
            return new Color(1, 1, 1, spoke * radial);
        });

        /// <summary>Full-screen anime impact frame: bright paper with dark radial ink streaks and a hot center.</summary>
        public static Texture2D ImpactFrameTex => Mats.MakeTex("impactframe", 256, 256, (u, v) =>
        {
            float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float ang = Mathf.Atan2(dy, dx);
            float s = (ang + Mathf.PI) / (Mathf.PI * 2f) * 90f;
            int bk = Mathf.FloorToInt(s);
            float w = s - bk;
            float streak = (H01(bk, 3) > 0.45f ? 1f : 0f) * Mathf.Clamp01(1f - Mathf.Abs(w - 0.5f) * 2.2f) * Mathf.Clamp01((r - 0.12f) / 0.35f);
            float k = Mathf.Lerp(1f, 0.05f, streak);
            k = Mathf.Max(k, Mathf.Clamp01(1f - r * 3f));
            return new Color(k, k, k, 1f);
        });

        /// <summary>Trail ribbon: bright center, soft edges (alpha falls off across the ribbon width).</summary>
        public static Texture2D TrailTex => Mats.MakeTex("trail_soft", 16, 32, (u, v) =>
        {
            float a = 1f - Mathf.Abs(v * 2f - 1f);
            a = a * a;
            return new Color(1, 1, 1, a);
        });

        static Color BallColor(Vector3 d, out float seam)
        {
            float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y), az = Mathf.Abs(d.z);
            int face; float p, q;
            if (ax >= ay && ax >= az) { face = d.x > 0f ? 0 : 1; p = d.y / ax; q = d.z / ax; }
            else if (ay >= az) { face = d.y > 0f ? 2 : 3; p = d.z / ay; q = d.x / ay; }
            else { face = d.z > 0f ? 4 : 5; p = d.x / az; q = d.y / az; }
            if ((face & 1) == 1) { float tmp = p; p = q; q = tmp; }               // pinwheel: neighbouring faces run perpendicular
            float qq = q + 0.34f * p;                                              // slant so the panels read as curved
            float s = (qq + 1.34f) / 2.68f * 3f;                                   // 0..3 across the three strips
            int strip = Mathf.Clamp(Mathf.FloorToInt(s), 0, 2);
            float fs = s - Mathf.Floor(s);
            bool yellow = ((strip + (face % 3 == 0 ? 1 : 0)) & 1) == 0;
            Color c = yellow ? new Color(1f, 0.80f, 0.08f) : new Color(0.08f, 0.32f, 0.86f);
            float edge = Mathf.Min(fs, 1f - fs) / 3f * 2.68f;                      // approx. distance in face units
            float face_edge = 1f - Mathf.Max(Mathf.Abs(p), Mathf.Abs(q));
            seam = Mathf.Min(edge * 1.4f, face_edge * 1.1f);
            return c;
        }

        public static Texture2D BallAlbedo() => Mats.MakeTex("ball_albedo", 1024, 512, (u, v) =>
        {
            float th = u * Mathf.PI * 2f, ph = (1f - v) * Mathf.PI;
            var d = new Vector3(Mathf.Sin(ph) * Mathf.Cos(th), Mathf.Cos(ph), Mathf.Sin(ph) * Mathf.Sin(th));
            Color c = BallColor(d, out float seam);
            c *= Mathf.Lerp(0.42f, 1f, Mathf.Clamp01(seam / 0.012f));
            // stitched dots along the seams
            if (seam > 0.014f && seam < 0.03f) c = Color.Lerp(c, new Color(0.96f, 0.96f, 0.9f), 0.25f);
            c.a = 1f;
            return c;
        }, true, TextureWrapMode.Repeat, FilterMode.Trilinear);

        public static Texture2D BallShade() => Mats.MakeTex("ball_shade", 512, 256, (u, v) =>
        {
            float th = u * Mathf.PI * 2f, ph = (1f - v) * Mathf.PI;
            var d = new Vector3(Mathf.Sin(ph) * Mathf.Cos(th), Mathf.Cos(ph), Mathf.Sin(ph) * Mathf.Sin(th));
            Color c = BallColor(d, out float seam);
            c = new Color(c.r * 0.62f, c.g * 0.55f, c.b * 0.9f, 1f);
            c *= Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(seam / 0.012f));
            return c;
        }, true, TextureWrapMode.Repeat);

        // ------------------------------------------------------------------ characters
        /// <summary>Matcap with a soft bright arc (anime hair "angel ring").</summary>
        public static Texture2D HairMatcap() => Mats.MakeTex("hair_matcap", 128, 128, (u, v) =>
        {
            float dx = u - 0.5f, dy = v - 0.66f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float ring = Mathf.Exp(-Mathf.Pow((r - 0.17f) / 0.045f, 2f));
            ring *= Mathf.Clamp01((v - 0.52f) * 12f);
            float lo = Mathf.Exp(-Mathf.Pow((Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.18f) * (v - 0.18f)) - 0.12f) / 0.05f, 2f)) * 0.25f;
            float k = Mathf.Clamp01(ring * 0.9f + lo);
            return new Color(k, k, k, 1f);
        });

        /// <summary>Plain white-ish radial gradient for glows.</summary>
        public static Texture2D StripeTex(string key, Color a, Color b, int n) => Mats.MakeTex(key, 64, 64, (u, v) => ((int)(v * n) & 1) == 0 ? a : b, true, TextureWrapMode.Repeat);
    }
}
