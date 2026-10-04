// Shared hall dimensions, the environment material set and a blocky "person" mesh builder (spectators, coaches, referee...).
using UnityEngine;

namespace Tobe.View
{
    public static class Hall
    {
        // Hall shell (meters). Court occupies x 0..9, z 0..18.
        public const float X0 = -14.5f, X1 = 23.5f, Z0 = -13.5f, Z1 = 31.5f, H = 16f;
        public const float CX = 4.5f, CZ = 9f;
        // Stands: inner edge of the seating blocks
        public const float StandX0 = -5f, StandX1 = 14f, StandZ0 = -4f, StandZ1 = 22f;
        public const int Rows = 10;
        public const float RowD = 0.9f, RowH = 0.6f;
        // Team colors used for decoration
        public static readonly Color Orange = new Color(1f, 0.52f, 0.08f);
        public static readonly Color Teal = new Color(0.12f, 0.72f, 0.70f);
        public static Color TeamAccent(int team) => team == 0 ? Orange : Teal;
    }

    /// <summary>Environment materials (MToon cel-shaded where lighting matters, unlit vertex color for the crowd).</summary>
    public static class Env
    {
        public static Material Steel => Mats.Toon("env_steel", new Color(0.34f, 0.37f, 0.45f), new Color(0.15f, 0.17f, 0.26f), false, null, 0.2f);
        public static Material SteelDark => Mats.Toon("env_steel_dark", new Color(0.16f, 0.17f, 0.23f), new Color(0.07f, 0.08f, 0.13f), false, null, 0.2f);
        public static Material Wall => Mats.Toon("env_wall", new Color(0.50f, 0.54f, 0.76f), new Color(0.32f, 0.35f, 0.56f), true, null, 0.35f);
        public static Material WallDeep => Mats.Toon("env_wall_deep", new Color(0.30f, 0.34f, 0.55f), new Color(0.18f, 0.20f, 0.38f), true, null, 0.35f);
        public static Material Ceiling => Mats.Toon("env_ceiling", new Color(0.20f, 0.22f, 0.34f), new Color(0.10f, 0.11f, 0.20f), true, null, 0.3f);
        public static Material OuterFloor => Mats.Toon("env_outer_floor", new Color(0.12f, 0.12f, 0.20f), new Color(0.06f, 0.06f, 0.12f), true);
        public static Material StandA => Mats.Toon("env_stand_a", new Color(0.30f, 0.33f, 0.50f), new Color(0.18f, 0.20f, 0.34f), false, null, 0.3f);
        public static Material StandB => Mats.Toon("env_stand_b", new Color(0.24f, 0.27f, 0.43f), new Color(0.14f, 0.16f, 0.30f), false, null, 0.3f);
        public static Material StandStep => Mats.Toon("env_stand_step", new Color(0.52f, 0.55f, 0.68f), new Color(0.34f, 0.36f, 0.50f));
        public static Material Orange => Mats.Toon("env_orange", Hall.Orange, new Color(0.75f, 0.30f, 0.04f));
        public static Material TealM => Mats.Toon("env_teal", Hall.Teal, new Color(0.05f, 0.40f, 0.45f));
        public static Material White => Mats.Toon("env_white", new Color(0.90f, 0.91f, 0.95f), new Color(0.58f, 0.62f, 0.80f));
        public static Material Black => Mats.Toon("env_black", new Color(0.10f, 0.10f, 0.13f), new Color(0.04f, 0.04f, 0.07f));
        public static Material Red => Mats.Toon("env_red", new Color(0.85f, 0.12f, 0.18f), new Color(0.5f, 0.05f, 0.12f));
        public static Material Blue => Mats.Toon("env_blue", new Color(0.12f, 0.30f, 0.85f), new Color(0.06f, 0.14f, 0.5f));
        public static Material Wood => Mats.Toon("env_wood", new Color(0.78f, 0.52f, 0.28f), new Color(0.50f, 0.30f, 0.16f));
        public static Material Pad(string key, Color c, Color shade) => Mats.Toon("env_pad_" + key, c, new Mats.ToonOpts { shade = shade, tex = ProcTex.PadTex(), shift = 0.25f, doubleSided = true });

