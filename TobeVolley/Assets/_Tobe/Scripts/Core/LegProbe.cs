// Датчик для разработки (меню Tobe/Dev): 30 секунд следит за ногами всех игроков и пишет в Logs/legs.csv,
// насколько разведены стопы поперёк тела. Отрицательное значение = левая стопа правее правой = ноги скрещены.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Tobe
{
    [DefaultExecutionOrder(30000)]   // после аниматора: читаем итоговую позу кадра
    public sealed class LegProbe : MonoBehaviour
    {
        const float Duration = 30f;
        sealed class Legs { public Transform ulL, ulR, fL, fR; }
        readonly Dictionary<int, Legs> legs = new Dictionary<int, Legs>();
        readonly StringBuilder sb = new StringBuilder(1 << 18);
        float t;

        public static void Begin()
        {
            var old = FindFirstObjectByType<LegProbe>();
            if (old != null) Destroy(old.gameObject);
            new GameObject("LegProbe").AddComponent<LegProbe>();
        }

        void Awake() { sb.AppendLine("t,id,team,bot,phase,pose,air,speed,sep,width"); }

        void LateUpdate()
        {
            var v = GameHub.View;
            if (v.phase == MatchPhase.Lobby) return;
            var c = CultureInfo.InvariantCulture;
            foreach (var p in v.players)
            {
                if (!legs.TryGetValue(p.id, out var l) || l.fL == null)
                {
                    var go = GameObject.Find("Player_" + p.id);
                    var an = go != null ? go.GetComponentInChildren<Animator>() : null;
                    if (an == null || !an.isHuman) continue;
                    l = new Legs
                    {
                        ulL = an.GetBoneTransform(HumanBodyBones.LeftUpperLeg), ulR = an.GetBoneTransform(HumanBodyBones.RightUpperLeg),
                        fL = an.GetBoneTransform(HumanBodyBones.LeftFoot), fR = an.GetBoneTransform(HumanBodyBones.RightFoot)
                    };
                    legs[p.id] = l;
                }
                if (l.ulL == null || l.ulR == null || l.fL == null || l.fR == null) continue;
                // ось «вправо» тела = от левого бедра к правому (по горизонтали)
                Vector3 right = l.ulR.position - l.ulL.position; right.y = 0f;
                float hipW = right.magnitude;
                if (hipW < 1e-4f) continue;
                right /= hipW;
                Vector3 d = l.fR.position - l.fL.position; d.y = 0f;
                float sep = Vector3.Dot(d, right);          // поперечное расстояние между стопами (м), < 0 = скрещены
                float sp = new Vector2(p.vel.x, p.vel.z).magnitude;
                sb.Append(t.ToString("F3", c)).Append(',').Append(p.id).Append(',').Append(p.team).Append(',').Append(p.isBot ? 1 : 0).Append(',')
                  .Append(v.phase).Append(',').Append(p.pose).Append(',').Append(p.air ? 1 : 0).Append(',').Append(sp.ToString("F2", c)).Append(',')
                  .Append(sep.ToString("F3", c)).Append(',').Append(hipW.ToString("F3", c)).AppendLine();
            }
            t += Time.deltaTime;
            if (t >= Duration) Finish();
        }

        void Finish()
        {
            string dir = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(dir);
            string path = Path.GetFullPath(Path.Combine(dir, "legs.csv"));
            File.WriteAllText(path, sb.ToString());
            Debug.Log("[Tobe] Датчик ног записан: " + path);
            Destroy(gameObject);
        }
    }
}
