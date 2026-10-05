// Authored volleyball key poses (used when no mocap / humanoid clip exists for the phase).
// Right arm = hitting arm. Directions: x outward (mirrored left), y up, z forward.
using UnityEngine;

namespace Tobe.View
{
    internal static class VolleyKeys
    {
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        // ---------------------------------------------------------------- stances (arm directions in the BODY frame: armFrame = 1)
        /// <summary>Ready ("готовность"): torso ~25 deg forward, hips back, elbows bent, forearms forward at waist height, head up.</summary>
        public static readonly PoseOv Ready = new PoseOv
        {
            uR = V(0.16f, -0.92f, 0.30f), lR = V(-0.12f, 0.06f, 0.99f), uL = V(0.16f, -0.92f, 0.30f), lL = V(-0.12f, 0.06f, 0.99f), armFrame = 1f,
            hips = V(15, 0, 0), spine = V(5, 0, 0), chest = V(4, 0, 0), neck = V(-13, 0, 0), head = V(-11, 0, 0)
        };
        /// <summary>Blocker waiting at the net: hands up at head height, palms to the net, knees bent.</summary>
        public static readonly PoseOv BlockReady = new PoseOv
        {
            uR = V(0.50f, -0.20f, 0.32f), lR = V(-0.08f, 0.95f, 0.30f), uL = V(0.50f, -0.20f, 0.32f), lL = V(-0.08f, 0.95f, 0.30f), armFrame = 1f,
            hips = V(10, 0, 0), spine = V(3, 0, 0), chest = V(2, 0, 0), neck = V(-9, 0, 0), head = V(-8, 0, 0)
        };
        /// <summary>Receiving: straight arms joined into a platform in front of the knees, torso leaned in.</summary>
        public static readonly PoseOv Platform = new PoseOv
        {
            uR = V(-0.06f, -0.62f, 0.78f), lR = V(-0.32f, -0.50f, 0.80f), uL = V(-0.06f, -0.62f, 0.78f), lL = V(-0.32f, -0.50f, 0.80f), armFrame = 1f,
            hips = V(19, 0, 0), spine = V(6, 0, 0), chest = V(5, 0, 0), neck = V(-15, 0, 0), head = V(-12, 0, 0)
        };
        /// <summary>Setter waiting: hands up at the forehead, triangle window, torso upright and a little back.</summary>
        public static readonly PoseOv SetReady = new PoseOv
        {
            uR = V(0.48f, 0.55f, 0.45f), lR = V(-0.55f, 0.72f, 0.40f), uL = V(0.48f, 0.55f, 0.45f), lL = V(-0.55f, 0.72f, 0.40f), armFrame = 1f,
            hips = V(7, 0, 0), spine = V(2, 0, 0), chest = V(-2, 0, 0), neck = V(-5, 0, 0), head = V(-5, 0, 0)
        };
        /// <summary>First step of the spike approach: both arms swung back.</summary>
        public static readonly PoseOv Approach = new PoseOv
        {
            uR = V(0.22f, -0.55f, -0.62f), lR = V(0.10f, -0.70f, -0.50f), uL = V(0.22f, -0.55f, -0.62f), lL = V(0.10f, -0.70f, -0.50f), armFrame = 1f,
            hips = V(12, 0, 0), spine = V(6, 0, 0), chest = V(4, 0, 0)
        };