        public static Material Lamp => Mats.Unlit("env_lamp", new Color(1.5f, 1.4f, 1.15f));
        public static Material LedOrange => Mats.Unlit("env_led_o", new Color(1.7f, 0.85f, 0.15f));
        public static Material LedTeal => Mats.Unlit("env_led_t", new Color(0.2f, 1.4f, 1.4f));
        public static Material LedWhite => Mats.Unlit("env_led_w", new Color(1.2f, 1.2f, 1.3f));
        public static Material Glass => Mats.Unlit("env_window", new Color(1.1f, 1.05f, 0.9f), ProcTex.WindowTex(), false, false, true);
        /// <summary>Unlit, multiplies by baked vertex colors (crowd, figures, pennants).</summary>
        public static Material Vc => Mats.VertexColorOpaque("env_vc2", new Color(0.82f, 0.82f, 0.86f));
        public static Material Shaft => Mats.AdditiveVertex("env_shaft");
        public static Material Pool => Mats.AdditiveVertex("env_pool", Mats.SoftDot);

        public static Material Ad(int i) => Mats.Unlit("env_ad" + i, new Color(0.9f, 0.9f, 0.9f), ProcTex.AdTex(i));
    }

    /// <summary>Low-poly blocky people for the arena (unlit, vertex-color shaded).</summary>
    public static class Figures
    {
        public enum Pose { Stand, Sit, Cheer, Clap, Coach }

        public static readonly Color[] HairPalette =
        {
            new Color(0.08f, 0.07f, 0.08f), new Color(0.22f, 0.14f, 0.09f), new Color(0.45f, 0.30f, 0.15f), new Color(0.85f, 0.7f, 0.3f),
            new Color(0.12f, 0.1f, 0.12f), new Color(0.6f, 0.2f, 0.12f), new Color(0.75f, 0.75f, 0.78f),
        };

        static void P(MeshBatch b, Material m, Vector3 pos, Quaternion q, Vector3 off, Vector3 size, Color c, Quaternion? local = null, bool skipBottom = false)
        {
            b.Box(m, pos + q * off, size, c, local.HasValue ? q * local.Value : q, true, skipBottom);
        }

