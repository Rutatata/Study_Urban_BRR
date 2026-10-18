// Замер производительности (меню Tobe/Dev): 10 с матча — время кадра (среднее, 95-й и 99-й процентили), память, выделенная
// за кадр (GC), вызовы отрисовки, треугольники. Пишет Logs/perf.txt, чтобы оптимизировать по цифрам, а не наугад.
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;

namespace Tobe
{
    public sealed class PerfProbe : MonoBehaviour
    {
        const float Duration = 10f;
        ProfilerRecorder gc, draws, tris, setPass;
        readonly List<float> ms = new List<float>(2000);
        readonly List<long> alloc = new List<long>(2000);
        long drawSum, triSum, passSum; float t;

        public static void Begin() { if (FindFirstObjectByType<PerfProbe>() == null) new GameObject("PerfProbe").AddComponent<PerfProbe>(); }

        void OnEnable()
        {
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
        }
        void OnDisable() { gc.Dispose(); draws.Dispose(); tris.Dispose(); setPass.Dispose(); }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (t > 0.5f)   // первые полсекунды — разгон после старта, не считаем
            {
                ms.Add(dt * 1000f); alloc.Add(gc.LastValue);
                drawSum += draws.LastValue; triSum += tris.LastValue; passSum += setPass.LastValue;
            }
            t += dt;
            if (t < Duration + 0.5f) return;
            int n = Mathf.Max(1, ms.Count);
            var sorted = new List<float>(ms); sorted.Sort();
            var asorted = new List<long>(alloc); asorted.Sort();
            float avg = 0f; foreach (var x in ms) avg += x; avg /= n;
            long aavg = 0; foreach (var x in alloc) aavg += x; aavg /= n;
            int framesWithGc = 0; foreach (var x in alloc) if (x > 0) framesWithGc++;
            string s = $"кадров {n}, среднее {avg:0.00} мс ({1000f / avg:0} к/с), p95 {sorted[(int)(n * 0.95f)]:0.00} мс, p99 {sorted[(int)(n * 0.99f)]:0.00} мс, худший {sorted[n - 1]:0.00} мс\n" +
                       $"GC за кадр: среднее {aavg} байт, p95 {asorted[(int)(n * 0.95f)]} байт, кадров с выделением {framesWithGc}\n" +
                       $"отрисовка за кадр: draw calls {drawSum / n}, SetPass {passSum / n}, треугольников {triSum / n}\n";
            File.WriteAllText(Path.Combine(Application.dataPath, "..", "Logs", "perf.txt"), s);
            Debug.Log("[Tobe] Производительность:\n" + s);
            Destroy(gameObject);
        }
    }
}
