// Floor + court lines, ad boards, team benches and coaches' area, referee stand, scorer's table, ball cart, post pads.
using UnityEngine;

namespace Tobe.View
{
    public static class CourtProps
    {
        public static void Build(Transform root, ArenaLive live)
        {
            var go = new GameObject("CourtProps");
            go.transform.SetParent(root, false);
            Floor(go.transform);
            Lines(go.transform);
            FloorText(go.transform);
            AdBoards(go.transform);
            Benches(go.transform);
            ScorersTable(go.transform);
            RefereeStand(go.transform);
            BallCart(go.transform);
            PostPads(go.transform);
            _ = live;
        }

        // ------------------------------------------------------------------ floor
        static void Floor(Transform parent)
        {
            // Optional textures added by the art pipeline (Assets/_Tobe/Resources/Textures/floor_wood_albedo|normal).
            var woodAlbedo = Mats.Res<Texture2D>("Textures/floor_wood_albedo");
            var woodNormal = Mats.Res<Texture2D>("Textures/floor_wood_normal");
            var baseTex = ProcTex.FloorBase(woodAlbedo != null);
            var mat = Mats.Lit("floor_v2", Color.white, 0.62f, 0f, null, baseTex, false, true);
            var nrm = ProcTex.FloorNormal();
            mat.SetTexture("_BumpMap", nrm);
            mat.SetFloat("_BumpScale", 0.9f);
            mat.EnableKeyword("_NORMALMAP");
            if (woodAlbedo != null)
            {
                mat.SetTexture("_DetailAlbedoMap", woodAlbedo);
                mat.SetTextureScale("_DetailAlbedoMap", new Vector2(ProcTex.FloorW / 2f, ProcTex.FloorL / 2f));
                mat.SetFloat("_DetailAlbedoMapScale", 1f);
                mat.EnableKeyword("_DETAIL_MULX2");
            }
            if (woodNormal != null)
            {
                mat.SetTexture("_DetailNormalMap", woodNormal);
                mat.SetTextureScale("_DetailNormalMap", new Vector2(ProcTex.FloorW / 2f, ProcTex.FloorL / 2f));
                mat.SetFloat("_DetailNormalMapScale", 0.8f);
                mat.EnableKeyword("_DETAIL_MULX2");
            }
            var b = new MeshBatch("Floor") { ReceiveShadows = true };
            b.Panel(mat, new Vector3(ProcTex.FloorX0 + ProcTex.FloorW * .5f, 0f, ProcTex.FloorZ0 + ProcTex.FloorL * .5f), Vector3.up, Vector3.forward,
                ProcTex.FloorW, ProcTex.FloorL, Color.white, false);
            b.Flush(parent);
        }

        static void Lines(Transform parent)
        {
            var b = new MeshBatch("CourtLines");
            var white = Mats.Unlit("court_line", new Color(0.96f, 0.96f, 0.93f));
            const float w = 0.05f, y = 0.009f;
            float W = Court.Width, L = Court.Length;
            void Seg(float x0, float z0, float x1, float z1)
            {
                float cx = (x0 + x1) * .5f, cz = (z0 + z1) * .5f;
                b.Panel(white, new Vector3(cx, y, cz), Vector3.up, Vector3.forward, Mathf.Abs(x1 - x0) + w, Mathf.Abs(z1 - z0) + w, Color.white);
            }
            Seg(0, 0, W, 0); Seg(0, L, W, L); Seg(0, 0, 0, L); Seg(W, 0, W, L);
            Seg(0, Court.NetZ, W, Court.NetZ);
            Seg(0, 6, W, 6); Seg(0, 12, W, 12);
            // dashed extensions of the attack lines (free zone)
            for (float x = -1.75f; x < -0.05f; x += 0.35f) { Seg(x, 6, x + 0.15f, 6); Seg(x, 12, x + 0.15f, 12); }
            for (float x = W + 0.2f; x < W + 1.75f; x += 0.35f) { Seg(x, 6, x + 0.15f, 6); Seg(x, 12, x + 0.15f, 12); }
            // service zone ticks
            foreach (float z in new[] { 0f, L })
                foreach (float x in new[] { 0f, W })
                    Seg(x + (x == 0f ? -0.15f : 0.15f), z + (z == 0f ? -0.15f : 0.15f), x, z + (z == 0f ? -0.15f : 0.15f));
            // orange tape rectangle: coaches' area on the bench side
            var tape = Mats.Unlit("coach_tape", new Color(1.4f, 0.75f, 0.15f));
            void Tape(float x0, float z0, float x1, float z1)
            {
                b.Panel(tape, new Vector3((x0 + x1) * .5f, y, (z0 + z1) * .5f), Vector3.up, Vector3.forward, Mathf.Abs(x1 - x0) + 0.04f, Mathf.Abs(z1 - z0) + 0.04f, Color.white);
            }
            Tape(10.3f, 0.5f, 10.3f, 17.5f); Tape(10.3f, 0.5f, 11.2f, 0.5f); Tape(10.3f, 17.5f, 11.2f, 17.5f);
            b.Flush(parent);
        }

