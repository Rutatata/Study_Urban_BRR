// Bleachers on four sides, a dense crowd (team-colored cheer sections), flags and the cheer squads with drums.
using System.Collections.Generic;
using UnityEngine;

namespace Tobe.View
{
    public static class Stands
    {
        static readonly Color[] Neutral =
        {
            new Color(0.9f,0.15f,0.15f), new Color(0.15f,0.4f,0.95f), new Color(0.95f,0.8f,0.15f), new Color(0.72f,0.78f,0.92f),
            new Color(0.15f,0.8f,0.4f), new Color(0.9f,0.4f,0.9f), new Color(0.2f,0.85f,0.9f), new Color(0.34f,0.36f,0.50f), new Color(0.95f,0.55f,0.65f),
        };
        static readonly Color[] Skin =
        {
            new Color(0.96f,0.8f,0.66f), new Color(0.85f,0.65f,0.5f), new Color(0.7f,0.5f,0.36f), new Color(0.5f,0.34f,0.24f), new Color(0.98f,0.86f,0.75f), new Color(0.9f,0.72f,0.58f),
        };
        static readonly Color[] Team0Shirts = { new Color(0.1f, 0.1f, 0.13f), new Color(0.1f, 0.1f, 0.13f), new Color(0.1f, 0.1f, 0.13f), new Color(1f, 0.52f, 0.08f), new Color(1f, 0.52f, 0.08f), new Color(0.72f, 0.78f, 0.92f) };
        static readonly Color[] Team1Shirts = { new Color(0.62f, 0.80f, 0.90f), new Color(0.62f, 0.80f, 0.90f), new Color(0.62f, 0.80f, 0.90f), new Color(0.12f, 0.72f, 0.70f), new Color(0.12f, 0.72f, 0.70f), new Color(0.1f, 0.25f, 0.35f) };

        const float Spacing = 0.6f;
        const int PerChunk = 46;

        public static void Build(Transform root, ArenaLive live)
        {
            var holder = new GameObject("Crowd");
            holder.transform.SetParent(root, false);
            var anim = holder.AddComponent<CrowdAnimator>();
            var rng = new System.Random(1234);

            float depth = Hall.Rows * Hall.RowD;
            float zc = (Hall.StandZ0 + Hall.StandZ1) * .5f, xc = (Hall.StandX0 + Hall.StandX1) * .5f;
            float sideLen = (Hall.StandZ1 - Hall.StandZ0) + 2f * depth, endLen = Hall.StandX1 - Hall.StandX0;

            var structure = new MeshBatch("StandStructure") { ReceiveShadows = false };
            var b = new MeshBatch("CrowdBatch") { };
            var crowdMat = Env.Vc;

            // left (-X), right (+X), back (-Z, team 0 end), front (+Z, team 1 end)
            Stand(holder.transform, structure, anim, live, crowdMat, rng, new Vector3(Hall.StandX0, 0, zc), new Vector3(-1, 0, 0), new Vector3(0, 0, 1), sideLen, 0);
            Stand(holder.transform, structure, anim, live, crowdMat, rng, new Vector3(Hall.StandX1, 0, zc), new Vector3(1, 0, 0), new Vector3(0, 0, 1), sideLen, 1);
            Stand(holder.transform, structure, anim, live, crowdMat, rng, new Vector3(xc, 0, Hall.StandZ0), new Vector3(0, 0, -1), new Vector3(1, 0, 0), endLen, 2);
            Stand(holder.transform, structure, anim, live, crowdMat, rng, new Vector3(xc, 0, Hall.StandZ1), new Vector3(0, 0, 1), new Vector3(1, 0, 0), endLen, 3);
            structure.Flush(root);
            _ = b;

            BuildSquad(root, live, 0, new Vector3(xc, 0, Hall.StandZ0), new Vector3(0, 0, -1), new Vector3(1, 0, 0));
            BuildSquad(root, live, 1, new Vector3(xc, 0, Hall.StandZ1), new Vector3(0, 0, 1), new Vector3(1, 0, 0));
        }

        // Reserved rectangle (cheer squad platform) in rows 0..1 at the center of both end stands.
        static bool InSquadZone(int standId, int row, float sPos) => standId >= 2 && row <= 1 && Mathf.Abs(sPos) < 3.4f;

        static Color PickShirt(System.Random rng, int standId, Vector3 pos)
        {
            double r = rng.NextDouble();
            float teamBias;     // probability to wear team 0 / team 1 colours
            int team;
            if (standId == 2) { team = 0; teamBias = 0.88f; }
            else if (standId == 3) { team = 1; teamBias = 0.88f; }
            else { team = pos.z < Hall.CZ ? 0 : 1; teamBias = 0.62f; }
            if (r < teamBias)
            {
                var arr = team == 0 ? Team0Shirts : Team1Shirts;
                return arr[rng.Next(arr.Length)];
            }
            return Neutral[rng.Next(Neutral.Length)];
        }

