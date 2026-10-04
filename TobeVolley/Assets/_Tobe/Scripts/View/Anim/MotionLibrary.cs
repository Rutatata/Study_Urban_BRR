// Loads the mocap clips (StreamingAssets/Motions/motions.json + <name>.bvh) once at startup and bakes every clip into
// per-frame Mecanim "HumanPose" muscle arrays. Baked data is avatar independent: any humanoid (VRM) can play it with
// HumanPoseHandler.SetHumanPose. The temporary BVH skeleton / Avatar / AnimationClip objects are destroyed after baking.
//
// Everything is optional: json may be missing (then *.bvh files with known names are picked up), any clip may be missing
// (PlayerAnimator has procedural fallbacks) and a broken file only logs a warning.
// Loading is spread over frames: file read + BVH text parse run on a worker thread, the Unity-side work (hierarchy, avatar,
// sampling) is time-sliced to ~3 ms per frame, so the main menu does not hitch.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UniHumanoid;
using Debug = UnityEngine.Debug;

namespace Tobe.View
{
    /// <summary>One baked clip. Muscles are laid out frame-major: muscles[frame * MC + muscleIndex].</summary>
    public sealed class MotionClip
    {
        public string name;
        public bool loop;
        public float fps;          // baked sample rate
        public int frames;
        public float duration;     // loop: frames / fps (wraps to frame 0); one-shot: (frames-1) / fps
        public float[] muscles;
        public Vector3[] bodyPos;  // HumanPose.bodyPosition (normalised units, horizontal travel removed)
        public Quaternion[] bodyRot; // facing normalised to +Z of the character
        public float speed;        // planar root speed in m/s of a 1.8 m character (0 for in-place clips)
        public Vector2 rootVel;    // (lateral x, forward z) average m/s in the clip's facing frame
        public float StrideDist => speed * duration; // metres travelled per loop cycle
        // jump analysis (frame indices): crouch bottom, leaving the ground, apex, just before touch down
        public int low, launch, apex, touch;

        /// <summary>Finds the two frames around <paramref name="time"/> (seconds) and the blend factor.</summary>
        public void Locate(float time, out int i0, out int i1, out float a)
        {
            float f = time * fps;
            if (loop)
            {
                f = Mathf.Repeat(f, frames);
                i0 = Mathf.Min((int)f, frames - 1);
                a = f - i0;
                i1 = i0 + 1 >= frames ? 0 : i0 + 1;
            }
            else
            {
                f = Mathf.Clamp(f, 0f, frames - 1);
                i0 = Mathf.Min((int)f, frames - 1);
                a = f - i0;
                i1 = Mathf.Min(i0 + 1, frames - 1);
            }
        }
    }

    /// <summary>Weighted accumulator of several clip samples (muscles, body position, body rotation).</summary>
    public sealed class PoseAccum
    {
        readonly int mc;
        public readonly float[] m;
        Vector3 pos;
        Quaternion q0 = Quaternion.identity;
        float qx, qy, qz, qw;
        bool hasQ;
        public float wsum;

        public PoseAccum() { mc = MotionLibrary.MC; m = new float[mc]; }

        public void Clear()
        {
            Array.Clear(m, 0, m.Length);
            pos = Vector3.zero; qx = qy = qz = qw = 0f; hasQ = false; wsum = 0f;
        }

        public void Add(MotionClip c, float time, float w, bool mirror = false)
        {
            if (c == null || c.frames <= 0 || w <= 1e-4f) return;
            c.Locate(time, out int i0, out int i1, out float a);
            int o0 = i0 * mc, o1 = i1 * mc;
            float w0 = w * (1f - a), w1 = w * a;
            var src = c.muscles;
            if (!mirror)
            {
                for (int j = 0; j < mc; j++) m[j] += w0 * src[o0 + j] + w1 * src[o1 + j];
            }
            else
            {
                var mi = MotionLibrary.MirrorIdx; var ms = MotionLibrary.MirrorSign;
                for (int j = 0; j < mc; j++) { int s = mi[j]; m[j] += ms[j] * (w0 * src[o0 + s] + w1 * src[o1 + s]); }
            }
            Vector3 p = Vector3.Lerp(c.bodyPos[i0], c.bodyPos[i1], a);
            Quaternion r = Quaternion.Slerp(c.bodyRot[i0], c.bodyRot[i1], a);
            if (mirror) { p.x = -p.x; r = new Quaternion(r.x, -r.y, -r.z, r.w); }
            pos += w * p;
            if (!hasQ) { q0 = r; hasQ = true; }
            if (Quaternion.Dot(q0, r) < 0f) { r.x = -r.x; r.y = -r.y; r.z = -r.z; r.w = -r.w; }
            qx += w * r.x; qy += w * r.y; qz += w * r.z; qw += w * r.w;
            wsum += w;
        }

