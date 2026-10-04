// Matchmaking & session lifecycle.
// Online: Unity Gaming Services "Multiplayer Services" sessions over Relay (quick join or create, private code).
// Offline practice: local host on 127.0.0.1, bots only.
using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Tobe.Net
{
    [DefaultExecutionOrder(-200)]   // update state (and smoothing) before any view reads it
    public sealed class NetSession : MonoBehaviour
    {
        NetworkManager nm;
        MatchServer server;
        MatchClient client;
        ISession session;
        int pendingTeamSize = 3;
        bool pendingPractice;

        public static NetSession Create(GameObject root)
        {
            var s = root.AddComponent<NetSession>();
            s.Init();
            return s;
        }

        void Init()
        {
            nm = gameObject.AddComponent<NetworkManager>();
            var utp = gameObject.AddComponent<UnityTransport>();
            nm.NetworkConfig = new NetworkConfig { NetworkTransport = utp, EnableSceneManagement = false, ConnectionApproval = false, TickRate = 60 };
            nm.OnServerStarted += OnServerStarted;
            nm.OnClientStarted += OnClientStarted;
            nm.OnClientStopped += _ => Cleanup();

            NetApi.QuickMatch = QuickMatch;
            NetApi.CreatePrivate = CreatePrivate;
            NetApi.JoinByCode = JoinByCode;
            NetApi.StartPractice = StartPractice;
            NetApi.Leave = () => _ = LeaveAsync();
            NetApi.StartNow = () => client?.RequestStart();
            NetApi.SetPaused = p => { if (pendingPractice && server != null) server.Paused = p; };
        }

        void OnServerStarted()
        {
            if (server != null && client != null) return;   // already created by OnClientStarted (host)
            server?.Dispose();
            server = new MatchServer(nm, pendingTeamSize, pendingPractice);
            NetApi.PauseAllowed = pendingPractice;
        }
        void OnClientStarted()
        {
            if (nm.IsServer && server == null) OnServerStarted();
            client?.Dispose();
            client = new MatchClient(nm, nm.IsServer ? server : null);
            NetApi.InSession = true;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            server?.Update(dt);
            client?.Update(dt);
        }

        // ---------------- online ----------------
        static async Task EnsureServices()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        async Task QuickMatch(int teamSize)
        {
            if (NetApi.Busy) return;
            try
            {
                NetApi.SetStatus("Подключение к сервисам...", true);
                await EnsureServices();
                NetApi.SetStatus($"Поиск матча {teamSize}×{teamSize}...", true);
                pendingTeamSize = teamSize; pendingPractice = false;
                var options = new SessionOptions { MaxPlayers = teamSize * 2, Name = $"tobe-{teamSize}v{teamSize}" }.WithRelayNetwork();
                var quick = new QuickJoinOptions { Timeout = TimeSpan.FromSeconds(4), CreateSession = true };
                session = await MultiplayerService.Instance.MatchmakeSessionAsync(quick, options);
                NetApi.SessionCode = session.Code ?? "";
                NetApi.SetStatus(session.IsHost ? "Матч создан — ждём игроков" : "Подключились к матчу", false);
            }
            catch (Exception e) { Fail(e); }
        }

        async Task<string> CreatePrivate(int teamSize)
        {
            if (NetApi.Busy) return "";
            try
            {
                NetApi.SetStatus("Создаём приватный матч...", true);
                await EnsureServices();
                pendingTeamSize = teamSize; pendingPractice = false;
                var options = new SessionOptions { MaxPlayers = teamSize * 2, IsPrivate = true, Name = $"tobe-private-{teamSize}" }.WithRelayNetwork();
                session = await MultiplayerService.Instance.CreateSessionAsync(options);
                NetApi.SessionCode = session.Code ?? "";
                NetApi.SetStatus($"Код матча: {NetApi.SessionCode}", false);
                return NetApi.SessionCode;
            }
            catch (Exception e) { Fail(e); return ""; }
        }

        async Task JoinByCode(string code)
        {
            if (NetApi.Busy) return;
            try
            {
                NetApi.SetStatus("Входим по коду...", true);
                await EnsureServices();
                session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
                NetApi.SessionCode = session.Code ?? code;
                NetApi.SetStatus("Подключились к матчу", false);
            }
            catch (Exception e) { Fail(e); }
        }

        void Fail(Exception e)
        {
            Debug.LogWarning("[Tobe] Online error: " + e);
            session = null;
            NetApi.SetStatus("Онлайн недоступен: " + e.Message + "\nПривяжи проект к Unity Gaming Services (см. README). Тренировка работает офлайн.", false);
        }

        // ---------------- offline ----------------
        void StartPractice(int teamSize)
        {
            if (nm.IsListening) return;
            pendingTeamSize = teamSize; pendingPractice = true;
            var utp = (UnityTransport)nm.NetworkConfig.NetworkTransport;
            utp.SetConnectionData("127.0.0.1", 7777);
            if (!nm.StartHost()) { NetApi.SetStatus("Не удалось запустить локальный матч (порт 7777 занят?)", false); return; }
            NetApi.SessionCode = "";
            NetApi.SetStatus("Тренировка с ботами", false);
        }

        // ---------------- leave ----------------
        async Task LeaveAsync()
        {
            var s = session; session = null;
            if (nm.IsListening) nm.Shutdown();
            Cleanup();
            if (s != null) { try { await s.LeaveAsync(); } catch (Exception e) { Debug.LogWarning(e); } }
            NetApi.SetStatus("", false);
        }
        void Cleanup()
        {
            server?.Dispose(); server = null;
            client?.Dispose(); client = null;
            var v = GameHub.View;
            v.players = new PlayerSnap[0]; v.localPlayerId = -1; v.phase = MatchPhase.Lobby; v.winner = -1; v.score[0] = v.score[1] = 0;
            v.landingValid = false; v.ball = default;
            NetApi.InSession = false; NetApi.SessionCode = "";
            GameHub.RaiseRoster();
        }

        void OnApplicationQuit() { if (nm != null && nm.IsListening) nm.Shutdown(); }
    }
}
