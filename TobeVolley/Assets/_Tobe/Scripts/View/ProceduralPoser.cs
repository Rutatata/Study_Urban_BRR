// Animates any humanoid (VRM or fallback) without animation clips by writing bone rotations in LateUpdate.
using UnityEngine;

namespace Tobe.View
{
    [DefaultExecutionOrder(-50)]
    public sealed class ProceduralPoser : MonoBehaviour
    {
        // ---- inputs (set by PlayerView each frame)
        public PoseId pose;
        public float poseT;
        public Vector3 localVel;   // velocity in the character's facing frame (x right, z forward)
        public bool air;

        sealed class B
        {
            public Transform t;
            public Quaternion rel;   // rest rotation relative to root
            public Vector3 dir;      // rest direction to child in root space (normalized)
            public float len;        // segment length in root-local units
            public float side = 1f;  // +1 / -1: outward direction sign in root x
            public float restVert;   // vertical extent of the segment at rest
        }

        sealed class Pose
        {
            public readonly Vector3[] arm = new Vector3[4]; // upperA, lowerA, upperB, lowerB (x = outward)
            public readonly Vector3[] leg = new Vector3[4];
            public Vector3 hips, spine, chest, neck, head;  // euler degrees: x pitch (+ = lean forward), y yaw, z roll
            public float hipsOff, ground, rootPitch, rootY, tau = 0.08f;

            public void SetIdle()
            {
                arm[0] = arm[2] = V(0.25f, -1f, 0.05f);
                arm[1] = arm[3] = V(0.12f, -0.95f, 0.28f);
                leg[0] = leg[2] = V(0.05f, -1f, 0f);
                leg[1] = leg[3] = V(0.02f, -1f, -0.02f);
                hips = spine = chest = neck = head = Vector3.zero;
                hipsOff = 0f; ground = 1f; rootPitch = 0f; rootY = 0f; tau = 0.08f;
            }

            public static void Blend(Pose cur, Pose tgt, float k)
            {
                for (int i = 0; i < 4; i++)
                {
                    cur.arm[i] = Vector3.Slerp(cur.arm[i], tgt.arm[i], k);
                    cur.leg[i] = Vector3.Slerp(cur.leg[i], tgt.leg[i], k);
                }
                cur.hips = Vector3.Lerp(cur.hips, tgt.hips, k);
                cur.spine = Vector3.Lerp(cur.spine, tgt.spine, k);
                cur.chest = Vector3.Lerp(cur.chest, tgt.chest, k);
                cur.neck = Vector3.Lerp(cur.neck, tgt.neck, k);
                cur.head = Vector3.Lerp(cur.head, tgt.head, k);
                cur.hipsOff = Mathf.Lerp(cur.hipsOff, tgt.hipsOff, k);
                cur.ground = Mathf.Lerp(cur.ground, tgt.ground, k);
                cur.rootPitch = Mathf.Lerp(cur.rootPitch, tgt.rootPitch, k);
                cur.rootY = Mathf.Lerp(cur.rootY, tgt.rootY, k);
            }
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        /// <summary>Direction hanging down, swung forward by ang radians.</summary>
        static Vector3 Sg(float ang, float x = 0f) => new Vector3(x, -Mathf.Cos(ang), Mathf.Sin(ang));

        readonly Pose cur = new Pose(), tgt = new Pose();
        Transform root;
        Animator anim;
        bool ready;
        B hips, spine, chest, neck, head, uaA, laA, uaB, laB, ulA, llA, ulB, llB, footA, footB;
        Vector3 restHipsRel;
        Quaternion baseLocalRot;
        Vector3 baseLocalPos;
        float runPhase, seed;
        bool hasCur;

        public bool Ready => ready;