        static void Stand(Transform parent, MeshBatch structure, CrowdAnimator anim, ArenaLive live, Material crowdMat, System.Random rng,
            Vector3 inner, Vector3 outDir, Vector3 along, float length, int standId)
        {
            var standA = Env.StandA; var standB = Env.StandB; var step = Env.StandStep;
            Vector3 absOut = new Vector3(Mathf.Abs(outDir.x), 0, Mathf.Abs(outDir.z));
            Vector3 absAlong = new Vector3(Mathf.Abs(along.x), 0, Mathf.Abs(along.z));
            float yaw = Mathf.Atan2(-outDir.x, -outDir.z) * Mathf.Rad2Deg;      // people look toward the court
            var q = Quaternion.Euler(0f, yaw, 0f);
            float[] aisles = { -0.33f, 0f, 0.33f };
            var sprites = new CrowdSprites();
            int count = Mathf.FloorToInt(length / Spacing);
            for (int i = 0; i < Hall.Rows; i++)
            {
                float top = (i + 1) * Hall.RowH;
                Vector3 c = inner + outDir * (i * Hall.RowD + Hall.RowD * .5f) + Vector3.up * (top * .5f);
                Vector3 sz = absOut * Hall.RowD + absAlong * length + Vector3.up * top;
                structure.Box((i & 1) == 0 ? standA : standB, c, sz);
                // aisle stairs (light nosing)
                foreach (float a in aisles)
                {
                    Vector3 sc = inner + outDir * (i * Hall.RowD + Hall.RowD * .5f) + along * (a * length) + Vector3.up * (top + 0.01f);
                    structure.Box(step, sc, absOut * (Hall.RowD * 0.98f) + absAlong * 0.9f + Vector3.up * 0.03f);
                }
                Vector3 rowPos = inner + outDir * (i * Hall.RowD + Hall.RowD * .5f);
                for (int k = 0; k < count; k++)
                {
                    float sPos = -length * .5f + (k + 0.5f) * Spacing + ((float)rng.NextDouble() - .5f) * 0.1f;
                    float frac = sPos / length;
                    bool aisle = false;
                    foreach (float a in aisles) if (Mathf.Abs(frac - a) * length < 0.62f) aisle = true;
                    if (aisle || InSquadZone(standId, i, sPos)) continue;
                    if (rng.NextDouble() < 0.07) continue;      // empty seat
                    Vector3 p = rowPos + along * sPos + Vector3.up * top;
                    Color shirt = PickShirt(rng, standId, p);
                    Color skin = Skin[rng.Next(Skin.Length)];
                    Color hair = Figures.HairPalette[rng.Next(Figures.HairPalette.Length)];
                    float hh = 0.92f + (float)rng.NextDouble() * 0.16f;
                    sprites.Add(p, shirt, skin, hair, rng.Next(8), (float)rng.NextDouble(), hh * 1.05f);
                }
                // front rail on the first row, back rail on the last
                if (i == 0)
                {
                    Vector3 rp = inner + outDir * 0.05f + Vector3.up * (top + 0.55f);
                    structure.Box(Env.Steel, rp, absOut * 0.06f + absAlong * length + Vector3.up * 0.06f);
                }
            }
            sprites.Flush(parent, "CrowdSprites" + standId);

            // supporter flags in the stands (waving): both end stands, 7 per team-ish
            int nFlags = standId >= 2 ? 8 : 5;
            for (int f = 0; f < nFlags; f++)
            {
                int row = 3 + rng.Next(Hall.Rows - 3);
                float fr = ((float)rng.NextDouble() - .5f) * 0.8f;
                float top = (row + 1) * Hall.RowH;
                Vector3 p = inner + outDir * (row * Hall.RowD + Hall.RowD * .5f) + along * (fr * length) + Vector3.up * top;
                int team = standId == 2 ? 0 : standId == 3 ? 1 : (p.z < Hall.CZ ? 0 : 1);
                AddHandFlag(parent, live, p, outDir, team, rng.Next(4), 2.2f + (float)rng.NextDouble() * 0.6f);
            }
        }

        static void AddHandFlag(Transform parent, ArenaLive live, Vector3 baseP, Vector3 outDir, int team, int variant, float poleLen)
        {
            var go = new GameObject("HandFlag");
            go.transform.SetParent(parent, false);
            go.transform.position = baseP + Vector3.up * 0.9f;
            var pole = Mats.Prim(PrimitiveType.Cylinder, go.transform, "Pole", new Vector3(0, poleLen * .5f, 0), new Vector3(0.04f, poleLen * .5f, 0.04f), Env.Steel);
            _ = pole;
            var tex = ProcTex.FlagTex(team, variant);
            var mat = Mats.Unlit("flagmat_" + team + "_" + variant, new Color(1.1f, 1.1f, 1.1f), tex, false, false, true);
            var cloth = Mats.Quad("Cloth", go.transform, new Vector3(0.55f, poleLen - 0.45f, 0), Vector3.zero, new Vector3(1.1f, 0.8f, 1f), mat);
            // face the court
            go.transform.rotation = Quaternion.LookRotation(-outDir, Vector3.up);
            live.AddSway(go.transform, new Vector3(0, 0, 1), 9f + Random.value * 6f, 1.6f + Random.value, Random.value * 6.28f, true);
            _ = cloth;
        }

