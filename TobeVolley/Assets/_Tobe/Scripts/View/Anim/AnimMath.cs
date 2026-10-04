// Frame-rate independent smoothing helpers shared by the animation code. Everything is expressed with time constants
// (1 - exp(-k * dt)) so the result is identical at 60 and 144 fps.
using UnityEngine;

namespace Tobe.View
{
    public static class AnimMath
    {
        /// <summary>Exponential blend factor for rate k (1/s): 1 - exp(-k dt).</summary>
        public static float Rate(float k, float dt) => dt <= 0f ? 0f : 1f - Mathf.Exp(-k * dt);

        public static float Damp(float cur, float target, float k, float dt) => cur + (target - cur) * Rate(k, dt);
        public static Vector2 Damp(Vector2 cur, Vector2 target, float k, float dt) => cur + (target - cur) * Rate(k, dt);

        /// <summary>Damps an angle (degrees) toward target along the shortest way; the result is NOT wrapped (continuous). maxSpeed in deg/s.</summary>
        public static float DampAngle(float cur, float target, float k, float dt, float maxSpeed = 100000f)
        {
            float d = Mathf.DeltaAngle(cur, target) * Rate(k, dt);
            float lim = maxSpeed * dt;
            return cur + Mathf.Clamp(d, -lim, lim);
        }

        public static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    }

    /// <summary>Critically damped scalar spring (Mathf.SmoothDamp) with an explicit first-use snap and speed limit.</summary>
    public struct Spring1
    {
        public float x, v;
        bool init;
        public void Reset() { init = false; v = 0f; }
        public float Step(float target, float smoothTime, float dt, float maxSpeed)
        {
            if (!init) { init = true; x = target; v = 0f; return x; }
            if (dt <= 0f) return x;
            x = Mathf.SmoothDamp(x, target, ref v, smoothTime, maxSpeed, dt);
            return x;
        }
    }
}
