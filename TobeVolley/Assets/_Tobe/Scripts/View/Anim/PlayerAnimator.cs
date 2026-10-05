// Per-character animation driver. Pipeline every frame (LateUpdate, before VRM spring bones):
//   1. base layer   : baked muscles (MotionLibrary: Unity Humanoid clips from Resources/Anim > CMU mocap) blended by mode / speed / air / landing
//   2. volleyball   : procedural footwork (GaitSolver + two-bone leg IK) in a low ready stance - the body keeps FACING the ball / net and
//                     shuffles, back-pedals or takes short steps; it only turns and runs for long distances (LocoBrain)
//   3. action layer : receive platform / set hands / block hands, spike, serve ... as humanoid clips (upper body or full body, timed by
//                     poseT) or authored key poses (VolleyKeys) when there is no clip
//   4. secondary    : lean into acceleration, breathing, head tracks the ball, blink / expressions, eyes via VRM LookAt
//   5. foot plant   : lowest body point pinned to the floor through a critically damped spring (no snapping)
//
// Everything is time based (1 - exp(-k dt)) so it looks the same at 60 and 144 fps.
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
        PoseId pose; float poseT; bool air; Vector3 worldVel, worldPos; float visYaw; int myId = -1, myTeam;

        public void Feed(in PlayerSnap s, Vector3 localVelocity, float visualYaw)
        {
            pose = s.pose; poseT = s.poseT; air = s.air; worldVel = s.vel; worldPos = s.pos; visYaw = visualYaw; myId = s.id; myTeam = s.team;
        }

        /// <summary>World yaw (deg) the body is facing right now (differs from the view yaw while facing the ball during shuffles).</summary>
        public float BodyYaw => bodyYaw;
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

        B ftR, ftL;
        float restAnkleY, hipHalf;            // root-local, measured in the bind pose
        float sc = 1f, legLocal = 0.9f, l1w = 0.45f, l2w = 0.45f;   // current scale, leg length (local units), thigh / shin length (world)

        // ------------------------------------------------------------------ clips (resolved when motions are loaded)
        MotionClip cIdle, cReady, cWalk, cRun, cSprint, cSide, cJumpV, cJumpA, cJumpB, cJumpR, cLand, cDive, cCelebrate, cSad;
        MotionClip cShL, cShR, cBack, cBump, cSet, cSpike, cBlock, cServeF, cServeJ;
        MotionClip[] movers = new MotionClip[0];
        bool clipsResolved;
        bool clipStance, clipGait;            // a Humanoid "ready" / "shuffle" clip exists -> it replaces the procedural stance / footwork

        // ------------------------------------------------------------------ state
        PoseId lastPose = (PoseId)255; int lastClass = -1;
        // PlayerSnap.poseT is a hold COUNTDOWN in the simulation (and 0 for most poses), not an elapsed time: keep our own pose clock.
        float poseAge, prevPoseT; PoseId prevPoseId = (PoseId)255;
        readonly LocoBrain brain = new LocoBrain();
        readonly GaitSolver gait = new GaitSolver();
        Spring1 plant;
        Vector2 vwSm, vwSlow, accW, vBody;
        float speed, phase, rateSm = 1f, sidePhase, tIdle, tReady, gaitPhase;
        float gaitW, legIkW, bodyYaw, bodyOff, gaze, yawRate, prevBodyYaw;
        bool stanceCtx, forceRun;
        float recvW, setW, blockW2, approachW, approachMul = 1f;
        bool prevAir, hadAir;
        float blockW, airW, airLin, airU, maxVy, lastVy, landT = 99f, landImpact;
        MotionClip airClip;
        MotionClip actClip; float actT, actW; bool actFull;
        PoseAccum acc2;
        float[] tmpM;
        float leanP, leanR;
        float lookYaw, lookPitch, lookW = 1f;
        float blinkTimer, blinkT = -1f;
        readonly float[] ex = new float[5]; // happy, sad, angry, surprised, relaxed
        PoseOv from = new PoseOv(), tgt = new PoseOv(), cur = new PoseOv(), tmpA = new PoseOv(), tmpB = new PoseOv();
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

                var tFR = Bone(HumanBodyBones.RightFoot); var tFL = Bone(HumanBodyBones.LeftFoot);
                ftR = tFR != null ? Make(tFR, null) : null; ftL = tFL != null ? Make(tFL, null) : null;
                if (ulR != null && ulL != null)
                    hipHalf = Mathf.Abs(root.InverseTransformPoint(ulR.t.position).x - root.InverseTransformPoint(ulL.t.position).x) * 0.5f;
                if (tFR != null && tFL != null)
                    restAnkleY = 0.5f * (root.InverseTransformPoint(tFR.position).y + root.InverseTransformPoint(tFL.position).y);
                plant.Reset(); brain.Reset(); gait.Reset();
                acc2 = new PoseAccum(); tmpM = new float[MotionLibrary.MC];

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
            firstFrame = true; lastClass = -1; lastPose = (PoseId)255; prevPoseId = (PoseId)255; poseAge = 0f; hadAir = false; prevAir = false; airW = 0f; actW = 0f; actClip = null; landT = 99f;
            gaitW = 0f; legIkW = 0f; recvW = setW = blockW2 = approachW = 0f; approachMul = 1f;
            brain.Reset(); gait.Reset(); plant.Reset();
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
            cShL = MotionLibrary.Get("shuffle_left"); cShR = MotionLibrary.Get("shuffle_right"); cBack = MotionLibrary.Get("backpedal");
            cBump = MotionLibrary.Get("bump"); cSet = MotionLibrary.Get("set"); cSpike = MotionLibrary.Get("spike"); cBlock = MotionLibrary.Get("block");
            cServeF = MotionLibrary.Get("serve_float"); cServeJ = MotionLibrary.Get("serve_jump");
            var list = new System.Collections.Generic.List<MotionClip>();
            foreach (var c in new[] { cWalk, cRun, cSprint }) if (c != null && c.speed > 0.4f && c.loop) list.Add(c);
            list.Sort((a, b) => a.speed.CompareTo(b.speed));
            movers = list.ToArray();
            clipStance = cReady != null && cReady.humanoid;
            clipGait = (cShL != null && cShL.humanoid) || (cShR != null && cShR.humanoid);
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

        static int PoseClass(PoseId p) => (p == PoseId.Idle || p == PoseId.Ready || p == PoseId.Run) ? 0 : (int)p + 1;

        /// <summary>Витрина для разработки (меню Tobe/Dev): игрок с этим id проигрывает только указанный клип, без процедурных слоёв.</summary>
        public static int DevShowId = -1;
        public static string DevShowClip;
        public static float DevShowTime = -1f;   // < 0 = клип идёт сам; >= 0 = стоп-кадр в этой секунде
        float devT; bool devPose;

        bool DevShow(float dt)
        {
            if (myId != DevShowId || string.IsNullOrEmpty(DevShowClip)) return false;
            if (DevShowClip.StartsWith("pose:"))
            {   // игровая поза целиком (все слои), замороженная в момент DevShowTime от её начала; «pose:Spike:air» — как в прыжке
                var parts = DevShowClip.Split(':');
                if (System.Enum.TryParse(parts[1], out PoseId pid)) { pose = pid; if (parts.Length > 2) air = parts[2] == "air"; }
                devPose = true;
                return false;
            }
            devPose = false;
            var c = MotionLibrary.Get(DevShowClip);
            if (c == null) return false;
            devT = DevShowTime >= 0f ? DevShowTime : devT + dt;
            acc.Clear();
            acc.Add(c, devT, 1f);
            acc.Finish(hp.muscles, out Vector3 bpos, out Quaternion brot);
            hp.bodyPosition = bpos; hp.bodyRotation = brot;
            handler.SetHumanPose(ref hp);
            AntiCross(dt);
            Plant(dt);
            return true;
        }

        void Step()
        {
            float dt = Mathf.Clamp(Time.deltaTime, 0f, 0.1f);
            float tm = Time.time + seed;
            if (DevShow(dt)) return;

            RefreshMetrics();
            if (pose != prevPoseId || Mathf.Abs(poseT - prevPoseT) > 0.2f || firstFrame) poseAge = 0f; else poseAge += dt;   // (re)triggered
            if (devPose && DevShowTime >= 0f) { poseAge = DevShowTime; blendT = 99f; }   // витрина: стоп-кадр позы
            prevPoseId = pose; prevPoseT = poseT;
            UpdateContext(dt);
            int cls = PoseClass(pose);
            if (cls != lastClass) OnPoseChange(cls);
            UpdateKinematics(dt);

            // ---- air / landing bookkeeping
            if (air && !prevAir) { airW = Mathf.Max(airW, 0f); maxVy = Mathf.Max(worldVel.y, 3.5f); airU = 0f; hadAir = true; PickAirClip(); }
            if (air) { maxVy = Mathf.Max(maxVy, worldVel.y); lastVy = worldVel.y; }
            if (!air && prevAir && hadAir) { landT = 0f; landImpact = Mathf.Clamp01(Mathf.Abs(lastVy) / 7f); }
            prevAir = air;
            landT += dt;
            // взлёт: быстро в позу прыжка; приземление: клип прыжка доигрывает свою амортизацию и плавно (0,28 с) уходит в стойку
            airLin = Mathf.MoveTowards(airLin, air ? 1f : 0f, dt / (air ? 0.07f : 0.28f));
            airW = air ? airLin : AnimMath.Smooth01(airLin);

            // ---- clip action layer (dive / celebrate / sad / bump / set / spike / block / serve)
            SelectAction(dt);

            // ---- base muscle pose
            acc.Clear();
            if (actClip != null && actFull && actW > 0.999f) AdvanceClocks(dt);
            else
            {
                float wAir = airW, wGnd = 1f - airW;
                if (wGnd > 1e-3f) AddGround(wGnd, dt);
                if (wAir > 1e-3f) AddAir(wAir, dt);
            }
            acc.Finish(hp.muscles, out Vector3 bpos, out Quaternion brot);
            ComposeAction(ref bpos, ref brot);

            // ---- override layer (key poses, stance, torso)
            EvalOverrides(dt, tm);
            Quaternion Q = Quaternion.Euler(0f, bodyOff, 0f);             // the body may face the ball while the view yaw follows the velocity
            hp.bodyPosition = Q * bpos;
            hp.bodyRotation = Q * (cur.pitch != 0f ? Quaternion.Euler(cur.pitch, 0f, 0f) * brot : brot);
            handler.SetHumanPose(ref hp);

            // ---- bones: torso deltas, leg / arm overrides, procedural footwork, secondary motion, foot plant
            Quaternion Rb = root.rotation * Q;
            ApplyTorso(Rb, tm, dt);
            ApplyLegs(Rb);
            ApplyGait(Rb, Q, legIkW);
            ApplyArms(Rb);
            AntiCross(dt);
            Plant(dt);
            try { UpdateFace(dt); } catch (Exception) { /* cosmetic only */ }
            firstFrame = false;
        }

        void OnPoseChange(int cls)
        {
            from.Set(cur);
            blendT = 0f;
            blendDur = BlendDur(pose);
            lastPose = pose; lastClass = cls;
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

        // ================================================================== context: what is going on in the match
        void RefreshMetrics()
        {
            sc = Mathf.Max(0.01f, root.lossyScale.y);
            if (ulR == null || llR == null || ulL == null || llL == null || ftR == null || ftL == null) return;
            l1w = 0.5f * ((llR.t.position - ulR.t.position).magnitude + (llL.t.position - ulL.t.position).magnitude);
            l2w = 0.5f * ((ftR.t.position - llR.t.position).magnitude + (ftL.t.position - llL.t.position).magnitude);
            if (l1w < 1e-3f || l2w < 1e-3f) { l1w = 0.45f * sc; l2w = 0.45f * sc; }
            legLocal = (l1w + l2w) / sc;
        }

        void UpdateContext(float dt)
        {
            var v = GameHub.View;
            bool rally = v.phase == MatchPhase.Rally;
            bool recvServe = v.phase == MatchPhase.Serve && myTeam != v.servingTeam && myId != v.serverPlayerId;
            stanceCtx = (rally || recvServe) && !air;

            PlanSnap pl = default; bool mine = false;
            if (v.plans != null && myTeam >= 0 && myTeam < v.plans.Length) { pl = v.plans[myTeam]; mine = pl.kind != PlanKind.None && pl.playerId == myId; }
            float tl = mine ? pl.timeLeft : 99f;
            float rT = 0f, sT = 0f, aT = 0f, mulT = 1f;
            forceRun = false;
            if (mine && !air)
            {
                if (pl.kind == PlanKind.Receive) rT = AnimMath.Smooth01((0.85f - tl) / 0.3f);
                else if (pl.kind == PlanKind.Set) sT = AnimMath.Smooth01((0.85f - tl) / 0.3f);
                else if (pl.kind == PlanKind.Attack && pose != PoseId.SpikeWind && pose != PoseId.Spike)
                {
                    forceRun = tl < 1.6f;                                  // spike approach: turn and run in
                    aT = AnimMath.Smooth01((0.3f - tl) / 0.12f);           // last step: both arms swing back
                    mulT = tl > 0.55f ? 0.8f : 1.3f;                       // classic 3-step approach: slow - fast - fast
                }
            }
            float bT = 0f;
            if (rally && !air && !mine && v.ball.live && Court.DistFromNet(myTeam, worldPos.z) < 1.7f && !Court.OnSide(myTeam, v.ball.pos.z)) bT = 1f;
            recvW = Mathf.MoveTowards(recvW, rT, dt / 0.15f);
            setW = Mathf.MoveTowards(setW, sT, dt / 0.15f);
            blockW2 = Mathf.MoveTowards(blockW2, bT, dt / 0.25f);
            approachW = Mathf.MoveTowards(approachW, aT, dt / 0.1f);
            approachMul = AnimMath.Damp(approachMul, mulT, 6f, dt);
        }

        // ================================================================== kinematics
        static bool GaitPose(PoseId p) => p == PoseId.Idle || p == PoseId.Ready || p == PoseId.Run || p == PoseId.Bump || p == PoseId.Set || p == PoseId.Block;

        Vector2 ToBody(Vector2 w)
        {
            float a = bodyYaw * Mathf.Deg2Rad, s = Mathf.Sin(a), c = Mathf.Cos(a);
            return new Vector2(w.x * c - w.y * s, w.x * s + w.y * c);   // x = right, y = forward
        }

        void UpdateKinematics(float dt)
        {
            Vector2 vw = new Vector2(worldVel.x, worldVel.z);
            if (firstFrame) { vwSm = vw; vwSlow = vw; bodyYaw = visYaw; prevBodyYaw = visYaw; gaze = visYaw; }
            // two smoothing stages of the (noisy, stepwise) network velocity; the lag between them gives a clean acceleration estimate
            vwSm = AnimMath.Damp(vwSm, vw, 10f, dt);
            vwSlow = AnimMath.Damp(vwSlow, vw, 3.5f, dt);
            accW = Vector2.ClampMagnitude((vwSm - vwSlow) / 0.28f, 25f);
            speed = vwSm.magnitude;

            // what the player looks at: the live ball, otherwise the net
            var v = GameHub.View;
            float gz = myTeam == 0 ? 0f : 180f;
            if (v.ball.live)
            {
                float dx = v.ball.pos.x - worldPos.x, dz = v.ball.pos.z - worldPos.z;
                if (dx * dx + dz * dz > 0.25f) gz = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
                else gz = gaze;
            }
            gaze = firstFrame ? gz : AnimMath.DampAngle(gaze, gz, 8f, dt);

            // locomotion mode with hysteresis, then smooth weights between the procedural stance / footwork and the mocap loco
            var mode = brain.Update(dt, vwSm, stanceCtx, forceRun);
            bool gaitOk = mode == LocoBrain.Mode.Gait && !air && GaitPose(pose);
            gaitW = Mathf.MoveTowards(gaitW, gaitOk ? 1f : 0f, dt / (gaitOk ? 0.18f : 0.12f));
            legIkW = gaitW * (clipGait ? 0f : (clipStance ? gait.moveAmt : 1f));

            // body facing: gaze while in the volleyball stance, the movement direction otherwise (the view yaw already follows the velocity)
            bool facing = gaitW > 0.5f;
            if (firstFrame) bodyYaw = visYaw;
            else bodyYaw = AnimMath.DampAngle(bodyYaw, facing ? gaze : visYaw, facing ? 9f : 16f, dt, 720f);
            bodyYaw = Mathf.Repeat(bodyYaw, 360f);
            bodyOff = Mathf.DeltaAngle(visYaw, bodyYaw);

            vBody = ToBody(vwSm);
            float footHalfW = (hipHalf + 0.19f * legLocal) * sc;
            gait.Update(dt, vBody, Mathf.Max(0.25f, 2f * footHalfW - 0.20f), 0.7f);

            if (dt > 1e-4f) yawRate = AnimMath.Damp(yawRate, Mathf.Clamp(Mathf.DeltaAngle(prevBodyYaw, bodyYaw) / dt, -540f, 540f), 8f, dt);
            prevBodyYaw = bodyYaw;

            // lean into acceleration (forward accel -> pitch forward, lateral accel / turning -> roll toward the inside) + into the shuffle direction
            Vector2 aB = ToBody(accW);
            float tp = Mathf.Clamp(aB.y * 1.3f, -9f, 11f) + gaitW * Mathf.Clamp(vBody.y * 1.2f, -5f, 6f);
            float tr = -Mathf.Clamp(aB.x * 1.3f + yawRate * speed * 0.012f, -9f, 9f) - gaitW * Mathf.Clamp(vBody.x * 1.6f, -6f, 6f);
            leanP = AnimMath.Damp(leanP, tp, 8f, dt); leanR = AnimMath.Damp(leanR, tr, 8f, dt);
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
                float env = u < 0.15f ? u / 0.15f : 1f - AnimMath.Smooth01((u - 0.15f) / 0.85f);
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
            float wG = w * gaitW, wM = w - wG;
            if (wG > 1e-3f) AddGaitBase(wG, dt);
            if (wM > 1e-3f) AddMocapLoco(wM, dt);
        }

        void AddReady(float w) { if (w > 1e-4f) acc.Add(cReady ?? cIdle, tReady, w); }

        /// <summary>Base muscles under the procedural stance: the ready clip, or (humanoid shuffle clips) a direction blend of walk / back-pedal / shuffles.</summary>
        void AddGaitBase(float w, float dt)
        {
            if (!clipGait) { AddReady(w); return; }
            float m = gait.moveAmt, sp = speed;
            AddReady(w * (1f - m));
            if (m < 1e-3f) return;
            float inv = sp > 0.05f ? 1f / sp : 0f;
            float cf = Mathf.Max(0f, vBody.y) * inv, cb = Mathf.Max(0f, -vBody.y) * inv, cr = Mathf.Max(0f, vBody.x) * inv, cl = Mathf.Max(0f, -vBody.x) * inv;
            float wm = w * m;
            var refClip = cShL ?? cShR;
            float rate = Mathf.Clamp(sp / Mathf.Max(0.5f, refClip.speed), 0.6f, 1.8f) / Mathf.Max(0.2f, refClip.duration);
            gaitPhase = Mathf.Repeat(gaitPhase + rate * dt, 1f);
            float wf = cf * cf, wb = cb * cb, wr = cr * cr, wl = cl * cl;
            if (wf > 1e-3f) { if (cWalk != null) acc.Add(cWalk, gaitPhase * cWalk.duration, wm * wf); else AddReady(wm * wf); }
            if (wb > 1e-3f) { if (cBack != null) acc.Add(cBack, gaitPhase * cBack.duration, wm * wb); else AddReady(wm * wb); }
            if (wl > 1e-3f) { if (cShL != null) acc.Add(cShL, gaitPhase * cShL.duration, wm * wl); else acc.Add(cShR, gaitPhase * cShR.duration, wm * wl, true); }
            if (wr > 1e-3f) { if (cShR != null) acc.Add(cShR, gaitPhase * cShR.duration, wm * wr); else acc.Add(cShL, gaitPhase * cShL.duration, wm * wr, true); }
        }

        /// <summary>Mocap idle / walk / run / sprint by speed. All stride cycles share one normalised phase (they start at "left leg forward"),
        /// the phase rate is damped so weight / speed changes never make the cycle jump.</summary>
        void AddMocapLoco(float w, float dt)
        {
            float sp = speed;
            int n = movers.Length;
            float wStat = 1f; int i0 = -1, i1 = -1; float f = 0f;
            if (n > 0)
            {
                if (sp <= movers[0].speed) { f = AnimMath.Smooth01(sp / movers[0].speed); i0 = 0; wStat = 1f - f; }
                else
                {
                    int i = 0;
                    while (i + 1 < n && sp > movers[i + 1].speed) i++;
                    if (i + 1 >= n) { i0 = n - 1; f = 1f; wStat = 0f; }
                    else { i0 = i; i1 = i + 1; f = AnimMath.Smooth01((sp - movers[i].speed) / (movers[i + 1].speed - movers[i].speed)); wStat = 0f; }
                }
            }

            // stationary: idle <-> ready (ready only inside a rally)
            if (wStat > 1e-3f)
            {
                float ws = w * wStat;
                if (cIdle == cReady) acc.Add(cIdle, tIdle, ws);
                else { float st = stanceCtx ? 1f : 0f; acc.Add(cIdle, tIdle, ws * (1f - st)); acc.Add(cReady, tReady, ws * st); }
            }

            if (n > 0)
            {
                float rateT;
                if (i1 < 0)
                {
                    var c = movers[i0];
                    rateT = Mathf.Clamp(sp / c.speed, 0.55f, 1.6f) / c.duration;
                    acc.Add(c, phase * c.duration, w * f);
                }
                else
                {
                    var a = movers[i0]; var b = movers[i1];
                    float ra = Mathf.Clamp(sp / a.speed, 0.55f, 1.6f) / a.duration, rb = Mathf.Clamp(sp / b.speed, 0.55f, 1.6f) / b.duration;
                    rateT = Mathf.Lerp(ra, rb, f);
                    acc.Add(a, phase * a.duration, w * (1f - f));
                    acc.Add(b, phase * b.duration, w * f);
                }
                rateSm = AnimMath.Damp(rateSm, rateT * approachMul, 9f, dt);
                phase = Mathf.Repeat(phase + rateSm * dt, 1f);
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
            airU = AnimMath.Damp(airU, u, 24f, dt);
            float blockTarget = (pose == PoseId.Block && cJumpB != null && cBlock == null) ? 1f : 0f;
            blockW = Mathf.MoveTowards(blockW, blockTarget, dt / 0.1f);
            if (blockW < 0.999f) acc.Add(airClip, JumpTime(airClip), w * (1f - blockW));
            if (blockW > 0.001f) acc.Add(cJumpB, JumpTime(cJumpB), w * blockW);   // basketball jump-shot: both arms overhead
        }

        float JumpTime(MotionClip c)
        {
            // уже на земле: продолжаем клип после кадра касания в реальном темпе — там записана настоящая амортизация
            if (!air && hadAir) return Mathf.Min(c.touch / c.fps + landT, c.duration);
            float fr = airU < 0.5f ? Mathf.Lerp(c.launch, c.apex, airU * 2f) : Mathf.Lerp(c.apex, c.touch, (airU - 0.5f) * 2f);
            return fr / c.fps;
        }

        // ================================================================== action layer: clips (humanoid / mocap) timed by poseT
        bool actWanted;

        void SelectAction(float dt)
        {
            MotionClip c = null; float t = 0f; bool full = false;
            float pt = poseAge;
            switch (pose)
            {
                case PoseId.Dive: c = cDive; full = true; if (c != null) t = pt * Mathf.Clamp(c.duration / 0.9f, 1f, 2.2f); break;
                case PoseId.Celebrate: c = cCelebrate; full = true; t = pt; break;
                case PoseId.Sad: c = cSad; full = true; if (c != null) t = pt * Mathf.Clamp(c.duration / 1.4f, 1f, 2f); break;
                case PoseId.Bump: c = cBump; full = air; if (c != null) t = pt * c.duration / 0.55f; break;
                case PoseId.Set: c = cSet; full = air; if (c != null) t = pt * c.duration / 0.45f; break;
                case PoseId.SpikeWind: c = cSpike; full = true; if (c != null) t = Mathf.Min(pt / 0.32f, 1f) * 0.55f * c.duration; break;
                case PoseId.Spike: c = cSpike; full = true; if (c != null) t = (0.55f + Mathf.Min(pt / 0.4f, 1f) * 0.45f) * c.duration; break;
                case PoseId.Block: c = cBlock; full = air; if (c != null) t = pt * c.duration / 0.5f; break;
                case PoseId.ServeToss: c = air ? (cServeJ ?? cServeF) : cServeF; full = air; if (c != null) t = Mathf.Min(pt / 0.8f, 1f) * 0.5f * c.duration; break;
                case PoseId.ServeHit: c = air ? (cServeJ ?? cServeF) : cServeF; full = air; if (c != null) t = (0.5f + Mathf.Min(pt / 0.45f, 1f) * 0.5f) * c.duration; break;
            }
            actWanted = c != null;
            if (c != null) { if (actClip != c) { actClip = c; } actT = t; actFull = full; actW = Mathf.MoveTowards(actW, 1f, dt / (full ? 0.09f : 0.07f)); }
            else
            {
                actT += dt;
                actW = Mathf.MoveTowards(actW, 0f, dt / 0.18f);
                if (actW <= 0f) actClip = null;
            }
            if (actClip == null) actW = 0f;
        }

        void ComposeAction(ref Vector3 bpos, ref Quaternion brot)
        {
            if (actClip == null || actW <= 1e-3f) return;
            acc2.Clear();
            acc2.Add(actClip, actT, 1f);
            acc2.Finish(tmpM, out Vector3 ap, out Quaternion ar);
            var mask = MotionLibrary.UpperMask;
            var m = hp.muscles;
            for (int j = 0; j < m.Length; j++)
            {
                float mw = actFull ? actW : (mask != null && j < mask.Length && mask[j] ? actW : 0f);
                if (mw > 0f) m[j] = Mathf.Lerp(m[j], tmpM[j], mw);
            }
            if (actFull) { bpos = Vector3.Lerp(bpos, ap, actW); brot = Quaternion.Slerp(brot, ar, actW); }
        }

        // ================================================================== override layer
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        void Overlay(PoseOv key, float w)
        {
            if (w < 0.01f) return;
            tmpA.Set(tgt);
            PoseOv.Blend(tgt, tmpA, key, w);
        }

        /// <summary>Ready stance (rally) with the anticipation overlays: receive platform, setter hands, blocker hands, approach arm swing.</summary>
        void BuildStance()
        {
            float sw = clipStance ? 0f : gaitW;
            PoseOv.Blend(tgt, PoseOv.NeutralRef, VolleyKeys.Ready, sw);
            if (sw > 0.01f) { float b = gait.bob * 4f * sw; tgt.lR.y += b; tgt.lL.y += b; }   // forearms follow the knee bounce
            Overlay(VolleyKeys.BlockReady, blockW2);
            Overlay(VolleyKeys.SetReady, setW);
            Overlay(VolleyKeys.Platform, recvW);
            Overlay(VolleyKeys.Approach, approachW);
        }

        void EvalOverrides(float dt, float tm)
        {
            float pt = poseAge;
            tgt.SetNeutral();
            bool ground = !air;
            switch (pose)
            {
                case PoseId.Idle: case PoseId.Ready: case PoseId.Run: BuildStance(); break;
                case PoseId.SpikeWind: if (!actWanted) VolleyKeys.Seq(tgt, pt, VolleyKeys.kWind, VolleyKeys.tWind, VolleyKeys.eWind); break;
                case PoseId.Spike: if (!actWanted) VolleyKeys.Seq(tgt, pt, VolleyKeys.kSpike, VolleyKeys.tSpike, VolleyKeys.eSpike); break;
                case PoseId.Block:
                    if (!actWanted) VolleyKeys.Seq(tgt, pt, VolleyKeys.kBlock, VolleyKeys.tBlock, VolleyKeys.eBlock);
                    if (ground) tgt.hips += VolleyKeys.BlockReady.hips * gaitW;
                    break;
                case PoseId.Bump:
                    if (!actWanted) VolleyKeys.Seq(tgt, pt, VolleyKeys.kBump, VolleyKeys.tBump, VolleyKeys.eBump);
                    if (ground) tgt.hips += V(8f, 0f, 0f) * gaitW;
                    break;
                case PoseId.Set:
                    if (!actWanted) VolleyKeys.Seq(tgt, pt, VolleyKeys.kSet, VolleyKeys.tSet, VolleyKeys.eSet);
                    if (ground) tgt.hips += V(2f, 0f, 0f) * gaitW;
                    break;
                case PoseId.ServeToss: if (!actWanted) VolleyKeys.Seq(tgt, pt, VolleyKeys.kToss, VolleyKeys.tToss, VolleyKeys.eToss); break;
                case PoseId.ServeHit: if (!actWanted) VolleyKeys.Seq(tgt, pt, VolleyKeys.kHit, VolleyKeys.tHit, VolleyKeys.eHit); break;
                case PoseId.Dive:
                    if (!actWanted)
                    {
                        tgt.pitch = 78f; tgt.wR = tgt.wL = 1f;
                        tgt.uR = tgt.uL = V(0.12f, 1f, 0.2f); tgt.lR = tgt.lL = V(0.08f, 1f, 0.25f);
                        tgt.legW = 1f; tgt.gUR = V(0.1f, -1f, -0.1f); tgt.gLR = V(0.08f, -0.75f, -0.7f); tgt.gUL = V(0.1f, -1f, 0.05f); tgt.gLL = V(0.05f, -1f, -0.2f);
                        tgt.neck = V(-15, 0, 0); tgt.head = V(-25, 0, 0);
                    }
                    break;
                case PoseId.Celebrate:
                    if (!actWanted)
                    {   // fist pump + hop: both fists drive up in the beat of the jump
                        float hop = Mathf.Abs(Mathf.Sin(pt * 9f));
                        float pump = 0.5f + 0.5f * Mathf.Sin(pt * 9f + 1.2f);
                        tgt.wR = tgt.wL = 1f; tgt.uR = tgt.uL = V(0.7f, 0.55f + 0.2f * pump, 0.1f); tgt.lR = tgt.lL = V(0.15f, 1f, 0.15f + 0.3f * (1f - pump));
                        tgt.legW = hop; tgt.gUR = tgt.gUL = V(0.05f, -0.95f, 0.35f * hop); tgt.gLR = tgt.gLL = V(0.02f, -0.9f, -0.45f * hop);
                        tgt.lift = hop * 0.14f; tgt.spine = V(-5, 0, 0); tgt.chest = V(-4, 0, 0); tgt.head = V(-12, 0, 0);
                    }
                    break;
                case PoseId.Sad:
                    if (!actWanted)
                    {   // hands on knees, head down
                        tgt.wR = tgt.wL = 1f; tgt.armFrame = 1f; tgt.uR = tgt.uL = V(0.12f, -0.97f, 0.22f); tgt.lR = tgt.lL = V(0.05f, -0.95f, 0.30f);
                        tgt.hips = V(14, 0, 0); tgt.spine = V(18, 0, 0); tgt.chest = V(14, 0, 0); tgt.neck = V(12, 0, 0); tgt.head = V(22, 0, 0);
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
                float k = blendDur > 1e-4f ? AnimMath.Smooth01(blendT / blendDur) : 1f;
                PoseOv.Blend(cur, from, tgt, k);
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
            // secondary motion: breathing, sway, lean into acceleration, torso leads turns (small, smoothed: no overshoot)
            float br = Mathf.Sin(tm * 1.9f), sw = Mathf.Sin(tm * 0.8f);
            float turnLead = Mathf.Clamp(yawRate * 0.035f, -8f, 8f) * (1f - 0.7f * gaitW);
            Vector3 lean = new Vector3(leanP, 0f, leanR);

            // head tracks the ball (limited), the eyes finish the job through VRM LookAt (UpdateFace)
            ComputeLook(dt, R, out Vector3 lookEul);

            if (hips != null) RotW(hips.t, R, cur.hips);
            if (spine != null) RotW(spine.t, R, cur.spine + lean * 0.45f + new Vector3(0f, 0f, sw * 1.0f));
            Vector3 ch = cur.chest + lean * 0.55f + new Vector3(br * 1.0f, turnLead, 0f);
            if (upperChest != null && chest != null) { RotW(chest.t, R, ch * 0.5f); RotW(upperChest.t, R, ch * 0.5f); }
            else if (chest != null) RotW(chest.t, R, ch);
            if (neck != null) RotW(neck.t, R, cur.neck + lookEul * 0.35f);
            RotW(headT, R, cur.head - (lean * 0.5f) + lookEul * 0.5f + new Vector3(br * 0.6f, Mathf.Sin(tm * 0.7f) * 1.5f, 0f));
        }

        void ComputeLook(float dt, Quaternion R, out Vector3 eul)
        {
            eul = Vector3.zero;
            var ball = GameHub.View.ball;
            float wTarget = (pose == PoseId.Sad || pose == PoseId.Stun) ? 0f : pose == PoseId.Celebrate ? 0.25f : 1f;
            lookW = Mathf.MoveTowards(lookW, wTarget, dt / 0.25f);
            if (head == null || ball.pos == Vector3.zero) return;
            Vector3 l = Quaternion.Inverse(R) * (ball.pos - head.t.position);
            float hd = Mathf.Sqrt(l.x * l.x + l.z * l.z);
            if (hd < 0.05f && Mathf.Abs(l.y) < 0.05f) return;
            float yaw = Mathf.Atan2(l.x, l.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(-l.y, hd) * Mathf.Rad2Deg;     // + = look down
            float fade = 1f - AnimMath.Smooth01((Mathf.Abs(yaw) - 90f) / 50f);  // ball behind us: stop turning the head
            float ty = Mathf.Clamp(yaw, -55f, 55f) * fade * lookW;
            float tp = Mathf.Clamp(pitch, -35f, 30f) * Mathf.Lerp(0.6f, 1f, fade) * lookW;
            lookYaw = AnimMath.Damp(lookYaw, ty, 7f, dt); lookPitch = AnimMath.Damp(lookPitch, tp, 7f, dt);
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
            if (cur.armFrame > 0.001f) D = Quaternion.Slerp(D, Quaternion.identity, Mathf.Clamp01(cur.armFrame));
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

        // ================================================================== procedural footwork (low stance + two-bone leg IK)
        /// <summary>Sets a bone so that it points along a WORLD direction (minimal rotation from its bind pose), blended by w.</summary>
        void PointWorld(B b, Vector3 dirW, float w)
        {
            if (b == null || w < 0.01f || dirW.sqrMagnitude < 1e-8f) return;
            Quaternion R0 = root.rotation;
            Quaternion target = Quaternion.FromToRotation(R0 * b.dir, dirW.normalized) * (R0 * b.rel);
            b.t.rotation = w >= 0.999f ? target : Quaternion.Slerp(b.t.rotation, target, w);
        }

        static void SolveLeg(Vector3 hip, Vector3 target, float L1, float L2, Vector3 pole, out Vector3 thigh, out Vector3 shin)
        {
            Vector3 d = target - hip;
            float dist = d.magnitude;
            Vector3 u = dist > 1e-5f ? d / dist : Vector3.down;
            dist = Mathf.Clamp(dist, Mathf.Abs(L1 - L2) + 0.02f, (L1 + L2) * 0.999f);
            float cosA = Mathf.Clamp((L1 * L1 + dist * dist - L2 * L2) / (2f * L1 * dist), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 perp = pole - Vector3.Dot(pole, u) * u;
            perp = perp.sqrMagnitude > 1e-6f ? perp.normalized : Vector3.Cross(u, Vector3.right).normalized;
            thigh = u * cosA + perp * sinA;
            Vector3 knee = hip + thigh * L1;
            Vector3 ankle = hip + u * dist;
            shin = (ankle - knee).normalized;
        }

        void ApplyGait(Quaternion Rb, Quaternion Q, float w)
        {
            if (w < 0.01f || hips == null || ulR == null || ulL == null || llR == null || llL == null || ftR == null || ftL == null) return;
            float leg = legLocal;
            float footHalf = hipHalf + 0.19f * leg;
            float crouch = Mathf.Lerp(0.84f, 0.88f, gait.moveAmt);
            if (pose == PoseId.Bump) crouch -= 0.03f; else if (pose == PoseId.Set) crouch += 0.03f;
            float homeZ = 0.10f * leg;                                   // feet slightly ahead of the pelvis (hips back, weight on the balls of the feet)
            float dx = footHalf - hipHalf;
            float D = crouch * leg;
            float hipY = restAnkleY + Mathf.Sqrt(Mathf.Max(0.01f, D * D - dx * dx - homeZ * homeZ)) + gait.bob / sc;

            // pelvis: the middle of the two hip joints goes to the desired spot in the (yaw only) body frame
            Vector3 mid = 0.5f * (ulR.t.position + ulL.t.position);
            Vector3 midLocal = root.InverseTransformPoint(mid);
            Vector3 want = Q * new Vector3(gait.sway / sc, hipY, 0f);
            hips.t.position += root.TransformVector(want - midLocal) * w;

            Vector3 fwdW = root.TransformDirection(Q * Vector3.forward);
            Vector3 rightW = root.TransformDirection(Q * Vector3.right);
            for (int i = 0; i < 2; i++)
            {
                float sg = i == 0 ? -1f : 1f;
                B ul = i == 0 ? ulL : ulR, ll = i == 0 ? llL : llR, ft = i == 0 ? ftL : ftR;
                gait.Foot(i, out Vector2 off, out float lift, out float swing);
                Vector3 fb = new Vector3(sg * footHalf + off.x / sc, restAnkleY + lift / sc, homeZ + off.y / sc);
                Vector3 target = root.TransformPoint(Q * fb);
                Vector3 pole = (fwdW + rightW * (sg * 0.22f)).normalized;
                SolveLeg(ul.t.position, target, l1w, l2w, pole, out Vector3 thigh, out Vector3 shin);
                PointWorld(ul, thigh, w);
                PointWorld(ll, shin, w);
                // foot: flat on the ground, heel slightly raised (weight on the balls), toes a little out, toes up while swinging
                float pitch = Mathf.Lerp(10f, -6f, Mathf.Clamp01(swing));
                Quaternion fr = Rb * Quaternion.Euler(pitch, sg * 8f, 0f) * ft.rel;
                ft.t.rotation = w >= 0.999f ? fr : Quaternion.Slerp(ft.t.rotation, fr, w);
            }
        }

        // ================================================================== анти-скрещивание ног
        // Записи CMU сами по себе ставят стопы в линию или крест-накрест («модельная» стойка в idle, танцевальные шаги в celebrate,
        // ноги в прыжках). Волейболист так не стоит: после любой позы проверяем поперечное расстояние между стопами и, если оно
        // меньше нормы, поворачиваем каждое бедро наружу. Поправка сглажена во времени, поэтому не дёргается.
        float crossFix;

        void AntiCross(float dt)
        {
            if (ulR == null || ulL == null || ftR == null || ftL == null) return;
            Vector3 right = ulR.t.position - ulL.t.position; right.y = 0f;
            float hipW = right.magnitude;
            if (hipW < 1e-4f) return;
            right /= hipW;
            Vector3 pR = ftR.t.position, pL = ftL.t.position;
            float sep = Vector3.Dot(pR - pL, right);
            // норма: стоя ~ ширина плеч, на бегу уже (стопы ближе к центру), в воздухе ноги слегка разведены
            float run = Mathf.Clamp01((speed - 1.5f) / 3f);
            float want = air ? hipW * 1.25f : Mathf.Lerp(hipW * 1.9f, hipW * 0.55f, run);
            if (pose == PoseId.Dive) want = hipW * 0.8f;
            float need = Mathf.Max(0f, want - sep);
            // быстро догоняем, когда ноги начинают сходиться, и мягко отпускаем
            crossFix = need > crossFix ? AnimMath.Damp(crossFix, need, 25f, dt) : AnimMath.Damp(crossFix, need, 8f, dt);
            if (crossFix < 0.002f) return;
            float half = crossFix * 0.5f;
            SpreadLeg(ulR.t, pR, right * half);
            SpreadLeg(ulL.t, pL, -right * half);
        }

        /// <summary>Поворачивает бедро так, чтобы стопа сместилась на shift (горизонтально), длина ноги не меняется.</summary>
        static void SpreadLeg(Transform thigh, Vector3 foot, Vector3 shift)
        {
            Vector3 v = foot - thigh.position;
            if (v.sqrMagnitude < 1e-6f) return;
            Vector3 v2 = (v + shift).normalized * v.magnitude;
            thigh.rotation = Quaternion.FromToRotation(v, v2) * thigh.rotation;
        }

        // ================================================================== foot plant
        /// <summary>The simulation owns the root: slide the whole body vertically so the lowest ground reference sits on the floor.
        /// The correction goes through a critically damped spring (clamped speed), so the mocap foot strikes never snap the body.</summary>
        void Plant(float dt)
        {
            if (groundRefs == null || hips == null) return;
            float low = LowestLocalY();
            float target = Mathf.Clamp((restRefY + cur.lift) - low, -0.8f, 0.8f);
            if (firstFrame) plant.Reset();
            // slow follow: the mocap already carries the right vertical bounce (flight phase of a run, crouch on landing); the plant only
            // removes the slow offset. A fast spring + hard clamp pulled the body down in every flight phase and popped it up at contact.
            float dy = plant.Step(target, 0.16f, dt, 2.5f);
            dy = Mathf.Max(dy, target - 0.06f);          // never sink deeper than 6 cm into the floor
            plant.x = dy;
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
