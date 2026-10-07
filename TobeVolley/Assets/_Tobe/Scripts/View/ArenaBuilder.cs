// Builds the stylized indoor arena (Haikyuu-style tournament gym, Rematch-style cel look): lighting, post FX and the
// hall pieces from View/Look (Gym, Stands, CourtProps). Keeps the public API used elsewhere (Adjust, BloomFx, Cheer, Excitement).
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tobe.View
{
    public static class ArenaBuilder
    {
        public static ColorAdjustments Adjust;
        public static Bloom BloomFx;
        /// <summary>Resting post exposure (EV). CameraRig adds its flash on top of this.</summary>
        public const float BaseExposure = -0.2f;
        public const float BaseBloom = 0.3f;
        static float excite;

        public static void Cheer(float amount) { excite = Mathf.Clamp(excite + amount, 0f, 1.6f); }
        public static float Excitement => excite;

        public static void Build(Transform parent)
        {
            var root = new GameObject("Arena").transform;
            root.SetParent(parent, false);
            var live = root.gameObject.AddComponent<ArenaLive>();

            BuildAtmosphere(root);
            Figures.Sprites = new CrowdSprites();
            CourtProps.Build(root, live);
            BuildNet(root);
            Gym.Build(root, live);
            Stands.Build(root, live);
            Figures.Sprites.Flush(root, "FigureSprites"); Figures.Sprites = null;
            BuildLights(root);
            BuildVolume(root);
        }

        // ------------------------------------------------------------------ atmosphere / post
        static void BuildAtmosphere(Transform root)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.30f, 0.46f);
            RenderSettings.ambientEquatorColor = new Color(0.26f, 0.24f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.17f, 0.16f);
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
            bloom.intensity.Override(BaseBloom);
            bloom.threshold.Override(1.15f);
            bloom.scatter.Override(0.6f);
            bloom.tint.Override(new Color(1f, 0.95f, 0.9f));
            BloomFx = bloom;

            var tm = prof.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);

            var ca = prof.Add<ColorAdjustments>(true);
            ca.saturation.Override(16f);
            ca.contrast.Override(10f);
            ca.postExposure.Override(BaseExposure);
            ca.colorFilter.Override(Color.white);
            Adjust = ca;

            var wb = prof.Add<WhiteBalance>(true);
            wb.temperature.Override(6f);
            wb.tint.Override(-3f);

            var vg = prof.Add<Vignette>(true);
            vg.intensity.Override(0.22f);
            vg.smoothness.Override(0.45f);
            vg.color.Override(new Color(0.05f, 0.02f, 0.12f));

            var chroma = prof.Add<ChromaticAberration>(true);
            chroma.intensity.Override(0.03f);
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
                return new Color(0.07f, 0.08f, 0.1f, line * 0.95f);
            }, true, TextureWrapMode.Repeat);
            var netMat = Mats.Unlit("netband2", new Color(1f, 1f, 1f, 1f), gridTex, true, false, true);
            netMat.mainTextureScale = new Vector2(w / 0.12f, (Court.NetTop - Court.NetBottom) / 0.12f);
            // back plane tint so the net reads as a surface
            var shade = Mats.Unlit("netshade2", new Color(0.02f, 0.02f, 0.04f, 0.2f), null, true, false, true);
            float h = Court.NetTop - Court.NetBottom, cy = (Court.NetTop + Court.NetBottom) * .5f;
            Mats.Quad("NetShade", net, new Vector3((left + right) * .5f, cy, Court.NetZ), Vector3.zero, new Vector3(w, h, 1), shade);
            Mats.Quad("NetBand", net, new Vector3((left + right) * .5f, cy, Court.NetZ + 0.002f), Vector3.zero, new Vector3(w, h, 1), netMat);

            var white = Mats.Lit("tapeW", new Color(0.97f, 0.97f, 0.97f), 0.3f);
            var black = Mats.Lit("tapeB", new Color(0.06f, 0.06f, 0.07f), 0.3f);
            Mats.Prim(PrimitiveType.Cube, net, "TopTape", new Vector3((left + right) * .5f, Court.NetTop - 0.055f, Court.NetZ), new Vector3(w, 0.11f, 0.035f), white, true);
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

        // ------------------------------------------------------------------ lights
        static Light MakeLight(Transform root, string name, LightType type, Color color, float intensity, Quaternion rot, LightShadows shadows = LightShadows.None)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.shadows = shadows;
            l.transform.rotation = rot;
            return l;
        }

        static void BuildLights(Transform root)
        {
            // warm key from the roof lights (casts the soft shadows)
            var sun = MakeLight(root, "KeyLight", LightType.Directional, new Color(1f, 0.93f, 0.82f), 1.35f, Quaternion.Euler(56f, -28f, 0f), LightShadows.Soft);
            sun.shadowStrength = 0.75f;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            RenderSettings.sun = sun;
            // cool fill from the opposite side
            MakeLight(root, "FillLight", LightType.Directional, new Color(0.55f, 0.66f, 1f), 0.3f, Quaternion.Euler(38f, 152f, 0f));
            // faint colored rim lights from each end of the hall (orange behind team 0's far end, teal behind team 1's)
            MakeLight(root, "RimTeal", LightType.Directional, new Color(0.25f, 0.9f, 1f), 0.22f, Quaternion.Euler(14f, 180f, 0f));
            MakeLight(root, "RimOrange", LightType.Directional, new Color(1f, 0.5f, 0.3f), 0.22f, Quaternion.Euler(14f, 0f, 0f));

            // two soft overhead spots (URP spot intensity falls off with distance squared, so these are only a gentle top light)
            Color[] tints = { new Color(1f, 0.92f, 0.8f), new Color(0.85f, 0.92f, 1f) };
            int n = 0;
            foreach (var p in new[] { new Vector3(4.5f, 11.8f, 4.5f), new Vector3(4.5f, 11.8f, 13.5f) })
            {
                var l = MakeLight(root, "Spot" + n, LightType.Spot, tints[n % 2], 60f, Quaternion.Euler(90f, 0f, 0f));
                l.transform.position = p;
                l.spotAngle = 80f;
                l.innerSpotAngle = 40f;
                l.range = 26f;
                n++;
            }
        }
    }
}
