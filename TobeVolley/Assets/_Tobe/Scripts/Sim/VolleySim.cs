// Server-authoritative volleyball simulation: rules, ball physics, bots. Pure C# + UnityEngine math types.
// Coordinates follow Tobe.Court: X across (0..9), Y up, Z along the court (net at Z = 9).
using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Tobe.Sim
{
    public sealed class SimPlayer
    {
        public byte id;
        public int team, slot;
        public bool isBot, connected = true;
        public ulong clientId;
        public PlayerProfile profile;
        public Stats st;
        public float reach, jumpH, spd;

        public Vector3 pos, vel;
        public bool air, sprint, blockJump;
        public float dvx, dvz;
        public float diveT, recT, stunT, armed, callT, poseT, stamina = 100, energy;
        public PoseId pose = PoseId.Idle;
        public float yaw;
        public bool hasBlockTask; public float blockX, blockJumpAt;
        public float celebrate;

        // human input state
        public InputCmd lastCmd;
        public ushort seenHitPress, seenHitRelease, seenCall, seenSpecial;
        public float hitBuf, serveHold;
        public bool serveCharging;

        public StyleDef Style => Styles.Get(profile.style);
        public void SetPose(PoseId p, float hold = 0.35f) { pose = p; poseT = hold; }
    }

    public sealed class SimBall
    {
        public Vector3 pos, vel;
        public SimPlayer held, lastP;
        public int lastTeam = -1;
        public bool live, blockChecked, blockTouch;
        public string kind = "";
        public float hitT, power;
        public SimPlayer superBy; public bool mini;
    }

    public sealed class Plan
    {
        public PlanKind kind;
        public SimPlayer p;
        public Vector3 point;
        public float t;           // absolute sim time of contact
        public bool freak;
    }

    public sealed class VolleySim
    {
        // ---- tuning ----
        const float NY = Court.NetZ, NH = Court.NetTop, R = Court.BallRadius, GB = Court.BallGravity, GP = Court.PlayerGravity;
        struct Diff { public float acc, rec, block, speed; public int blockers; }
        static readonly Diff Bots = new Diff { acc = 0.8f, rec = 0.02f, block = 1f, speed = 1f, blockers = 2 };

        public readonly List<SimPlayer> players = new List<SimPlayer>();
        public readonly SimBall ball = new SimBall();
        public readonly Plan[] plans = { null, null };
        public readonly int[] score = new int[2];
        public readonly int[] touches = new int[2];
        public readonly int[] serverIdx = new int[2];
        public MatchPhase phase = MatchPhase.Serve;
        public int servingTeam, targetScore = 15, teamSize = 6, winner = -1;
        public float t, phaseT, slowT, freezeT;
        public SimPlayer server;
        Action pendingAfterFreeze;
        public readonly List<GameEvent> events = new List<GameEvent>();

        public float TimeScale => freezeT > 0 ? 0f : slowT > 0 ? 0.2f : 1f;

        // ================= setup =================
        static readonly Vector2[] Slots6 = { new Vector2(1.7f, 1.8f), new Vector2(4.5f, 1.4f), new Vector2(7.3f, 1.8f), new Vector2(1.7f, 6.2f), new Vector2(4.5f, 7.0f), new Vector2(7.3f, 6.2f) };
        static readonly Vector2[] Slots3 = { new Vector2(5.6f, 1.4f), new Vector2(2.2f, 4.6f), new Vector2(6.8f, 5.6f) };
        static readonly Vector2[] Slots4 = { new Vector2(5.8f, 1.3f), new Vector2(2.0f, 2.2f), new Vector2(2.8f, 6.2f), new Vector2(6.6f, 6.0f) };
        Vector2 SlotPos(int slot) => teamSize >= 6 ? Slots6[slot % 6] : teamSize == 4 ? Slots4[slot % 4] : Slots3[slot % 3];
        bool IsFront(SimPlayer p) => teamSize >= 6 ? p.slot <= 2 : SlotPos(p.slot).y < 3f;

        public static readonly PlayStyle[] BotStyles6 = { PlayStyle.Outside, PlayStyle.Middle, PlayStyle.Setter, PlayStyle.Outside, PlayStyle.Libero, PlayStyle.Opposite };
        public static readonly PlayStyle[] BotStyles3 = { PlayStyle.Setter, PlayStyle.Outside, PlayStyle.Middle };
        public static readonly PlayStyle[] BotStyles4 = { PlayStyle.Setter, PlayStyle.Outside, PlayStyle.Middle, PlayStyle.Libero };
        public static PlayStyle BotStyleFor(int teamSize, int slot) =>
            teamSize >= 6 ? BotStyles6[slot % 6] : teamSize == 4 ? BotStyles4[slot % 4] : BotStyles3[slot % 3];
        static readonly string[] BotNames = { "Кагеяма-бот", "Хината-бот", "Нишиноя-бот", "Танака-бот", "Цукки-бот", "Асахи-бот", "Ойкава-бот", "Ивайзуми-бот", "Куроо-бот", "Кенма-бот", "Бокуто-бот", "Акааши-бот" };

        public SimPlayer AddPlayer(byte id, int team, int slot, PlayerProfile prof, bool bot, ulong clientId)
        {
            var p = new SimPlayer { id = id, team = team, slot = slot, isBot = bot, clientId = clientId };
            ApplyProfile(p, prof);
            players.Add(p);
            return p;
        }
        public void ApplyProfile(SimPlayer p, PlayerProfile prof)
        {
            p.profile = prof;
            p.st = Styles.Get(prof.style).stats;
            float h = prof.style == PlayStyle.Middle ? 1.04f : prof.style == PlayStyle.Libero ? 0.95f : 1f;
            p.reach = 2.15f * h; p.jumpH = 0.5f + p.st.jump * 0.065f; p.spd = 3.6f + p.st.speed * 0.28f;
        }
        public static PlayerProfile BotProfile(int teamSize, int team, int slot)
        {
            int k = (team * 6 + slot) % BotNames.Length;
            var rng = new System.Random(team * 101 + slot * 17 + teamSize);
            return new PlayerProfile
            {
                nick = BotNames[k], style = BotStyleFor(teamSize, slot), model = (byte)((team * 3 + slot) % Mathf.Max(1, GameHub.ModelNames.Length)),
                hair = (byte)((slot * 3 + team) % TeamLook.HairPresets.Length), number = (byte)(1 + (slot * 3 + team * 5) % 15),
                skin = (byte)rng.Next(TeamLook.SkinTones.Length), height = (byte)rng.Next(40, 230), build = (byte)rng.Next(60, 230),
                eyes = (byte)rng.Next(TeamLook.EyeColors.Length),
                gear = (byte)(Gear.KneePads | (rng.Next(2) == 0 ? Gear.Headband : 0) | (rng.Next(2) == 0 ? Gear.Wristbands : 0) | (rng.Next(4) == 0 ? Gear.ArmSleeve : 0) | (rng.Next(6) == 0 ? Gear.Glasses : 0)),
            };
        }

        public void StartMatch(int firstServer)
        {
            score[0] = score[1] = 0; winner = -1;
            servingTeam = firstServer;
            for (int i = 0; i < 2; i++) serverIdx[i] = 0;
            SetupServe();
        }
        List<SimPlayer> Team(int i) { var l = new List<SimPlayer>(); foreach (var p in players) if (p.team == i) l.Add(p); return l; }
        List<SimPlayer> ServeOrder(int i)
        {
            var l = Team(i); l.RemoveAll(p => p.profile.style == PlayStyle.Libero && Team(i).Count > 3);
            l.Sort((a, b) => a.slot.CompareTo(b.slot));
            return l;
        }
        // ----- rotation (clockwise; the player rotating into the back-right position serves) -----
        int[] RotationCycle()
        {
            if (teamSize >= 6) return new[] { 2, 5, 4, 3, 0, 1 };   // FR -> BR -> BM -> BL -> FL -> FM -> FR
            if (teamSize == 4) return new[] { 0, 3, 2, 1 };         // FR -> BR -> BL -> FL -> FR
            return new[] { 0, 2, 1 };                               // F -> BR -> BL -> F
        }
        int ServerSlot => teamSize >= 6 ? 5 : teamSize == 4 ? 3 : 2;
        bool HasLibero(int team) => teamSize >= 6 && players.Exists(p => p.team == team && p.profile.style == PlayStyle.Libero);
        void Rotate(int team)
        {
            var cycle = new List<int>(RotationCycle());
            bool lib = HasLibero(team);
            if (lib) cycle.Remove(4);                                // the libero stays in the back row (middle back)
            var next = new Dictionary<int, int>();
            for (int i = 0; i < cycle.Count; i++) next[cycle[i]] = cycle[(i + 1) % cycle.Count];
            foreach (var p in players)
                if (p.team == team && !(lib && p.profile.style == PlayStyle.Libero) && next.TryGetValue(p.slot, out int ns)) p.slot = ns;
        }
        SimPlayer ServerOf(int team)
        {
            var p = players.Find(q => q.team == team && q.slot == ServerSlot && q.profile.style != PlayStyle.Libero);
            if (p != null) return p;
            var order = ServeOrder(team);
            return order[serverIdx[team] % order.Count];
        }
        bool IsBackRow(SimPlayer p) => teamSize >= 6 && p.slot >= 3;

        void SetupServe()
        {
            phase = MatchPhase.Serve; phaseT = 1.8f; slowT = 0;
            for (int i = 0; i < 2; i++) { touches[i] = 0; plans[i] = null; }
            foreach (var p in players)
            {
                var sp = SlotPos(p.slot);
                float recv = p.team != servingTeam && !(p.profile.style == PlayStyle.Setter) ? 1.3f : 0f;
                p.pos = new Vector3(sp.x, 0, Court.WorldZ(p.team, sp.y + recv)); p.vel = Vector3.zero; p.air = false;
                p.diveT = p.recT = p.stunT = 0; p.hasBlockTask = false; p.celebrate = 0; p.SetPose(PoseId.Ready, 0);
                p.yaw = p.team == 0 ? 0 : 180; p.serveCharging = false; p.hitBuf = 0;
            }
            server = ServerOf(servingTeam);
            server.pos = new Vector3(6.8f, 0, Court.WorldZ(servingTeam, 9.9f));
            ball.held = server; ball.live = false; ball.superBy = null; ball.mini = false; ball.lastP = null; ball.lastTeam = -1;
            ball.vel = Vector3.zero; ball.blockTouch = false; ball.kind = "serve";
        }

        // ================= events =================
        void Ev(GameEventType type, string text = null, Color? color = null, Vector3 pos = default, SimPlayer p = null, int i = 0, float f = 0)
            => events.Add(new GameEvent { type = type, text = text ?? "", color = color ?? Color.white, pos = pos, playerId = p != null ? p.id : (byte)255, intArg = i, floatArg = f });
        void Popup(string text, Color c, bool big = false) => Ev(GameEventType.Popup, text, c, default, null, big ? 1 : 0);
        static readonly Color Gold = new Color(1f, 0.82f, 0.25f), Cyan = new Color(0.16f, 0.9f, 1f), Pink = new Color(1f, 0.23f, 0.55f), Grey = new Color(0.7f, 0.7f, 0.7f), Orange = new Color(1f, 0.54f, 0.1f);
        Color TeamCol(int team) => team == 0 ? Orange : Cyan;

        // ================= helpers =================
        static float Rnd(float a, float b) => Random.Range(a, b);
        static float Hyp(float a, float b) => Mathf.Sqrt(a * a + b * b);
        float FlatDist(SimPlayer p, Vector3 q) => Hyp(p.pos.x - q.x, p.pos.z - q.z);
        int Other(int team) => 1 - team;

        void Launch(Vector3 target, float T)
        {
            ball.vel = new Vector3((target.x - ball.pos.x) / T, (target.y - ball.pos.y + 0.5f * GB * T * T) / T, (target.z - ball.pos.z) / T);
            ball.held = null; ball.live = true; ball.blockChecked = false; ball.hitT = 0;
        }
        bool ClearsNet(Vector3 target, float T)
        {
            float vz = (target.z - ball.pos.z) / T;
            if (Mathf.Abs(vz) < 1e-3f) return true;
            float tn = (NY - ball.pos.z) / vz;
            if (tn < 0 || tn > T) return true;
            float vy = (target.y - ball.pos.y + 0.5f * GB * T * T) / T;
            return ball.pos.y + vy * tn - 0.5f * GB * tn * tn > NH + R + 0.12f;
        }
        float SafeT(Vector3 target, float T) { int k = 0; while (!ClearsNet(target, T) && k++ < 14) T *= 1.12f; return T; }
        /// <summary>Where/when the ball descends through height y (ignoring drag).</summary>
        public Vector3 PredictY(float y, out float time)
        {
            var b = ball; float disc = b.vel.y * b.vel.y + 2 * GB * (b.pos.y - y);
            if (disc < 0 || b.held != null) { time = 0; return b.pos; }
            time = Mathf.Max(0, (b.vel.y + Mathf.Sqrt(disc)) / GB);
            return new Vector3(b.pos.x + b.vel.x * time, y, b.pos.z + b.vel.z * time);
        }

        // ================= rules =================
        bool RegisterTouch(SimPlayer p, bool isBlock = false)
        {
            int t = p.team;
            if (isBlock) { touches[t] = 0; touches[Other(t)] = 0; }
            else if (ball.lastTeam != t) { touches[t] = 1; touches[Other(t)] = 0; }
            else
            {
                if (ball.lastP == p && !ball.blockTouch) { Fault(t, "ДВОЙНОЕ КАСАНИЕ"); return false; }
                touches[t]++;
                if (touches[t] > 3) { Fault(t, "ЧЕТЫРЕ КАСАНИЯ"); return false; }
            }
            ball.lastP = p; ball.lastTeam = t; ball.blockTouch = isBlock; ball.superBy = null; ball.mini = false;
            plans[t] = null;
            p.energy = Mathf.Min(100, p.energy + (p.isBot ? 3 : 7));
            return true;
        }
        void Fault(int team, string why) => AwardPoint(Other(team), why, null);

        void AwardPoint(int team, string why, SimPlayer hero)
        {
            if (phase != MatchPhase.Rally) return;
            phase = MatchPhase.Point; phaseT = 2.6f; ball.live = false;
            score[team]++;
            Ev(GameEventType.Point, why, TeamCol(team), ball.pos, hero, team);
            Popup(why, team == 0 ? Gold : Pink, true);
            foreach (var p in players) { p.celebrate = p.team == team ? 1 : -1; p.hasBlockTask = false; }
            if (hero != null) hero.energy = Mathf.Min(100, hero.energy + 12);
            if (servingTeam != team) { servingTeam = team; Rotate(team); }   // side-out: the team that wins the serve rotates
        }
        bool CheckWin()
        {
            if ((score[0] >= targetScore || score[1] >= targetScore) && Mathf.Abs(score[0] - score[1]) >= 2) { winner = score[0] > score[1] ? 0 : 1; return true; }
            return false;
        }

        // ================= planning (bots' brain) =================
        SimPlayer NearestFree(int team, Vector3 at, SimPlayer exclude)
        {
            SimPlayer best = null; float bt = 1e9f;
            foreach (var p in players)
            {
                if (p.team != team || p == exclude || p.pos.y > 0.3f || p.stunT > 0 || !p.connected && !p.isBot) continue;
                float tt = FlatDist(p, at) / p.spd - (p.isBot ? 0 : 0.15f);
                if (tt < bt) { bt = tt; best = p; }
            }
            return best;
        }
        SimPlayer SetterOf(int team, SimPlayer exclude)
        {
            foreach (var p in players)
                if (p.team == team && p != exclude && p.profile.style == PlayStyle.Setter && p.stunT <= 0 && !(!p.isBot && p.callT > 0)) return p;
            return NearestFree(team, new Vector3(5.8f, 0, Court.WorldZ(team, 1f)), exclude);
        }
        void Replan()
        {
            if (!ball.live) return;
            var L = PredictY(R, out _);
            for (int i = 0; i < 2; i++) if (plans[i] != null && plans[i].kind != PlanKind.Attack && plans[i].kind != PlanKind.Set) plans[i] = null;
            int team = L.z < NY ? 0 : 1;
            if (plans[team] != null) return;
            int n = ball.lastTeam == team ? touches[team] : 0;
            if (n >= 3) return;
            var C = PredictY(n == 0 ? 0.95f : 1.2f, out float ct);
            if (!Court.OnSide(team, C.z)) return;
            if (!Court.InCourt(L.x, L.z, 0.15f) && n == 0 && Random.value < 0.85f) { plans[team] = new Plan { kind = PlanKind.Leave }; return; }
            var excl = ball.lastP != null && ball.lastP.team == team && !ball.blockTouch ? ball.lastP : null;
            var p = NearestFree(team, C, excl);
            if (p == null) return;
            plans[team] = new Plan { kind = n == 0 ? PlanKind.Receive : n == 1 ? PlanKind.Set : PlanKind.Over, p = p, point = C, t = t + ct };
        }
        SimPlayer ChooseAttacker(int team, SimPlayer setter)
        {
            SimPlayer caller = null, best = null; float bs = -1e9f;
            foreach (var p in players)
            {
                if (p.team != team || p == setter || p.profile.style == PlayStyle.Libero || p.stunT > 0) continue;
                if (!p.isBot && p.callT > 0) caller = p;
                float sc = p.st.spike * 0.6f + (IsFront(p) ? 3 : 0) + Rnd(0, 5) + (!p.isBot ? (IsFront(p) ? 4.5f : 3f) : 0);
                // players far from where they'd attack (e.g. the receiver deep in the court) can't get there in time
                float dn = Court.DistFromNet(team, p.pos.z), want = IsFront(p) ? 2.6f : 4.2f;
                sc -= Mathf.Max(0, dn - want) * 2.5f + (p.air || p.recT > 0 ? 6 : 0);
                if (sc > bs) { bs = sc; best = p; }
            }
            return caller ?? best;
        }
        float AttackHeight(SimPlayer p) => p.reach + p.jumpH - 0.08f;

        // ================= hits =================
        static float Quality(float d, float rad) => Mathf.Clamp01(1 - d / rad);

        void DoPass(SimPlayer p, float q, Vector3? toward)
        {
            int t = p.team; q = Mathf.Clamp(q, 0.05f, 1f);
            if (toward.HasValue && !Court.OnSide(t, toward.Value.z)) { DoOver(p, toward); return; }
            var setter = toward.HasValue ? (NearestFree(t, toward.Value, p) ?? SetterOf(t, p)) : SetterOf(t, p);
            float sx = toward.HasValue ? toward.Value.x : 5.8f, sz = toward.HasValue ? toward.Value.z : Court.WorldZ(t, 0.9f);
            float err = (1 - q) * 2.6f;
            var target = new Vector3(Mathf.Clamp(sx + Rnd(-err, err), -1, Court.Width + 1), 2.35f, sz - Court.Fwd(t) * Rnd(0, err));
            float T = 1.15f + (1 - q) * 0.3f;
            Launch(target, T);
            ball.kind = "pass";
            p.SetPose(PoseId.Bump);
            Ev(GameEventType.Hit, null, null, ball.pos, p, (int)PoseId.Bump, 0.3f);
            if (setter != null) plans[t] = new Plan { kind = PlanKind.Set, p = setter, point = target, t = t + T };
        }
        void DoSet(SimPlayer p, float q, SimPlayer attacker, bool special)
        {
            int tm = p.team; q = Mathf.Clamp(q, 0.05f, 1f);
            attacker = attacker ?? ChooseAttacker(tm, p);
            if (attacker == null) { DoOver(p, null); return; }
            Vector3 target; float T;
            bool freak = !attacker.isBot && attacker.air && attacker.vel.y > 0 && (p.st.set >= 9 || special);
            float tz = AttackHeight(attacker);
            if (freak)
            {
                T = Mathf.Max(0.2f, attacker.vel.y / GP);
                target = new Vector3(attacker.pos.x + attacker.vel.x * T, attacker.pos.y + attacker.vel.y * T - 0.5f * GP * T * T + attacker.reach,
                    attacker.pos.z + attacker.vel.z * T + Court.Fwd(tm) * 0.15f);
                Popup("СТРАННАЯ БЫСТРАЯ!", Orange);
                slowT = 0.5f;
            }
            else if (!attacker.isBot)
            {
                float dn = Court.DistFromNet(tm, attacker.pos.z);
                target = new Vector3(Mathf.Clamp(attacker.pos.x, 0.8f, Court.Width - 0.8f), tz, Court.WorldZ(tm, dn < 3.6f ? 0.6f : 3.3f));
                T = 1.0f;
            }
            else
            {
                var sp = SlotPos(attacker.slot);
                if (!IsFront(attacker)) { target = new Vector3(attacker.pos.x, tz, Court.WorldZ(tm, 3.3f)); T = 1.0f; }
                else if (sp.x < 3) { target = new Vector3(0.9f, tz, Court.WorldZ(tm, 0.7f)); T = 1.15f; }
                else if (sp.x < 6) { target = new Vector3(4.0f, tz, Court.WorldZ(tm, 0.55f)); T = 0.62f; }
                else { target = new Vector3(8.1f, tz, Court.WorldZ(tm, 0.7f)); T = 0.95f; }
            }
            float err = freak ? 0 : (1 - q) * 1.3f;
            target.x += Rnd(-err, err); target.z -= Court.Fwd(tm) * Rnd(0, err * 0.6f);
            Launch(target, T);
            ball.kind = "set";
            p.SetPose(ball.pos.y > 1.6f ? PoseId.Set : PoseId.Bump);
            Ev(GameEventType.Hit, null, null, ball.pos, p, (int)PoseId.Set, 0.2f);
            plans[tm] = new Plan { kind = PlanKind.Attack, p = attacker, point = target, t = t + T, freak = freak };
            PlanBlock(Other(tm), target.x, t + T);
        }
        void PlanBlock(int team, float x, float tHit)
        {
            var front = players.FindAll(p => p.team == team && p.isBot && IsFront(p) && p.profile.style != PlayStyle.Libero);
            front.Sort((a, b) => Mathf.Abs(a.pos.x - x).CompareTo(Mathf.Abs(b.pos.x - x)));
            for (int k = 0; k < Mathf.Min(Bots.blockers, front.Count); k++)
            {
                var p = front[k];
                float tPeak = Mathf.Sqrt(2 * GP * p.jumpH * 0.85f) / GP;
                p.hasBlockTask = true;
                p.blockX = Mathf.Clamp(x + (k > 0 ? 0.5f : -0.25f) * (x < 4.5f ? 1 : -1), 0.3f, Court.Width - 0.3f);
                p.blockJumpAt = tHit - tPeak + 0.04f + Rnd(-0.06f, 0.08f) / Bots.block;
            }
        }
        void DoSpike(SimPlayer p, float q, Vector3? aim, bool special)
        {
            if (ball.pos.y > NH + 0.05f)
            {
                if (p.profile.style == PlayStyle.Libero && teamSize >= 6) { Fault(p.team, "ЛИБЕРО НЕ АТАКУЕТ"); return; }
                if (IsBackRow(p) && Court.DistFromNet(p.team, p.pos.z) < Court.AttackLine - 0.1f && !p.isBot) { Fault(p.team, "ОШИБКА ЗАДНЕЙ ЛИНИИ"); return; }
            }
            int tm = p.team, o = Other(tm);
            Vector3 target;
            if (aim.HasValue && Court.OnSide(o, aim.Value.z)) target = new Vector3(aim.Value.x, R, aim.Value.z);
            else
            {
                Vector2[] cands = { new Vector2(0.9f, 7.7f), new Vector2(8.1f, 7.7f), new Vector2(0.9f, 4.5f), new Vector2(8.1f, 4.5f), new Vector2(4.5f, 7.9f), new Vector2(2.2f, 2.4f), new Vector2(6.8f, 2.4f) };
                float bs = -1e9f; target = new Vector3(4.5f, R, Court.WorldZ(o, 6));
                foreach (var c in cands)
                {
                    var cp = new Vector3(c.x, R, Court.WorldZ(o, c.y));
                    float md = 1e9f; foreach (var d in players) if (d.team == o && d.pos.y < 0.3f) md = Mathf.Min(md, FlatDist(d, cp));
                    float sc = md + Rnd(0, 1.5f);
                    if (sc > bs) { bs = sc; target = cp; }
                }
                float e = (1 - Bots.acc) * 1.6f;
                target.x += Rnd(-e, e); target.z += Rnd(-e, e);
            }
            float S = (12.5f + p.st.spike * 1.25f) * (0.7f + 0.35f * q) * (special ? 1.35f : 1f);
            bool tip = p.isBot && !special && Random.value < 0.08f;
            if (tip) { target = new Vector3(Mathf.Clamp(p.pos.x + Rnd(-2, 2), 0.5f, 8.5f), R, Court.WorldZ(o, Rnd(1.2f, 2.2f))); S = 6; }
            float dist = Hyp(target.x - ball.pos.x, target.z - ball.pos.z);
            float T = SafeT(target, Mathf.Max(0.12f, dist / S));
            Launch(target, T);
            ball.power = S; ball.kind = tip ? "tip" : "spike";
            p.SetPose(PoseId.Spike, 0.4f);
            Ev(GameEventType.Hit, null, null, ball.pos, p, (int)PoseId.Spike, tip ? 0.2f : 0.6f + q * 0.4f);
            if (tip) Popup("ОБМАНКА!", Cyan);
            if (special) { ball.superBy = p; ball.mini = false; }
            else if (q > 0.82f && !tip)
            {
                Popup("ИДЕАЛЬНО!", Gold);
                slowT = 0.45f;
                Ev(GameEventType.Cinematic, null, null, p.pos + Vector3.up * 2, p, 0, 0.9f);
                ball.superBy = p; ball.mini = true;
            }
        }
        void DoOver(SimPlayer p, Vector3? aim)
        {
            int o = Other(p.team);
            var target = aim.HasValue && Court.OnSide(o, aim.Value.z) ? new Vector3(aim.Value.x, R, aim.Value.z) : new Vector3(Rnd(1.5f, 7.5f), R, Court.WorldZ(o, Rnd(5, 8)));
            Launch(target, SafeT(target, 1.25f));
            ball.kind = "free";
            p.SetPose(ball.pos.y > 1.6f ? PoseId.Set : PoseId.Bump);
            Ev(GameEventType.Hit, null, null, ball.pos, p, (int)PoseId.Bump, 0.3f);
        }
        void DoServe(SimPlayer p, float power, Vector3? aim, bool special)
        {
            int o = Other(p.team);
            var target = aim.HasValue && Court.OnSide(o, aim.Value.z) ? new Vector3(aim.Value.x, R, aim.Value.z)
                : new Vector3(Rnd(0.8f, 8.2f), R, Court.WorldZ(o, Rnd(3, 8.3f)));
            float T = special ? 0.62f : Mathf.Lerp(1.75f, 0.85f, power) - (p.isBot ? p.st.serve * 0.02f : 0);
            T = SafeT(target, T);
            Launch(target, T);
            ball.kind = "serve"; ball.power = Hyp(target.x - ball.pos.x, target.z - ball.pos.z) / T;
            ball.lastP = p; ball.lastTeam = p.team; touches[p.team] = 0; touches[o] = 0;
            p.SetPose(PoseId.ServeHit, 0.4f);
            Ev(GameEventType.Hit, null, null, ball.pos, p, (int)PoseId.ServeHit, 0.4f + power * 0.4f);
            if (special) ball.superBy = p;
            phase = MatchPhase.Rally; t = 0;
            Replan();
        }

        float ReceiveRoll(SimPlayer p, float posQ)
        {
            float sp = ball.vel.magnitude;
            float q = 0.45f + p.st.receive * 0.05f - Mathf.Max(0, sp - 11) * 0.03f + (p.isBot ? Bots.rec : 0.04f) + posQ * 0.25f;
            if (ball.superBy != null) q -= ball.mini ? 0.25f : 0.55f;
            return q;
        }
        void TryReceive(SimPlayer p, float d, float rad, Vector3? toward, bool auto)
        {
            if (!RegisterTouch(p)) return;
            float q = ReceiveRoll(p, Quality(d, rad)) - (auto ? 0.1f : 0);
            if (p.armed > 0 && p.energy >= 100 && p.Style.finisherKind == FinisherKind.Dig)
            {
                UseFinisher(p, () => { DoPass(p, 1, toward); Popup(p.Style.finisherName.ToUpper(), p.Style.c1); });
                return;
            }
            if (ball.superBy != null && !ball.mini && Random.value > q)
            {
                p.stunT = 0.8f; ball.vel = new Vector3(ball.vel.x * 0.5f, Mathf.Abs(ball.vel.y) * 0.3f + 2, ball.vel.z * 0.5f);
                Popup("СНЕСЛО!", Pink); return;
            }
            if (Random.value > Mathf.Clamp(q + 0.3f, 0.05f, 0.97f))
            {
                ball.vel = new Vector3(Rnd(-4, 4), Rnd(2.5f, 5.5f), -Court.Fwd(p.team) * Rnd(0, 3) + Rnd(-1, 1));
                ball.held = null; ball.blockChecked = false;
                p.SetPose(PoseId.Bump, 0.3f);
                Ev(GameEventType.Hit, null, null, ball.pos, p, (int)PoseId.Bump, 0.2f);
                if (!p.isBot) Popup("КРИВОЙ ПРИЁМ", Grey);
                Replan();
                return;
            }
            if (p.diveT > 0 && Random.value < 0.5f) Popup("ВЫТАЩИЛ!", new Color(0.25f, 1f, 0.7f));
            DoPass(p, Mathf.Clamp(q, 0.1f, 1f), toward);
        }
        void UseFinisher(SimPlayer p, Action after)
        {
            p.energy = 0; p.armed = 0;
            freezeT = 1.25f; pendingAfterFreeze = after;
            Ev(GameEventType.Cutin, p.Style.finisherName, p.Style.c1, p.pos, p, (int)p.Style.finisherKind, 1.25f);
        }

        struct Reach { public bool ok, air; public float d, rad; }
        Reach ReachInfo(SimPlayer p)
        {
            var b = ball.pos;
            if (p.air)
            {
                var hand = new Vector3(p.pos.x, p.pos.y + p.reach, p.pos.z + Court.Fwd(p.team) * 0.15f);
                float d = Vector3.Distance(b, hand);
                float ar = p.isBot ? 0.85f : 1.0f;
                return d < ar ? new Reach { ok = true, air = true, d = d, rad = ar } : default;
            }
            float hd = Hyp(p.pos.x - b.x, p.pos.z - b.z), rad = (p.diveT > 0 ? 1.6f : 1.0f) * (p.isBot ? 1f : 1.15f);
            if (hd < rad && b.y > 0.12f && b.y < p.reach + 0.35f && (p.diveT <= 0 || b.y < 1f)) return new Reach { ok = true, d = hd, rad = rad };
            return default;
        }
        bool LegalFor(SimPlayer p)
        {
            if (!ball.live || ball.held != null) return false;
            if (!(Court.OnSide(p.team, ball.pos.z) || Mathf.Abs(ball.pos.z - NY) < 0.25f)) return false;
            if (ball.hitT < 0.12f && ball.lastP == p) return false;
            return true;
        }
        void BotHit(SimPlayer p, Plan plan, Reach ri)
        {
            int tm = p.team;
            int n = ball.lastTeam == tm && !ball.blockTouch ? touches[tm] + 1 : 1;
            if (n == 1) { TryReceive(p, ri.d, ri.rad, null, false); return; }
            if (!RegisterTouch(p)) return;
            if (n == 2)
            {
                float q = Mathf.Clamp(0.55f + p.st.set * 0.045f + Quality(ri.d, ri.rad) * 0.2f - (ball.pos.y < 1.5f ? 0.25f : 0), 0.1f, 1f);
                if (p.st.set >= 9 && p.energy >= 100 && Random.value < 0.5f)
                {
                    var atk = ChooseAttacker(tm, p);
                    UseFinisher(p, () => DoSet(p, 1, atk, true));
                    return;
                }
                DoSet(p, q, null, false);
                return;
            }
            if (ri.air)
            {
                float q = Mathf.Clamp01(Quality(ri.d, ri.rad) * 0.7f + 0.3f * Mathf.Clamp01(1 - Mathf.Abs(p.vel.y) / 3) + Rnd(-0.1f, 0.1f));
                if (p.energy >= 100 && p.Style.finisherKind == FinisherKind.Spike && Random.value < 0.7f) { UseFinisher(p, () => DoSpike(p, 1, null, true)); return; }
                DoSpike(p, q, null, false);
            }
            else DoOver(p, null);
        }

        // ================= human hits =================
        void HumanHit(SimPlayer u, Reach ri, bool auto)
        {
            int tm = u.team;
            int n = ball.lastTeam == tm && !ball.blockTouch ? touches[tm] + 1 : 1;
            Vector3 aim = u.lastCmd.aim;
            bool aimOwn = Court.OnSide(tm, aim.z);
            bool special = u.armed > 0 && u.energy >= 100;
            if (n == 1 && !ri.air) { TryReceive(u, ri.d, ri.rad, aimOwn ? aim : (Vector3?)null, auto); return; }
            if (!RegisterTouch(u)) return;
            if (ri.air)
            {
                if (aimOwn && n < 3)
                {
                    var mate = NearestFree(tm, aim, u);
                    DoSet(u, 0.8f, mate != null && mate != u ? mate : null, false); return;
                }
                float q = Mathf.Clamp01(Quality(ri.d, ri.rad) * 0.7f + 0.3f * Mathf.Clamp01(1 - Mathf.Abs(u.vel.y) / 3));
                if (special && u.Style.finisherKind == FinisherKind.Spike) { var a = aim; UseFinisher(u, () => DoSpike(u, 1, a, true)); return; }
                DoSpike(u, q, aim, false);
                return;
            }
            if (n == 2 && aimOwn)
            {
                float q = Mathf.Clamp(0.5f + u.st.set * 0.05f + Quality(ri.d, ri.rad) * 0.2f, 0.1f, 1f);
                SimPlayer mate = null; float bd = 1e9f;
                foreach (var p in players)
                    if (p.team == tm && p != u && p.profile.style != PlayStyle.Libero) { float d = Hyp(p.pos.x - aim.x, p.pos.z - aim.z); if (d < bd) { bd = d; mate = p; } }
                if (special && u.Style.finisherKind == FinisherKind.Set) { var m = mate; UseFinisher(u, () => DoSet(u, 1, m, true)); return; }
                DoSet(u, q, mate, false);
                return;
            }
            DoOver(u, aim);
        }

        /// <summary>Apply the latest input of a connected human (called every server tick before Step).</summary>
        public void ApplyHuman(SimPlayer u, InputCmd c, float dt)
        {
            if (c.hitPressCount != u.seenHitPress) { u.seenHitPress = c.hitPressCount; OnHumanPress(u); }
            if (c.hitReleaseCount != u.seenHitRelease) { u.seenHitRelease = c.hitReleaseCount; OnHumanRelease(u); }
            if (c.callCount != u.seenCall) { u.seenCall = c.callCount; u.callT = 2.5f; Ev(GameEventType.Popup, "ДАЙ МНЕ!", Orange, default, u, 0); }
            if (c.specialCount != u.seenSpecial)
            {
                u.seenSpecial = c.specialCount;
                if (u.energy >= 100) { u.armed = 4; }
            }
            u.lastCmd = c;
            // client-authoritative movement with sanity limits
            bool serving = phase == MatchPhase.Serve && u == server;
            if (serving) u.pos.x = Mathf.Clamp(c.clientPos.x, 0.3f, Court.Width - 0.3f);
            bool locked = u.stunT > 0 || serving;
            if (!locked)
            {
                var np = c.clientPos;
                float maxStep = (u.spd * 1.6f + 7f) * Mathf.Max(dt, 0.02f) * 3f;
                var delta = np - u.pos; delta.y = 0;
                if (delta.magnitude > maxStep) np = u.pos + delta.normalized * maxStep + Vector3.up * (np.y - u.pos.y);
                np.x = Mathf.Clamp(np.x, -3, Court.Width + 3);
                np.z = u.team == 0 ? Mathf.Clamp(np.z, -4, NY - 0.35f) : Mathf.Clamp(np.z, NY + 0.35f, Court.Length + 4);
                np.y = Mathf.Clamp(np.y, 0, 1.6f);
                u.vel = c.clientVel; u.pos = np;
                bool wasAir = u.air;
                u.air = c.clientAir && np.y > 0.01f;
                if (u.air && !wasAir) u.blockJump = Mathf.Abs(u.pos.z - NY) < 1.2f && ball.lastTeam != u.team;
                if (c.clientDiving && u.diveT <= 0) u.diveT = 0.5f;
                if (!c.clientDiving && u.diveT > 0.1f) u.diveT = 0.1f;
            }
            if (u.vel.sqrMagnitude > 0.3f) u.yaw = Mathf.Atan2(u.vel.x, u.vel.z) * Mathf.Rad2Deg;
        }
        void OnHumanPress(SimPlayer u)
        {
            if (phase == MatchPhase.Serve && ball.held == u)
            {
                ball.held = null; ball.live = false; ball.vel = new Vector3(0, 4.6f, 0); ball.pos.y = 1.8f;
                u.serveCharging = true; u.serveHold = 0; u.SetPose(PoseId.ServeToss, 1f);
                return;
            }
            if (phase == MatchPhase.Rally) u.hitBuf = 0.35f;
        }
        void OnHumanRelease(SimPlayer u)
        {
            if (phase == MatchPhase.Serve && u.serveCharging) ServeRelease(u);
        }
        void ServeRelease(SimPlayer u)
        {
            u.serveCharging = false;
            float power = Mathf.Clamp(u.serveHold / 0.9f, 0.1f, 1f);
            var aim = u.lastCmd.aim;
            Vector3? target = Court.OnSide(Other(u.team), aim.z) ? aim : (Vector3?)null;
            if (u.armed > 0 && u.energy >= 100 && u.Style.finisherKind == FinisherKind.Serve)
            {
                ball.vel = Vector3.zero; ball.pos.y = Mathf.Max(ball.pos.y, 2.6f);
                phase = MatchPhase.Rally; t = 0;
                UseFinisher(u, () => DoServe(u, 1, target, true));
                return;
            }
            DoServe(u, power, target, false);
        }

        // ================= bots =================
        void Seek(SimPlayer p, float tx, float tz, float arrive)
        {
            float dx = tx - p.pos.x, dz = tz - p.pos.z, d = Hyp(dx, dz);
            float m = d < 0.05f ? 0 : Mathf.Clamp01(d / arrive);
            p.dvx = d > 0 ? dx / d * m : 0; p.dvz = d > 0 ? dz / d * m : 0;
        }
        void Jump(SimPlayer p, float k)
        {
            if (p.air || p.recT > 0 || p.stunT > 0) return;
            float run = Hyp(p.vel.x, p.vel.z);
            p.vel.y = Mathf.Sqrt(2 * GP * p.jumpH * k) * (1 + Mathf.Min(run, 6) * 0.012f);
            p.air = true; p.blockJump = false;
        }
        void Dive(SimPlayer p, float dx, float dz)
        {
            float d = Hyp(dx, dz); if (d < 1e-3f) d = 1;
            p.diveT = 0.5f; p.vel.x = dx / d * 6.5f; p.vel.z = dz / d * 6.5f;
            Ev(GameEventType.Sound, "bump", null, p.pos, p, 0, 0.3f);
        }
        void BotThink(SimPlayer p)
        {
            int tm = p.team; float f = Court.Fwd(tm); var plan = plans[tm];
            p.sprint = false;
            if (p.stunT > 0 || p.recT > 0 || p.diveT > 0) { p.dvx = p.dvz = 0; BotContact(p, plan); return; }
            if (p.hasBlockTask && (plan == null || plan.p != p))
            {
                Seek(p, p.blockX, NY - f * 0.45f, 0.4f); p.sprint = true;
                if (!p.air && t >= p.blockJumpAt) { Jump(p, 0.85f); p.blockJump = true; p.hasBlockTask = false; }
                if (t > p.blockJumpAt + 0.8f) p.hasBlockTask = false;
                return;
            }
            if (plan != null && plan.p == p && plan.kind != PlanKind.Leave)
            {
                if (plan.kind == PlanKind.Attack)
                {
                    float tPeak = Mathf.Sqrt(2 * GP * p.jumpH) / GP;
                    float sx = plan.point.x, sz = plan.point.z - f * 0.25f, tl = plan.t - t;
                    if (!p.air)
                    {
                        if (tl > tPeak + 0.45f) Seek(p, sx, sz - f * 1.6f, 0.3f); else Seek(p, sx, sz, 0.2f);
                        p.sprint = true;
                        if (tl <= tPeak + 0.01f && Hyp(p.pos.x - sx, p.pos.z - sz) < 1.7f)
                        {
                            Jump(p, 1);
                            // steer the jump so the hitting hand meets the ball at the peak
                            float jx = plan.point.x - p.pos.x, jz = plan.point.z - f * 0.15f - p.pos.z, k = 1f / Mathf.Max(tPeak, 0.2f);
                            var hv = new Vector2(jx * k, jz * k); if (hv.magnitude > 4.5f) hv = hv.normalized * 4.5f;
                            p.vel.x = hv.x; p.vel.z = hv.y;
                        }
                    }
                }
                else
                {
                    Seek(p, plan.point.x, plan.point.z - f * 0.25f, 0.25f); p.sprint = true;
                    float d = Hyp(p.pos.x - ball.pos.x, p.pos.z - ball.pos.z);
                    if (ball.live && ball.held == null && ball.pos.y < 0.9f && ball.vel.y < 0 && d > 0.95f && d < 2.6f && p.diveT <= 0 && Court.OnSide(tm, ball.pos.z))
                        Dive(p, ball.pos.x - p.pos.x, ball.pos.z - p.pos.z);
                }
                BotContact(p, plan);
                return;
            }
            var spos = SlotPos(p.slot);
            float hx = spos.x, hu = spos.y;
            var land = PredictY(R, out _);
            bool ourBall = ball.lastTeam == tm && Court.OnSide(tm, land.z);
            if (ourBall && IsFront(p)) hu = 2.6f;
            if (!ourBall && IsFront(p) && Court.OnSide(Other(tm), ball.pos.z)) hu = 1.0f;
            if (p.profile.style == PlayStyle.Setter && ourBall) { hx = 6; hu = 1.2f; }
            Seek(p, hx, Court.WorldZ(tm, hu), 0.6f);
        }
        void BotContact(SimPlayer p, Plan plan)
        {
            if (!LegalFor(p) || plan == null || plan.p != p) return;
            var ri = ReachInfo(p);
            if (ri.ok) BotHit(p, plan, ri);
        }

        void PhysBot(SimPlayer p, float dt)
        {
            if (p.diveT > 0) { p.diveT -= dt; float k = Mathf.Exp(-3 * dt); p.vel.x *= k; p.vel.z *= k; if (p.diveT <= 0) p.recT = 0.45f; }
            else if (p.air) { float k = Mathf.Exp(-0.6f * dt); p.vel.x *= k; p.vel.z *= k; }
            else if (p.recT > 0 || p.stunT > 0) { float k = Mathf.Exp(-10 * dt); p.vel.x *= k; p.vel.z *= k; }
            else
            {
                float ms = p.spd * (p.sprint ? 1.3f : 1f) * Bots.speed, a = Mathf.Min(1, dt * 10);
                p.vel.x += (p.dvx * ms - p.vel.x) * a; p.vel.z += (p.dvz * ms - p.vel.z) * a;
            }
            p.pos.x += p.vel.x * dt; p.pos.z += p.vel.z * dt;
            if (p.air)
            {
                p.vel.y -= GP * dt; p.pos.y += p.vel.y * dt;
                if (p.pos.y <= 0) { p.pos.y = 0; p.vel.y = 0; p.air = false; p.recT = Mathf.Max(p.recT, 0.18f); p.blockJump = false; }
            }
            else p.vel.y = 0;
            p.pos.z = p.team == 0 ? Mathf.Clamp(p.pos.z, -4, NY - 0.3f) : Mathf.Clamp(p.pos.z, NY + 0.3f, Court.Length + 4);
            p.pos.x = Mathf.Clamp(p.pos.x, -3, Court.Width + 3);
            // volleyball footwork: keep facing the ball (or the net) and shuffle; only turn to run on long, fast moves
            float spd = Hyp(p.vel.x, p.vel.z);
            float face;
            if (spd > 4.2f && !p.air) face = Mathf.Atan2(p.vel.x, p.vel.z) * Mathf.Rad2Deg;
            else if (ball.held == null && (ball.live || phase == MatchPhase.Serve)) face = Mathf.Atan2(ball.pos.x - p.pos.x, ball.pos.z - p.pos.z) * Mathf.Rad2Deg;
            else face = p.team == 0 ? 0 : 180;
            if (Court.Fwd(p.team) * Mathf.Cos(face * Mathf.Deg2Rad) < -0.2f && spd < 4.2f) face = p.team == 0 ? 0 : 180; // don't turn the back to the net
            p.yaw = Mathf.LerpAngle(p.yaw, face, 1 - Mathf.Exp(-12f * dt));
        }

        void UpdatePose(SimPlayer p, float dt)
        {
            if (p.poseT > 0) { p.poseT -= dt; return; }
            PoseId np;
            if (phase == MatchPhase.Point && p.celebrate != 0) np = p.celebrate > 0 ? PoseId.Celebrate : PoseId.Sad;
            else if (p.stunT > 0) np = PoseId.Stun;
            else if (p.diveT > 0) np = PoseId.Dive;
            else if (p.air)
            {
                var pl = plans[p.team];
                if (p.blockJump || p.hasBlockTask) np = PoseId.Block;
                else if (pl != null && pl.p == p && pl.kind == PlanKind.Attack) np = PoseId.SpikeWind;
                else if (!p.isBot && ball.live && Hyp(ball.pos.x - p.pos.x, ball.pos.z - p.pos.z) < 2.5f) np = PoseId.SpikeWind;
                else if (Mathf.Abs(p.pos.z - NY) < 1.2f) np = PoseId.Block;
                else np = PoseId.Jump;
            }
            else if (p == server && phase == MatchPhase.Serve) np = PoseId.Idle;
            else if (Hyp(p.vel.x, p.vel.z) > 0.6f) np = PoseId.Run;
            else np = phase == MatchPhase.Rally ? PoseId.Ready : PoseId.Idle;
            p.pose = np;
        }

        void Separate()
        {
            const float minD = 0.55f;
            for (int i = 0; i < players.Count; i++)
                for (int j = i + 1; j < players.Count; j++)
                {
                    var a = players[i]; var c = players[j];
                    if (a.team != c.team) continue;
                    float dx = c.pos.x - a.pos.x, dz = c.pos.z - a.pos.z, d = Hyp(dx, dz);
                    if (d >= minD || d < 1e-4f) continue;
                    float push = (minD - d) * 0.5f; dx /= d; dz /= d;
                    if (a.isBot) { a.pos.x -= dx * push * (c.isBot ? 1 : 2); a.pos.z -= dz * push * (c.isBot ? 1 : 2); }
                    if (c.isBot) { c.pos.x += dx * push * (a.isBot ? 1 : 2); c.pos.z += dz * push * (a.isBot ? 1 : 2); }
                }
        }

        // ================= ball =================
        void UpdateBall(float dt)
        {
            var b = ball;
            if (b.held != null)
            {
                var p = b.held;
                b.pos = new Vector3(p.pos.x + 0.25f, p.pos.y + 1.1f, p.pos.z + Court.Fwd(p.team) * 0.2f);
                return;
            }
            float pz = b.pos.z;
            b.hitT += dt;
            b.vel.y -= GB * dt;
            b.pos += b.vel * dt;

            if (b.live && (pz - NY) * (b.pos.z - NY) <= 0 && pz != b.pos.z)
            {
                if (b.pos.y < NH + R && b.pos.y > -1 && b.pos.x > -0.6f && b.pos.x < Court.Width + 0.6f)
                {
                    b.pos.z = pz < NY ? NY - R - 0.01f : NY + R + 0.01f;
                    b.vel = new Vector3(b.vel.x * 0.5f, Mathf.Min(b.vel.y, 0) * 0.3f, -b.vel.z * 0.2f);
                    Ev(GameEventType.Sound, "net", null, b.pos, null, 0, 0.8f);
                    Popup("В СЕТКУ!", Grey);
                    Replan();
                }
                else if (b.pos.x < -0.05f || b.pos.x > Court.Width + 0.05f)
                {
                    int hitter = b.lastTeam >= 0 ? b.lastTeam : (pz < NY ? 0 : 1);
                    Fault(hitter, "МИМО АНТЕНН");
                    return;
                }
                else { touches[0] = touches[1] = 0; }
            }
            if (b.live && !b.blockChecked && Mathf.Abs(b.pos.z - NY) < 0.45f && b.lastTeam >= 0)
            {
                int def = b.vel.z > 0 ? 1 : 0;
                if (def != b.lastTeam)
                    foreach (var p in players)
                    {
                        if (p.team != def || !p.air || Mathf.Abs(p.pos.z - NY) > 0.9f) continue;
                        if (p.profile.style == PlayStyle.Libero && teamSize >= 6) continue;   // libero may not block
                        float top = p.pos.y + p.reach + 0.3f, bot = p.pos.y + p.reach - 0.55f;
                        if (Mathf.Abs(b.pos.x - p.pos.x) < 0.65f && b.pos.y > bot && b.pos.y < top) { b.blockChecked = true; BlockHit(p); break; }
                    }
            }
            if (b.pos.y <= R && b.vel.y < 0)
            {
                b.pos.y = R;
                if (b.live) Land();
                b.vel = new Vector3(b.vel.x * 0.7f, -b.vel.y * 0.45f, b.vel.z * 0.7f);
            }
            if (b.pos.x < -7 || b.pos.x > Court.Width + 7) b.vel.x *= -0.5f;
            if (b.pos.z < -6 || b.pos.z > Court.Length + 6) b.vel.z *= -0.5f;
        }
        void BlockHit(SimPlayer p)
        {
            var b = ball; var attacker = b.lastP; float sp = Hyp(b.vel.x, b.vel.z);
            p.SetPose(PoseId.Block, 0.4f);
            bool special = p.armed > 0 && p.energy >= 100 && p.Style.finisherKind == FinisherKind.Block;
            if (b.superBy != null && !b.mini && !special)
            {
                p.stunT = 0.6f; b.vel.x *= 0.85f; b.vel.z *= 0.85f;
                Popup("ПРОБИЛ БЛОК!", Pink);
                Ev(GameEventType.Block, null, Pink, b.pos, p);
                return;
            }
            RegisterTouch(p, true);
            float pk = special ? 1 : Mathf.Clamp(0.32f + p.st.block * 0.045f * Bots.block - (sp - 14) * 0.02f, 0.08f, 0.85f);
            float r = Random.value;
            Ev(GameEventType.Block, null, Color.white, b.pos, p);
            if (r < pk)
            {
                float tx = Mathf.Clamp(b.pos.x + Rnd(-1.5f, 1.5f), 0.3f, Court.Width - 0.3f);
                float tz = attacker != null ? Court.WorldZ(attacker.team, Rnd(0.6f, 2.6f)) : b.pos.z - Mathf.Sign(b.vel.z) * 2;
                const float T = 0.32f;
                b.vel = new Vector3((tx - b.pos.x) / T, (R - b.pos.y + 0.5f * GB * T * T) / T, (tz - b.pos.z) / T);
                if (special) { p.energy = 0; p.armed = 0; freezeT = 1.0f; pendingAfterFreeze = null; Ev(GameEventType.Cutin, p.Style.finisherName, p.Style.c1, p.pos, p, (int)FinisherKind.Block, 1f); }
                Popup(special ? p.Style.finisherName.ToUpper() : "БЛОК!", p.team == 0 ? Cyan : Pink);
                slowT = 0.35f;
            }
            else if (r < pk + 0.35f) { b.vel = new Vector3(b.vel.x * 0.25f, 3.8f, b.vel.z * 0.2f); Popup("СМЯГЧИЛ", Grey); }
            else { b.vel = new Vector3((b.pos.x < 4.5f ? -1 : 1) * Rnd(5, 8), 3, b.vel.z * 0.5f); Popup("ОТ БЛОКА В АУТ", Pink); }
            Replan();
        }
        void Land()
        {
            var b = ball;
            bool inside = Court.InCourt(b.pos.x, b.pos.z, R);
            int sideTeam = b.pos.z < NY ? 0 : 1;
            bool big = b.superBy != null && !b.mini;
            var hero = b.superBy;
            Color c1 = big ? hero.Style.c1 : Color.white;
            Ev(GameEventType.Impact, null, c1, new Vector3(b.pos.x, 0, b.pos.z), hero, big ? 1 : 0, b.power);
            b.live = false; b.superBy = null;
            if (inside)
            {
                int win = Other(sideTeam);
                string why = "ОЧКО!";
                if (b.kind == "serve" && b.lastTeam == win) why = "ЭЙС!";
                else if (b.blockTouch && b.lastTeam == win) why = "БЛОК!";
                else if (b.kind == "spike") why = big ? "ДОБИВАНИЕ!!" : "УБОЙНЫЙ!";
                else if (b.kind == "tip") why = "ОБМАНКА!";
                if (big || why == "УБОЙНЫЙ!") { Ev(GameEventType.Cinematic, null, null, new Vector3(b.pos.x, 0.5f, b.pos.z), null, 0, 1.1f); slowT = 0.8f; }
                if (big) Popup("ドン!!", Color.white);
                AwardPoint(win, why, b.lastTeam == win ? b.lastP : hero);
            }
            else
            {
                int loser = b.lastTeam >= 0 ? b.lastTeam : sideTeam;
                AwardPoint(Other(loser), b.blockTouch ? "ОТ БЛОКА!" : "АУТ!", null);
            }
        }

        // ================= main step =================
        public void Step(float rdt)
        {
            if (freezeT > 0)
            {
                freezeT -= rdt;
                if (freezeT <= 0) { var a = pendingAfterFreeze; pendingAfterFreeze = null; a?.Invoke(); }
                return;
            }
            float dt = rdt * (slowT > 0 ? 0.2f : 1f);
            if (slowT > 0) slowT -= rdt;
            phaseT -= dt;
            foreach (var p in players)
            {
                p.stunT = Mathf.Max(0, p.stunT - dt); p.recT = Mathf.Max(0, p.recT - dt); p.callT = Mathf.Max(0, p.callT - dt);
                p.armed = Mathf.Max(0, p.armed - dt);
                if (!p.isBot && p.diveT > 0) p.diveT = Mathf.Max(0, p.diveT - dt);
                if (!p.isBot) p.stamina = 100; // stamina is simulated client-side for humans
            }

            switch (phase)
            {
                case MatchPhase.Serve: StepServe(dt); break;
                case MatchPhase.Point:
                    foreach (var p in players)
                    {
                        if (p.isBot) { p.dvx = p.dvz = 0; if (p.celebrate > 0 && !p.air && Random.value < dt * 1.5f) { p.vel.y = 4; p.air = true; } PhysBot(p, dt); }
                        UpdatePose(p, dt);
                    }
                    UpdateBall(dt);
                    if (phaseT <= 0) { if (CheckWin()) { phase = MatchPhase.End; Ev(GameEventType.MatchEnd, null, null, default, null, winner); } else SetupServe(); }
                    break;
                case MatchPhase.Rally: StepRally(dt); break;
            }
        }
        void StepServe(float dt)
        {
            foreach (var p in players)
            {
                if (p.isBot)
                {
                    if (p == server) { p.dvx = p.dvz = 0; }
                    else { var sp = SlotPos(p.slot); Seek(p, sp.x, Court.WorldZ(p.team, sp.y + (p.team != servingTeam && p.profile.style != PlayStyle.Setter ? 1.3f : 0)), 0.5f); }
                    PhysBot(p, dt);
                }
                else if (p == server) { p.pos.z = Court.WorldZ(p.team, 9.9f); p.pos.y = 0; }
                UpdatePose(p, dt);
            }
            if (server == null) return;
            if (server.serveCharging) server.serveHold += dt;
            if (!server.isBot && server.connected && ball.held == server && phaseT < -8f)
            {   // 8 seconds to serve
                phase = MatchPhase.Rally; Fault(server.team, "8 СЕКУНД НА ПОДАЧУ"); return;
            }
            if (server.isBot && phaseT <= 0.6f && ball.held != null)
            {
                ball.held = null; ball.vel = new Vector3(0, 4.6f, 0); ball.pos.y = 1.8f; server.SetPose(PoseId.ServeToss, 1f);
                if (server.st.serve >= 8) Jump(server, 0.8f);
            }
            if (!server.isBot && !server.connected && phaseT <= -3f && ball.held != null)
            {   // disconnected human server: auto-serve
                ball.held = null; DoServe(server, 0.5f, null, false); return;
            }
            UpdateBall(dt);
            if (ball.held == null && phase == MatchPhase.Serve)
            {
                if (server.isBot && ball.vel.y <= 0.2f)
                {
                    bool special = server.energy >= 100 && server.Style.finisherKind == FinisherKind.Serve && Random.value < 0.6f;
                    if (special) { var s = server; phase = MatchPhase.Rally; t = 0; UseFinisher(s, () => DoServe(s, 1, null, true)); }
                    else DoServe(server, Rnd(0.45f, 0.9f), null, false);
                }
                else if (!server.isBot && server.serveCharging && ball.pos.y < 1.3f && ball.vel.y < 0) ServeRelease(server);
            }
        }
        void StepRally(float dt)
        {
            t += dt;
            foreach (var p in players)
            {
                if (p.isBot) continue;
                // human: buffered hit + auto-receive assist
                if (p.hitBuf > 0)
                {
                    p.hitBuf -= dt;
                    if (LegalFor(p)) { var ri = ReachInfo(p); if (ri.ok) { p.hitBuf = 0; HumanHit(p, ri, false); } }
                }
                var plan = plans[p.team];
                if (plan != null && plan.p == p && plan.kind == PlanKind.Receive && LegalFor(p) && !p.air)
                {
                    float hd = Hyp(p.pos.x - ball.pos.x, p.pos.z - ball.pos.z);
                    if (hd < 0.55f && ball.pos.y < 1.0f && ball.vel.y < 0) { var ri = ReachInfo(p); if (ri.ok) { HumanHit(p, ri, true); } }
                }
                if (!p.connected && plan != null && plan.p == p) BotThinkAsProxy(p, plan);
                if (phase != MatchPhase.Rally) return;
            }
            foreach (var p in players) if (p.isBot) BotThink(p);
            Separate();
            foreach (var p in players)
            {
                if (p.isBot) { PhysBot(p, dt); p.energy = Mathf.Min(100, p.energy + dt * 0.4f); }
                else p.energy = Mathf.Min(100, p.energy + dt * 1.0f);
                UpdatePose(p, dt);
            }
            if (phase != MatchPhase.Rally) return;
            UpdateBall(dt);
            if (ball.live && (ball.pos.y < -2 || t > 40)) { ball.live = false; AwardPoint(Other(ball.lastTeam >= 0 ? ball.lastTeam : 0), "АУТ!", null); }
        }
        // a disconnected human still standing on court: let a teammate cover by re-planning
        void BotThinkAsProxy(SimPlayer p, Plan plan) { plans[p.team] = null; Replan(); }

        // ================= snapshot export =================
        public void Export(MatchView v)
        {
            v.phase = phase; v.score[0] = score[0]; v.score[1] = score[1];
            v.servingTeam = servingTeam; v.targetScore = targetScore; v.teamSize = teamSize; v.phaseTime = phaseT;
            v.winner = winner;
            v.slowMo = freezeT > 0 ? 1f : slowT > 0 ? 0.8f : 0f;
            v.serverPlayerId = server != null && phase == MatchPhase.Serve ? server.id : -1;
            if (v.players.Length != players.Count) v.players = new PlayerSnap[players.Count];
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                v.players[i] = new PlayerSnap
                {
                    id = p.id, team = (byte)p.team, slot = (byte)p.slot, isBot = p.isBot, connected = p.connected, profile = p.profile,
                    pos = p.pos, vel = p.vel, yaw = p.yaw, pose = p.pose, poseT = p.poseT, air = p.air, armed = p.armed > 0, calling = p.callT > 0,
                    energy = p.energy, stamina = p.stamina,
                };
            }
            v.ball = new BallSnap { pos = ball.pos, vel = ball.vel, live = ball.live, held = ball.held != null, superBy = ball.superBy != null ? ball.superBy.id : (byte)255, mini = ball.mini };
            for (int i = 0; i < 2; i++)
            {
                var pl = plans[i];
                v.plans[i] = pl == null || pl.p == null ? new PlanSnap { kind = PlanKind.None, playerId = 255 }
                    : new PlanSnap { kind = pl.kind, playerId = pl.p.id, point = pl.point, timeLeft = pl.t - t };
            }
            if (ball.live && ball.held == null) { v.landing = PredictY(R, out _); v.landing.y = 0; v.landingValid = true; }
            else v.landingValid = false;
        }
    }
}
