// Procedural volleyball footwork. Given the velocity in the BODY frame (x = right, y = forward, body faces the ball / net)
// it produces foot offsets for an alternating low stepping cycle: side shuffle, back-pedal, forward short steps, or a small
// bounce on the spot when standing. Stance phase moves the foot backwards at exactly the body speed (no foot sliding), the
// swing phase carries it forward with a small lift. Step frequency rises with speed (short, quick steps) and stride is
// limited so the feet never cross each other during lateral movement.
using UnityEngine;

namespace Tobe.View
{
    internal sealed class GaitSolver
    {
        const float Duty = 0.62f;           // fraction of the cycle a foot is on the ground

        public float phase;                  // 0..1
        public float freq = 1.4f;            // cycles per second per foot
        public float moveAmt;                // 0 = standing, 1 = stepping
        public Vector2 stride;               // metres a foot travels during its stance (body frame)
        public float bob;                    // pelvis vertical offset (m)
        public float sway;                   // pelvis lateral offset (m)
        public float liftMax = 0.08f;
        float t;

        public void Reset() { phase = 0f; freq = 1.4f; moveAmt = 0f; t = 0f; }

        /// <param name="vb">body-frame velocity (m/s)</param>
        /// <param name="maxStrideX">largest lateral stride that keeps the feet apart (m)</param>
        public void Update(float dt, Vector2 vb, float maxStrideX, float maxStrideZ)
        {
            t += dt;
            float sp = vb.magnitude;
            moveAmt = AnimMath.Smooth01((sp - 0.15f) / 0.5f);

            float fBase = 2.2f + 0.55f * sp;
            float need = Mathf.Max(Duty * Mathf.Abs(vb.x) / Mathf.Max(0.05f, maxStrideX), Duty * Mathf.Abs(vb.y) / Mathf.Max(0.05f, maxStrideZ));
            float fMove = Mathf.Min(Mathf.Max(fBase, need), 6.0f);
            float fTarget = Mathf.Lerp(1.3f, fMove, moveAmt);
            freq = AnimMath.Damp(freq, fTarget, 8f, dt);
            phase = Mathf.Repeat(phase + freq * dt, 1f);

            Vector2 s = vb * (Duty / Mathf.Max(0.5f, freq));
            float k = 1f;
            if (Mathf.Abs(s.x) > maxStrideX) k = Mathf.Min(k, maxStrideX / Mathf.Abs(s.x));
            if (Mathf.Abs(s.y) > maxStrideZ) k = Mathf.Min(k, maxStrideZ / Mathf.Abs(s.y));
            stride = s * k;

            liftMax = 0.05f + 0.035f * Mathf.Min(1f, sp / 3f);
            float idleBob = 0.011f * Mathf.Sin(t * 2f * Mathf.PI * 1.3f);
            float stepBob = -0.02f * (0.5f + 0.5f * Mathf.Cos(phase * 4f * Mathf.PI));
            bob = Mathf.Lerp(idleBob, stepBob, moveAmt);
            sway = Mathf.Lerp(0.012f * Mathf.Sin(t * 2f * Mathf.PI * 0.55f), 0.018f * Mathf.Cos(phase * 2f * Mathf.PI), moveAmt);
        }

        /// <summary>Offset of a foot from its home position (body frame, metres) and its lift / swing amount (0..1).</summary>
        public void Foot(int side, out Vector2 off, out float lift, out float swing)
        {
            float f = Mathf.Repeat(phase + 0.5f * side, 1f);
            if (f < Duty)
            {
                float u = f / Duty;
                off = stride * (0.5f - u);
                lift = 0f; swing = 0f;
            }
            else
            {
                float w = (f - Duty) / (1f - Duty);
                float e = AnimMath.Smooth01(w);
                off = stride * (-0.5f + e);
                swing = Mathf.Sin(w * Mathf.PI);
                lift = swing * liftMax * moveAmt;
                swing *= moveAmt;
            }
        }
    }
}
