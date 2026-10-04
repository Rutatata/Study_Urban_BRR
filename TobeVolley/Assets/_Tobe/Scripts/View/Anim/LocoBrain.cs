// Decides HOW a player moves on the ground, with hysteresis and minimum hold times so the pose never flickers:
//   Relaxed : between rallies - mocap idle / walk / jog (character faces its movement direction)
//   Gait    : volleyball stance - character keeps facing the ball / net and moves with shuffle / back-pedal / short steps
//   Run     : long distance or very high speed - turn and run (character faces its movement direction)
using UnityEngine;

namespace Tobe.View
{
    internal sealed class LocoBrain
    {
        public enum Mode : byte { Relaxed, Gait, Run }

        public const float RunSpeedEnter = 5.0f;    // m/s: always run above this
        public const float RunDistance = 3.4f;      // m travelled in one direction before we stop shuffling and run
        const float MinHold = 0.3f;

        public Mode mode = Mode.Relaxed;
        public float modeTime;
        public float runDist;
        Vector2 dirStart;
        float slowT;

        public void Reset() { mode = Mode.Relaxed; modeTime = 0f; runDist = 0f; slowT = 0f; }

        /// <param name="v">smoothed planar world velocity</param>
        public Mode Update(float dt, Vector2 v, bool stanceContext, bool forceRun)
        {
            modeTime += dt;
            float sp = v.magnitude;
            if (sp > 1.0f)
            {
                Vector2 d = v / sp;
                if (runDist < 0.05f) dirStart = d;
                else if (Vector2.Dot(d, dirStart) < 0.25f) { runDist = 0f; dirStart = d; }   // sharp direction change = a new short move
                runDist += sp * dt;
            }
            else runDist = Mathf.Max(0f, runDist - 5f * dt);
            if (sp < 0.6f) slowT += dt; else slowT = 0f;

            Mode want;
            if (!stanceContext) want = Mode.Relaxed;
            else
            {
                bool run;
                if (mode == Mode.Run) run = forceRun || (sp > 2.2f && (runDist > 1.0f || sp > RunSpeedEnter));
                else run = forceRun || sp > RunSpeedEnter || (sp > 2.8f && runDist > RunDistance);
                want = run ? Mode.Run : Mode.Gait;
            }
            bool both = (want == Mode.Gait && mode == Mode.Run) || (want == Mode.Run && mode == Mode.Gait);
            if (both && modeTime < MinHold && !forceRun) want = mode;      // minimum hold between shuffle and run
            if (want != mode) { mode = want; modeTime = 0f; }
            return mode;
        }
    }
}
