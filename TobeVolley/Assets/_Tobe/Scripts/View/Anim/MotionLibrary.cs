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
