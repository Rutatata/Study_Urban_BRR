// Wire format. Payloads are built with BinaryWriter and shipped as byte blobs through NGO named messages,
// so the game needs no NetworkObjects/prefabs (everything is bootstrapped from code).
using System.IO;
using System.Text;
using UnityEngine;

namespace Tobe.Net
{
    public static class Msg
    {
        public const string Hello = "tobe.hello";     // client -> server (reliable): profile
        public const string Input = "tobe.in";        // client -> server (unreliable sequenced): InputCmd
        public const string Start = "tobe.start";     // client -> server (reliable): host asks to start now
        public const string Snap = "tobe.snap";       // server -> client (unreliable sequenced): MatchView
        public const string Roster = "tobe.roster";   // server -> client (reliable): roster + your player id
        public const string Event = "tobe.evt";       // server -> client (reliable): GameEvent batch
    }

    public static class Ser
    {
        /// <summary>Server's idea of the local player's position (the local player itself is client-simulated).</summary>
        public static Vector3 LocalServerPos;
        public static float LocalServerYaw;
        public static void W(this BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        public static Vector3 V3(this BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        public static void W(this BinaryWriter w, Color c) { w.Write((byte)(Mathf.Clamp01(c.r) * 255)); w.Write((byte)(Mathf.Clamp01(c.g) * 255)); w.Write((byte)(Mathf.Clamp01(c.b) * 255)); }
        public static Color Col(this BinaryReader r) => new Color(r.ReadByte() / 255f, r.ReadByte() / 255f, r.ReadByte() / 255f);
        // compact vectors for snapshots (centimeter precision is plenty)
        public static void WS(this BinaryWriter w, Vector3 v) { w.Write((short)Mathf.Clamp(v.x * 100, -32000, 32000)); w.Write((short)Mathf.Clamp(v.y * 100, -32000, 32000)); w.Write((short)Mathf.Clamp(v.z * 100, -32000, 32000)); }
        public static Vector3 VS(this BinaryReader r) => new Vector3(r.ReadInt16() / 100f, r.ReadInt16() / 100f, r.ReadInt16() / 100f);

        public static void W(this BinaryWriter w, PlayerProfile p)
        {
            w.Write(p.nick ?? ""); w.Write((byte)p.style); w.Write(p.model); w.Write(p.hair); w.Write(p.number);
            w.Write(p.skin); w.Write(p.height); w.Write(p.build); w.Write(p.eyes); w.Write(p.gear);
        }
        public static PlayerProfile Profile(this BinaryReader r)
        {
            var p = new PlayerProfile { nick = r.ReadString(), style = (PlayStyle)r.ReadByte(), model = r.ReadByte(), hair = r.ReadByte(), number = r.ReadByte(),
                skin = r.ReadByte(), height = r.ReadByte(), build = r.ReadByte(), eyes = r.ReadByte(), gear = r.ReadByte() };
            if (p.nick.Length > 16) p.nick = p.nick.Substring(0, 16);
            if ((int)p.style > 4) p.style = PlayStyle.Outside;
            return p;
        }

        public static byte[] Input(in InputCmd c)
        {
            using var ms = new MemoryStream(64); using var w = new BinaryWriter(ms);
            w.Write(c.move.x); w.Write(c.move.y); w.Write(c.sprint); w.W(c.aim);
            w.Write(c.jumpCount); w.Write(c.hitPressCount); w.Write(c.hitReleaseCount); w.Write(c.diveCount); w.Write(c.callCount); w.Write(c.specialCount);
            w.Write(c.hitHeld); w.W(c.clientPos); w.W(c.clientVel); w.Write(c.clientAir); w.Write(c.clientDiving);
            return ms.ToArray();
        }
        public static InputCmd Input(byte[] b)
        {
            using var r = new BinaryReader(new MemoryStream(b));
            var c = new InputCmd();
            c.move = new Vector2(r.ReadSingle(), r.ReadSingle()); c.sprint = r.ReadBoolean(); c.aim = r.V3();
            c.jumpCount = r.ReadUInt16(); c.hitPressCount = r.ReadUInt16(); c.hitReleaseCount = r.ReadUInt16(); c.diveCount = r.ReadUInt16(); c.callCount = r.ReadUInt16(); c.specialCount = r.ReadUInt16();
            c.hitHeld = r.ReadBoolean(); c.clientPos = r.V3(); c.clientVel = r.V3(); c.clientAir = r.ReadBoolean(); c.clientDiving = r.ReadBoolean();
            return c;
        }

        // ---- snapshot: everything except profiles (those travel in the roster) ----
        public static byte[] Snapshot(MatchView v)
        {
            using var ms = new MemoryStream(512); using var w = new BinaryWriter(ms);
            w.Write((byte)v.phase); w.Write((byte)v.score[0]); w.Write((byte)v.score[1]); w.Write((byte)v.servingTeam);
            w.Write((byte)v.targetScore); w.Write((byte)v.teamSize); w.Write(v.phaseTime); w.Write(v.slowMo);
            w.Write((sbyte)v.serverPlayerId); w.Write((sbyte)v.winner); w.Write(v.lobbyStatus ?? "");
            w.Write((byte)v.players.Length);
            foreach (var p in v.players)
            {
                w.Write(p.id); w.WS(p.pos); w.WS(p.vel); w.Write((short)p.yaw); w.Write((byte)p.pose); w.Write((byte)Mathf.Clamp(p.poseT * 50, 0, 255));
                byte f = (byte)((p.air ? 1 : 0) | (p.armed ? 2 : 0) | (p.calling ? 4 : 0) | (p.connected ? 8 : 0));
                w.Write(f); w.Write((byte)p.energy); w.Write((byte)p.stamina);
            }
            var b = v.ball;
            w.WS(b.pos); w.WS(b.vel); w.Write((byte)((b.live ? 1 : 0) | (b.held ? 2 : 0) | (b.mini ? 4 : 0))); w.Write(b.superBy);
            for (int i = 0; i < 2; i++) { var pl = v.plans[i]; w.Write((byte)pl.kind); w.Write(pl.playerId); w.WS(pl.point); w.Write(pl.timeLeft); }
            w.Write(v.landingValid); w.WS(v.landing);
            return ms.ToArray();
        }
        /// <summary>Applies a snapshot onto an existing view; per-player profiles/team/slot/isBot are kept from the roster.</summary>
        public static void ReadSnapshot(byte[] data, MatchView v)
        {
            using var r = new BinaryReader(new MemoryStream(data));
            v.phase = (MatchPhase)r.ReadByte(); v.score[0] = r.ReadByte(); v.score[1] = r.ReadByte(); v.servingTeam = r.ReadByte();
            v.targetScore = r.ReadByte(); v.teamSize = r.ReadByte(); v.phaseTime = r.ReadSingle(); v.slowMo = r.ReadSingle();
            v.serverPlayerId = r.ReadSByte(); v.winner = r.ReadSByte(); v.lobbyStatus = r.ReadString();
            int n = r.ReadByte();
            for (int k = 0; k < n; k++)
            {
                byte id = r.ReadByte();
                var pos = r.VS(); var vel = r.VS(); float yaw = r.ReadInt16(); var pose = (PoseId)r.ReadByte(); float poseT = r.ReadByte() / 50f;
                byte f = r.ReadByte(); float energy = r.ReadByte(), stam = r.ReadByte();
                for (int i = 0; i < v.players.Length; i++)
                {
                    if (v.players[i].id != id) continue;
                    ref var p = ref v.players[i];
                    if (id != v.localPlayerId) { p.pos = pos; p.vel = vel; p.yaw = yaw; p.air = (f & 1) != 0; }
                    else { LocalServerPos = pos; LocalServerYaw = yaw; }
                    p.pose = pose; p.poseT = poseT; p.armed = (f & 2) != 0; p.calling = (f & 4) != 0; p.connected = (f & 8) != 0;
                    p.energy = energy; if (id != v.localPlayerId) p.stamina = stam;
                    break;
                }
            }
            var ball = new BallSnap { pos = r.VS(), vel = r.VS() };
            byte bf = r.ReadByte(); ball.live = (bf & 1) != 0; ball.held = (bf & 2) != 0; ball.mini = (bf & 4) != 0; ball.superBy = r.ReadByte();
            v.ball = ball;
            for (int i = 0; i < 2; i++) v.plans[i] = new PlanSnap { kind = (PlanKind)r.ReadByte(), playerId = r.ReadByte(), point = r.VS(), timeLeft = r.ReadSingle() };
            v.landingValid = r.ReadBoolean(); v.landing = r.VS();
        }

        public static byte[] Roster(MatchView v, int yourId)
        {
            using var ms = new MemoryStream(512); using var w = new BinaryWriter(ms);
            w.Write((sbyte)yourId); w.Write((byte)v.players.Length);
            foreach (var p in v.players) { w.Write(p.id); w.Write(p.team); w.Write(p.slot); w.Write(p.isBot); w.W(p.profile); w.WS(p.pos); }
            return ms.ToArray();
        }
        public static void ReadRoster(byte[] data, MatchView v)
        {
            using var r = new BinaryReader(new MemoryStream(data));
            v.localPlayerId = r.ReadSByte();
            int n = r.ReadByte();
            var arr = new PlayerSnap[n];
            for (int i = 0; i < n; i++)
            {
                arr[i] = new PlayerSnap { id = r.ReadByte(), team = r.ReadByte(), slot = r.ReadByte(), isBot = r.ReadBoolean(), profile = r.Profile(), pos = r.VS(), connected = true, stamina = 100 };
                for (int k = 0; k < v.players.Length; k++)          // keep live state for players we already know
                    if (v.players[k].id == arr[i].id) { var old = v.players[k]; old.team = arr[i].team; old.slot = arr[i].slot; old.isBot = arr[i].isBot; old.profile = arr[i].profile; arr[i] = old; break; }
            }
            v.players = arr;
            foreach (var p in arr) if (p.id == v.localPlayerId) LocalServerPos = p.pos;
        }

        public static byte[] Events(System.Collections.Generic.List<GameEvent> evs, int start, int count)
        {
            using var ms = new MemoryStream(256); using var w = new BinaryWriter(ms, Encoding.UTF8);
            w.Write((byte)count);
            for (int i = start; i < start + count; i++)
            {
                var e = evs[i];
                w.Write((byte)e.type); w.Write(e.text ?? ""); w.W(e.color); w.W(e.pos); w.Write(e.playerId); w.Write(e.intArg); w.Write(e.floatArg);
            }
            return ms.ToArray();
        }
        public static GameEvent[] ReadEvents(byte[] data)
        {
            using var r = new BinaryReader(new MemoryStream(data), Encoding.UTF8);
            int n = r.ReadByte();
            var a = new GameEvent[n];
            for (int i = 0; i < n; i++)
                a[i] = new GameEvent { type = (GameEventType)r.ReadByte(), text = r.ReadString(), color = r.Col(), pos = r.V3(), playerId = r.ReadByte(), intArg = r.ReadInt32(), floatArg = r.ReadSingle() };
            return a;
        }
    }
}
