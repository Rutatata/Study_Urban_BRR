// Ball mesh, trail, finisher glow, blob shadow, landing marker and the local player's plan marker.
using UnityEngine;

namespace Tobe.View
{
    public sealed class BallView : MonoBehaviour
    {
        const float VisualScale = 0.11f * 2f * 1.25f;

        Transform ball;
        TrailRenderer trail;
        Light glowLight;
        ParticleSystem glowPs;
        ParticleSystem.EmissionModule glowEm;
        Transform shadow; Material shadowMat;
        Transform landRing, landDisc; Material landRingMat, landDiscMat;
        readonly Transform[] planRing = new Transform[2];
        readonly Material[] planMat = new Material[2];
        readonly LineRenderer[] beam = new LineRenderer[2];
        Material beamMat;

        Vector3 smPos;
        bool inited;
        Material trailMat;

        public static BallView Create(Transform parent)
        {
            var go = new GameObject("BallView");
            go.transform.SetParent(parent, false);
            return go.AddComponent<BallView>();
        }

        static Texture2D BallTexture() => Mats.MakeTex("ball", 512, 256, (u, v) =>
        {
            if (v < 0.1f || v > 0.9f) return new Color(0.95f, 0.95f, 0.95f, 1f);
            float t = u * 6f + (v - 0.5f) * 0.9f;
            t -= Mathf.Floor(t);
            float seg = t * 3f; seg -= Mathf.Floor(seg);
            Color c = t < 0.3333f ? new Color(1f, 0.82f, 0.1f) : (t < 0.6667f ? new Color(0.96f, 0.96f, 0.96f) : new Color(0.1f, 0.35f, 0.85f));
            if (seg < 0.04f || seg > 0.97f) c *= 0.35f;
            return c;
        }, true, TextureWrapMode.Repeat);

        void Awake()
        {
            var sphere = Mats.Prim(PrimitiveType.Sphere, transform, "Ball", Vector3.zero, Vector3.one * VisualScale,
                Mats.Lit("ball", Color.white, 0.45f, 0f, null, BallTexture()), true);
            ball = sphere.transform;

            // trail
            trail = sphere.AddComponent<TrailRenderer>();
            trailMat = Mats.Particle("ball_trail", Mats.SquareTex, true);
            trail.sharedMaterial = trailMat;
            trail.time = 0.28f;
            trail.widthMultiplier = 0.2f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.minVertexDistance = 0.05f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;

            // glow
            var lg = new GameObject("GlowLight");
            lg.transform.SetParent(ball, false);
            glowLight = lg.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.range = 6f; glowLight.intensity = 0f; glowLight.shadows = LightShadows.None;
            glowPs = FxManager.MakeSystem(transform, "BallGlow", Mats.Particle("ball_glow", Mats.SoftDot, true), 200, 0.6f, 0.2f, 0f);
            var main = glowPs.main; main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.2f);
            glowEm = glowPs.emission; glowEm.enabled = false; glowEm.rateOverTime = 60f;
            var sh = glowPs.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.12f;

            // blob shadow
            shadowMat = Mats.Instance(Mats.Unlit("ball_shadow", Color.black, Mats.SoftDot, true, false));
            shadow = Mats.FloorQuad("BallShadow", transform, Vector3.zero, 1f, 1f, shadowMat).transform;

            // landing marker
            landRingMat = Mats.Instance(Mats.Unlit("land_ring", Color.white, Mats.RingTex, true, true));
            landDiscMat = Mats.Instance(Mats.Unlit("land_disc", Color.white, Mats.DiscTex, true, true));
            landDisc = Mats.FloorQuad("LandDisc", transform, Vector3.zero, 1f, 1f, landDiscMat).transform;
            landRing = Mats.FloorQuad("LandRing", transform, Vector3.zero, 1f, 1f, landRingMat).transform;
            landDisc.gameObject.SetActive(false); landRing.gameObject.SetActive(false);

            // plan markers
            var planBase = Mats.Unlit("plan_ring", Color.white, Mats.RingTex, true, true);
            beamMat = Mats.Instance(Mats.Particle("plan_beam", Mats.DashTex, true));
            beamMat.mainTextureScale = new Vector2(5f, 1f);
            for (int i = 0; i < 2; i++)
            {
                planMat[i] = Mats.Instance(planBase);
                planRing[i] = Mats.FloorQuad("PlanRing" + i, transform, Vector3.zero, 1f, 1f, planMat[i]).transform;
                planRing[i].gameObject.SetActive(false);
                var go = new GameObject("PlanBeam" + i);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.useWorldSpace = true;
                lr.widthMultiplier = 0.07f;
                lr.textureMode = LineTextureMode.Tile;
                lr.sharedMaterial = beamMat;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.startColor = lr.endColor = new Color(1f, 0.85f, 0.25f, 1f);
                lr.enabled = false;
                beam[i] = lr;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var view = GameHub.View;
            var b = view.ball;
            bool vis = b.live || b.held || b.pos.sqrMagnitude > 0.01f;
            ball.gameObject.SetActive(vis);
            shadow.gameObject.SetActive(vis);
            if (vis) UpdateBall(b, dt, view); else { trail.emitting = false; glowLight.intensity = 0f; glowEm.enabled = false; }
            UpdateLanding(view);
            UpdatePlans(view);
        }

