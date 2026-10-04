// Procedural jersey number textures (blocky 3x5 digits with an outline) used as decals on the shirts. No fonts, no TextMesh.
using UnityEngine;

namespace Tobe.View
{
    public static class JerseyNumbers
    {
        // rows top -> bottom, 3 columns
        static readonly string[] Glyph =
        {
            "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001",
            "111100111001111", "111100111101111", "111001010010010", "111101111101111", "111101111001111",
        };
        const int Cell = 16, Pad = 8, Outline = 5;

        public static string Key(int n, Color fill, Color outline)
            => "jersey_" + n + "_" + ColorUtility.ToHtmlStringRGB(fill) + "_" + ColorUtility.ToHtmlStringRGB(outline);

        public static Texture2D Get(int n, Color fill, Color outline)
        {
            n = Mathf.Clamp(n, 0, 99);
            string key = Key(n, fill, outline);
            if (Mats.TryGetTex(key, out var cached)) return cached;
            string txt = n.ToString();
            int d = txt.Length;
            int w = (d * 3 + (d - 1)) * Cell + Pad * 2, h = 5 * Cell + Pad * 2;
            var fillMask = new bool[w * h];
            for (int i = 0; i < d; i++)
            {
                string g = Glyph[txt[i] - '0'];
                for (int r = 0; r < 5; r++)
                    for (int c = 0; c < 3; c++)
                    {
                        if (g[r * 3 + c] != '1') continue;
                        int x0 = Pad + (i * 4 + c) * Cell, y0 = h - Pad - (r + 1) * Cell;
                        for (int y = y0; y < y0 + Cell; y++)
                            for (int x = x0; x < x0 + Cell; x++) fillMask[y * w + x] = true;
                    }
            }
            var ring = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!fillMask[y * w + x]) continue;
                    for (int dy = -Outline; dy <= Outline; dy++)
                        for (int dx = -Outline; dx <= Outline; dx++)
                        {
                            if (dx * dx + dy * dy > Outline * Outline) continue;
                            int xx = x + dx, yy = y + dy;
                            if (xx >= 0 && yy >= 0 && xx < w && yy < h) ring[yy * w + xx] = true;
                        }
                }
            Color clear = new Color(outline.r, outline.g, outline.b, 0f);
            Color f = new Color(fill.r, fill.g, fill.b, 1f), o = new Color(outline.r, outline.g, outline.b, 1f);
            return Mats.MakeTex(key, w, h, (u, v) =>
            {
                int x = Mathf.Clamp((int)(u * w), 0, w - 1), y = Mathf.Clamp((int)(v * h), 0, h - 1);
                int i = y * w + x;
                return fillMask[i] ? f : (ring[i] ? o : clear);
            }, true, TextureWrapMode.Clamp, FilterMode.Bilinear);
        }

        /// <summary>Alpha-blended unlit decal material for the number (cached per number + colors).</summary>
        public static Material Material(int n, Color fill, Color outline, out float aspect)
        {
            var tex = Get(n, fill, outline);
            aspect = tex.width / (float)tex.height;
            return Mats.Unlit("mat_" + Key(Mathf.Clamp(n, 0, 99), fill, outline), new Color(0.94f, 0.94f, 0.94f, 1f), tex, true, false, false);
        }
    }
}