        public void Finish(float[] outMuscles, out Vector3 p, out Quaternion r)
        {
            if (wsum < 1e-5f) { Array.Clear(outMuscles, 0, outMuscles.Length); p = Vector3.zero; r = Quaternion.identity; return; }
            float inv = 1f / wsum;
            for (int j = 0; j < mc; j++) outMuscles[j] = m[j] * inv;
            p = pos * inv;
            var q = new Quaternion(qx, qy, qz, qw);
            float n = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            r = n > 1e-6f ? new Quaternion(q.x / n, q.y / n, q.z / n, q.w / n) : Quaternion.identity;
        }
    }

    public static class MotionLibrary
    {
        public static int MC { get; private set; } = 95;
        /// <summary>For each muscle the index of its left/right counterpart, and the sign to apply when mirroring.</summary>
        public static int[] MirrorIdx { get; private set; }
        public static float[] MirrorSign { get; private set; }

        static readonly Dictionary<string, MotionClip> clips = new Dictionary<string, MotionClip>(StringComparer.OrdinalIgnoreCase);
        static bool started;
        public static bool Loaded { get; private set; }
        public static int Count => clips.Count;
        public static event Action OnLoaded;

        public static string Dir => Path.Combine(Application.streamingAssetsPath, "Motions");

        /// <summary>Null when the clip does not exist (or is not loaded yet).</summary>
        public static MotionClip Get(string name)
        {
            return name != null && clips.TryGetValue(name, out var c) ? c : null;
        }

        /// <summary>True when there are enough clips for PlayerAnimator to replace the procedural poser.</summary>
        public static bool HasCore => Loaded && (Get("idle") != null || Get("ready") != null) &&
                                       (Get("walk") != null || Get("run") != null || Get("sprint") != null);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { clips.Clear(); started = false; Loaded = false; OnLoaded = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart() { EnsureStarted(); }

        public static void EnsureStarted()
        {
            if (started) return;
            started = true;
            try
            {
                BuildMirrorTable();
                var go = new GameObject("TobeMotionLoader");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<MotionLoader>();
            }
            catch (Exception e) { Debug.LogWarning("[Tobe] MotionLibrary start failed: " + e.Message); Loaded = true; }
        }

        internal static void Register(MotionClip c) { clips[c.name] = c; }
        internal static void Finish() { Loaded = true; Debug.Log("[Tobe] Motions ready: " + clips.Count + " clip(s)"); OnLoaded?.Invoke(); }

        static void BuildMirrorTable()
        {
            var names = HumanTrait.MuscleName;
            MC = names.Length;
            MirrorIdx = new int[MC]; MirrorSign = new float[MC];
            var map = new Dictionary<string, int>();
            for (int i = 0; i < MC; i++) map[names[i]] = i;
            for (int i = 0; i < MC; i++)
            {
                string n = names[i]; int j = i;
                if (n.StartsWith("Left ", StringComparison.Ordinal) && map.TryGetValue("Right " + n.Substring(5), out int r)) j = r;
                else if (n.StartsWith("Right ", StringComparison.Ordinal) && map.TryGetValue("Left " + n.Substring(6), out int l)) j = l;
                MirrorIdx[i] = j;
                // centre muscles that swing left/right (spine bend/twist, neck tilt/turn, jaw) flip sign; side muscles keep it
                bool centre = j == i;
                MirrorSign[i] = centre && n.Contains("Left-Right") ? -1f : 1f;
            }
        }

        public static int MuscleIndex(string name) => Array.IndexOf(HumanTrait.MuscleName, name);
    }