        static void FloorText(Transform parent)
        {
            foreach (int end in new[] { 0, 1 })
            {
                var tm = Mats.Text(parent, "TOBE VOLLEY", 0.16f, 64, new Color(0.85f, 1f, 0.95f, 0.5f));
                tm.transform.position = new Vector3(4.5f, 0.011f, end == 0 ? -1.5f : 19.5f);
                tm.transform.rotation = Quaternion.Euler(90f, end == 0 ? 0f : 180f, 0f);
                var jp = Mats.Text(parent, "飛べ", 0.2f, 64, new Color(1f, 0.75f, 0.35f, 0.4f));
                jp.transform.position = new Vector3(end == 0 ? 11f : -2f, 0.011f, end == 0 ? 4.5f : 13.5f);
                jp.transform.rotation = Quaternion.Euler(90f, end == 0 ? 90f : -90f, 0f);
            }
        }

        // ------------------------------------------------------------------ advertising boards around the court
        static void AdBoards(Transform parent)
        {
            var b = new MeshBatch("AdBoards");
            var back = Env.SteelDark;
            string[] words = { "TOBE", "VOLLEY", "飛べ", "SPIKE!", "LEGEND", "JUMP", "繋げ", "SERVE" };
            int idx = 0;
            void Board(Vector3 center, Vector3 facing, bool text)
            {
                b.Box(back, center + facing * -0.08f + Vector3.up * 0.0f, new Vector3(Mathf.Abs(facing.x) > 0.5f ? 0.16f : 3.0f, 0.9f, Mathf.Abs(facing.z) > 0.5f ? 0.16f : 3.0f));
                b.Panel(Env.Ad(idx % 6), center + facing * 0.005f, facing, Vector3.up, 2.9f, 0.8f, Color.white);
                if (text && idx % 2 == 0)
                {
                    var tm = Mats.Text(parent, words[idx % words.Length], 0.085f, 64, new Color(1f, 1f, 1f));
                    tm.transform.position = center + facing * 0.03f;
                    tm.transform.rotation = Quaternion.LookRotation(-facing, Vector3.up);
                }
                idx++;
            }
            const float x0 = -3.7f, x1 = 12.7f, z0 = -3.7f, z1 = 21.7f;
            for (float z = -2.5f; z <= 20.6f; z += 3.1f)
            {
                Board(new Vector3(x0, 0.55f, z), Vector3.right, true);
                Board(new Vector3(x1, 0.55f, z), Vector3.left, true);
            }
            for (float x = -1.5f; x <= 10.6f; x += 3.1f)
            {
                Board(new Vector3(x, 0.55f, z0), Vector3.forward, true);
                Board(new Vector3(x, 0.55f, z1), Vector3.back, true);
            }
            b.Flush(parent);
        }