        void UpdateBall(BallSnap b, float dt, MatchView view)
        {
            if (!inited || (smPos - b.pos).sqrMagnitude > 9f)
            {
                smPos = b.pos; inited = true; trail.Clear();
            }
            smPos = Vector3.Lerp(smPos, b.pos, 1f - Mathf.Exp(-dt * 28f));
            ball.position = smPos;

            // spin
            Vector3 hv = new Vector3(b.vel.x, 0f, b.vel.z);
            float sp = b.vel.magnitude;
            if (sp > 0.2f)
            {
                Vector3 axis = Vector3.Cross(Vector3.up, hv.sqrMagnitude > 0.01f ? hv : b.vel);
                if (axis.sqrMagnitude > 1e-6f)
                    ball.Rotate(axis.normalized, Mathf.Min(sp, 30f) / 0.14f * Mathf.Rad2Deg * 0.5f * dt, Space.World);
            }

            // finisher / perfect glow
            bool fin = b.superBy != 255;
            Color c1 = Color.white, c2 = new Color(1f, 0.85f, 0.3f);
            float power = 0f;
            if (fin)
            {
                if (view.TryGet(b.superBy, out var hitter)) { var def = Styles.Get(hitter.profile.style); c1 = def.c1; c2 = def.c2; }
                else { c1 = new Color(1f, 0.5f, 0.2f); c2 = Color.white; }
                power = 1f;
            }
            else if (b.mini) power = 0.45f;

            bool fast = sp > 9f;
            trail.emitting = fast || power > 0f;
            if (trail.emitting)
            {
                trail.widthMultiplier = 0.2f + power * 0.2f;
                trail.time = 0.28f + power * 0.25f;
                if (power > 0f)
                {
                    trail.startColor = new Color(c1.r * 3f, c1.g * 3f, c1.b * 3f, 0.9f);
                    trail.endColor = new Color(c2.r, c2.g, c2.b, 0f);
                }
                else
                {
                    trail.startColor = new Color(1f, 1f, 1f, 0.45f);
                    trail.endColor = new Color(1f, 1f, 1f, 0f);
                }
            }

            glowLight.color = c1;
            glowLight.range = fin ? 6f : 3f;
            glowLight.intensity = power * (fin ? 3f : 1.5f) * (0.85f + 0.15f * Mathf.Sin(Time.time * 20f));
            glowEm.enabled = power > 0f;
            if (power > 0f)
            {
                var main = glowPs.main;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(c1.r * 2.5f, c1.g * 2.5f, c1.b * 2.5f, 1f), new Color(c2.r * 2f, c2.g * 2f, c2.b * 2f, 1f));
                main.startSize = new ParticleSystem.MinMaxCurve(fin ? 0.12f : 0.07f, fin ? 0.3f : 0.15f);
                glowEm.rateOverTime = fin ? 70f : 30f;
            }
            glowPs.transform.position = smPos;

            // blob shadow
            float h = Mathf.Max(0f, smPos.y);
            float size = Mathf.Clamp(0.32f + h * 0.07f, 0.32f, 0.9f);
            shadow.position = new Vector3(smPos.x, 0.015f, smPos.z);
            shadow.localScale = new Vector3(size, size, 1f);
            Mats.SetColor(shadowMat, new Color(0f, 0f, 0f, 0.6f / (1f + h * 0.35f)));
        }

        void UpdateLanding(MatchView view)
        {
            bool on = view.landingValid && (view.ball.live || view.ball.held);
            landRing.gameObject.SetActive(on);
            landDisc.gameObject.SetActive(on);
            if (!on) return;
            bool danger = false;
            if (view.TryGetLocal(out var lp)) danger = Court.OnSide(lp.team, view.landing.z);
            Color c = danger ? new Color(1f, 0.15f, 0.15f) : new Color(0.1f, 0.9f, 1f);
            float s = 1.5f + 0.15f * Mathf.Sin(Time.time * 8f);
            landRing.position = new Vector3(view.landing.x, 0.035f, view.landing.z);
            landDisc.position = new Vector3(view.landing.x, 0.03f, view.landing.z);
            landRing.localScale = new Vector3(s, s, 1f);
            landDisc.localScale = new Vector3(s * 0.8f, s * 0.8f, 1f);
            Mats.SetColor(landRingMat, new Color(c.r * 2f, c.g * 2f, c.b * 2f, 1f));
            Mats.SetColor(landDiscMat, new Color(c.r, c.g, c.b, 0.22f));
        }

        void UpdatePlans(MatchView view)
        {
            for (int i = 0; i < 2; i++)
            {
                bool on = false;
                if (view.plans != null && i < view.plans.Length)
                {
                    var pl = view.plans[i];
                    if (pl.kind != PlanKind.None && pl.playerId == view.localPlayerId && view.localPlayerId >= 0)
                    {
                        on = true;
                        float s = 0.55f + Mathf.Clamp01(pl.timeLeft) * 0.9f;
                        planRing[i].position = new Vector3(pl.point.x, 0.04f, pl.point.z);
                        planRing[i].localScale = new Vector3(s, s, 1f);
                        Mats.SetColor(planMat[i], new Color(1f, 0.82f, 0.2f) * 2.2f);
                        bool atk = pl.kind == PlanKind.Attack;
                        beam[i].enabled = atk;
                        if (atk)
                        {
                            beam[i].SetPosition(0, new Vector3(pl.point.x, 0.05f, pl.point.z));
                            beam[i].SetPosition(1, new Vector3(pl.point.x, Mathf.Max(0.5f, pl.point.y), pl.point.z));
                        }
                    }
                }
                planRing[i].gameObject.SetActive(on);
                if (!on) beam[i].enabled = false;
            }
        }
    }
}