        // ---------------------------------------------------------------- actions (arm directions in the TORSO frame so they follow arch / lean)
        public static readonly PoseOv SW0 = new PoseOv { uR = V(0.25f, -0.5f, -0.6f), lR = V(0.1f, -0.7f, -0.5f), uL = V(0.25f, -0.5f, -0.6f), lL = V(0.1f, -0.7f, -0.5f), spine = V(12, 0, 0), chest = V(8, 0, 0) };
        public static readonly PoseOv SW1 = new PoseOv { uR = V(0.5f, 0.75f, 0.35f), lR = V(0.3f, 0.8f, 0.3f), uL = V(0.3f, 1f, 0.3f), lL = V(0.15f, 1f, 0.4f), spine = V(-4, 0, 0), chest = V(-6, 0, 0) };
        // «Натянутый лук»: левая рука прямая и указывает на мяч, правое плечо отведено назад-в сторону, локоть выше плеча,
        // кисть у уха; корпус прогнут и развёрнут вправо (открыт), голени заведены назад.
        public static readonly PoseOv SW2 = new PoseOv
        {
            uR = V(0.78f, 0.38f, -0.5f), lR = V(-0.3f, 0.8f, -0.52f), uL = V(0.12f, 0.88f, 0.46f), lL = V(0.06f, 0.86f, 0.5f),
            spine = V(-12, 8, 0), chest = V(-16, 28, -4), head = V(-10, -18, 0), legW = 0.6f,
            gUR = V(0.07f, -0.95f, -0.3f), gLR = V(0.03f, -0.45f, -0.9f), gUL = V(0.07f, -0.95f, -0.15f), gLL = V(0.03f, -0.6f, -0.8f)
        };
        // Контакт: правая рука полностью выпрямлена вверх-вперёд (мяч в высшей точке перед плечом), левая рука прижата к груди,
        // корпус уже разворачивается влево и начинает складываться, ноги выносятся вперёд.
        public static readonly PoseOv SP1 = new PoseOv
        {
            uR = V(0.1f, 0.9f, 0.42f), lR = V(0.04f, 0.86f, 0.5f), uL = V(0.3f, -0.55f, 0.6f), lL = V(-0.75f, 0.15f, 0.45f),
            spine = V(4, -6, 0), chest = V(8, -16, 2), head = V(-12, 4, 0), legW = 0.5f,
            gUR = V(0.07f, -0.92f, 0.15f), gLR = V(0.03f, -0.75f, -0.6f), gUL = V(0.07f, -0.92f, 0.1f), gLL = V(0.03f, -0.75f, -0.6f)
        };
        // Проводка: рука хлёстом уходит по диагонали через тело к левому бедру, корпус сложен и развёрнут влево, ноги впереди к приземлению.
        public static readonly PoseOv SP2 = new PoseOv
        {
            uR = V(-0.25f, -0.35f, 0.9f), lR = V(-0.55f, -0.65f, 0.5f), uL = V(0.45f, -0.75f, -0.1f), lL = V(0.2f, -0.95f, 0.1f),
            spine = V(16, -8, 0), chest = V(24, -26, 0), head = V(10, 6, 0), legW = 0.55f,
            gUR = V(0.07f, -0.8f, 0.55f), gLR = V(0.03f, -0.9f, -0.35f), gUL = V(0.07f, -0.82f, 0.5f), gLL = V(0.03f, -0.9f, -0.35f)
        };
        public static readonly PoseOv SP3 = new PoseOv { uR = V(0.3f, -0.9f, 0.3f), lR = V(0.1f, -0.95f, 0.3f), uL = V(0.3f, -0.9f, 0.2f), lL = V(0.1f, -0.95f, 0.2f), spine = V(8, 0, 0), chest = V(10, 0, 0) };
        public static readonly PoseOv BL0 = new PoseOv { uR = V(0.3f, 0.4f, 0.7f), lR = V(0.2f, 0.4f, 0.8f), uL = V(0.3f, 0.4f, 0.7f), lL = V(0.2f, 0.4f, 0.8f), spine = V(6, 0, 0) };
        public static readonly PoseOv BL1 = new PoseOv { uR = V(0.2f, 0.98f, 0.1f), lR = V(0.18f, 1f, 0.12f), uL = V(0.2f, 0.98f, 0.1f), lL = V(0.18f, 1f, 0.12f), spine = V(4, 0, 0), chest = V(-2, 0, 0), head = V(-10, 0, 0) };
        public static readonly PoseOv BL2 = new PoseOv { uR = V(0.18f, 0.9f, 0.4f), lR = V(0.15f, 0.88f, 0.45f), uL = V(0.18f, 0.9f, 0.4f), lL = V(0.15f, 0.88f, 0.45f), spine = V(10, 0, 0), chest = V(10, 0, 0), head = V(-6, 0, 0) };
        // Приём снизу: прямые руки «платформой», спина около 45° (наклон даёт уже сама низкая стойка), взгляд на мяч
        public static readonly PoseOv BP0 = new PoseOv { uR = V(0.0f, -0.62f, 0.78f), lR = V(-0.30f, -0.5f, 0.80f), uL = V(0.0f, -0.62f, 0.78f), lL = V(-0.30f, -0.5f, 0.80f), spine = V(4, 0, 0), chest = V(2, 0, 0), head = V(-10, 0, 0) };
        public static readonly PoseOv BP1 = new PoseOv { uR = V(0.0f, -0.55f, 0.83f), lR = V(-0.28f, -0.42f, 0.86f), uL = V(0.0f, -0.55f, 0.83f), lL = V(-0.28f, -0.42f, 0.86f), spine = V(6, 0, 0), chest = V(4, 0, 0), head = V(-14, 0, 0) };
        public static readonly PoseOv BP2 = new PoseOv { uR = V(0.0f, -0.3f, 0.95f), lR = V(-0.2f, -0.2f, 0.96f), uL = V(0.0f, -0.3f, 0.95f), lL = V(-0.2f, -0.2f, 0.96f), spine = V(2, 0, 0), chest = V(0, 0, 0), head = V(-12, 0, 0) };
        // Передача: корпус прямой и чуть откинут, руки «окном» над лбом, толчок ногами вверх
        public static readonly PoseOv ST0 = new PoseOv { uR = V(0.35f, -0.3f, 0.6f), lR = V(0f, 0.3f, 0.9f), uL = V(0.35f, -0.3f, 0.6f), lL = V(0f, 0.3f, 0.9f), spine = V(-6, 0, 0), chest = V(-6, 0, 0), head = V(-10, 0, 0) };
        public static readonly PoseOv ST1 = new PoseOv { uR = V(0.55f, 0.78f, 0.3f), lR = V(-0.35f, 0.85f, 0.45f), uL = V(0.55f, 0.78f, 0.3f), lL = V(-0.35f, 0.85f, 0.45f), spine = V(-12, 0, 0), chest = V(-14, 0, 0), head = V(-16, 0, 0) };
        public static readonly PoseOv ST2 = new PoseOv { uR = V(0.3f, 0.95f, 0.25f), lR = V(0.05f, 1f, 0.35f), uL = V(0.3f, 0.95f, 0.25f), lL = V(0.05f, 1f, 0.35f), spine = V(-8, 0, 0), chest = V(-8, 0, 0), head = V(-12, 0, 0) };
        public static readonly PoseOv SV0 = new PoseOv { uR = V(0.3f, -0.9f, 0.1f), lR = V(0.1f, -0.95f, 0.2f), uL = V(0.3f, -0.9f, 0.1f), lL = V(0.1f, -0.95f, 0.2f) };
        public static readonly PoseOv SV1 = new PoseOv { uR = V(0.45f, -0.2f, -0.4f), lR = V(0.15f, 0.15f, -0.8f), uL = V(0.25f, 0.9f, 0.35f), lL = V(0.1f, 1f, 0.2f), spine = V(-5, 0, 0), chest = V(-6, 8, 0) };
        public static readonly PoseOv SV2 = new PoseOv { uR = V(0.55f, 0.6f, -0.5f), lR = V(0.15f, 0.6f, -0.8f), uL = V(0.25f, 1f, 0.3f), lL = V(0.1f, 1f, 0.2f), spine = V(-10, 0, 0), chest = V(-14, 20, 0), head = V(-6, 0, 0) };
        public static readonly PoseOv SH1 = new PoseOv { uR = V(0.12f, 0.95f, 0.3f), lR = V(0.06f, 1f, 0.15f), uL = V(0.4f, -0.4f, 0.3f), lL = V(0.2f, -0.8f, 0.3f), spine = V(4, 0, 0), chest = V(6, -8, 0) };
        public static readonly PoseOv SH2 = new PoseOv { uR = V(0.3f, -0.3f, 0.85f), lR = V(0.12f, -0.7f, 0.7f), uL = V(0.4f, -0.7f, 0.2f), lL = V(0.2f, -0.9f, 0.2f), spine = V(10, 0, 0), chest = V(22, -20, 0) };
        public static readonly PoseOv SH3 = new PoseOv { uR = V(0.3f, -0.9f, 0.3f), lR = V(0.1f, -0.95f, 0.3f), uL = V(0.3f, -0.9f, 0.1f), lL = V(0.1f, -0.95f, 0.2f), spine = V(6, 0, 0), chest = V(6, 0, 0) };

