using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tobe.View
{
    /// <summary>Makes the crowd chunks bounce; excitement (goals) boosts the amplitude.</summary>
    public sealed class CrowdAnimator : MonoBehaviour
    {
        struct Chunk { public Transform t; public float phase, speed; }
        readonly List<Chunk> chunks = new List<Chunk>();

        public void Register(Transform t, float phase, float speed) => chunks.Add(new Chunk { t = t, phase = phase, speed = speed });

        void Update()
        {
            float e = ArenaBuilder.Excitement;
            ArenaBuilder.Cheer(-Time.deltaTime * 0.3f);
            float amp = 0.015f + 0.16f * Mathf.Clamp01(e);
            float freq = 1.6f + 4.5f * Mathf.Clamp01(e);
            float tm = Time.time;
            for (int i = 0; i < chunks.Count; i++)
            {
                var c = chunks[i];
                if (c.t == null) continue;
                float y = Mathf.Abs(Mathf.Sin(tm * freq * c.speed + c.phase)) * amp;
                c.t.localPosition = new Vector3(0, y, 0);
            }
        }
    }
}