        // ------------------------------------------------------------------ cheer squads (oendan) with drums
        static void BuildSquad(Transform root, ArenaLive live, int team, Vector3 inner, Vector3 outDir, Vector3 along)
        {
            float top = 2 * Hall.RowH;                  // platform covers rows 0 and 1
            Vector3 center = inner + outDir * (Hall.RowD * 1.0f) + Vector3.up * top;
            float yaw = Mathf.Atan2(-outDir.x, -outDir.z) * Mathf.Rad2Deg;
            var q = Quaternion.Euler(0f, yaw, 0f);
            Color accent = Hall.TeamAccent(team);
            Color uniform = team == 0 ? new Color(0.08f, 0.08f, 0.1f) : new Color(0.95f, 0.95f, 0.97f);
            var plat = new MeshBatch("SquadPlatform");
            plat.Box(Env.SteelDark, inner + outDir * (Hall.RowD) + Vector3.up * (top * .5f), new Vector3(Mathf.Abs(outDir.z) > 0.5f ? 7f : Hall.RowD * 2f, top, Mathf.Abs(outDir.z) > 0.5f ? Hall.RowD * 2f : 7f));
            plat.Box(team == 0 ? Env.Orange : Env.TealM, center + q * new Vector3(0, 0.0f, -0.9f) , new Vector3(6.8f, 0.06f, 0.1f));
            // drums: three taiko in front
            var drumBody = Mats.Toon("env_drum_body", new Color(0.55f, 0.18f, 0.1f), new Color(0.3f, 0.08f, 0.06f));
            var drumTop = Mats.Toon("env_drum_top", new Color(0.97f, 0.93f, 0.82f), new Color(0.7f, 0.62f, 0.5f));
            for (int i = -1; i <= 1; i++)
            {
                Vector3 dp = center + q * new Vector3(i * 1.5f, 0f, 0.25f);
                plat.Cyl(drumBody, dp + Vector3.up * 0.15f, dp + Vector3.up * 0.95f, 0.46f, 0.46f, 16, null, false);
                plat.Cyl(drumTop, dp + Vector3.up * 0.95f, dp + Vector3.up * 0.97f, 0.46f, 0.46f, 16, null, true);
                plat.Beam(Env.Wood, dp + Vector3.up * 1.05f + q * new Vector3(0.1f, 0, 0.3f), dp + Vector3.up * 1.35f + q * new Vector3(0.3f, 0, 0.4f), 0.03f);
            }
            plat.Flush(root);
            // performers (bobbing)
            var mat = Env.Vc;
            var rng = new System.Random(900 + team);
            float[] xs = { -2.6f, -1.5f, 0f, 1.5f, 2.6f };
            for (int i = 0; i < xs.Length; i++)
            {
                var fb = new MeshBatch("SquadPerson");
                Vector3 local = new Vector3(xs[i], 0f, i == 2 ? -0.5f : -0.45f);
                Vector3 wp = center + q * local;
                var pose = (i == 2) ? Figures.Pose.Cheer : (i == 0 || i == 4) ? Figures.Pose.Clap : Figures.Pose.Cheer;
                Figures.Person(fb, mat, Vector3.zero, yaw, uniform, uniform, Skin[rng.Next(Skin.Length)], Figures.HairPalette[rng.Next(Figures.HairPalette.Length)], pose, 1.05f, accent);
                var t = fb.Flush(root);
                t.position = wp;
                live.AddBob(t, 0.12f, 2.6f + i * 0.15f, i * 0.9f);
            }
            // captain's big flag behind them
            Vector3 fp = center + q * new Vector3(0f, 0f, -0.8f);
            var flagGo = new GameObject("SquadFlag");
            flagGo.transform.SetParent(root, false);
            flagGo.transform.position = fp;
            flagGo.transform.rotation = q;
            Mats.Prim(PrimitiveType.Cylinder, flagGo.transform, "Pole", new Vector3(0, 2.4f, 0), new Vector3(0.06f, 2.4f, 0.06f), Env.Steel);
            var fmat = Mats.Unlit("squadflag" + team, new Color(1.15f, 1.15f, 1.15f), ProcTex.FlagTex(team, 0), false, false, true);
            Mats.Quad("Cloth", flagGo.transform, new Vector3(1.2f, 4.0f, 0), Vector3.zero, new Vector3(2.4f, 1.5f, 1f), fmat);
            live.AddSway(flagGo.transform, new Vector3(0, 0, 1), 16f, 2.4f, team * 1.7f, true);
        }
    }
}
