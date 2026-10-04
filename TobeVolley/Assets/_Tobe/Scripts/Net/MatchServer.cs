// Authoritative match host: lobby/matchmaking flow, bots filling empty slots, 60 Hz simulation, 30 Hz snapshots.
using System.Collections.Generic;
using Tobe.Sim;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Tobe.Net
{
    public sealed class MatchServer
    {
        const float Tick = 1f / 60f, SnapInterval = 1f / 30f;
        const float LobbyWait = 25f;   // seconds of waiting for humans before bots fill in

        readonly NetworkManager nm;
        readonly VolleySim sim = new VolleySim();
        readonly MatchView exportView = new MatchView();
        readonly Dictionary<ulong, SimPlayer> humans = new Dictionary<ulong, SimPlayer>();
        readonly bool practice;
        readonly int teamSize;
        byte nextId;
        float acc, snapAcc, lobbyT;
        bool started, rosterDirty;
        int sentEvents;

        /// <summary>Called by the host's own client to receive messages without a network round trip.</summary>
        public System.Action<string, byte[]> LocalDelivery;
        public ulong LocalClientId => nm.LocalClientId;

        public MatchServer(NetworkManager nm, int teamSize, bool practice)
        {
            this.nm = nm; this.teamSize = Mathf.Clamp(teamSize, 2, 6); this.practice = practice;
            sim.teamSize = this.teamSize; sim.targetScore = practice ? 15 : 15;
            sim.phase = MatchPhase.Lobby;
            var cmm = nm.CustomMessagingManager;
            cmm.RegisterNamedMessageHandler(Msg.Hello, (sender, reader) => OnHello(sender, NetIO.ReadBlob(reader)));
            cmm.RegisterNamedMessageHandler(Msg.Input, (sender, reader) => OnInput(sender, NetIO.ReadBlob(reader)));
            cmm.RegisterNamedMessageHandler(Msg.Start, (sender, reader) => { if (sender == nm.LocalClientId) StartNow(); });
            nm.OnClientDisconnectCallback += OnDisconnect;
        }

        public void Dispose()
        {
            if (nm == null || nm.CustomMessagingManager == null) return;
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(Msg.Hello);
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(Msg.Input);
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(Msg.Start);
            nm.OnClientDisconnectCallback -= OnDisconnect;
        }

        // ---------- incoming ----------
        public void OnHello(ulong sender, byte[] data)
        {
            PlayerProfile prof;
            using (var r = new System.IO.BinaryReader(new System.IO.MemoryStream(data))) prof = r.Profile();
            if (humans.TryGetValue(sender, out var existing)) { sim.ApplyProfile(existing, prof); rosterDirty = true; return; }

            // team with fewer humans; slot preferred by style
            int c0 = 0, c1 = 0; foreach (var h in humans.Values) if (h.team == 0) c0++; else c1++;
            int team = c0 <= c1 ? 0 : 1;
            if ((team == 0 ? c0 : c1) >= teamSize) team = 1 - team;
            if ((team == 0 ? c0 : c1) >= teamSize && (team == 0 ? c1 : c0) >= teamSize) { nm.DisconnectClient(sender); return; }

            SimPlayer p;
            var bot = started ? sim.players.Find(b => b.isBot && b.team == team) : null;
            if (bot != null)
            {   // join in progress: take over a bot's spot (Rematch-style)
                p = bot; p.isBot = false; p.clientId = sender; p.connected = true; sim.ApplyProfile(p, prof);
            }
            else
            {
                int slot = FreeSlot(team, prof.style);
                if (slot < 0) { nm.DisconnectClient(sender); return; }
                p = sim.AddPlayer(nextId++, team, slot, prof, false, sender);
                p.pos = new Vector3(1.5f + slot * 1.2f, 0, Court.WorldZ(team, 4));
            }
            humans[sender] = p;
            rosterDirty = true;
        }
        int FreeSlot(int team, PlayStyle style)
        {
            var used = new bool[teamSize];
            foreach (var q in sim.players) if (q.team == team && q.slot < teamSize) used[q.slot] = true;
            for (int s = 0; s < teamSize; s++) if (!used[s] && VolleySim.BotStyleFor(teamSize, s) == style) return s;
            for (int s = 0; s < teamSize; s++) if (!used[s]) return s;
            return -1;
        }
        public void OnInput(ulong sender, byte[] data)
        {
            if (!humans.TryGetValue(sender, out var p)) return;
            var cmd = Ser.Input(data);
            if (started) sim.ApplyHuman(p, cmd, Tick); else { p.lastCmd = cmd; p.pos = cmd.clientPos; }
        }
        void OnDisconnect(ulong clientId)
        {
            if (!humans.TryGetValue(clientId, out var p)) return;
            humans.Remove(clientId);
            if (started) { p.isBot = true; p.connected = true; p.profile.nick += " (бот)"; }
            else sim.players.Remove(p);
            rosterDirty = true;
        }

        public void StartNow()
        {
            if (started) return;
            started = true;
            for (int team = 0; team < 2; team++)
                for (int s = 0; s < teamSize; s++)
                {
                    bool taken = sim.players.Exists(q => q.team == team && q.slot == s);
                    if (!taken) sim.AddPlayer(nextId++, team, s, VolleySim.BotProfile(teamSize, team, s), true, 0);
                }
            sim.StartMatch(Random.value < 0.5f ? 0 : 1);
            sim.events.Add(new GameEvent { type = GameEventType.Whistle, playerId = 255 });
            sim.events.Add(new GameEvent { type = GameEventType.Popup, text = "МАТЧ НАЧАЛСЯ!", color = new Color(1f, 0.82f, 0.25f), intArg = 1, playerId = 255 });
            rosterDirty = true;
        }

        // ---------- loop ----------
        public void Update(float dt)
        {
            if (!started)
            {
                int humansN = humans.Count;
                lobbyT += dt;
                if (practice && humansN > 0 && lobbyT > 1f) StartNow();
                else if (!practice)
                {
                    if (humansN >= teamSize * 2) StartNow();
                    else if (humansN > 0 && lobbyT > LobbyWait) StartNow();
                }
            }
            else
            {
                acc += Mathf.Min(dt, 0.1f);
                while (acc >= Tick) { acc -= Tick; sim.Step(Tick); }
            }

            snapAcc += dt;
            if (rosterDirty) { rosterDirty = false; SendRoster(); }
            FlushEvents();
            if (snapAcc >= SnapInterval) { snapAcc = 0; SendSnapshot(); }
            if (sim.phase == MatchPhase.End && sim.events.Count > 2000) sim.events.Clear();
        }

        void Export()
        {
            sim.Export(exportView);
            if (!started)
            {
                exportView.phase = humans.Count > 0 && !practice && lobbyT > LobbyWait - 5 ? MatchPhase.Countdown : MatchPhase.Lobby;
                exportView.phaseTime = Mathf.Max(0, LobbyWait - lobbyT);
                exportView.lobbyStatus = practice ? "Тренировка: боты выходят на площадку..." :
                    $"Ищем игроков: {humans.Count}/{teamSize * 2}. Через {Mathf.CeilToInt(Mathf.Max(0, LobbyWait - lobbyT))} c свободные места займут боты";
            }
            else exportView.lobbyStatus = "";
        }
        void SendSnapshot()
        {
            Export();
            var data = Ser.Snapshot(exportView);
            foreach (var id in nm.ConnectedClientsIds)
            {
                if (id == nm.LocalClientId) continue;
                NetIO.Send(nm, Msg.Snap, id, data, NetworkDelivery.UnreliableSequenced);
            }
            LocalDelivery?.Invoke(Msg.Snap, data);
        }
        void SendRoster()
        {
            Export();
            foreach (var id in nm.ConnectedClientsIds)
            {
                int yours = humans.TryGetValue(id, out var p) ? p.id : -1;
                var data = Ser.Roster(exportView, yours);
                if (id == nm.LocalClientId) LocalDelivery?.Invoke(Msg.Roster, data);
                else NetIO.Send(nm, Msg.Roster, id, data, NetworkDelivery.ReliableSequenced);
            }
        }
        void FlushEvents()
        {
            var evs = sim.events;
            while (sentEvents < evs.Count)
            {
                int n = Mathf.Min(32, evs.Count - sentEvents);
                var data = Ser.Events(evs, sentEvents, n);
                sentEvents += n;
                foreach (var id in nm.ConnectedClientsIds)
                {
                    if (id == nm.LocalClientId) continue;
                    NetIO.Send(nm, Msg.Event, id, data, NetworkDelivery.ReliableSequenced);
                }
                LocalDelivery?.Invoke(Msg.Event, data);
            }
            if (sentEvents > 512) { evs.RemoveRange(0, sentEvents); sentEvents = 0; }
        }
    }

    public static class NetIO
    {
        public static byte[] ReadBlob(FastBufferReader reader)
        {
            reader.ReadValueSafe(out int len);
            var buf = new byte[len];
            reader.ReadBytesSafe(ref buf, len);
            return buf;
        }
        public static void Send(NetworkManager nm, string msg, ulong clientId, byte[] data, NetworkDelivery delivery)
        {
            var w = new FastBufferWriter(data.Length + 8, Allocator.Temp);
            try
            {
                w.WriteValueSafe(data.Length);
                w.WriteBytesSafe(data);
                nm.CustomMessagingManager.SendNamedMessage(msg, clientId, w, delivery);
            }
            finally { w.Dispose(); }
        }
    }
}