    // ====================================================================================================== loader
    sealed class MotionLoader : MonoBehaviour
    {
        sealed class Entry
        {
            public string name, path;
            public bool loop;
            public float jsonFps, jsonSpeed;
            public int jsonFrames;
            public Task<Bvh> task;
        }

        static readonly string[] Known =
        {
            "idle", "ready", "run", "walk", "sprint", "sidestep", "jump_vertical", "jump_approach", "land", "dive", "celebrate", "sad", "throw_overhead"
        };
        static readonly HashSet<string> LoopNames = new HashSet<string> { "idle", "ready", "walk", "run", "sprint", "sidestep", "celebrate" };
        static readonly HashSet<string> LocoNames = new HashSet<string> { "walk", "run", "sprint", "sidestep" };

        const float BudgetMs = 3f;
        const float HipScale = 0.95f;   // hip height (m) of a 1.8 m character; BVH hierarchy is normalised to 1 m hips
        readonly Stopwatch sw = new Stopwatch();

        IEnumerator Start()
        {
            // let the first frames (menu) render before we start working
            yield return null; yield return null;
            var entries = new List<Entry>();
            try { entries = Discover(); }
            catch (Exception e) { Debug.LogWarning("[Tobe] Motion discovery failed: " + e.Message); }

            // parse everything on worker threads right away
            foreach (var en in entries)
            {
                string p = en.path;
                en.task = Task.Run(() => Bvh.Parse(Sanitize(File.ReadAllText(p, Encoding.UTF8))));
            }

            foreach (var en in entries)
            {
                while (!en.task.IsCompleted) yield return null;
                if (en.task.IsFaulted || en.task.Result == null)
                {
                    Debug.LogWarning("[Tobe] BVH parse failed for " + en.name + ": " + (en.task.Exception != null ? en.task.Exception.GetBaseException().Message : "null"));
                    continue;
                }
                yield return Bake(en, en.task.Result);
                yield return null;
            }
            MotionLibrary.Finish();
            Destroy(gameObject);
        }

        // ---------------------------------------------------------------- discovery
        List<Entry> Discover()
        {
            var list = new List<Entry>();
            string dir = MotionLibrary.Dir;
            if (!Directory.Exists(dir)) return list;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string jsonPath = Path.Combine(dir, "motions.json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    var root = MiniJson.Parse(File.ReadAllText(jsonPath, Encoding.UTF8));
                    foreach (var obj in EnumerateObjects(root))
                    {
                        string name = Str(obj, "name");
                        string file = Str(obj, "file");
                        if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(file)) name = Path.GetFileNameWithoutExtension(file);
                        if (string.IsNullOrEmpty(name)) continue;
                        if (string.IsNullOrEmpty(file)) file = name + ".bvh";
                        string p = Path.Combine(dir, file);
                        if (!File.Exists(p)) { p = Path.Combine(dir, name + ".bvh"); if (!File.Exists(p)) continue; }
                        if (!seen.Add(name)) continue;
                        list.Add(new Entry
                        {
                            name = name, path = p,
                            loop = obj.TryGetValue("loop", out var lv) && lv is bool lb ? lb : LoopNames.Contains(name),
                            jsonFps = (float)Num(obj, "fps"), jsonSpeed = (float)Num(obj, "speed"), jsonFrames = (int)Num(obj, "frames")
                        });
                    }
                }
                catch (Exception e) { Debug.LogWarning("[Tobe] motions.json unreadable (" + e.Message + "), scanning folder instead"); }
            }
            // folder scan: known names that the json did not list (or no json at all)
            string[] files = new string[0];
            try { files = Directory.GetFiles(dir, "*.bvh", SearchOption.TopDirectoryOnly); } catch { }
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (var f in files)
            {
                string n = Path.GetFileNameWithoutExtension(f);
                if (!seen.Add(n)) continue;
                list.Add(new Entry { name = n, path = f, loop = LoopNames.Contains(n) });
            }
            // priority order so the first clips needed (idle, run) are ready first
            list.Sort((a, b) => Prio(a.name).CompareTo(Prio(b.name)));
            return list;
        }