        /// <summary>Adds one person. pos = feet position (for Sit: seat top under the pelvis).</summary>
        public static void Person(MeshBatch b, Material m, Vector3 pos, float yaw, Color shirt, Color pants, Color skin, Color hair, Pose pose, float scale = 1f, Color? trim = null)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            float s = scale;
            Color shoe = new Color(0.9f, 0.9f, 0.92f);
            bool sit = pose == Pose.Sit;
            float hip = sit ? 0.02f * s : 0.46f * s;
            if (sit)
            {
                P(b, m, pos, q, new Vector3(-0.1f * s, hip + 0.06f * s, 0.18f * s), new Vector3(0.13f * s, 0.13f * s, 0.4f * s), pants);
                P(b, m, pos, q, new Vector3(0.1f * s, hip + 0.06f * s, 0.18f * s), new Vector3(0.13f * s, 0.13f * s, 0.4f * s), pants);
                P(b, m, pos, q, new Vector3(-0.1f * s, hip - 0.18f * s, 0.38f * s), new Vector3(0.12f * s, 0.4f * s, 0.12f * s), skin);
                P(b, m, pos, q, new Vector3(0.1f * s, hip - 0.18f * s, 0.38f * s), new Vector3(0.12f * s, 0.4f * s, 0.12f * s), skin);
                P(b, m, pos, q, new Vector3(-0.1f * s, hip - 0.36f * s, 0.44f * s), new Vector3(0.12f * s, 0.07f * s, 0.24f * s), shoe);
                P(b, m, pos, q, new Vector3(0.1f * s, hip - 0.36f * s, 0.44f * s), new Vector3(0.12f * s, 0.07f * s, 0.24f * s), shoe);
                hip += 0.13f * s;
            }
            else
            {
                P(b, m, pos, q, new Vector3(-0.1f * s, 0.23f * s, 0f), new Vector3(0.14f * s, 0.46f * s, 0.15f * s), pants);
                P(b, m, pos, q, new Vector3(0.1f * s, 0.23f * s, 0f), new Vector3(0.14f * s, 0.46f * s, 0.15f * s), pants);
                P(b, m, pos, q, new Vector3(-0.1f * s, 0.03f * s, 0.04f * s), new Vector3(0.15f * s, 0.07f * s, 0.26f * s), shoe);
                P(b, m, pos, q, new Vector3(0.1f * s, 0.03f * s, 0.04f * s), new Vector3(0.15f * s, 0.07f * s, 0.26f * s), shoe);
            }
            float torsoH = 0.52f * s;
            P(b, m, pos, q, new Vector3(0f, hip + torsoH * .5f, 0f), new Vector3(0.40f * s, torsoH, 0.22f * s), shirt);
            if (trim.HasValue)
                P(b, m, pos, q, new Vector3(0f, hip + torsoH * .5f, 0.0f), new Vector3(0.405f * s, 0.07f * s, 0.225f * s), trim.Value);
            float shY = hip + torsoH - 0.06f * s;
            // arms
            switch (pose)
            {
                case Pose.Cheer:
                    foreach (int sg in new[] { -1, 1 })
                        P(b, m, pos, q, new Vector3(sg * 0.27f * s, shY + 0.2f * s, 0.02f * s), new Vector3(0.09f * s, 0.46f * s, 0.09f * s), shirt, Quaternion.Euler(0f, 0f, -sg * 14f));
                    break;
                case Pose.Clap:
                    foreach (int sg in new[] { -1, 1 })
                        P(b, m, pos, q, new Vector3(sg * 0.12f * s, shY - 0.05f * s, 0.2f * s), new Vector3(0.09f * s, 0.09f * s, 0.4f * s), shirt, Quaternion.Euler(-25f, sg * -10f, 0f));
                    break;
                case Pose.Coach:
                    P(b, m, pos, q, new Vector3(-0.25f * s, shY - 0.2f * s, 0.0f), new Vector3(0.09f * s, 0.44f * s, 0.09f * s), shirt);
                    P(b, m, pos, q, new Vector3(0.2f * s, shY - 0.12f * s, 0.14f * s), new Vector3(0.09f * s, 0.09f * s, 0.34f * s), shirt, Quaternion.Euler(-35f, 0f, 0f));
                    break;
                default:
                    foreach (int sg in new[] { -1, 1 })
                        P(b, m, pos, q, new Vector3(sg * 0.25f * s, shY - 0.2f * s, sit ? 0.06f * s : 0f), new Vector3(0.09f * s, 0.44f * s, 0.09f * s), shirt, sit ? Quaternion.Euler(-20f, 0f, 0f) : (Quaternion?)null);
                    break;
            }
            float headY = hip + torsoH + 0.02f * s;
            P(b, m, pos, q, new Vector3(0f, headY + 0.13f * s, 0f), new Vector3(0.23f * s, 0.25f * s, 0.23f * s), skin);
            P(b, m, pos, q, new Vector3(0f, headY + 0.27f * s, -0.01f * s), new Vector3(0.25f * s, 0.09f * s, 0.25f * s), hair);
            P(b, m, pos, q, new Vector3(0f, headY + 0.17f * s, -0.1f * s), new Vector3(0.25f * s, 0.2f * s, 0.07f * s), hair);
            // face: dark eye dots (tiny boxes)
            Color eye = new Color(0.08f, 0.07f, 0.1f);
            P(b, m, pos, q, new Vector3(-0.055f * s, headY + 0.14f * s, 0.118f * s), new Vector3(0.04f * s, 0.045f * s, 0.01f * s), eye);
            P(b, m, pos, q, new Vector3(0.055f * s, headY + 0.14f * s, 0.118f * s), new Vector3(0.04f * s, 0.045f * s, 0.01f * s), eye);
        }
    }
}
