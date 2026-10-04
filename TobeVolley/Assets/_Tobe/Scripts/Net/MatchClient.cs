// Client side: receives roster/snapshots/events into GameHub, simulates the local player's movement
// (responsive, client-authoritative like Rematch) and streams input to the server.
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Tobe.Net
{
    public sealed class MatchClient
    {
        readonly NetworkManager nm;
        readonly MatchServer localServer;   // non-null on the host
        InputCmd cmd;
        float sendAcc;
        MatchPhase lastPhase = MatchPhase.Lobby;
        bool helloSent;

        // local movement state
        Vector3 pos, vel;
        bool air, diving, hasPos;
        float diveT, recT, stamina = 100;

        public MatchClient(NetworkManager nm, MatchServer localServer)
        {
            this.nm = nm; this.localServer = localServer;
            var v = GameHub.View;
            v.players = new PlayerSnap[0]; v.localPlayerId = -1; v.phase = MatchPhase.Lobby; v.score[0] = v.score[1] = 0; v.winner = -1;
            if (localServer != null) localServer.LocalDelivery = Deliver;
            else
            {
                var cmm = nm.CustomMessagingManager;
                cmm.RegisterNamedMessageHandler(Msg.Snap, (s, r) => Deliver(Msg.Snap, NetIO.ReadBlob(r)));
                cmm.RegisterNamedMessageHandler(Msg.Roster, (s, r) => Deliver(Msg.Roster, NetIO.ReadBlob(r)));
                cmm.RegisterNamedMessageHandler(Msg.Event, (s, r) => Deliver(Msg.Event, NetIO.ReadBlob(r)));
            }
        }

        public void Dispose()
        {
            if (localServer != null || nm == null || nm.CustomMessagingManager == null) return;
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(Msg.Snap);
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(Msg.Roster);
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(Msg.Event);
        }

        void Deliver(string msg, byte[] data)
        {
            var v = GameHub.View;
            switch (msg)
            {
                case Msg.Snap: Ser.ReadSnapshot(data, v); break;
                case Msg.Roster:
                    int before = v.localPlayerId;
                    Ser.ReadRoster(data, v);
                    if (v.localPlayerId != before) hasPos = false;
                    GameHub.RaiseRoster();
                    break;
                case Msg.Event:
                    foreach (var e in Ser.ReadEvents(data)) GameHub.Raise(e);
                    break;
            }
        }

        public void SendHello()
        {
            using var ms = new System.IO.MemoryStream(); using var w = new System.IO.BinaryWriter(ms);
            w.W(GameHub.LocalProfile);
            var data = ms.ToArray();
            if (localServer != null) localServer.OnHello(nm.LocalClientId, data);
            else NetIO.Send(nm, Msg.Hello, NetworkManager.ServerClientId, data, NetworkDelivery.ReliableSequenced);
            helloSent = true;
        }
        public void RequestStart()
        {
            if (localServer != null) localServer.StartNow();
            else NetIO.Send(nm, Msg.Start, NetworkManager.ServerClientId, new byte[] { 1 }, NetworkDelivery.ReliableSequenced);
        }

        public void Update(float dt)
        {
            if (!helloSent && nm.IsConnectedClient) SendHello();
            var v = GameHub.View;
            if (v.localPlayerId < 0 || !v.TryGetLocal(out var me)) { lastPhase = v.phase; return; }

            // snap to the server position on (re)spawn, at every serve setup, while serving, or if we drifted far away
            bool serving = v.phase == MatchPhase.Serve && v.serverPlayerId == me.id;
            bool newServe = v.phase == MatchPhase.Serve && lastPhase != MatchPhase.Serve;
            if (!hasPos || newServe || serving && (Ser.LocalServerPos - pos).sqrMagnitude > 0.04f || (Ser.LocalServerPos - pos).sqrMagnitude > 16f && v.phase != MatchPhase.Lobby)
            { pos = Ser.LocalServerPos; vel = Vector3.zero; air = false; hasPos = true; }
            lastPhase = v.phase;

            float timeScale = 1f - Mathf.Clamp01(v.slowMo);
            Simulate(me, dt * timeScale, serving);
            WriteLocal(v, me.id);

            sendAcc += dt;
            if (sendAcc >= 1f / 30f)
            {
                sendAcc = 0;
                cmd.clientPos = pos; cmd.clientVel = vel; cmd.clientAir = air; cmd.clientDiving = diving;
                cmd.aim = GameHub.AimValid ? GameHub.AimPoint : pos + Quaternion.Euler(0, GameHub.CameraYaw, 0) * Vector3.forward * 8f;
                var data = Ser.Input(cmd);
                if (localServer != null) localServer.OnInput(nm.LocalClientId, data);
                else NetIO.Send(nm, Msg.Input, NetworkManager.ServerClientId, data, NetworkDelivery.UnreliableSequenced);
            }
        }

        void Simulate(PlayerSnap me, float dt, bool serving)
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            bool canPlay = !GameHub.UiBlocking && (GameHub.View.phase == MatchPhase.Serve || GameHub.View.phase == MatchPhase.Rally || GameHub.View.phase == MatchPhase.Point || GameHub.View.phase == MatchPhase.Lobby || GameHub.View.phase == MatchPhase.Countdown);
            Vector2 mv = Vector2.zero; bool sprint = false;
            if (canPlay && kb != null)
            {
                float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0);
                float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0);
                var dir = Quaternion.Euler(0, GameHub.CameraYaw, 0) * new Vector3(x, 0, y);
                mv = new Vector2(dir.x, dir.z); if (mv.sqrMagnitude > 1) mv.Normalize();
                sprint = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                if (kb.spaceKey.wasPressedThisFrame || mouse != null && mouse.rightButton.wasPressedThisFrame) { cmd.jumpCount++; TryJump(me); }
                if (kb.eKey.wasPressedThisFrame) { cmd.diveCount++; TryDive(mv); }
                if (kb.cKey.wasPressedThisFrame) cmd.callCount++;
                if (kb.qKey.wasPressedThisFrame) cmd.specialCount++;
                bool hitDown = mouse != null && mouse.leftButton.wasPressedThisFrame || kb.jKey.wasPressedThisFrame;
                bool hitUp = mouse != null && mouse.leftButton.wasReleasedThisFrame || kb.jKey.wasReleasedThisFrame;
                if (hitDown) cmd.hitPressCount++;
                if (hitUp) cmd.hitReleaseCount++;
                cmd.hitHeld = mouse != null && mouse.leftButton.isPressed || kb.jKey.isPressed;
            }
            cmd.move = mv; cmd.sprint = sprint;
            if (dt <= 0) return;

            var st = Styles.Get(me.profile.style).stats;
            float spd = 3.6f + st.speed * 0.28f;
            bool stunned = me.pose == PoseId.Stun;
            if (diveT > 0) { diveT -= dt; float k = Mathf.Exp(-3 * dt); vel.x *= k; vel.z *= k; if (diveT <= 0) { diving = false; recT = 0.45f; } }
            else if (air) { float k = Mathf.Exp(-0.6f * dt); vel.x *= k; vel.z *= k; }
            else if (recT > 0 || stunned || serving) { recT -= dt; float k = Mathf.Exp(-10 * dt); vel.x *= k; vel.z *= k; }
            else
            {
                bool useSprint = sprint && mv.sqrMagnitude > 0.01f && stamina > 2;
                if (useSprint) stamina -= 28 * dt; else stamina = Mathf.Min(100, stamina + 14 * dt);
                float ms = spd * (useSprint ? 1.3f : 1f), a = Mathf.Min(1, dt * 10);
                vel.x += (mv.x * ms - vel.x) * a; vel.z += (mv.y * ms - vel.z) * a;
            }
            if (serving) { vel.x = mv.x * 2f; vel.z = 0; }
            pos.x += vel.x * dt; pos.z += vel.z * dt;
            if (air)
            {
                vel.y -= Court.PlayerGravity * dt; pos.y += vel.y * dt;
                if (pos.y <= 0) { pos.y = 0; vel.y = 0; air = false; recT = Mathf.Max(recT, 0.18f); }
            }
            int team = me.team;
            // don't walk through teammates
            foreach (var o in GameHub.View.players)
            {
                if (o.id == me.id || o.team != team) continue;
                float dx = pos.x - o.pos.x, dz = pos.z - o.pos.z, d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < 0.55f && d > 1e-4f) { pos.x += dx / d * (0.55f - d); pos.z += dz / d * (0.55f - d); }
            }
            // never cross (or touch) the net: stay on your own side, inside the hall
            pos.z = team == 0 ? Mathf.Clamp(pos.z, -4, Court.NetZ - 0.35f) : Mathf.Clamp(pos.z, Court.NetZ + 0.35f, Court.Length + 4);
            pos.x = Mathf.Clamp(pos.x, -3, Court.Width + 3);
            if (serving) pos.z = Ser.LocalServerPos.z;
        }
        void TryJump(PlayerSnap me)
        {
            if (air || recT > 0 || diveT > 0 || me.pose == PoseId.Stun) return;
            var st = Styles.Get(me.profile.style).stats;
            float jumpH = 0.5f + st.jump * 0.065f, run = new Vector2(vel.x, vel.z).magnitude;
            vel.y = Mathf.Sqrt(2 * Court.PlayerGravity * jumpH) * (1 + Mathf.Min(run, 6) * 0.012f);
            air = true;
        }
        void TryDive(Vector2 mv)
        {
            if (air || diveT > 0 || recT > 0) return;
            if (mv.sqrMagnitude < 0.01f) mv = new Vector2(Mathf.Sin(GameHub.CameraYaw * Mathf.Deg2Rad), Mathf.Cos(GameHub.CameraYaw * Mathf.Deg2Rad));
            mv.Normalize();
            diveT = 0.5f; diving = true; vel.x = mv.x * 6.5f; vel.z = mv.y * 6.5f;
        }
        void WriteLocal(MatchView v, int id)
        {
            for (int i = 0; i < v.players.Length; i++)
            {
                if (v.players[i].id != id) continue;
                ref var p = ref v.players[i];
                p.pos = pos; p.vel = vel; p.air = air; p.stamina = stamina;
                var flat = new Vector2(vel.x, vel.z);
                // volleyball footwork: face where the camera looks (ball/net) and shuffle; turn into the run only when sprinting far
                float face = flat.magnitude > 4.2f && !air ? Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg : GameHub.CameraYaw;
                p.yaw = Mathf.LerpAngle(p.yaw, face, 1 - Mathf.Exp(-14f * Time.unscaledDeltaTime));
                // immediate local pose feedback (server pose arrives a bit later)
                if (diving) p.pose = PoseId.Dive;
                else if (air && p.pose != PoseId.Spike && p.pose != PoseId.Block && p.pose != PoseId.SpikeWind) p.pose = PoseId.Jump;
                else if (!air && flat.sqrMagnitude > 0.4f && (p.pose == PoseId.Idle || p.pose == PoseId.Ready)) p.pose = PoseId.Run;
                break;
            }
        }
    }
}
