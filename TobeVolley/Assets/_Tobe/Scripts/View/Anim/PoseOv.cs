// A pose "override": bone directions for arms / legs (and euler deltas for the torso) that sit on top of the mocap base pose.
// Arm directions are expressed in the torso frame (armFrame = 0) or in the yaw-only body frame (armFrame = 1):
// x outward (mirrored for the left side), y up, z forward.
using UnityEngine;

namespace Tobe.View
{
    internal sealed class PoseOv
    {
        static readonly Vector3 Hang = new Vector3(0.12f, -1f, 0.05f);
        public Vector3 uR = Hang, lR = Hang, uL = Hang, lL = Hang;     // arm directions
        public float wR = 1f, wL = 1f;                                  // arm override weights
        public float armFrame;                                          // 0 torso frame .. 1 body (yaw only) frame
        public Vector3 hips, spine, chest, neck, head;                  // euler deltas (deg): x pitch fwd+, y yaw right+, z roll left+
        public float legW;
        public Vector3 gUR = Hang, gLR = Hang, gUL = Hang, gLL = Hang;  // leg directions in the hips frame
        public float pitch, lift;                                       // whole-body pitch (dive) / extra ground clearance (hop)

        public static PoseOv Neutral() { var o = new PoseOv(); o.wR = o.wL = 0f; return o; }
        public static readonly PoseOv NeutralRef = Neutral();
        public void SetNeutral() { Set(NeutralRef); }

        public void Set(PoseOv o)
        {
            uR = o.uR; lR = o.lR; uL = o.uL; lL = o.lL; wR = o.wR; wL = o.wL; armFrame = o.armFrame;
            hips = o.hips; spine = o.spine; chest = o.chest; neck = o.neck; head = o.head;
            legW = o.legW; gUR = o.gUR; gLR = o.gLR; gUL = o.gUL; gLL = o.gLL; pitch = o.pitch; lift = o.lift;
        }

        static Vector3 Dir(Vector3 a, float wa, Vector3 b, float wb, float k)
        {
            if (wa < 0.02f) a = b; else if (wb < 0.02f) b = a;
            return Vector3.Slerp(a.normalized, b.normalized, k);
        }

        /// <summary>d = lerp(a, b, k). d must not alias a or b.</summary>
        public static void Blend(PoseOv d, PoseOv a, PoseOv b, float k)
        {
            d.uR = Dir(a.uR, a.wR, b.uR, b.wR, k); d.lR = Dir(a.lR, a.wR, b.lR, b.wR, k);
            d.uL = Dir(a.uL, a.wL, b.uL, b.wL, k); d.lL = Dir(a.lL, a.wL, b.lL, b.wL, k);
            d.wR = Mathf.Lerp(a.wR, b.wR, k); d.wL = Mathf.Lerp(a.wL, b.wL, k);
            d.armFrame = a.wR < 0.02f && a.wL < 0.02f ? b.armFrame : (b.wR < 0.02f && b.wL < 0.02f ? a.armFrame : Mathf.Lerp(a.armFrame, b.armFrame, k));
            d.hips = Vector3.Lerp(a.hips, b.hips, k); d.spine = Vector3.Lerp(a.spine, b.spine, k); d.chest = Vector3.Lerp(a.chest, b.chest, k);
            d.neck = Vector3.Lerp(a.neck, b.neck, k); d.head = Vector3.Lerp(a.head, b.head, k);
            d.legW = Mathf.Lerp(a.legW, b.legW, k);
            d.gUR = Dir(a.gUR, a.legW, b.gUR, b.legW, k); d.gLR = Dir(a.gLR, a.legW, b.gLR, b.legW, k);
            d.gUL = Dir(a.gUL, a.legW, b.gUL, b.legW, k); d.gLL = Dir(a.gLL, a.legW, b.gLL, b.legW, k);
            d.pitch = Mathf.Lerp(a.pitch, b.pitch, k); d.lift = Mathf.Lerp(a.lift, b.lift, k);
        }
    }
}