        // ------------------------------------------------------------------ team benches + coaches
        static void Benches(Transform parent)
        {
            var b = new MeshBatch("Benches");
            var vc = Env.Vc;
            var rng = new System.Random(55);
            for (int team = 0; team < 2; team++)
            {
                float zc = team == 0 ? 3.6f : 14.4f;
                Color shirt = TeamLook.Shirt[team], trim = TeamLook.Trim[team], shorts = TeamLook.Shorts[team];
                // bench: seat + legs + back rail
                b.Box(Env.Wood, new Vector3(11.9f, 0.44f, zc), new Vector3(0.45f, 0.06f, 5.2f));
                foreach (float dz in new[] { -2.4f, 0f, 2.4f })
                    b.Box(Env.Steel, new Vector3(11.9f, 0.21f, zc + dz), new Vector3(0.4f, 0.42f, 0.06f));
                b.Box(team == 0 ? Env.Orange : Env.TealM, new Vector3(12.25f, 0.8f, zc), new Vector3(0.06f, 0.5f, 5.2f));
                // players sitting
                for (int i = 0; i < 6; i++)
                {
                    float z = zc - 2.2f + i * 0.88f;
                    Color skin = Skin(rng);
                    Figures.Person(b, vc, new Vector3(11.9f, 0.47f, z), -90f, i == 0 ? trim : shirt, shorts, skin, Figures.HairPalette[rng.Next(Figures.HairPalette.Length)], Figures.Pose.Sit, 1.0f, trim);
                }
                // water bottles / towels on the floor
                for (int i = 0; i < 4; i++)
                    b.Box(Env.Blue, new Vector3(11.3f, 0.12f, zc - 1.8f + i * 1.2f), new Vector3(0.09f, 0.24f, 0.09f));
                // coach standing in the coaches' area
                float cz = team == 0 ? 7.0f : 11.0f;
                Figures.Person(b, vc, new Vector3(10.75f, 0f, cz), -90f, team == 0 ? new Color(0.1f, 0.1f, 0.13f) : new Color(0.12f, 0.45f, 0.5f),
                    new Color(0.1f, 0.1f, 0.13f), Skin(rng), Figures.HairPalette[rng.Next(Figures.HairPalette.Length)], Figures.Pose.Coach, 1.05f, trim);
                // team name board behind the bench
                var tm = Mats.Text(parent, TeamLook.Names[team], 0.07f, 64, trim);
                tm.transform.position = new Vector3(12.5f, 1.15f, zc);
                tm.transform.rotation = Quaternion.Euler(0, -90f, 0);
                b.Box(Env.Black, new Vector3(12.55f, 1.15f, zc), new Vector3(0.05f, 0.45f, 2.2f));
            }
            b.Flush(parent);
        }

        static Color[] skins = { new Color(0.96f, 0.8f, 0.66f), new Color(0.85f, 0.65f, 0.5f), new Color(0.7f, 0.5f, 0.36f), new Color(0.98f, 0.86f, 0.75f) };
        static Color Skin(System.Random r) => skins[r.Next(skins.Length)];

        // ------------------------------------------------------------------ scorer's table
        static void ScorersTable(Transform parent)
        {
            var b = new MeshBatch("ScorersTable");
            var vc = Env.Vc;
            float x = 11.7f, z = 9f;
            b.Box(Env.White, new Vector3(x, 0.76f, z), new Vector3(0.9f, 0.05f, 3.2f));
            b.Box(Env.White, new Vector3(x - 0.42f, 0.38f, z), new Vector3(0.04f, 0.76f, 3.2f));
            b.Panel(Env.Ad(1), new Vector3(x - 0.45f, 0.4f, z), Vector3.left, Vector3.up, 3.0f, 0.55f, Color.white);
            var screen = Mats.Unlit("laptop_screen", new Color(0.8f, 1.6f, 2.0f));
            foreach (float dz in new[] { -0.9f, 0.9f })
            {
                b.Box(Env.Black, new Vector3(x + 0.05f, 0.8f, z + dz), new Vector3(0.28f, 0.02f, 0.38f));
                b.Panel(screen, new Vector3(x + 0.18f, 0.93f, z + dz), Vector3.left, Vector3.up, 0.34f, 0.2f, Color.white);
                // chair + official
                b.Box(Env.Black, new Vector3(x + 0.85f, 0.45f, z + dz), new Vector3(0.45f, 0.06f, 0.45f));
                b.Box(Env.Black, new Vector3(x + 1.08f, 0.75f, z + dz), new Vector3(0.05f, 0.55f, 0.45f));
                Figures.Person(b, vc, new Vector3(x + 0.85f, 0.48f, z + dz), -90f, new Color(0.15f, 0.17f, 0.3f), new Color(0.1f, 0.1f, 0.14f), new Color(0.92f, 0.76f, 0.62f), Figures.HairPalette[0], Figures.Pose.Sit, 1.0f);
            }
            // flip score device
            b.Box(Env.Black, new Vector3(x - 0.1f, 0.95f, z), new Vector3(0.12f, 0.34f, 0.7f));
            b.Panel(Env.LedWhite, new Vector3(x - 0.17f, 0.95f, z), Vector3.left, Vector3.up, 0.6f, 0.25f, Color.white);
            b.Flush(parent);
        }