        public static readonly PoseOv[] kWind = { SW0, SW1, SW2 };        public static readonly float[] tWind = { 0f, 0.14f, 0.32f };       public static readonly int[] eWind = { 0, 0, 0 };
        public static readonly PoseOv[] kSpike = { SW2, SP1, SP2, SP3 };  public static readonly float[] tSpike = { 0f, 0.05f, 0.15f, 0.42f }; public static readonly int[] eSpike = { 0, 1, 2, 0 };
        public static readonly PoseOv[] kBlock = { BL0, BL1, BL2 };       public static readonly float[] tBlock = { 0f, 0.1f, 0.3f };        public static readonly int[] eBlock = { 0, 2, 0 };
        public static readonly PoseOv[] kBump = { BP0, BP1, BP2 };        public static readonly float[] tBump = { 0f, 0.12f, 0.35f };       public static readonly int[] eBump = { 0, 1, 0 };
        public static readonly PoseOv[] kSet = { ST0, ST1, ST2 };         public static readonly float[] tSet = { 0f, 0.1f, 0.22f };         public static readonly int[] eSet = { 0, 0, 1 };
        public static readonly PoseOv[] kToss = { SV0, SV1, SV2 };        public static readonly float[] tToss = { 0f, 0.4f, 0.8f };         public static readonly int[] eToss = { 0, 0, 0 };
        public static readonly PoseOv[] kHit = { SV2, SH1, SH2, SH3 };    public static readonly float[] tHit = { 0f, 0.07f, 0.2f, 0.45f };  public static readonly int[] eHit = { 0, 1, 2, 0 };

        /// <summary>Key sequence player: ease per key (0 smooth, 1 accelerate = whip, 2 decelerate).</summary>
        public static void Seq(PoseOv dst, float t, PoseOv[] keys, float[] times, int[] ease)
        {
            int n = keys.Length;
            if (t <= times[0]) { dst.Set(keys[0]); return; }
            if (t >= times[n - 1]) { dst.Set(keys[n - 1]); return; }
            int i = 0;
            while (i + 1 < n - 1 && t >= times[i + 1]) i++;
            float k = (t - times[i]) / (times[i + 1] - times[i]);
            switch (ease[i + 1]) { case 1: k *= k; break; case 2: k = 1f - (1f - k) * (1f - k); break; default: k = AnimMath.Smooth01(k); break; }
            PoseOv.Blend(dst, keys[i], keys[i + 1], k);
        }
    }
}
