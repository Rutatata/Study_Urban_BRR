// Dev menu for automated checks (also driven through MCP for Unity: execute_menu_item).
using UnityEditor;
using UnityEngine;

namespace Tobe.EditorTools
{
    public static class TobeDevTools
    {
        [MenuItem("Tobe/Dev/Start Practice 3x3")]
        static void StartPractice()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[Tobe] Enter Play mode first."); return; }
            if (NetApi.StartPractice == null) { Debug.LogWarning("[Tobe] NetApi.StartPractice is not ready yet."); return; }
            if (!NetApi.InSession) NetApi.StartPractice(3);
        }

        [MenuItem("Tobe/Dev/Run Movement Probe")]
        static void RunProbe()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[Tobe] Enter Play mode first."); return; }
            StartPractice();
            DevProbe.Begin();
            Debug.Log("[Tobe] DevProbe started: waiting for the match, then WASD script (~9 s).");
        }

        [MenuItem("Tobe/Dev/Dump Motion Clips")]
        static void DumpClips()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var n in new[] { "idle", "ready", "walk", "run", "sprint", "sidestep", "jump_vertical", "jump_approach", "jump_block", "jump_run", "land", "dive", "celebrate", "sad", "high_five", "throw_overhead" })
            {
                var c = Tobe.View.MotionLibrary.Get(n);
                if (c == null) { sb.AppendLine(n + ": -"); continue; }
                Vector3 mn = Vector3.positiveInfinity, mx = Vector3.negativeInfinity; float maxStep = 0f;
                for (int i = 0; i < c.bodyPos.Length; i++)
                {
                    mn = Vector3.Min(mn, c.bodyPos[i]); mx = Vector3.Max(mx, c.bodyPos[i]);
                    if (i > 0) maxStep = Mathf.Max(maxStep, Vector3.Distance(c.bodyPos[i], c.bodyPos[i - 1]));
                }
                float wrap = c.bodyPos.Length > 1 ? Vector3.Distance(c.bodyPos[0], c.bodyPos[c.bodyPos.Length - 1]) : 0f;
                sb.AppendLine($"{n}: loop={c.loop} humanoid={c.humanoid} frames={c.frames} fps={c.fps} dur={c.duration:F2} speed={c.speed:F2} bodyPos min={mn:F3} max={mx:F3} maxStep={maxStep:F3} wrap={wrap:F3}");
            }
            Debug.Log("[Tobe] Clips:\n" + sb);
        }
    }
}