        // ------------------------------------------------------------------ referee stand
        static void RefereeStand(Transform parent)
        {
            var b = new MeshBatch("RefStand");
            float x = -1.9f, z = Court.NetZ, hgt = 1.75f;
            var steel = Env.Steel;
            foreach (float dx in new[] { -0.45f, 0.45f })
                foreach (float dz in new[] { -0.45f, 0.45f })
                    b.Beam(steel, new Vector3(x + dx * 1.3f, 0f, z + dz * 1.3f), new Vector3(x + dx, hgt, z + dz), 0.07f);
            b.Box(steel, new Vector3(x, hgt, z), new Vector3(1.1f, 0.07f, 1.1f));
            b.Box(Env.Blue, new Vector3(x - 0.52f, hgt + 0.28f, z), new Vector3(0.07f, 0.5f, 1.1f));
            b.Box(Env.Black, new Vector3(x, hgt + 0.1f, z), new Vector3(0.5f, 0.06f, 0.5f));
            // ladder on the outside
            foreach (float dz in new[] { -0.3f, 0.3f }) b.Beam(steel, new Vector3(x - 0.9f, 0f, z + dz), new Vector3(x - 0.55f, hgt, z + dz), 0.05f);
            for (float y = 0.3f; y < hgt; y += 0.3f) b.Beam(steel, new Vector3(x - 0.9f + y / hgt * 0.35f, y, z - 0.3f), new Vector3(x - 0.9f + y / hgt * 0.35f, y, z + 0.3f), 0.04f);
            // rails
            b.Beam(steel, new Vector3(x + 0.55f, hgt + 0.6f, z - 0.55f), new Vector3(x + 0.55f, hgt + 0.6f, z + 0.55f), 0.05f);
            Figures.Person(b, Env.Vc, new Vector3(x, hgt + 0.13f, z), 90f, new Color(0.96f, 0.96f, 0.98f), new Color(0.08f, 0.08f, 0.12f), new Color(0.9f, 0.72f, 0.58f), Figures.HairPalette[1], Figures.Pose.Sit, 1.0f, new Color(0.1f, 0.1f, 0.12f));
            b.Flush(parent);
        }

        // ------------------------------------------------------------------ ball cart + post pads
        static void BallCart(Transform parent)
        {
            var b = new MeshBatch("BallCart");
            float x = 11.9f, z = -1.9f;
            var steel = Env.Steel;
            foreach (float dx in new[] { -0.35f, 0.35f })
                foreach (float dz in new[] { -0.55f, 0.55f })
                {
                    b.Beam(steel, new Vector3(x + dx, 0.12f, z + dz), new Vector3(x + dx, 0.75f, z + dz), 0.04f);
                    b.Cyl(Env.Black, new Vector3(x + dx - 0.03f, 0.07f, z + dz), new Vector3(x + dx + 0.03f, 0.07f, z + dz), 0.07f, 0.07f, 10);
                }
            b.Box(Env.Blue, new Vector3(x, 0.78f, z), new Vector3(0.8f, 0.05f, 1.2f));
            b.Beam(steel, new Vector3(x - 0.4f, 0.8f, z - 0.6f), new Vector3(x - 0.4f, 1.05f, z - 0.6f), 0.03f);
            b.Beam(steel, new Vector3(x - 0.4f, 0.8f, z + 0.6f), new Vector3(x - 0.4f, 1.05f, z + 0.6f), 0.03f);
            var ballMat = Mats.Toon("env_ball", Color.white, new Mats.ToonOpts { tex = ProcTex.BallAlbedo(), shadeTex = ProcTex.BallShade(), shift = 0.2f });
            for (int i = 0; i < 8; i++)
            {
                float bx = x - 0.2f + (i % 2) * 0.4f, bz = z - 0.45f + (i / 2) * 0.3f;
                b.Sphere(ballMat, new Vector3(bx, 0.9f, bz), 0.105f, 14, 10);
            }
            b.Flush(parent);
        }

        static void PostPads(Transform parent)
        {
            var b = new MeshBatch("PostPads");
            foreach (float x in new[] { -0.7f, Court.Width + 0.7f })
            {
                b.Box(Env.Blue, new Vector3(x, 0.8f, Court.NetZ), new Vector3(0.34f, 1.6f, 0.34f));
                b.Box(Env.White, new Vector3(x, 1.62f, Court.NetZ), new Vector3(0.36f, 0.05f, 0.36f));
            }
            b.Flush(parent);
        }
    }
}
