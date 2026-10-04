// Dev-only movement probe (menu Tobe/Dev): drives the local player through a fixed WASD script and records every rendered frame
// (simulated position, view position, hips / feet bones, pose) into a CSV, so jitter and "teleports" can be measured instead of guessed.
using System.Globalization;
using System.IO;
using System.Text;
using Tobe.Net;
using UnityEngine;

namespace Tobe
{
    [DefaultExecutionOrder(30000)]   // after PlayerAnimator (-50) and the views: we read the final pose of the frame
    public sealed class DevProbe : MonoBehaviour
    {
        struct Step { public float dur; public Vector2 move; public bool sprint; public string name; public Step(string n, float d, float x, float y, bool s = false) { name = n; dur = d; move = new Vector2(x, y); sprint = s; } }

        static readonly Step[] Script =
        {
            new Step("idle", 1.0f, 0, 0),
            new Step("fwd", 1.2f, 0, 1),
            new Step("stop", 0.8f, 0, 0),
            new Step("right", 1.0f, 1, 0),
            new Step("left", 1.0f, -1, 0),
            new Step("back", 1.0f, 0, -1),
            new Step("stop2", 0.6f, 0, 0),
            new Step("sprint", 1.2f, 0.3f, 1, true),
            new Step("tapW", 0.15f, 0, 1),
            new Step("tap0", 0.3f, 0, 0),
            new Step("tapD", 0.15f, 1, 0),
            new Step("end", 0.8f, 0, 0),
        };

        public static string LastFile;
        public static bool Running => instance != null;
        static DevProbe instance;

        readonly StringBuilder sb = new StringBuilder(1 << 16);
        float t; int step = -1; float stepT; bool started;
        Transform hips, footL, footR, headT, toeL, toeR;
        Vector3 prevHips; bool havePrev;

        public static void Begin()
        {
            if (instance != null) Destroy(instance.gameObject);
            var go = new GameObject("DevProbe");
            instance = go.AddComponent<DevProbe>();
        }

        void Awake()
        {
            sb.AppendLine("frame,t,dt,step,phase,pose,simX,simZ,velX,velZ,viewX,viewY,viewZ,yaw,hipsX,hipsY,hipsZ,hipsStep,footLY,footRY,footLX,footLZ,footRX,footRZ,serving,toeLY,toeRY");
        }

        void Update()
        {
            var v = GameHub.View;
            bool ready = v.localPlayerId >= 0 && (v.phase == MatchPhase.Serve || v.phase == MatchPhase.Rally) && v.serverPlayerId != v.localPlayerId && !GameHub.UiBlocking;
            if (!started) { if (!ready) return; started = true; step = 0; stepT = 0; }
            if (step >= Script.Length) return;
            stepT += Time.deltaTime;
            if (stepT >= Script[step].dur) { stepT = 0; step++; }
            if (step >= Script.Length) { MatchClient.DevMove = null; MatchClient.DevSprint = false; Finish(); return; }
            MatchClient.DevMove = Script[step].move;
            MatchClient.DevSprint = Script[step].sprint;
        }

        void LateUpdate()
        {
            if (!started || step >= Script.Length) return;
            var v = GameHub.View;
            if (!v.TryGetLocal(out var me)) return;
            if (hips == null)
            {
                var pgo = GameObject.Find("Player_" + me.id);
                var an = pgo != null ? pgo.GetComponentInChildren<Animator>() : null;
                if (an != null && an.isHuman)
                {
                    hips = an.GetBoneTransform(HumanBodyBones.Hips);
                    footL = an.GetBoneTransform(HumanBodyBones.LeftFoot);
                    footR = an.GetBoneTransform(HumanBodyBones.RightFoot);
                    headT = an.GetBoneTransform(HumanBodyBones.Head);
                    toeL = an.GetBoneTransform(HumanBodyBones.LeftToes) ?? footL;
                    toeR = an.GetBoneTransform(HumanBodyBones.RightToes) ?? footR;
                }
            }
            var pv = GameObject.Find("Player_" + me.id);
            Vector3 vp = pv != null ? pv.transform.position : Vector3.zero;
            Vector3 hp = hips != null ? hips.position : Vector3.zero;
            float hs = havePrev ? Vector3.Distance(hp, prevHips) : 0f;
            prevHips = hp; havePrev = true;
            Vector3 fl = footL != null ? footL.position : Vector3.zero, fr = footR != null ? footR.position : Vector3.zero;
            var c = CultureInfo.InvariantCulture;
            sb.Append(Time.frameCount).Append(',').Append(t.ToString("F4", c)).Append(',').Append(Time.deltaTime.ToString("F4", c)).Append(',')
              .Append(Script[step].name).Append(',').Append(v.phase).Append(',').Append(me.pose).Append(',')
              .Append(F(me.pos.x)).Append(',').Append(F(me.pos.z)).Append(',').Append(F(me.vel.x)).Append(',').Append(F(me.vel.z)).Append(',')
              .Append(F(vp.x)).Append(',').Append(F(vp.y)).Append(',').Append(F(vp.z)).Append(',').Append(F(pv != null ? pv.transform.Find("Visual").eulerAngles.y : 0)).Append(',')
              .Append(F(hp.x)).Append(',').Append(F(hp.y)).Append(',').Append(F(hp.z)).Append(',').Append(F(hs)).Append(',')
              .Append(F(fl.y)).Append(',').Append(F(fr.y)).Append(',').Append(F(fl.x)).Append(',').Append(F(fl.z)).Append(',').Append(F(fr.x)).Append(',').Append(F(fr.z)).Append(',')
              .Append(v.serverPlayerId == me.id ? 1 : 0).Append(',')
              .Append(F(toeL != null ? toeL.position.y : 0)).Append(',').Append(F(toeR != null ? toeR.position.y : 0)).AppendLine();
            t += Time.deltaTime;
        }

        static string F(float x) => x.ToString("F4", CultureInfo.InvariantCulture);

        void Finish()
        {
            string dir = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(dir);
            LastFile = Path.GetFullPath(Path.Combine(dir, "devprobe.csv"));
            File.WriteAllText(LastFile, sb.ToString());
            Debug.Log("[Tobe] DevProbe written: " + LastFile);
            instance = null;
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (instance == this) { instance = null; MatchClient.DevMove = null; MatchClient.DevSprint = false; }
        }
    }
}