        static int Prio(string n) { int i = Array.IndexOf(Known, n); return i < 0 ? 100 : i; }

        static IEnumerable<Dictionary<string, object>> EnumerateObjects(object node)
        {
            if (node is List<object> l)
            {
                foreach (var x in l) foreach (var o in EnumerateObjects(x)) yield return o;
            }
            else if (node is Dictionary<string, object> d)
            {
                if (d.ContainsKey("name") || d.ContainsKey("file")) { yield return d; yield break; }
                foreach (var kv in d)
                {
                    if (kv.Value is Dictionary<string, object> inner && !inner.ContainsKey("name") && !inner.ContainsKey("file"))
                    {   // { "idle": { "file": ... } } style map keyed by name -> only if it looks like a clip entry
                        if (inner.ContainsKey("fps") || inner.ContainsKey("loop") || inner.ContainsKey("frames") || inner.ContainsKey("speed"))
                        { inner["name"] = kv.Key; yield return inner; continue; }
                    }
                    foreach (var o in EnumerateObjects(kv.Value))
                    {
                        if (!o.ContainsKey("name")) o["name"] = kv.Key;
                        yield return o;
                    }
                }
            }
        }
        static string Str(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is string s ? s : null;
        static double Num(Dictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is double n ? n : 0.0;

        /// <summary>UniHumanoid's BVH parser compares whole lines ("HIERARCHY", "MOTION"): strip BOM / trailing blanks / CR.</summary>
        static string Sanitize(string text)
        {
            int idx = text.IndexOf("HIERARCHY", StringComparison.Ordinal);
            if (idx > 0) text = text.Substring(idx);
            var sb = new StringBuilder(text.Length);
            int start = 0;
            for (int i = 0; i <= text.Length; i++)
            {
                if (i == text.Length || text[i] == '\n')
                {
                    int e = i;
                    while (e > start && (text[e - 1] == '\r' || text[e - 1] == ' ' || text[e - 1] == '\t')) e--;
                    int s = start;
                    while (s < e && (text[s] == ' ' || text[s] == '\t')) s++;
                    if (e > s) sb.Append(text, s, e - s);
                    sb.Append('\n');
                    start = i + 1;
                }
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- bake
        IEnumerator Bake(Entry en, Bvh bvh)
        {
            RigCtx ctx = null;
            HumanPoseHandler handler = null;
            MotionClip clip = null;
            bool ok = true;
            try
            {
                ctx = BuildRig(en, bvh);
                if (ctx.Avatar == null || !ctx.Avatar.isValid || !ctx.Avatar.isHuman) throw new Exception("BVH skeleton did not produce a valid humanoid avatar");
                handler = new HumanPoseHandler(ctx.Avatar, ctx.Root.transform);
            }
            catch (Exception e) { Debug.LogWarning("[Tobe] BVH import failed for " + en.name + ": " + e.Message); ok = false; }
            yield return null;

            if (ok)
            {
                // ---- sample all frames (time-sliced)
                int mc = MotionLibrary.MC;
                float frameTime = (float)bvh.FrameTime.TotalSeconds;
                float srcFps = frameTime > 1e-5f ? 1f / frameTime : (en.jsonFps > 0 ? en.jsonFps : 30f);
                int srcFrames = bvh.FrameCount;
                if (en.jsonFrames > 1 && en.jsonFrames < srcFrames) srcFrames = en.jsonFrames;
                if (srcFrames < 2) { Debug.LogWarning("[Tobe] BVH " + en.name + " has too few frames"); ok = false; }
                if (ok)
                {
                    float outFps = Mathf.Min(srcFps, 60f);
                    float srcDur = (srcFrames - 1) / srcFps;
                    int n = Mathf.Max(2, Mathf.FloorToInt(srcDur * outFps) + 1);
                    var mus = new float[n * mc];
                    var bp = new Vector3[n]; var br = new Quaternion[n]; var hips = new Vector3[n];
                    Transform hipT = ctx.Root.transform.childCount > 0 ? ctx.Root.transform.GetChild(0) : ctx.Root.transform;
                    var pose = new HumanPose();
                    int k = 0;
                    // one time slice: sample frames until the per-frame budget is used (no yield inside: see try/catch)
                    bool Slice()
                    {
                        try
                        {
                            sw.Restart();
                            while (k < n)
                            {
                                float t = Mathf.Min(k / outFps, srcDur);
                                ctx.Animation.SampleAnimation(ctx.Root, t);
                                handler.GetHumanPose(ref pose);
                                for (int j = 0; j < mc; j++) { float v = pose.muscles[j]; mus[k * mc + j] = float.IsNaN(v) || float.IsInfinity(v) ? 0f : v; }
                                bp[k] = pose.bodyPosition; br[k] = pose.bodyRotation; hips[k] = hipT.position;
                                k++;
                                if (sw.Elapsed.TotalMilliseconds > BudgetMs) break;
                            }
                            return true;
                        }
                        catch (Exception e) { Debug.LogWarning("[Tobe] BVH sampling failed for " + en.name + ": " + e.Message); return false; }
                    }
                    while (ok && k < n)
                    {
                        ok = Slice();
                        if (ok && k < n) yield return null;
                    }
                    if (ok)
                    {
                        try { clip = PostProcess(en, mus, bp, br, hips, n, outFps); }
                        catch (Exception e) { Debug.LogWarning("[Tobe] Motion post-process failed for " + en.name + ": " + e.Message); }
                    }
                }
            }

            // ---- throw the temporary skeleton away
            try
            {
                if (handler != null) handler.Dispose();
                if (ctx != null)
                {
                    var av = ctx.Avatar; var anim = ctx.Animation; var desc = ctx.AvatarDescription;
                    if (ctx.Root != null) DestroyImmediate(ctx.Root);
                    if (av != null) Destroy(av);
                    if (anim != null) Destroy(anim);
                    if (desc != null) Destroy(desc);
                }
            }
            catch (Exception e) { Debug.LogWarning("[Tobe] BVH cleanup: " + e.Message); }

            if (clip != null) MotionLibrary.Register(clip);
        }

        sealed class RigCtx
        {
            public GameObject Root; public AnimationClip Animation; public Avatar Avatar; public ScriptableObject AvatarDescription;
        }

        // CMU joint name -> humanoid bone. Zero-offset helper joints (LHipJoint, LowerBack, Neck1) stay unmapped; UniHumanoid's
        // skeleton estimator is confused by them, so the avatar is built from this explicit map.
        static readonly Dictionary<string, HumanBodyBones> CmuMap = new Dictionary<string, HumanBodyBones>(StringComparer.OrdinalIgnoreCase)
        {
            { "Hips", HumanBodyBones.Hips }, { "Spine", HumanBodyBones.Spine }, { "Spine1", HumanBodyBones.Chest },
            { "Neck", HumanBodyBones.Neck }, { "Head", HumanBodyBones.Head },
            { "LeftUpLeg", HumanBodyBones.LeftUpperLeg }, { "LeftLeg", HumanBodyBones.LeftLowerLeg }, { "LeftFoot", HumanBodyBones.LeftFoot }, { "LeftToeBase", HumanBodyBones.LeftToes },
            { "RightUpLeg", HumanBodyBones.RightUpperLeg }, { "RightLeg", HumanBodyBones.RightLowerLeg }, { "RightFoot", HumanBodyBones.RightFoot }, { "RightToeBase", HumanBodyBones.RightToes },
            { "LeftShoulder", HumanBodyBones.LeftShoulder }, { "LeftArm", HumanBodyBones.LeftUpperArm }, { "LeftForeArm", HumanBodyBones.LeftLowerArm }, { "LeftHand", HumanBodyBones.LeftHand },
            { "RightShoulder", HumanBodyBones.RightShoulder }, { "RightArm", HumanBodyBones.RightUpperArm }, { "RightForeArm", HumanBodyBones.RightLowerArm }, { "RightHand", HumanBodyBones.RightHand },
        };

        /// <summary>Builds the BVH skeleton (hips normalised to 1 m), a legacy AnimationClip (quaternion curves, no Euler interpolation)
        /// and a humanoid Avatar from the explicit name map; falls back to UniHumanoid's estimating importer for unknown skeletons.</summary>
        static RigCtx BuildRig(Entry en, Bvh bvh)
        {
            var rig = new RigCtx();
            var root = new GameObject("bvh_" + en.name);
            try
            {
                var hips = BuildNode(root.transform, bvh.Root);
                int yCh = bvh.Root.GetChannelIndex(BvhChannel.Yposition);
                float hipHeight = bvh.Channels[yCh].Keys[0];
                if (Mathf.Abs(hipHeight) < 1e-4f) throw new Exception("zero hip height");
                float scaling = 1f / hipHeight;
                foreach (var t in root.transform.Traverse()) t.localPosition *= scaling;
                hips.position = new Vector3(0f, 1f, 0f);

                rig.Animation = BvhAnimation.CreateAnimationClip(bvh, scaling);
                rig.Animation.name = root.name;
                rig.Animation.legacy = true;
                rig.Animation.wrapMode = WrapMode.Loop;

                UniGLTF.Utils.ForceTransformUniqueName.Process(root.transform);
                var map = new List<(Transform, HumanBodyBones)>();
                foreach (var t in root.GetComponentsInChildren<Transform>())
                    if (CmuMap.TryGetValue(t.name, out var hb)) map.Add((t, hb));
                if (map.Count < 15) throw new Exception("not a CMU skeleton (" + map.Count + " mapped joints)");
                rig.Avatar = HumanoidLoader.BuildHumanAvatarFromMap(root.transform, map);
                rig.Avatar.name = "bvh_avatar";
                rig.Root = root;
                return rig;
            }
            catch (Exception e)
            {
                Debug.Log("[Tobe] explicit BVH rig failed for " + en.name + " (" + e.Message + "), trying UniHumanoid estimation");
                DestroyImmediate(root);
                if (rig.Animation != null) Destroy(rig.Animation);
                var ctx = new BvhImporterContext { Path = en.path, Source = "", Bvh = bvh };
                ctx.Load();
                var an = ctx.Root.GetComponent<Animation>(); if (an != null) { an.Stop(); DestroyImmediate(an); }
                var hpt = ctx.Root.GetComponent<HumanPoseTransfer>(); if (hpt != null) DestroyImmediate(hpt);
                return new RigCtx { Root = ctx.Root, Animation = ctx.Animation, Avatar = ctx.Avatar, AvatarDescription = ctx.AvatarDescription };
            }
        }

        static Transform BuildNode(Transform parent, BvhNode node)
        {
            var go = new GameObject(node.Name);
            go.transform.localPosition = node.Offset.ToXReversedVector3();
            go.transform.SetParent(parent, false);
            foreach (var c in node.Children) BuildNode(go.transform, c);
            return go.transform;
        }

        // ---------------------------------------------------------------- post-processing
        MotionClip PostProcess(Entry en, float[] mus, Vector3[] bp, Quaternion[] br, Vector3[] hips, int n, float fps)
        {
            int mc = MotionLibrary.MC;

            // 1) facing normalisation: rotate everything so the character looks along +Z
            float yaw0;
            {
                Vector2 acc = Vector2.zero;
                int cnt = en.loop ? n : Mathf.Min(n, 4);
                for (int i = 0; i < cnt; i++) { var f = br[i] * Vector3.forward; acc += new Vector2(f.x, f.z); }
                yaw0 = acc.sqrMagnitude > 1e-6f ? Mathf.Atan2(acc.x, acc.y) * Mathf.Rad2Deg : 0f;
            }
            var qInv = Quaternion.Euler(0f, -yaw0, 0f);
            for (int i = 0; i < n; i++)
            {
                br[i] = qInv * br[i];
                bp[i] = qInv * bp[i];
                hips[i] = qInv * hips[i];
            }

            // 2) loop segment (stride cycle for locomotion, whole clip for the rest)
            int a = 0, b = n;               // [a, b) used frames; b may be extended by crossfade tail
            bool loco = LocoNames.Contains(en.name);
            if (en.loop && loco)
            {
                FindCycle(mus, n, mc, fps, out a, out b);
            }
            else if (en.loop)
            {
                a = 0; b = n;
            }
            else { a = 0; b = n; }

            // 3) remove horizontal travel (position comes from the network, not from the clip): linear trend over the segment
            Vector3 travelBody = en.loop ? (b < n ? bp[b] : bp[n - 1]) - bp[a] : bp[n - 1] - bp[0];
            float segFrames = en.loop ? (b < n ? b - a : n - 1 - a) : n - 1;
            Vector3 trend = segFrames > 0 ? new Vector3(travelBody.x, 0f, travelBody.z) / segFrames : Vector3.zero;
            for (int i = 0; i < n; i++) { bp[i] -= trend * (i - a); }

            // 4) root velocity from the hip trajectory (world metres in a 1 m-hip skeleton -> character scale)
            Vector3 hd; float dur;
            if (en.loop && b - a >= 2 && b < n) { hd = hips[b] - hips[a]; dur = (b - a) / fps; }
            else if (en.loop && b - a >= 2) { hd = hips[n - 1] - hips[a]; dur = (n - 1 - a) / fps; }
            else { hd = hips[n - 1] - hips[0]; dur = (n - 1) / fps; }
            Vector2 rv = dur > 0.05f ? new Vector2(hd.x, hd.z) * (HipScale / dur) : Vector2.zero;
            float spd = rv.magnitude;
            if (spd < 0.1f && en.jsonSpeed > 0.1f && loco) { spd = en.jsonSpeed; rv = new Vector2(0f, spd); }
            if (!loco && en.loop) { rv = Vector2.zero; spd = 0f; }   // idle/ready/celebrate stay in place

            // 5) assemble the final arrays
            var clip = new MotionClip { name = en.name, loop = en.loop, fps = fps, rootVel = rv, speed = spd };
            if (en.loop)
            {
                int L, K;
                if (loco && b < n && b - a >= 4)
                {
                    L = b - a; K = Mathf.Clamp(L / 8, 2, 5);
                }
                else
                {
                    // whole clip: trim the tail and blend it into the head so the loop is seamless
                    K = Mathf.Clamp(Mathf.RoundToInt(fps * 0.3f), 2, n / 3);
                    L = n - K; a = 0;
                }
                if (L < 2) { L = n; K = 0; a = 0; }
                clip.frames = L; clip.fps = fps; clip.duration = L / fps;
                clip.muscles = new float[L * mc]; clip.bodyPos = new Vector3[L]; clip.bodyRot = new Quaternion[L];
                for (int j = 0; j < L; j++)
                {
                    int si = a + j;
                    int ti = loco ? a + (b - a) + j : L + j;        // continuation of the cycle after its end
                    float w = (K > 0 && j < K && ti < n) ? (float)j / K : 1f;
                    int s2 = Mathf.Min(si, n - 1);
                    if (w >= 1f)
                    {
                        Array.Copy(mus, s2 * mc, clip.muscles, j * mc, mc);
                        clip.bodyPos[j] = bp[s2]; clip.bodyRot[j] = br[s2];
                    }
                    else
                    {
                        // out[j] = lerp(tail continuation, head, w): continuous at the wrap point, joins the head after K frames
                        for (int m = 0; m < mc; m++) clip.muscles[j * mc + m] = Mathf.Lerp(mus[ti * mc + m], mus[s2 * mc + m], w);
                        clip.bodyPos[j] = Vector3.Lerp(bp[ti], bp[s2], w);
                        clip.bodyRot[j] = Quaternion.Slerp(br[ti], br[s2], w);
                    }
                }
            }
            else
            {
                clip.frames = n; clip.duration = (n - 1) / fps;
                clip.muscles = mus; clip.bodyPos = bp; clip.bodyRot = br;
            }

            // 6) jump analysis on the baked hip height
            AnalyseJump(clip);
            return clip;
        }

        /// <summary>Finds one stride cycle: from one "left upper leg most forward" peak to the next, choosing the pair whose poses match best.</summary>
        static void FindCycle(float[] mus, int n, int mc, float fps, out int a, out int b)
        {
            a = 0; b = n;
            int idx = MotionLibrary.MuscleIndex("Left Upper Leg Front-Back");
            if (idx < 0 || n < 8) return;
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0; int c = 0;
                for (int d = -2; d <= 2; d++) { int q = i + d; if (q < 0 || q >= n) continue; sum += mus[q * mc + idx]; c++; }
                s[i] = sum / c;
            }
            float mn = float.MaxValue, mx = float.MinValue;
            for (int i = 0; i < n; i++) { mn = Mathf.Min(mn, s[i]); mx = Mathf.Max(mx, s[i]); }
            if (mx - mn < 0.12f) return;
            float thr = mn + 0.6f * (mx - mn);
            int sep = Mathf.Max(2, Mathf.RoundToInt(fps * 0.3f));
            var peaks = new List<int>();
            for (int i = 1; i < n - 1; i++)
            {
                if (s[i] < thr || s[i] < s[i - 1] || s[i] <= s[i + 1]) continue;
                if (peaks.Count > 0 && i - peaks[peaks.Count - 1] < sep)
                {
                    if (s[i] > s[peaks[peaks.Count - 1]]) peaks[peaks.Count - 1] = i;
                    continue;
                }
                peaks.Add(i);
            }
            if (peaks.Count < 2) return;
            float best = float.MaxValue; int bi = -1;
            for (int i = 0; i + 1 < peaks.Count; i++)
            {
                int L = peaks[i + 1] - peaks[i];
                if (L < fps * 0.3f || L > fps * 2.2f) continue;
                float err = 0f;
                for (int m = 0; m < mc; m++) err += Mathf.Abs(mus[peaks[i] * mc + m] - mus[peaks[i + 1] * mc + m]);
                if (err < best) { best = err; bi = i; }
            }
            if (bi < 0) return;
            a = peaks[bi]; b = peaks[bi + 1];
        }

        static void AnalyseJump(MotionClip c)
        {
            int n = c.frames;
            int apex = 0; float hi = float.MinValue;
            for (int i = 0; i < n; i++) if (c.bodyPos[i].y > hi) { hi = c.bodyPos[i].y; apex = i; }
            int low = 0; float lo = float.MaxValue;
            for (int i = 0; i <= apex; i++) if (c.bodyPos[i].y < lo) { lo = c.bodyPos[i].y; low = i; }
            if (low >= apex) low = 0;
            c.apex = apex; c.low = low;
            c.launch = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(low, apex, 0.55f)), 0, Mathf.Max(0, apex));
            c.touch = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(apex, n - 1, 0.65f)), apex, n - 1);
        }
    }

    // ====================================================================================================== tiny JSON reader
    /// <summary>Minimal JSON reader (objects -> Dictionary, arrays -> List, numbers -> double). JsonUtility cannot read top-level arrays / unknown shapes.</summary>
    static class MiniJson
    {
        public static object Parse(string s)
        {
            int i = 0;
            var v = Value(s, ref i);
            return v;
        }

        static void Ws(string s, ref int i) { while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == '﻿')) i++; }

        static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end");
            char c = s[i];
            if (c == '{')
            {
                i++; var d = new Dictionary<string, object>();
                Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i);
                    string k = Str(s, ref i);
                    Ws(s, ref i); if (s[i] != ':') throw new FormatException("':' expected"); i++;
                    d[k] = Value(s, ref i);
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException("',' or '}' expected");
                }
            }
            if (c == '[')
            {
                i++; var l = new List<object>();
                Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i));
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw new FormatException("',' or ']' expected");
                }
            }
            if (c == '"') return Str(s, ref i);
            if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            int st = i;
            while (i < s.Length && ("+-0123456789.eE".IndexOf(s[i]) >= 0)) i++;
            if (st == i) throw new FormatException("bad token at " + st);
            return double.Parse(s.Substring(st, i - st), System.Globalization.CultureInfo.InvariantCulture);
        }

        static string Str(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("string expected");
            i++; var sb = new StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    switch (s[i])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'u': if (i + 4 < s.Length) { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; } break;
                        default: sb.Append(s[i]); break;
                    }
                }
                else sb.Append(s[i]);
                i++;
            }
            i++;
            return sb.ToString();
        }
    }
}
