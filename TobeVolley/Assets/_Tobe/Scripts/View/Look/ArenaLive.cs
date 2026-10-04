// Animates the living parts of the arena: flags, banners, the cheer squad, light shafts and the scoreboard (live score from GameHub.View).
using System.Collections.Generic;
using UnityEngine;

namespace Tobe.View
{
    public sealed class ArenaLive : MonoBehaviour
    {
        public sealed class ScoreFace
        {
            public TextMesh scoreL, scoreR, nameL, nameR, status;
            public Transform dot;
            public Vector3 dotL, dotR, dotBase;
        }

        struct Sway { public Transform t; public Quaternion baseRot; public Vector3 axis; public float amp, speed, phase; public bool flutter; }
        struct Bob { public Transform t; public Vector3 basePos; public float amp, speed, phase; }

        readonly List<Sway> sways = new List<Sway>();
        readonly List<Bob> bobs = new List<Bob>();
        readonly List<ScoreFace> faces = new List<ScoreFace>();

        int lastS0 = -1, lastS1 = -1, lastServe = -2;
        MatchPhase lastPhase = (MatchPhase)99;
        float t0;

        public void AddSway(Transform t, Vector3 axis, float ampDeg, float speed, float phase, bool flutter)
            => sways.Add(new Sway { t = t, baseRot = t.localRotation, axis = axis, amp = ampDeg, speed = speed, phase = phase, flutter = flutter });

        public void AddBob(Transform t, float amp, float speed, float phase)
            => bobs.Add(new Bob { t = t, basePos = t.localPosition, amp = amp, speed = speed, phase = phase });

        public void AddFace(ScoreFace f) => faces.Add(f);

        void Start() { t0 = Time.time; }

        void Update()
        {
            float e = Mathf.Clamp01(ArenaBuilder.Excitement);
            float tm = Time.time;
            for (int i = 0; i < sways.Count; i++)
            {
                var s = sways[i];
                if (s.t == null) continue;
                float k = 1f + e * 1.6f;
                float a = Mathf.Sin(tm * s.speed * k + s.phase) * s.amp * (0.7f + e * 0.8f);
                var q = Quaternion.AngleAxis(a, s.axis);
                if (s.flutter) q *= Quaternion.AngleAxis(Mathf.Sin(tm * s.speed * 2.3f * k + s.phase * 1.7f) * s.amp * 0.35f, Vector3.up);
                s.t.localRotation = s.baseRot * q;
            }
            for (int i = 0; i < bobs.Count; i++)
            {
                var b = bobs[i];
                if (b.t == null) continue;
                float amp = b.amp * (0.5f + e * 1.6f);
                b.t.localPosition = b.basePos + Vector3.up * (Mathf.Abs(Mathf.Sin(tm * b.speed * (1f + e) + b.phase)) * amp);
            }

            // light shaft shimmer + excitement boost
            var shaft = Env.Shaft;
            if (shaft != null)
            {
                float k = 0.88f + 0.12f * Mathf.Sin(tm * 0.7f) + e * 0.3f;
                Mats.SetColor(shaft, new Color(k, k, k, 1f));
            }
            if (ArenaBuilder.BloomFx != null) ArenaBuilder.BloomFx.intensity.Override(0.85f + e * 0.45f);

            UpdateScoreboard();
        }

        void UpdateScoreboard()
        {
            var v = GameHub.View;
            int s0 = v.score != null && v.score.Length > 0 ? v.score[0] : 0;
            int s1 = v.score != null && v.score.Length > 1 ? v.score[1] : 0;
            int srv = v.phase == MatchPhase.Lobby ? -1 : v.servingTeam;
            if (s0 != lastS0 || s1 != lastS1 || srv != lastServe || v.phase != lastPhase)
            {
                lastS0 = s0; lastS1 = s1; lastServe = srv; lastPhase = v.phase;
                string st = StatusText(v);
                foreach (var f in faces)
                {
                    if (f.scoreL != null) f.scoreL.text = s0.ToString();
                    if (f.scoreR != null) f.scoreR.text = s1.ToString();
                    if (f.status != null) f.status.text = st;
                    if (f.dot != null)
                    {
                        f.dot.gameObject.SetActive(srv >= 0);
                        if (srv >= 0) f.dot.position = f.dotBase + (srv == 0 ? f.dotL : f.dotR);
                    }
                }
            }
            // gentle pulse of the serving marker
            foreach (var f in faces)
                if (f.dot != null && f.dot.gameObject.activeSelf) f.dot.localScale = Vector3.one * (0.28f + 0.03f * Mathf.Sin((Time.time - t0) * 6f));
        }

        static string StatusText(MatchView v)
        {
            switch (v.phase)
            {
                case MatchPhase.Lobby: return "WAITING";
                case MatchPhase.Countdown: return "GET READY";
                case MatchPhase.Serve: return "SERVE";
                case MatchPhase.Rally: return "RALLY";
                case MatchPhase.Point: return "POINT!";
                case MatchPhase.End: return "GAME SET";
            }
            return "";
        }
    }
}
