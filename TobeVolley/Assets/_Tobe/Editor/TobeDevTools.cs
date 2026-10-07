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

        [MenuItem("Tobe/Dev/Ноги: датчик скрещивания (30 с)")]
        static void RunLegProbe()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[Tobe] Сначала войдите в Play."); return; }
            StartPractice();
            LegProbe.Begin();
            Debug.Log("[Tobe] Датчик ног запущен на 30 с.");
        }

        /// <summary>Logs/devshow.txt: «номер_игрока имя_клипа секунда» (секунда -1 = клип идёт сам). Пустой файл или «off» = выключить.</summary>
        [MenuItem("Tobe/Dev/Витрина клипа: применить Logs\\devshow.txt")]
        static void ApplyDevShow()
        {
            string path = System.IO.Path.Combine(Application.dataPath, "..", "Logs", "devshow.txt");
            string s = System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path).Trim() : "";
            var a = s.Split(new[] { ' ', '\t', '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (a.Length < 2 || a[0] == "off")
            {
                Tobe.View.PlayerAnimator.DevShowId = -1; Tobe.View.PlayerAnimator.DevShowClip = null;
                NetApi.SetPaused?.Invoke(false);
                Debug.Log("[Tobe] Витрина выключена.");
                return;
            }
            Tobe.View.PlayerAnimator.DevShowId = int.Parse(a[0]);
            Tobe.View.PlayerAnimator.DevShowClip = a[1];
            Tobe.View.PlayerAnimator.DevShowTime = a.Length > 2 ? float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture) : -1f;
            NetApi.SetPaused?.Invoke(true);
            Debug.Log($"[Tobe] Витрина: игрок {a[0]}, клип {a[1]}, время {Tobe.View.PlayerAnimator.DevShowTime}");
        }

        [MenuItem("Tobe/Dev/Инерция поз: вкл-выкл")]
        static void ToggleInertia() { Tobe.View.PlayerAnimator.InertiaOn = !Tobe.View.PlayerAnimator.InertiaOn; Debug.Log("[Tobe] Инерция поз: " + Tobe.View.PlayerAnimator.InertiaOn); }

        [MenuItem("Tobe/Dev/Звуки: что загружено")]
        static void DumpAudio()
        {
            var am = Tobe.UI.AudioManager.Instance;
            Debug.Log(am == null ? "[Tobe] AudioManager ещё не создан (войдите в Play)." : "[Tobe] Звуки:\n" + am.Describe());
        }

        [MenuItem("Tobe/Dev/Игроки: модели и материалы")]
        static void DumpPlayers()
        {
            var sb = new System.Text.StringBuilder();
            var v = GameHub.View;
            foreach (var p in v.players)
            {
                string mname = p.profile.model < GameHub.ModelNames.Length ? GameHub.ModelNames[p.profile.model] : "?";
                sb.AppendLine($"Игрок {p.id} «{p.profile.nick}» команда {p.team} модель {p.profile.model} ({mname}) кожа {p.profile.skin} волосы {p.profile.hair} позиция {p.pos.x:F2} {p.pos.y:F2} {p.pos.z:F2} поворот {p.yaw:F0}");
                var go = GameObject.Find("Player_" + p.id);
                if (go == null) { sb.AppendLine("  объект не найден"); continue; }
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer || !r.enabled) continue;
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        Texture tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : (m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null);
                        Color col = m.HasProperty("_Color") ? m.GetColor("_Color") : (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.magenta);
                        Color shade = m.HasProperty("_ShadeColor") ? m.GetColor("_ShadeColor") : Color.clear;
                        sb.AppendLine($"  {r.name} / {m.name}: шейдер {m.shader.name}, текстура {(tex != null ? tex.name : "НЕТ")}, цвет {col}, тень {shade}");
                    }
                }
            }
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "players.txt"));
            System.IO.File.WriteAllText(path, sb.ToString());
            Debug.Log("[Tobe] Игроки записаны в " + path);
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
                // muscle seam: pose change across the loop wrap vs the average pose change between neighbouring frames
                int mc = Tobe.View.MotionLibrary.MC, L = c.frames; float avg = 0f, seam = 0f;
                for (int i = 1; i < L; i++) { float s = 0f; for (int m = 0; m < mc; m++) s += Mathf.Abs(c.muscles[i * mc + m] - c.muscles[(i - 1) * mc + m]); avg += s / (L - 1); }
                for (int m = 0; m < mc; m++) seam += Mathf.Abs(c.muscles[m] - c.muscles[(L - 1) * mc + m]);
                sb.AppendLine($"{n}: loop={c.loop} frames={c.frames} dur={c.duration:F2} speed={c.speed:F2} bodyPos min={mn:F3} max={mx:F3} maxStep={maxStep:F3} wrap={wrap:F3} seam/avgStep={(avg > 1e-5f ? seam / avg : 0f):F2}");
            }
            Debug.Log("[Tobe] Clips:\n" + sb);
        }
    }
}
