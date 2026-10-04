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
        static readonly HashSet<string> LoopNames = new HashSet<string> { "idle", "ready", "walk", "run", "sprint", "sidestep", "celebrate", "shuffle_left", "shuffle_right", "backpedal" };
        static readonly HashSet<string> LocoNames = new HashSet<string> { "walk", "run", "sprint", "sidestep", "shuffle_left", "shuffle_right", "backpedal" };
        /// <summary>Names a Humanoid AnimationClip in Resources/Anim may have (see AnimImportPostprocessor).</summary>
        static readonly string[] HumanoidNames =
        {
            "idle", "ready", "shuffle_left", "shuffle_right", "backpedal", "run", "sprint", "jump_vertical", "jump_approach", "land", "bump", "set",
            "spike", "block", "serve_float", "serve_jump", "dive", "celebrate", "sad", "walk"
        };
        // humanoid clips that loop (everything else is a one-shot driven by PoseId.poseT)
        static readonly HashSet<string> HumanoidLoops = new HashSet<string> { "idle", "ready", "shuffle_left", "shuffle_right", "backpedal", "run", "sprint", "walk" };

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
            // Unity Humanoid clips (Resources/Anim) are baked last: they override BVH clips and authored poses
            yield return BakeHumanoidClips();
            MotionLibrary.Finish();
            Destroy(gameObject);
        }

        // ---------------------------------------------------------------- Humanoid AnimationClips (Resources/Anim)
        /// <summary>"Model@ready" / "ready" / "Ready" -> "ready"; null when the name is not one of the known clip names.</summary>
        static string HumanoidKey(string clipName)
        {
            if (string.IsNullOrEmpty(clipName) || clipName.StartsWith("__", StringComparison.Ordinal)) return null;
            string n = clipName.Trim();
            int at = n.LastIndexOf('@');
            if (at >= 0) n = n.Substring(at + 1);
            n = n.ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
            return Array.IndexOf(HumanoidNames, n) >= 0 ? n : null;
        }

        IEnumerator BakeHumanoidClips()
        {
            var chosen = new Dictionary<string, AnimationClip>();
            try
            {
                var all = Resources.LoadAll<AnimationClip>("Anim");
                if (all != null)
                    foreach (var c in all)
                    {
                        if (c == null) continue;
                        string key = HumanoidKey(c.name);
                        if (key != null) chosen[key] = c;
                    }
            }
            catch (Exception e) { Debug.LogWarning("[Tobe] Resources/Anim load failed: " + e.Message); }
            if (chosen.Count == 0) yield break;

            HumanoidRig rig = null;
            try { rig = HumanoidRig.Build(); }
            catch (Exception e) { Debug.LogWarning("[Tobe] humanoid bake rig failed: " + e.Message); }
            if (rig == null) yield break;
            yield return null;

            foreach (var kv in chosen)
            {
                yield return BakeHumanoid(kv.Key, kv.Value, rig);
                yield return null;
            }
            try { rig.Dispose(); } catch (Exception) { }
            Debug.Log("[Tobe] Humanoid clips baked: " + chosen.Count);
        }

        IEnumerator BakeHumanoid(string name, AnimationClip src, HumanoidRig rig)
        {
            MotionClip clip = null;
            int mc = MotionLibrary.MC;
            bool ok = true;
            float srcDur = Mathf.Max(0.05f, src.length);
            float outFps = 60f;
            int n = Mathf.Max(2, Mathf.FloorToInt(srcDur * outFps) + 1);
            var mus = new float[n * mc];
            var bp = new Vector3[n]; var br = new Quaternion[n]; var hips = new Vector3[n];
            var pose = new HumanPose();
            int k = 0;
            try { rig.Bind(src); } catch (Exception e) { Debug.LogWarning("[Tobe] humanoid clip " + name + " bind failed: " + e.Message); ok = false; }
            bool Slice()
            {
                try
                {
                    sw.Restart();
                    while (k < n)
                    {
                        float t = Mathf.Min(k / outFps, srcDur - 1e-3f);   // exactly at the end Unity wraps a looping clip back to frame 0
                        rig.Sample(src, t);
                        rig.handler.GetHumanPose(ref pose);
                        for (int j = 0; j < mc; j++) { float v = pose.muscles[j]; mus[k * mc + j] = float.IsNaN(v) || float.IsInfinity(v) ? 0f : v; }
                        bp[k] = pose.bodyPosition; br[k] = pose.bodyRotation; hips[k] = rig.hips.position;
                        k++;
                        if (sw.Elapsed.TotalMilliseconds > BudgetMs) break;
                    }
                    return true;
                }
                catch (Exception e) { Debug.LogWarning("[Tobe] humanoid clip " + name + " sampling failed: " + e.Message); return false; }
            }
            while (ok && k < n)
            {
                ok = Slice();
                if (ok && k < n) yield return null;
            }
            if (ok)
            {
                try
                {
                    var en = new Entry { name = name, loop = HumanoidLoops.Contains(name), jsonFps = outFps, jsonFrames = n };
                    clip = PostProcess(en, mus, bp, br, hips, n, outFps);
                    if (clip != null) clip.humanoid = true;
                }
                catch (Exception e) { Debug.LogWarning("[Tobe] humanoid clip " + name + " post-process failed: " + e.Message); }
            }
            if (clip != null) MotionLibrary.Register(clip);
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
                                float t = Mathf.Min(k / outFps, srcDur - 0.02f / srcFps);   // exactly at the end the legacy clip wraps back to frame 0
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

            // 3) remove horizontal travel: the position comes from the simulation, the clip must play "in place" (otherwise the body runs
            //    ahead of the character and snaps back at every loop wrap = the visible teleport).
            //    Loops: least-squares linear trend over the cycle (robust, unlike two end points). One-shots (jumps, dive, celebrate ...):
            //    all horizontal travel relative to the first frame is removed, like Mixamo "In Place".
            int segEnd = en.loop ? Mathf.Min(b, n) : n;
            Vector2 slope = PlanarSlope(bp, a, segEnd);              // body units per frame
            if (en.loop)
            {
                var trend = new Vector3(slope.x, 0f, slope.y);
                for (int i = 0; i < n; i++) bp[i] -= trend * (i - a);
            }
            else
            {
                Vector3 p0 = bp[0];
                for (int i = 0; i < n; i++) { bp[i].x = p0.x; bp[i].z = p0.z; }
            }

            // 4) root velocity from the body trajectory (bodyPosition is in units of the 1 m-hip skeleton -> character scale)
            Vector2 rv = en.loop && segEnd - a >= 3 ? slope * (fps * HipScale) : Vector2.zero;
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

        /// <summary>Least-squares slope (per frame) of the horizontal body position over frames [a, end).</summary>
        static Vector2 PlanarSlope(Vector3[] p, int a, int end)
        {
            int m = end - a;
            if (m < 3) return Vector2.zero;
            float mt = (m - 1) * 0.5f, sxx = 0f; Vector2 mean = Vector2.zero, sxy = Vector2.zero;
            for (int i = 0; i < m; i++) mean += new Vector2(p[a + i].x, p[a + i].z);
            mean /= m;
            for (int i = 0; i < m; i++)
            {
                float dt = i - mt;
                sxx += dt * dt;
                sxy += dt * (new Vector2(p[a + i].x, p[a + i].z) - mean);
            }
            return sxx > 1e-6f ? sxy / sxx : Vector2.zero;
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
}
