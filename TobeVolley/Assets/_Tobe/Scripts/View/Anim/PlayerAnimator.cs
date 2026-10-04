// Per-character animation driver. Pipeline every frame (LateUpdate, before VRM spring bones):
//   1. base layer   : baked mocap muscles (MotionLibrary) blended by speed / air / landing / full-body actions
//                     -> HumanPoseHandler.SetHumanPose (works with ControlRigGenerationOption.None)
//   2. action layer : volleyball key poses (bump, set, spike, block, serve...) authored as bone-direction overrides on
//                     arms / legs / torso with anticipation -> contact -> follow-through timing (driven by poseT)
//   3. secondary    : lean into acceleration, breathing, head tracks the ball, blink / expressions, eyes via VRM LookAt
//   4. foot plant   : the lowest body point is pinned to the ground plane (the simulation owns the root position)
//
// Execution order: this script runs at -50, Vrm10Instance.LateUpdate at 11000 and FastSpringBoneService at 11010, so hair /
// cloth jiggle simulates on top of the pose we just wrote. The model's Animator stays disabled (we are the only writer).
using System;
using UnityEngine;
using UniVRM10;

namespace Tobe.View
{
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerAnimator : MonoBehaviour
    {
        // ------------------------------------------------------------------ inputs (PlayerView.Tick)
        PoseId pose; float poseT; bool air; Vector3 worldVel; Vector3 localVel; float visYaw;

        public void Feed(in PlayerSnap s, Vector3 localVelocity, float visualYaw)
        {
            pose = s.pose; poseT = s.poseT; air = s.air; worldVel = s.vel; localVel = localVelocity; visYaw = visualYaw;
        }

        /// <summary>True when the model is a valid humanoid and the animator did not hit repeated errors.</summary>
        public bool Usable => initialised && !failed;
        public static bool CanAnimate(GameObject model)
        {
            if (model == null) return false;
            var a = model.GetComponentInChildren<Animator>();
            return a != null && a.isHuman && a.avatar != null && a.avatar.isValid;
        }

        // ------------------------------------------------------------------ rig
        sealed class B
        {
            public Transform t;
            public Quaternion rel = Quaternion.identity;   // rest rotation relative to the root
            public Vector3 dir = Vector3.down;             // rest direction to the child (root space)
            public float side = 1f;                        // +1/-1: outward direction sign in root x
        }

        Transform root;
        Animator anim;
        HumanPoseHandler handler;
        HumanPose hp;
        PoseAccum acc;
        Vrm10Instance vrm;
        B hips, spine, chest, upperChest, neck, head, uaR, laR, uaL, laL, ulR, llR, ulL, llL;
        Transform[] groundRefs;
        float restRefY;
        bool initialised, failed;
        int errCount;
        float seed;

        // ------------------------------------------------------------------ clips (resolved when motions are loaded)
        MotionClip cIdle, cReady, cWalk, cRun, cSprint, cSide, cJumpV, cJumpA, cJumpB, cJumpR, cLand, cDive, cCelebrate, cSad;
        MotionClip[] movers = new MotionClip[0];
        bool clipsResolved;

        // ------------------------------------------------------------------ state
        PoseId lastPose = (PoseId)255;
        Vector2 smVel, prevSmVel, smAcc;
        float speed, stanceW, phase, sidePhase, tIdle, tReady;
        bool prevAir, hadAir;
        float blockW, airW, airU, maxVy, lastVy, landT = 99f, landImpact;
        MotionClip airClip;
        float fullW, fbT;
        MotionClip fbClip;
        float prevYaw, yawRate;
        float leanP, leanR;
        float lookYaw, lookPitch, lookW = 1f;
        float blinkTimer, blinkT = -1f;
        readonly float[] ex = new float[5]; // happy, sad, angry, surprised, relaxed
        Ov from = new Ov(), tgt = new Ov(), cur = new Ov();
        float blendT, blendDur = 0.15f;
        bool firstFrame = true;

        // ================================================================== init
        /// <summary>Call right after the model is instantiated (bones still in bind pose). Returns false when the model is not a usable humanoid.</summary>
        public bool Init(GameObject model)
        {
            initialised = false; failed = false;
            if (model == null) return false;
            anim = model.GetComponent<Animator>();
            if (anim == null) anim = model.GetComponentInChildren<Animator>();
            if (anim == null || !anim.isHuman || anim.avatar == null || !anim.avatar.isValid) return false;
            MotionLibrary.EnsureStarted();
            try
            {
                root = anim.transform;
                seed = UnityEngine.Random.value * 30f;
                handler?.Dispose();
                handler = new HumanPoseHandler(anim.avatar, root);   // UniVRM puts a human Avatar on the root Animator (Vrm10Importer)
                hp = new HumanPose { muscles = new float[MotionLibrary.MC] };
                acc = new PoseAccum();
                vrm = model.GetComponent<Vrm10Instance>();
                if (vrm == null) vrm = model.GetComponentInChildren<Vrm10Instance>();

                var tHips = anim.GetBoneTransform(HumanBodyBones.Hips);
                var tHead = anim.GetBoneTransform(HumanBodyBones.Head);
                if (tHips == null || tHead == null) return false;
                var tSpine = Bone(HumanBodyBones.Spine); var tChest = Bone(HumanBodyBones.Chest);
                var tUpper = Bone(HumanBodyBones.UpperChest); var tNeck = Bone(HumanBodyBones.Neck);

                hips = Make(tHips, tSpine ?? tHead);
                spine = tSpine != null ? Make(tSpine, tChest ?? tNeck ?? tHead) : null;
                chest = tChest != null ? Make(tChest, tUpper ?? tNeck ?? tHead) : null;
                upperChest = tUpper != null ? Make(tUpper, tNeck ?? tHead) : null;
                neck = tNeck != null ? Make(tNeck, tHead) : null;
                head = Make(tHead, null);
                uaR = Limb(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm); laR = Limb(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
                uaL = Limb(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm); laL = Limb(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
                ulR = Limb(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg); llR = Limb(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
                ulL = Limb(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg); llL = Limb(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
                foreach (var b in new[] { uaR, laR, uaL, laL, ulR, llR, ulL, llL })
                    if (b != null) b.side = root.InverseTransformPoint(b.t.position).x - root.InverseTransformPoint(tHips.position).x >= 0f ? 1f : -1f;

                // ground reference points: whichever of these is lowest touches the floor (feet standing, hands/head when diving)
                var refs = new System.Collections.Generic.List<Transform>();
                foreach (var hb in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes,
                                           HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.Head, HumanBodyBones.Hips })
                { var t = Bone(hb); if (t != null) refs.Add(t); }
                groundRefs = refs.ToArray();
                restRefY = LowestLocalY();

                anim.enabled = false;   // we own the bones; a disabled Animator still lets HumanPoseHandler work
                initialised = true;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Tobe] PlayerAnimator init failed: " + e.Message);
                return false;
            }
        }

        Transform Bone(HumanBodyBones hb) => anim.GetBoneTransform(hb);
        B Limb(HumanBodyBones a, HumanBodyBones c) { var t = Bone(a); var ch = Bone(c); return t != null && ch != null ? Make(t, ch) : null; }
        B Make(Transform t, Transform child)
        {
            var b = new B { t = t, rel = Quaternion.Inverse(root.rotation) * t.rotation };
            if (child != null)
            {
                Vector3 d = root.InverseTransformDirection(child.position - t.position);
                if (d.sqrMagnitude > 1e-10f) b.dir = d.normalized;
            }
            return b;
        }

        float LowestLocalY()
        {
            float m = float.MaxValue;
            for (int i = 0; i < groundRefs.Length; i++) m = Mathf.Min(m, root.InverseTransformPoint(groundRefs[i].position).y);
            return m == float.MaxValue ? 0f : m;
        }

        /// <summary>Gives the root transform back untouched (nothing to restore: we never move the root).</summary>
        void OnEnable()
        {
            ResolveClips();
            firstFrame = true; lastPose = (PoseId)255; hadAir = false; prevAir = false; airW = 0f; fullW = 0f; landT = 99f;
        }

        void OnDestroy() { handler?.Dispose(); handler = null; }

        void ResolveClips()
        {
            cIdle = MotionLibrary.Get("idle"); cReady = MotionLibrary.Get("ready");
            if (cIdle == null) cIdle = cReady; if (cReady == null) cReady = cIdle;
            cWalk = MotionLibrary.Get("walk"); cRun = MotionLibrary.Get("run"); cSprint = MotionLibrary.Get("sprint");
            cSide = MotionLibrary.Get("sidestep");
            cJumpV = MotionLibrary.Get("jump_vertical"); cJumpA = MotionLibrary.Get("jump_approach"); cJumpB = MotionLibrary.Get("jump_block"); cJumpR = MotionLibrary.Get("jump_run"); cLand = MotionLibrary.Get("land");
            cDive = MotionLibrary.Get("dive"); cCelebrate = MotionLibrary.Get("celebrate"); cSad = MotionLibrary.Get("sad");
            var list = new System.Collections.Generic.List<MotionClip>();
            foreach (var c in new[] { cWalk, cRun, cSprint }) if (c != null && c.speed > 0.4f && c.loop) list.Add(c);
            list.Sort((a, b) => a.speed.CompareTo(b.speed));
            movers = list.ToArray();
            clipsResolved = true;
        }

        // ================================================================== frame
        void LateUpdate()
        {
            if (!initialised || failed || root == null || handler == null) return;
            if (!clipsResolved) ResolveClips();
            try { Step(); errCount = 0; }
            catch (Exception e)
            {
                if (++errCount >= 3) { failed = true; Debug.LogWarning("[Tobe] PlayerAnimator disabled after errors: " + e); }
            }
        }

        void Step()
        {
            float dt = Mathf.Clamp(Time.deltaTime, 0f, 0.1f);
            float tm = Time.time + seed;

            if (pose != lastPose) OnPoseChange();
            UpdateKinematics(dt);

            // ---- air / landing bookkeeping
            if (air && !prevAir) { airW = Mathf.Max(airW, 0f); maxVy = Mathf.Max(worldVel.y, 3.5f); airU = 0f; hadAir = true; PickAirClip(); }
            if (air) { maxVy = Mathf.Max(maxVy, worldVel.y); lastVy = worldVel.y; }
            if (!air && prevAir && hadAir) { landT = 0f; landImpact = Mathf.Clamp01(Mathf.Abs(lastVy) / 7f); }
            prevAir = air;
            landT += dt;
            airW = Mathf.MoveTowards(airW, air ? 1f : 0f, dt / 0.07f);

            // ---- full-body clip layer (dive / celebrate / sad)
            MotionClip want = null; float rate = 1f;
            switch (pose)
            {
                case PoseId.Dive: want = cDive; if (want != null) rate = Mathf.Clamp(want.duration / 0.9f, 1f, 2.2f); break;
                case PoseId.Celebrate: want = cCelebrate; break;
                case PoseId.Sad: want = cSad; if (want != null) rate = Mathf.Clamp(want.duration / 1.4f, 1f, 2f); break;
            }
            if (want != null) { if (fbClip != want) { fbClip = want; } fbT = poseT * rate; fullW = Mathf.MoveTowards(fullW, 1f, dt / 0.1f); }
            else { fbT += dt; fullW = Mathf.MoveTowards(fullW, 0f, dt / 0.18f); }
            if (fullW <= 0f && want == null) fbClip = null;
            if (fbClip == null) fullW = 0f;

            // ---- base muscle pose
            acc.Clear();
            float wRest = 1f - fullW;
            if (wRest > 1e-3f)
            {
                float wAir = airW * wRest, wGnd = wRest - wAir;
                if (wGnd > 1e-3f) AddGround(wGnd, dt);
                if (wAir > 1e-3f) AddAir(wAir, dt);
            }
            else AdvanceClocks(dt);
            if (fbClip != null && fullW > 1e-3f) acc.Add(fbClip, fbT * (fbClip.loop ? 1f : 1f), fullW);
            acc.Finish(hp.muscles, out Vector3 bpos, out Quaternion brot);

            // ---- action layer
            EvalOverrides(dt, tm);
            hp.bodyPosition = bpos;
            hp.bodyRotation = cur.pitch != 0f ? Quaternion.Euler(cur.pitch, 0f, 0f) * brot : brot;
            handler.SetHumanPose(ref hp);

            // ---- bones: torso deltas, leg / arm overrides, secondary motion, foot plant
            Quaternion R = root.rotation;
            ApplyTorso(R, tm, dt);
            ApplyLegs(R);
            ApplyArms(R);
            Plant();
            try { UpdateFace(dt); } catch (Exception) { /* cosmetic only */ }
            firstFrame = false;
        }

        void OnPoseChange()
        {
            from.Set(cur);
            blendT = 0f;
            blendDur = BlendDur(pose);
            lastPose = pose;
        }

        static float BlendDur(PoseId p)
        {
            switch (p)
            {
                case PoseId.Spike: case PoseId.ServeHit: return 0.08f;
                case PoseId.Block: case PoseId.Dive: case PoseId.Stun: return 0.1f;
                case PoseId.SpikeWind: case PoseId.Bump: case PoseId.Set: return 0.12f;
                case PoseId.Celebrate: return 0.15f;
                default: return 0.2f;
            }
        }

        // ================================================================== kinematics
        void UpdateKinematics(float dt)
        {
            Vector2 lv = new Vector2(localVel.x, localVel.z);
            float k = dt > 1e-5f ? 1f - Mathf.Exp(-dt / 0.08f) : 1f;
            if (firstFrame) { smVel = lv; prevSmVel = lv; }
            smVel = Vector2.Lerp(smVel, lv, k);
            if (dt > 1e-5f)
            {
                Vector2 a = (smVel - prevSmVel) / dt;
                smAcc = Vector2.Lerp(smAcc, Vector2.ClampMagnitude(a, 25f), 1f - Mathf.Exp(-dt / 0.12f));
            }
            prevSmVel = smVel;
            speed = smVel.magnitude;

            float yr = dt > 1e-5f ? Mathf.DeltaAngle(prevYaw, visYaw) / dt : 0f;
            if (firstFrame) yr = 0f;
            prevYaw = visYaw;
            yawRate = Mathf.Lerp(yawRate, Mathf.Clamp(yr, -720f, 720f), dt > 1e-5f ? 1f - Mathf.Exp(-dt / 0.1f) : 1f);

            // lean into acceleration (forward accel -> pitch forward, lateral accel / turning -> roll toward the inside)
            float tp = Mathf.Clamp(smAcc.y * 1.3f, -9f, 11f);
            float tr = -Mathf.Clamp(smAcc.x * 1.3f + yawRate * speed * 0.012f, -9f, 9f);
            float lk = dt > 1e-5f ? 1f - Mathf.Exp(-dt / 0.12f) : 1f;
            leanP = Mathf.Lerp(leanP, tp, lk); leanR = Mathf.Lerp(leanR, tr, lk);

            // stance: relaxed idle <-> ready crouch
            float stTarget = (pose == PoseId.Idle || pose == PoseId.Celebrate || pose == PoseId.Sad) ? 0f : 1f;
            stanceW = Mathf.MoveTowards(stanceW, stTarget, dt / 0.15f);
        }

        void AdvanceClocks(float dt) { tIdle += dt; tReady += dt; }

        // ================================================================== base layer: ground locomotion
        void AddGround(float w, float dt)
        {
            // landing envelope (additive crouch): either the mocap "land" clip or the ready crouch
            float landDur = cLand != null ? Mathf.Min(cLand.duration, 0.6f) : 0.35f;
            float landW = 0f;
            if (landT < landDur)
            {
                float u = landT / landDur;
                float env = u < 0.15f ? u / 0.15f : 1f - Smooth((u - 0.15f) / 0.85f);
                landW = env * Mathf.Lerp(0.5f, 1f, landImpact);
            }
            AddLoco(w * (1f - landW), dt);
            if (landW > 1e-3f)
            {
                if (cLand != null) acc.Add(cLand, landT, w * landW);
                else acc.Add(cReady ?? cIdle, tReady, w * landW);
            }
        }

        void AddLoco(float w, float dt)
        {
            tIdle += dt; tReady += dt;
            float sp = speed;

            // lateral-ness -> sidestep clip
            float sideW = 0f;
            if (cSide != null && sp > 0.3f)
                sideW = Smooth(Mathf.Clamp01((Mathf.Abs(smVel.x) / sp - 0.55f) / 0.3f)) * Smooth(Mathf.Clamp01((sp - 0.3f) / 0.8f))
                        * (1f - Smooth((Mathf.Abs(smVel.x) - 1.8f) / 1.5f));   // fast lateral runs use the run cycle (sidestep clip is slow)
            float wLoco = w * (1f - sideW);

            // mover weights between anchors (walk / run / sprint) by planar speed
            int n = movers.Length;
            float wStat = 1f; int i0 = -1, i1 = -1; float f = 0f;
            if (n > 0)
            {
                if (sp <= movers[0].speed) { f = Smooth(sp / movers[0].speed); i0 = 0; wStat = 1f - f; }
                else
                {
                    int i = 0;
                    while (i + 1 < n && sp > movers[i + 1].speed) i++;
                    if (i + 1 >= n) { i0 = n - 1; f = 1f; wStat = 0f; }
                    else { i0 = i; i1 = i + 1; f = Smooth((sp - movers[i].speed) / (movers[i + 1].speed - movers[i].speed)); wStat = 0f; }
                }
            }

            // stationary: idle <-> ready
            if (wStat > 1e-3f)
            {
                float ws = wLoco * wStat;
                if (cIdle == cReady) acc.Add(cIdle, tIdle, ws);
                else { acc.Add(cIdle, tIdle, ws * (1f - stanceW)); acc.Add(cReady, tReady, ws * stanceW); }
            }

            // movers share one normalised phase (all stride cycles start at "left leg forward") -> clean blends, no foot sliding
            if (n > 0)
            {
                float rate;
                if (i1 < 0)
                {
                    var c = movers[i0];
                    rate = Mathf.Clamp(sp / c.speed, 0.55f, 1.6f) / c.duration;
                    acc.Add(c, phase * c.duration, wLoco * f);
                    if (i0 == 0 && wStat <= 1e-3f) { }
                }
                else
                {
                    var a = movers[i0]; var b = movers[i1];
                    float ra = Mathf.Clamp(sp / a.speed, 0.55f, 1.6f) / a.duration, rb = Mathf.Clamp(sp / b.speed, 0.55f, 1.6f) / b.duration;
                    rate = Mathf.Lerp(ra, rb, f);
                    acc.Add(a, phase * a.duration, wLoco * (1f - f));
                    acc.Add(b, phase * b.duration, wLoco * f);
                }
                float dirSign = smVel.y < -0.3f && Mathf.Abs(smVel.y) > Mathf.Abs(smVel.x) ? -1f : 1f;   // backpedal plays the cycle in reverse
                phase = Mathf.Repeat(phase + dirSign * rate * dt, 1f);
            }
            else if (wLoco > 0f && wStat < 1f) { }

            if (sideW > 1e-3f)
            {
                float rate = Mathf.Clamp(Mathf.Abs(smVel.x) / Mathf.Max(0.3f, cSide.speed), 0.55f, 1.7f) / cSide.duration;
                bool mirror = Mathf.Abs(cSide.rootVel.x) > 0.1f && Mathf.Sign(smVel.x) != Mathf.Sign(cSide.rootVel.x);
                acc.Add(cSide, sidePhase * cSide.duration, w * sideW, mirror);
                sidePhase = Mathf.Repeat(sidePhase + rate * dt, 1f);
            }
        }

        // ================================================================== base layer: air
        void PickAirClip()
        {
            float sp = new Vector2(worldVel.x, worldVel.z).magnitude;
            var appr = cJumpA ?? cJumpR;
            airClip = (appr != null && (sp > 2.2f || cJumpV == null)) ? appr : (cJumpV ?? appr);
            if (airClip == null) airClip = cJumpB;
        }

        void AddAir(float w, float dt)
        {
            AdvanceClocks(dt);
            if (airClip == null) { PickAirClip(); }
            if (airClip == null)
            {   // no jump mocap: ready crouch body + procedural leg tuck (EvalOverrides)
                acc.Add(cReady ?? cIdle, tReady, w);
                return;
            }
            // vertical velocity -> jump phase: 0 = leaving the ground, 0.5 = apex, 1 = touching down
            float vy = worldVel.y, m = Mathf.Max(maxVy, 3.5f);
            float u = vy >= 0f ? 0.5f * (1f - Mathf.Clamp01(vy / m)) : 0.5f + 0.5f * Mathf.Clamp01(-vy / m);
            airU = dt > 1e-5f ? Mathf.Lerp(airU, u, 1f - Mathf.Exp(-dt * 24f)) : u;
            float blockTarget = (pose == PoseId.Block && cJumpB != null) ? 1f : 0f;
            blockW = Mathf.MoveTowards(blockW, blockTarget, dt / 0.1f);
            if (blockW < 0.999f) acc.Add(airClip, JumpTime(airClip), w * (1f - blockW));
            if (blockW > 0.001f) acc.Add(cJumpB, JumpTime(cJumpB), w * blockW);   // basketball jump-shot: both arms overhead
        }

        float JumpTime(MotionClip c)
        {
            float fr = airU < 0.5f ? Mathf.Lerp(c.launch, c.apex, airU * 2f) : Mathf.Lerp(c.apex, c.touch, (airU - 0.5f) * 2f);
            return fr / c.fps;
        }

        // ================================================================== action layer
        sealed class Ov
        {
            static readonly Vector3 Hang = new Vector3(0.12f, -1f, 0.05f);
            public Vector3 uR = Hang, lR = Hang, uL = Hang, lL = Hang;   // arm directions in the torso frame (x outward, y up, z forward)
            public float wR = 1f, wL = 1f;                                // arm override weights
            public Vector3 hips, spine, chest, neck, head;               // euler deltas (deg): x pitch fwd+, y yaw right+, z roll left+
            public float legW;
            public Vector3 gUR = Hang, gLR = Hang, gUL = Hang, gLL = Hang; // leg directions in the hips frame
            public float pitch, lift;                                     // whole-body pitch (dive) / extra ground clearance (hop)

            public static Ov Neutral() { var o = new Ov(); o.wR = o.wL = 0f; return o; }
            public void SetNeutral() { Set(NeutralRef); }
            static readonly Ov NeutralRef = Neutral();

            public void Set(Ov o)
            {
                uR = o.uR; lR = o.lR; uL = o.uL; lL = o.lL; wR = o.wR; wL = o.wL;
                hips = o.hips; spine = o.spine; chest = o.chest; neck = o.neck; head = o.head;
                legW = o.legW; gUR = o.gUR; gLR = o.gLR; gUL = o.gUL; gLL = o.gLL; pitch = o.pitch; lift = o.lift;
            }

            static Vector3 Dir(Vector3 a, float wa, Vector3 b, float wb, float k)
            {
                if (wa < 0.02f) a = b; else if (wb < 0.02f) b = a;
                return Vector3.Slerp(a.normalized, b.normalized, k);
            }

            public static void Lerp(Ov d, Ov a, Ov b, float k)
            {
                d.uR = Dir(a.uR, a.wR, b.uR, b.wR, k); d.lR = Dir(a.lR, a.wR, b.lR, b.wR, k);
                d.uL = Dir(a.uL, a.wL, b.uL, b.wL, k); d.lL = Dir(a.lL, a.wL, b.lL, b.wL, k);
                d.wR = Mathf.Lerp(a.wR, b.wR, k); d.wL = Mathf.Lerp(a.wL, b.wL, k);
                d.hips = Vector3.Lerp(a.hips, b.hips, k); d.spine = Vector3.Lerp(a.spine, b.spine, k); d.chest = Vector3.Lerp(a.chest, b.chest, k);
                d.neck = Vector3.Lerp(a.neck, b.neck, k); d.head = Vector3.Lerp(a.head, b.head, k);
                d.legW = Mathf.Lerp(a.legW, b.legW, k);
                d.gUR = Dir(a.gUR, a.legW, b.gUR, b.legW, k); d.gLR = Dir(a.gLR, a.legW, b.gLR, b.legW, k);
                d.gUL = Dir(a.gUL, a.legW, b.gUL, b.legW, k); d.gLL = Dir(a.gLL, a.legW, b.gLL, b.legW, k);
                d.pitch = Mathf.Lerp(a.pitch, b.pitch, k); d.lift = Mathf.Lerp(a.lift, b.lift, k);
            }
        }

        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        // ---- authored key poses (right arm = hitting arm). Arm directions are in the TORSO frame so they follow arch / lean.
        static readonly Vector3 LegHang = V(0.06f, -1f, 0f);
        // Spike: wind-up (swing back/down) -> arms drive up -> cock (elbow high & back, chest arched, left arm up) -> whip -> follow-through
        static readonly Ov SW0 = new Ov { uR = V(0.25f, -0.5f, -0.6f), lR = V(0.1f, -0.7f, -0.5f), uL = V(0.25f, -0.5f, -0.6f), lL = V(0.1f, -0.7f, -0.5f), spine = V(12, 0, 0), chest = V(8, 0, 0) };
        static readonly Ov SW1 = new Ov { uR = V(0.5f, 0.75f, 0.35f), lR = V(0.3f, 0.8f, 0.3f), uL = V(0.3f, 1f, 0.3f), lL = V(0.15f, 1f, 0.4f), spine = V(-4, 0, 0), chest = V(-6, 0, 0) };
        static readonly Ov SW2 = new Ov
        {
            uR = V(0.55f, 0.6f, -0.5f), lR = V(0.15f, 0.55f, -0.8f), uL = V(0.35f, 0.95f, 0.35f), lL = V(0.15f, 1f, 0.4f),
            spine = V(-12, 0, 0), chest = V(-14, 22, 0), head = V(-6, 0, 0), legW = 0.55f,
            gUR = V(0.06f, -0.95f, -0.25f), gLR = V(0.03f, -0.5f, -0.85f), gUL = V(0.06f, -0.95f, -0.2f), gLL = V(0.03f, -0.6f, -0.8f)
        };
        static readonly Ov SP1 = new Ov
        {
            uR = V(0.12f, 0.95f, 0.3f), lR = V(0.06f, 1f, 0.15f), uL = V(0.45f, -0.3f, 0.4f), lL = V(0.2f, -0.8f, 0.3f),
            spine = V(6, 0, 0), chest = V(8, -10, 0), legW = 0.4f,
            gUR = V(0.06f, -0.95f, -0.1f), gLR = V(0.03f, -0.8f, -0.55f), gUL = V(0.06f, -0.95f, -0.1f), gLL = V(0.03f, -0.8f, -0.55f)
        };
        static readonly Ov SP2 = new Ov
        {
            uR = V(0.3f, -0.2f, 0.85f), lR = V(0.15f, -0.6f, 0.8f), uL = V(0.5f, -0.5f, 0.2f), lL = V(0.25f, -0.9f, 0.1f),
            spine = V(14, 0, 0), chest = V(24, -22, 0), head = V(6, 0, 0), legW = 0.5f,
            gUR = V(0.06f, -0.8f, 0.55f), gLR = V(0.03f, -0.9f, -0.4f), gUL = V(0.06f, -0.8f, 0.5f), gLL = V(0.03f, -0.9f, -0.4f)
        };
        static readonly Ov SP3 = new Ov { uR = V(0.3f, -0.9f, 0.3f), lR = V(0.1f, -0.95f, 0.3f), uL = V(0.3f, -0.9f, 0.2f), lL = V(0.1f, -0.95f, 0.2f), spine = V(8, 0, 0), chest = V(10, 0, 0) };
        // Block: hands up fast, straight arms, hands spread, then press over the net
        static readonly Ov BL0 = new Ov { uR = V(0.3f, 0.4f, 0.7f), lR = V(0.2f, 0.4f, 0.8f), uL = V(0.3f, 0.4f, 0.7f), lL = V(0.2f, 0.4f, 0.8f), spine = V(6, 0, 0) };
        static readonly Ov BL1 = new Ov { uR = V(0.2f, 0.98f, 0.1f), lR = V(0.18f, 1f, 0.12f), uL = V(0.2f, 0.98f, 0.1f), lL = V(0.18f, 1f, 0.12f), spine = V(4, 0, 0), chest = V(-2, 0, 0), head = V(-10, 0, 0) };
        static readonly Ov BL2 = new Ov { uR = V(0.18f, 0.9f, 0.4f), lR = V(0.15f, 0.88f, 0.45f), uL = V(0.18f, 0.9f, 0.4f), lL = V(0.15f, 0.88f, 0.45f), spine = V(10, 0, 0), chest = V(10, 0, 0), head = V(-6, 0, 0) };
        // Bump (forearm pass): arms low, hands joined, platform swings up through the ball
        static readonly Ov BP0 = new Ov { uR = V(0.15f, -0.9f, 0.3f), lR = V(0.05f, -0.8f, 0.5f), uL = V(0.15f, -0.9f, 0.3f), lL = V(0.05f, -0.8f, 0.5f), spine = V(18, 0, 0), chest = V(14, 0, 0) };
        static readonly Ov BP1 = new Ov { uR = V(0.0f, -0.6f, 0.8f), lR = V(-0.2f, -0.45f, 0.85f), uL = V(0.0f, -0.6f, 0.8f), lL = V(-0.2f, -0.45f, 0.85f), spine = V(24, 0, 0), chest = V(16, 0, 0), head = V(-8, 0, 0) };
        static readonly Ov BP2 = new Ov { uR = V(0.0f, -0.3f, 0.95f), lR = V(-0.1f, -0.2f, 0.95f), uL = V(0.0f, -0.3f, 0.95f), lL = V(-0.1f, -0.2f, 0.95f), spine = V(14, 0, 0), chest = V(10, 0, 0) };
        // Set (overhead pass): hands to chest -> above forehead -> push through
        static readonly Ov ST0 = new Ov { uR = V(0.35f, -0.3f, 0.6f), lR = V(0f, 0.3f, 0.9f), uL = V(0.35f, -0.3f, 0.6f), lL = V(0f, 0.3f, 0.9f), spine = V(4, 0, 0) };
        static readonly Ov ST1 = new Ov { uR = V(0.55f, 0.78f, 0.3f), lR = V(-0.35f, 0.85f, 0.45f), uL = V(0.55f, 0.78f, 0.3f), lL = V(-0.35f, 0.85f, 0.45f), spine = V(-3, 0, 0), chest = V(-8, 0, 0), head = V(-8, 0, 0) };
        static readonly Ov ST2 = new Ov { uR = V(0.3f, 0.95f, 0.25f), lR = V(0.05f, 1f, 0.35f), uL = V(0.3f, 0.95f, 0.25f), lL = V(0.05f, 1f, 0.35f), spine = V(4, 0, 0), chest = V(4, 0, 0) };
        // Serve: toss arm lifts while the hitting arm draws back, then whip + follow-through (hit reuses spike keys)
        static readonly Ov SV0 = new Ov { uR = V(0.3f, -0.9f, 0.1f), lR = V(0.1f, -0.95f, 0.2f), uL = V(0.3f, -0.9f, 0.1f), lL = V(0.1f, -0.95f, 0.2f) };
        static readonly Ov SV1 = new Ov { uR = V(0.45f, -0.2f, -0.4f), lR = V(0.15f, 0.15f, -0.8f), uL = V(0.25f, 0.9f, 0.35f), lL = V(0.1f, 1f, 0.2f), spine = V(-5, 0, 0), chest = V(-6, 8, 0) };
        static readonly Ov SV2 = new Ov { uR = V(0.55f, 0.6f, -0.5f), lR = V(0.15f, 0.6f, -0.8f), uL = V(0.25f, 1f, 0.3f), lL = V(0.1f, 1f, 0.2f), spine = V(-10, 0, 0), chest = V(-14, 20, 0), head = V(-6, 0, 0) };
        static readonly Ov SH1 = new Ov { uR = V(0.12f, 0.95f, 0.3f), lR = V(0.06f, 1f, 0.15f), uL = V(0.4f, -0.4f, 0.3f), lL = V(0.2f, -0.8f, 0.3f), spine = V(4, 0, 0), chest = V(6, -8, 0) };
        static readonly Ov SH2 = new Ov { uR = V(0.3f, -0.3f, 0.85f), lR = V(0.12f, -0.7f, 0.7f), uL = V(0.4f, -0.7f, 0.2f), lL = V(0.2f, -0.9f, 0.2f), spine = V(10, 0, 0), chest = V(22, -20, 0) };
        static readonly Ov SH3 = new Ov { uR = V(0.3f, -0.9f, 0.3f), lR = V(0.1f, -0.95f, 0.3f), uL = V(0.3f, -0.9f, 0.1f), lL = V(0.1f, -0.95f, 0.2f), spine = V(6, 0, 0), chest = V(6, 0, 0) };

        // key sequence player: easeIn per key (0 smooth, 1 accelerate = whip, 2 decelerate)
        static void Seq(Ov dst, float t, Ov[] keys, float[] times, int[] ease)
        {
            int n = keys.Length;
            if (t <= times[0]) { dst.Set(keys[0]); return; }
            if (t >= times[n - 1]) { dst.Set(keys[n - 1]); return; }
            int i = 0;
            while (i + 1 < n - 1 && t >= times[i + 1]) i++;
            float k = (t - times[i]) / (times[i + 1] - times[i]);
            switch (ease[i + 1]) { case 1: k *= k; break; case 2: k = 1f - (1f - k) * (1f - k); break; default: k = Smooth(k); break; }
            Ov.Lerp(dst, keys[i], keys[i + 1], k);
        }

        static readonly Ov[] kWind = { SW0, SW1, SW2 };          static readonly float[] tWind = { 0f, 0.14f, 0.32f };      static readonly int[] eWind = { 0, 0, 0 };
        static readonly Ov[] kSpike = { SW2, SP1, SP2, SP3 };    static readonly float[] tSpike = { 0f, 0.06f, 0.17f, 0.4f }; static readonly int[] eSpike = { 0, 1, 2, 0 };
        static readonly Ov[] kBlock = { BL0, BL1, BL2 };         static readonly float[] tBlock = { 0f, 0.1f, 0.3f };       static readonly int[] eBlock = { 0, 2, 0 };
        static readonly Ov[] kBump = { BP0, BP1, BP2 };          static readonly float[] tBump = { 0f, 0.12f, 0.35f };      static readonly int[] eBump = { 0, 1, 0 };
        static readonly Ov[] kSet = { ST0, ST1, ST2 };           static readonly float[] tSet = { 0f, 0.1f, 0.22f };        static readonly int[] eSet = { 0, 0, 1 };
        static readonly Ov[] kToss = { SV0, SV1, SV2 };          static readonly float[] tToss = { 0f, 0.4f, 0.8f };        static readonly int[] eToss = { 0, 0, 0 };
        static readonly Ov[] kHit = { SV2, SH1, SH2, SH3 };      static readonly float[] tHit = { 0f, 0.07f, 0.2f, 0.45f }; static readonly int[] eHit = { 0, 1, 2, 0 };

        void EvalOverrides(float dt, float tm)
        {
            float pt = Mathf.Max(0f, poseT);
            tgt.SetNeutral();
            switch (pose)
            {
                case PoseId.SpikeWind: Seq(tgt, pt, kWind, tWind, eWind); break;
                case PoseId.Spike: Seq(tgt, pt, kSpike, tSpike, eSpike); break;
                case PoseId.Block: Seq(tgt, pt, kBlock, tBlock, eBlock); break;
                case PoseId.Bump: Seq(tgt, pt, kBump, tBump, eBump); break;
                case PoseId.Set: Seq(tgt, pt, kSet, tSet, eSet); break;
                case PoseId.ServeToss: Seq(tgt, pt, kToss, tToss, eToss); break;
                case PoseId.ServeHit: Seq(tgt, pt, kHit, tHit, eHit); break;
                case PoseId.Dive:
                    if (cDive == null)
                    {
                        tgt.pitch = 78f; tgt.wR = tgt.wL = 1f;
                        tgt.uR = tgt.uL = V(0.12f, 1f, 0.2f); tgt.lR = tgt.lL = V(0.08f, 1f, 0.25f);
                        tgt.legW = 1f; tgt.gUR = V(0.1f, -1f, -0.1f); tgt.gLR = V(0.08f, -0.75f, -0.7f); tgt.gUL = V(0.1f, -1f, 0.05f); tgt.gLL = V(0.05f, -1f, -0.2f);
                        tgt.neck = V(-15, 0, 0); tgt.head = V(-25, 0, 0);
                    }
                    break;
                case PoseId.Celebrate:
                    if (cCelebrate == null)
                    {
                        float hop = Mathf.Abs(Mathf.Sin(pt * 9f));
                        tgt.wR = tgt.wL = 1f; tgt.uR = tgt.uL = V(0.7f, 0.65f, 0.1f); tgt.lR = tgt.lL = V(0.15f, 1f, 0.15f);
                        tgt.legW = hop; tgt.gUR = tgt.gUL = V(0.05f, -0.95f, 0.35f * hop); tgt.gLR = tgt.gLL = V(0.02f, -0.9f, -0.45f * hop);
                        tgt.lift = hop * 0.14f; tgt.spine = V(-5, 0, 0); tgt.head = V(-10, 0, 0);
                    }
                    break;
                case PoseId.Sad:
                    if (cSad == null)
                    {
                        tgt.wR = tgt.wL = 1f; tgt.uR = tgt.uL = V(0.08f, -1f, 0.05f); tgt.lR = tgt.lL = V(0f, -1f, 0.1f);
                        tgt.spine = V(14, 0, 0); tgt.chest = V(10, 0, 0); tgt.neck = V(12, 0, 0); tgt.head = V(22, 0, 0);
                    }
                    break;
                case PoseId.Stun:
                    {
                        float s1 = Mathf.Sin(pt * 10f), s2 = Mathf.Sin(pt * 7f);
                        tgt.wR = tgt.wL = 0.8f;
                        tgt.uR = V(0.4f + 0.2f * s1, -1f, 0.3f * s2); tgt.lR = V(0.2f, -1f, 0.3f + 0.2f * Mathf.Sin(pt * 9f));
                        tgt.uL = V(0.4f - 0.2f * s1, -1f, -0.3f * s2); tgt.lL = V(0.2f, -1f, 0.3f - 0.2f * Mathf.Sin(pt * 9f));
                        tgt.spine = V(10, Mathf.Sin(pt * 9f) * 12f, Mathf.Sin(pt * 14f) * 10f); tgt.head = V(12, 0, Mathf.Sin(pt * 11f + 1f) * 14f);
                        break;
                    }
            }
            // airborne without jump mocap: tuck the legs procedurally
            if (air && airClip == null && tgt.legW < 0.5f && pose != PoseId.Dive)
            {
                tgt.legW = Mathf.Max(tgt.legW, 0.7f);
                tgt.gUR = tgt.gUL = V(0.08f, -0.9f, 0.4f); tgt.gLR = tgt.gLL = V(0.02f, -0.6f, -0.8f);
            }

            if (firstFrame) { cur.Set(tgt); from.Set(tgt); blendT = blendDur; }
            else
            {
                blendT += dt;
                float k = blendDur > 1e-4f ? Smooth(blendT / blendDur) : 1f;
                Ov.Lerp(cur, from, tgt, k);
            }
        }

        // ================================================================== applying overrides to bones
        static void RotW(Transform t, Quaternion R, Vector3 euler)
        {
            if (t == null || (euler.x == 0f && euler.y == 0f && euler.z == 0f)) return;
            t.rotation = R * Quaternion.Euler(euler) * Quaternion.Inverse(R) * t.rotation;
        }

        Transform headT => head != null ? head.t : null;

        void ApplyTorso(Quaternion R, float tm, float dt)
        {
            // secondary motion: breathing, sway, lean into acceleration, torso leads turns
            float br = Mathf.Sin(tm * 1.9f), sw = Mathf.Sin(tm * 0.8f);
            float turnLead = Mathf.Clamp(yawRate * 0.05f, -14f, 14f);
            Vector3 lean = new Vector3(leanP, 0f, leanR);

            // head tracks the ball (limited), the eyes finish the job through VRM LookAt (UpdateFace)
            Vector3 lookEul = Vector3.zero;
            ComputeLook(dt, out lookEul);

            if (hips != null) RotW(hips.t, R, cur.hips);
            if (spine != null) RotW(spine.t, R, cur.spine + lean * 0.45f + new Vector3(0f, 0f, sw * 1.0f));
            Vector3 ch = cur.chest + lean * 0.55f + new Vector3(br * 1.0f, turnLead, 0f);
            if (upperChest != null && chest != null) { RotW(chest.t, R, ch * 0.5f); RotW(upperChest.t, R, ch * 0.5f); }
            else if (chest != null) RotW(chest.t, R, ch);
            if (neck != null) RotW(neck.t, R, cur.neck + lookEul * 0.35f);
            RotW(headT, R, cur.head - (lean * 0.5f) + lookEul * 0.5f + new Vector3(br * 0.6f, Mathf.Sin(tm * 0.7f) * 1.5f, 0f));
        }

        void ComputeLook(float dt, out Vector3 eul)
        {
            eul = Vector3.zero;
            var ball = GameHub.View.ball;
            float wTarget = (pose == PoseId.Sad || pose == PoseId.Stun) ? 0f : pose == PoseId.Celebrate ? 0.25f : 1f;
            lookW = Mathf.MoveTowards(lookW, wTarget, dt / 0.25f);
            if (head == null || ball.pos == Vector3.zero) return;
            Vector3 l = root.InverseTransformDirection(ball.pos - head.t.position);
            float hd = Mathf.Sqrt(l.x * l.x + l.z * l.z);
            if (hd < 0.05f && Mathf.Abs(l.y) < 0.05f) return;
            float yaw = Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(-l.y, hd) * Mathf.Rad2Deg;     // + = look down
            float fade = 1f - Smooth((Mathf.Abs(yaw) - 90f) / 50f);  // ball behind us: stop turning the head
            float ty = Mathf.Clamp(yaw, -55f, 55f) * fade * lookW;
            float tp = Mathf.Clamp(pitch, -35f, 30f) * Mathf.Lerp(0.6f, 1f, fade) * lookW;
            float k = dt > 1e-5f ? 1f - Mathf.Exp(-dt * 7f) : 1f;
            lookYaw = Mathf.Lerp(lookYaw, ty, k); lookPitch = Mathf.Lerp(lookPitch, tp, k);
            eul = new Vector3(lookPitch * 0.8f, lookYaw * 0.8f, 0f);
        }

        void ApplyLegs(Quaternion R)
        {
            if (cur.legW < 0.01f || hips == null) return;
            Quaternion D = hips.t.rotation * Quaternion.Inverse(R * hips.rel);
            Limb(ulR, cur.gUR, cur.legW, D, R); Limb(llR, cur.gLR, cur.legW, D, R);
            Limb(ulL, cur.gUL, cur.legW, D, R); Limb(llL, cur.gLL, cur.legW, D, R);
        }

        void ApplyArms(Quaternion R)
        {
            if (cur.wR < 0.01f && cur.wL < 0.01f) return;
            B tb = upperChest ?? chest ?? spine ?? hips;
            Quaternion D = tb.t.rotation * Quaternion.Inverse(R * tb.rel);
            Limb(uaR, cur.uR, cur.wR, D, R); Limb(laR, cur.lR, cur.wR, D, R);
            Limb(uaL, cur.uL, cur.wL, D, R); Limb(laL, cur.lL, cur.wL, D, R);
        }

        /// <summary>Points a limb segment along dir (frame D, x mirrored for the left side), blended with the mocap pose by w.</summary>
        void Limb(B b, Vector3 dir, float w, Quaternion D, Quaternion R)
        {
            if (b == null || w < 0.01f) return;
            Vector3 v = new Vector3(dir.x * b.side, dir.y, dir.z);
            if (v.sqrMagnitude < 1e-6f) return;
            Quaternion target = D * R * Quaternion.FromToRotation(b.dir, v.normalized) * b.rel;
            b.t.rotation = w >= 0.999f ? target : Quaternion.Slerp(b.t.rotation, target, w);
        }

        // ================================================================== foot plant
        /// <summary>The simulation owns the root: slide the whole body vertically so the lowest ground reference sits on the floor.</summary>
        void Plant()
        {
            if (groundRefs == null || hips == null) return;
            float low = LowestLocalY();
            float dy = (restRefY + cur.lift) - low;
            dy = Mathf.Clamp(dy, -0.8f, 0.8f);
            if (Mathf.Abs(dy) > 1e-4f) hips.t.position += root.TransformVector(0f, dy, 0f);
        }

        // ================================================================== face: eyes, blink, expressions
        static readonly ExpressionKey kBlink = ExpressionKey.Blink, kHappy = ExpressionKey.Happy, kSad = ExpressionKey.Sad,
            kAngry = ExpressionKey.Angry, kSurprised = ExpressionKey.Surprised, kRelaxed = ExpressionKey.Relaxed;

        void UpdateFace(float dt)
        {
            if (vrm == null) return;
            var rt = vrm.Runtime;
            if (rt == null) return;

            // eyes follow the ball (head already turned part of the way; VRM clamps to the model's LookAt ranges)
            var ball = GameHub.View.ball;
            if (rt.LookAt != null && vrm.LookAtTargetType != UniVRM10.VRM10ObjectLookAt.LookAtTargetTypes.SpecifiedTransform && ball.pos != Vector3.zero && lookW > 0.05f)
            {
                var (yaw, pitch) = rt.LookAt.CalculateYawPitchFromLookAtPosition(ball.pos);
                rt.LookAt.SetYawPitchManually(Mathf.Clamp(yaw, -40f, 40f), Mathf.Clamp(pitch, -25f, 25f));
            }

            // expressions by pose
            float h = 0, s = 0, a = 0, su = 0, rl = 0;
            switch (pose)
            {
                case PoseId.Celebrate: h = 0.85f; break;
                case PoseId.Sad: s = 0.8f; break;
                case PoseId.Stun: su = 0.7f; break;
                case PoseId.Spike: case PoseId.SpikeWind: case PoseId.ServeHit: case PoseId.Block: a = 0.35f; break;
                case PoseId.Dive: a = 0.25f; break;
                case PoseId.Idle: rl = 0.15f; break;
            }
            float k = 1f - Mathf.Exp(-dt * 10f);
            ex[0] = Mathf.Lerp(ex[0], h, k); ex[1] = Mathf.Lerp(ex[1], s, k); ex[2] = Mathf.Lerp(ex[2], a, k);
            ex[3] = Mathf.Lerp(ex[3], su, k); ex[4] = Mathf.Lerp(ex[4], rl, k);

            // blink every 2.5-5 s
            blinkTimer -= dt;
            if (blinkT < 0f && blinkTimer <= 0f) { blinkT = 0f; blinkTimer = UnityEngine.Random.Range(2.5f, 5f); }
            float bw = 0f;
            if (blinkT >= 0f) { blinkT += dt; float u = blinkT / 0.16f; bw = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI); if (u >= 1f) blinkT = -1f; }

            var ev = rt.Expression;
            if (ev == null) return;
            ev.SetWeight(kHappy, ex[0]); ev.SetWeight(kSad, ex[1]); ev.SetWeight(kAngry, ex[2]);
            ev.SetWeight(kSurprised, ex[3]); ev.SetWeight(kRelaxed, ex[4]);
            ev.SetWeight(kBlink, Mathf.Max(bw, ex[1] * 0.3f));
        }
    }
}