        // ------------------------------------------------------------------ init
        public void Init(GameObject model)
        {
            ready = false;
            root = model.transform;
            anim = model.GetComponentInChildren<Animator>();
            baseLocalRot = root.localRotation;
            baseLocalPos = root.localPosition;
            seed = Random.value * 20f;

            var tHips = Get(HumanBodyBones.Hips);
            if (tHips == null) { Debug.LogWarning("[Tobe] Poser: no Hips bone found"); return; }

            var tSpine = Get(HumanBodyBones.Spine);
            var tChest = Get(HumanBodyBones.Chest);
            var tNeck = Get(HumanBodyBones.Neck);
            var tHead = Get(HumanBodyBones.Head);

            hips = Make(tHips, tSpine ?? tHead);
            spine = tSpine != null ? Make(tSpine, tChest ?? tNeck ?? tHead) : null;
            chest = tChest != null ? Make(tChest, tNeck ?? tHead) : null;
            neck = tNeck != null ? Make(tNeck, tHead) : null;
            head = tHead != null ? Make(tHead, null) : null;

            uaA = Limb(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm);
            laA = Limb(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
            uaB = Limb(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm);
            laB = Limb(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            ulA = Limb(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg);
            llA = Limb(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
            ulB = Limb(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg);
            llB = Limb(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
            var tFA = Get(HumanBodyBones.RightFoot); var tFB = Get(HumanBodyBones.LeftFoot);
            footA = tFA != null ? Make(tFA, null) : null;
            footB = tFB != null ? Make(tFB, null) : null;

            // side signs: outward = away from the body centerline in root x
            foreach (var b in new[] { uaA, laA, uaB, laB, ulA, llA, ulB, llB })
                if (b != null) b.side = SideOf(b.t);
            foreach (var b in new[] { ulA, llA, ulB, llB })
                if (b != null) b.restVert = b.len * Mathf.Max(0.05f, -b.dir.y);

            restHipsRel = root.InverseTransformPoint(tHips.position);
            if (anim != null) anim.enabled = false;   // we own the bones now
            cur.SetIdle(); tgt.SetIdle();
            hasCur = false;
            ready = true;
        }

        /// <summary>Restores the model root (the poser pitches / offsets it for dives) so another animator can take over.</summary>
        public void Release()
        {
            if (root == null) return;
            root.localRotation = baseLocalRot;
            root.localPosition = baseLocalPos;
            hasCur = false;
        }

        Transform Get(HumanBodyBones hb)
        {
            Transform t = null;
            if (anim != null && anim.isHuman) t = anim.GetBoneTransform(hb);
            if (t == null) t = FindDeep(root, hb.ToString());
            return t;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        B Limb(HumanBodyBones bone, HumanBodyBones child)
        {
            var t = Get(bone); var c = Get(child);
            if (t == null || c == null) return null;
            return Make(t, c);
        }

        B Make(Transform t, Transform child)
        {
            var b = new B { t = t, rel = Quaternion.Inverse(root.rotation) * t.rotation, dir = Vector3.down };
            if (child != null)
            {
                Vector3 d = root.InverseTransformDirection(child.position - t.position);
                b.len = (root.InverseTransformPoint(child.position) - root.InverseTransformPoint(t.position)).magnitude;
                if (d.sqrMagnitude > 1e-8f) b.dir = d.normalized;
            }
            return b;
        }

        float SideOf(Transform t)
        {
            Vector3 p = root.InverseTransformPoint(t.position) - root.InverseTransformPoint(hips.t.position);
            return p.x >= 0f ? 1f : -1f;
        }

        // ------------------------------------------------------------------ per frame
        void LateUpdate()
        {
            if (!ready || root == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);

            float spd = new Vector2(localVel.x, localVel.z).magnitude;
            if (pose == PoseId.Run)
                runPhase += dt * (4f + spd * 1.1f) * (localVel.z < -0.5f ? -1f : 1f);

            Fill(tgt);
            if (!hasCur) { Pose.Blend(cur, tgt, 1f); hasCur = true; }
            else Pose.Blend(cur, tgt, 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, tgt.tau)));
            Apply();
        }

        void Fill(Pose p)
        {
            p.SetIdle();
            float pt = poseT, ph = runPhase, spd = new Vector2(localVel.x, localVel.z).magnitude;
            bool legsSet = false;
            switch (pose)
            {
                case PoseId.Idle: break;

                case PoseId.Ready:
                    if (!air) { p.leg[0] = p.leg[2] = V(0.12f, -1f, 0.85f); p.leg[1] = p.leg[3] = V(0.05f, -1f, -0.7f); legsSet = true; }
                    p.arm[0] = p.arm[2] = V(0.25f, -0.55f, 0.8f);
                    p.arm[1] = p.arm[3] = V(0.15f, -0.35f, 0.9f);
                    p.spine = V(12f, 0, 0); p.chest = V(10f, 0, 0); p.head = V(-14f, 0, 0);
                    p.tau = 0.1f;
                    break;

                case PoseId.Run:
                    {
                        float amp = Mathf.Clamp(spd / 6f, 0.35f, 1f);
                        float s = Mathf.Sin(ph), c = Mathf.Cos(ph);
                        float swA = 0.75f * amp * s, bdA = 0.25f + 1.2f * amp * Mathf.Max(0f, c);
                        float swB = -swA, bdB = 0.25f + 1.2f * amp * Mathf.Max(0f, -c);
                        p.leg[0] = Sg(swA, 0.05f); p.leg[1] = Sg(swA - bdA, 0.02f);
                        p.leg[2] = Sg(swB, 0.05f); p.leg[3] = Sg(swB - bdB, 0.02f);
                        legsSet = true;
                        float aA = -0.8f * amp * s, aB = 0.8f * amp * s;
                        p.arm[0] = Sg(aA, 0.25f); p.arm[1] = Sg(aA + 1.2f, 0.1f);
                        p.arm[2] = Sg(aB, 0.25f); p.arm[3] = Sg(aB + 1.2f, 0.1f);
                        float lean = 8f * amp + Mathf.Clamp(localVel.z, -4f, 8f) * 0.8f;
                        p.spine = V(lean, -s * 10f * amp, -Mathf.Clamp(localVel.x, -6f, 6f) * 1.2f);
                        p.chest = V(2f, 0, 0);
                        p.hips = V(0, s * 8f * amp, 0);
                        p.head = V(-lean * 0.8f, 0, 0);
                        p.tau = 0.06f;
                        break;
                    }

                case PoseId.Bump:
                    {
                        float lift = Mathf.Sin(Mathf.Clamp01(pt / 0.3f) * Mathf.PI) * 0.5f;
                        if (!air) { p.leg[0] = p.leg[2] = V(0.12f, -1f, 1.1f); p.leg[1] = p.leg[3] = V(0.05f, -1f, -0.8f); legsSet = true; }
                        p.arm[0] = p.arm[2] = V(-0.1f, -0.75f + lift, 0.65f);
                        p.arm[1] = p.arm[3] = V(-0.18f, -0.55f + lift, 0.8f);
                        p.spine = V(22f, 0, 0); p.chest = V(15f, 0, 0); p.head = V(-18f, 0, 0);
                        p.tau = 0.07f;
                        break;
                    }

                case PoseId.Set:
                    if (!air) { p.leg[0] = p.leg[2] = V(0.06f, -1f, 0.3f); p.leg[1] = p.leg[3] = V(0.03f, -1f, -0.25f); legsSet = true; }
                    p.arm[0] = p.arm[2] = V(0.55f, 0.8f, 0.25f);
                    p.arm[1] = p.arm[3] = V(-0.45f, 0.7f, 0.55f);
                    p.spine = V(-4f, 0, 0); p.chest = V(-6f, 0, 0); p.head = V(-20f, 0, 0);
                    break;

                case PoseId.Jump:
                    p.arm[0] = p.arm[2] = V(0.5f, 0.9f, 0.1f);
                    p.arm[1] = p.arm[3] = V(0.35f, 1f, 0.1f);
                    p.head = V(-8f, 0, 0);
                    break;

                case PoseId.SpikeWind:
                    p.arm[0] = V(0.5f, 0.8f, -0.35f); p.arm[1] = V(0.15f, 0.6f, -0.8f);
                    p.arm[2] = V(0.3f, 1f, 0.3f); p.arm[3] = V(0.15f, 1f, 0.4f);
                    p.spine = V(-12f, 0, 0); p.chest = V(-14f, 0, 0); p.head = V(-8f, 0, 0); p.hips = V(-4f, 0, 0);
                    p.leg[0] = p.leg[2] = Sg(0.7f, 0.05f); p.leg[1] = p.leg[3] = Sg(-0.5f, 0.02f); legsSet = true;
                    p.ground = 0f; p.tau = 0.06f;
                    break;

                case PoseId.Spike:
                    p.arm[0] = V(0.2f, -0.15f, 0.95f); p.arm[1] = V(0.1f, -0.55f, 0.85f);
                    p.arm[2] = V(0.35f, -0.7f, 0.35f); p.arm[3] = V(0.2f, -0.95f, 0.2f);
                    p.spine = V(20f, 0, 0); p.chest = V(18f, 0, 0); p.head = V(12f, 0, 0);
                    p.leg[0] = p.leg[2] = Sg(0.5f, 0.05f); p.leg[1] = p.leg[3] = Sg(-0.5f, 0.02f); legsSet = true;
                    p.ground = 0f; p.tau = 0.035f;
                    break;

                case PoseId.Block:
                    p.arm[0] = p.arm[2] = V(0.02f, 1f, 0.18f);
                    p.arm[1] = p.arm[3] = V(0f, 1f, 0.3f);
                    p.spine = V(6f, 0, 0); p.head = V(-6f, 0, 0);
                    break;

                case PoseId.Dive:
                    p.rootPitch = 75f; p.rootY = -0.65f;
                    p.arm[0] = p.arm[2] = V(0.12f, 1f, 0.25f);
                    p.arm[1] = p.arm[3] = V(0.08f, 1f, 0.3f);
                    p.leg[0] = V(0.1f, -1f, -0.1f); p.leg[1] = V(0.08f, -0.75f, -0.7f);
                    p.leg[2] = V(0.1f, -1f, 0.05f); p.leg[3] = V(0.05f, -1f, -0.2f);
                    legsSet = true;
                    p.neck = V(-15f, 0, 0); p.head = V(-35f, 0, 0);
                    p.ground = 0f; p.tau = 0.07f;
                    break;

                case PoseId.ServeToss:
                    if (!air) { p.leg[0] = p.leg[2] = V(0.08f, -1f, 0.25f); p.leg[1] = p.leg[3] = V(0.03f, -1f, -0.2f); legsSet = true; }
                    p.arm[2] = V(0.25f, 1f, 0.3f); p.arm[3] = V(0.1f, 1f, 0.2f);
                    p.arm[0] = V(0.35f, -0.8f, 0.1f); p.arm[1] = V(0.2f, -0.9f, 0.2f);
                    p.spine = V(-5f, 0, 0); p.chest = V(-6f, 0, 0); p.head = V(-18f, 0, 0);
                    break;

                case PoseId.ServeHit:
                    if (!air) { p.leg[0] = p.leg[2] = Sg(0.2f, 0.05f); p.leg[1] = p.leg[3] = Sg(-0.1f, 0.02f); legsSet = true; }
                    p.arm[0] = V(0.3f, 0.75f, 0.55f); p.arm[1] = V(0.1f, 0.85f, 0.4f);
                    p.arm[2] = V(0.4f, -0.8f, 0.2f); p.arm[3] = V(0.2f, -0.9f, 0.2f);
                    p.spine = V(12f, 0, 0); p.chest = V(10f, 0, 0); p.head = V(-8f, 0, 0);
                    p.tau = 0.04f;
                    break;

                case PoseId.Celebrate:
                    {
                        float hop = Mathf.Abs(Mathf.Sin(pt * 9f));
                        p.arm[0] = p.arm[2] = V(0.7f, 0.65f, 0.1f);
                        p.arm[1] = p.arm[3] = V(0.15f, 1f, 0.15f);
                        p.leg[0] = p.leg[2] = Sg(0.35f * hop, 0.05f); p.leg[1] = p.leg[3] = Sg(-0.45f * hop, 0.02f);
                        legsSet = true;
                        p.rootY = hop * 0.14f; p.ground = 0f;
                        p.spine = V(-5f, 0, 0); p.head = V(-10f, 0, 0);
                        break;
                    }

                case PoseId.Sad:
                    p.arm[0] = p.arm[2] = V(0.08f, -1f, 0.05f);
                    p.arm[1] = p.arm[3] = V(0f, -1f, 0.1f);
                    p.spine = V(14f, 0, 0); p.chest = V(10f, 0, 0); p.neck = V(12f, 0, 0); p.head = V(22f, 0, 0);
                    p.tau = 0.15f;
                    break;

                case PoseId.Stun:
                    {
                        p.arm[0] = V(0.4f + 0.2f * Mathf.Sin(pt * 10f), -1f, 0.3f * Mathf.Sin(pt * 7f));
                        p.arm[1] = V(0.2f, -1f, 0.3f + 0.2f * Mathf.Sin(pt * 9f));
                        p.arm[2] = V(0.4f + 0.2f * Mathf.Sin(pt * 10f + 2f), -1f, 0.3f * Mathf.Sin(pt * 7f + 1f));
                        p.arm[3] = V(0.2f, -1f, 0.3f + 0.2f * Mathf.Sin(pt * 9f + 1f));
                        p.leg[0] = Sg(0.15f * Mathf.Sin(pt * 8f), 0.08f); p.leg[2] = Sg(-0.15f * Mathf.Sin(pt * 8f), 0.08f);
                        p.leg[1] = p.leg[3] = Sg(-0.1f);
                        legsSet = true;
                        p.spine = V(10f, Mathf.Sin(pt * 9f) * 12f, Mathf.Sin(pt * 14f) * 10f);
                        p.head = V(12f, 0, Mathf.Sin(pt * 11f + 1f) * 14f);
                        p.tau = 0.05f;
                        break;
                    }
            }

            if (air && !legsSet)
            {
                p.leg[0] = p.leg[2] = Sg(0.5f, 0.08f);
                p.leg[1] = p.leg[3] = Sg(-0.6f, 0.02f);
            }
            if (air) p.ground = 0f;
        }

        void Apply()
        {
            float t = Time.time + seed;
            // whole-model pitch about the hips (dive) + vertical offset
            Quaternion rq = Quaternion.Euler(cur.rootPitch, 0f, 0f);
            root.localRotation = rq * baseLocalRot;
            Vector3 pivot = new Vector3(0f, restHipsRel.y * root.localScale.y, 0f);
            root.localPosition = baseLocalPos + pivot - rq * pivot + Vector3.up * cur.rootY;
            Quaternion R = root.rotation;

            // idle breathing / sway (always on, small)
            float br = Mathf.Sin(t * 1.9f), sw = Mathf.Sin(t * 0.8f);

            // vertical: keep the lowest foot planted while grounded
            float off = cur.hipsOff;
            if (cur.ground > 0.001f)
            {
                float extA = LegExtent(ulA, llA, cur.leg[0], cur.leg[1]);
                float extB = LegExtent(ulB, llB, cur.leg[2], cur.leg[3]);
                float restA = ulA != null && llA != null ? ulA.restVert + llA.restVert : 0f;
                float restB = ulB != null && llB != null ? ulB.restVert + llB.restVert : 0f;
                float auto = Mathf.Max(extA - restA, extB - restB);
                if (restA <= 0f && restB <= 0f) auto = 0f;
                off = Mathf.Lerp(cur.hipsOff, Mathf.Min(0f, auto), cur.ground);
            }

            Quaternion q = E(cur.hips + new Vector3(0f, sw * 1.5f, sw * 0.8f));
            hips.t.rotation = R * q * hips.rel;
            hips.t.position = root.TransformPoint(restHipsRel + new Vector3(0f, off + br * 0.004f, 0f));

            if (spine != null) { q *= E(cur.spine + new Vector3(0f, 0f, sw * 1.2f)); spine.t.rotation = R * q * spine.rel; }
            if (chest != null) { q *= E(cur.chest + new Vector3(br * 1.2f, 0f, 0f)); chest.t.rotation = R * q * chest.rel; }
            if (neck != null) { q *= E(cur.neck); neck.t.rotation = R * q * neck.rel; }
            if (head != null) { q *= E(cur.head + new Vector3(Mathf.Sin(t * 1.1f), Mathf.Sin(t * 0.7f) * 2f, 0f)); head.t.rotation = R * q * head.rel; }

            Vector3 breathe = new Vector3(0f, 0f, br * 0.025f);
            LimbRot(R, uaA, cur.arm[0] + breathe); LimbRot(R, laA, cur.arm[1] + breathe);
            LimbRot(R, uaB, cur.arm[2] - breathe); LimbRot(R, laB, cur.arm[3] - breathe);
            LimbRot(R, ulA, cur.leg[0]); LimbRot(R, llA, cur.leg[1]);
            LimbRot(R, ulB, cur.leg[2]); LimbRot(R, llB, cur.leg[3]);

            Quaternion fq = Quaternion.Euler(Mathf.Lerp(25f, 0f, cur.ground), 0f, 0f);
            if (footA != null) footA.t.rotation = R * fq * footA.rel;
            if (footB != null) footB.t.rotation = R * fq * footB.rel;
        }

        static Quaternion E(Vector3 euler) => Quaternion.Euler(euler.x, euler.y, euler.z);

        static float LegExtent(B up, B low, Vector3 du, Vector3 dl)
        {
            if (up == null || low == null) return 0f;
            return up.len * Mathf.Max(0f, -du.normalized.y) + low.len * Mathf.Max(0f, -dl.normalized.y);
        }

        void LimbRot(Quaternion R, B b, Vector3 d)
        {
            if (b == null) return;
            Vector3 v = new Vector3(d.x * b.side, d.y, d.z);
            if (v.sqrMagnitude < 1e-6f) return;
            b.t.rotation = R * Quaternion.FromToRotation(b.dir, v.normalized) * b.rel;
        }
    }
}
