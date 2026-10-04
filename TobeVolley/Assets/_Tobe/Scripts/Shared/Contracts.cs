// Shared contracts between simulation (server), networking and presentation (view/UI).
// World units are meters. Court: X across [0..9], Z along [0..18], net plane at Z = 9, Y is up.
// Team 0 plays on Z < 9 (near side of the default camera), team 1 on Z > 9.
using System;
using UnityEngine;

namespace Tobe
{
    public static class Court
    {
        public const float Width = 9f, Length = 18f, NetZ = 9f, NetTop = 2.43f, NetBottom = 1.43f, BallRadius = 0.11f;
        public const float AttackLine = 3f;
        public const float BallGravity = 9.8f, PlayerGravity = 14f;
        public static bool OnSide(int team, float z) => team == 0 ? z < NetZ : z > NetZ;
        public static float Fwd(int team) => team == 0 ? 1f : -1f;                 // direction toward the net along Z
        public static float WorldZ(int team, float distFromNet) => team == 0 ? NetZ - distFromNet : NetZ + distFromNet;
        public static float DistFromNet(int team, float z) => team == 0 ? NetZ - z : z - NetZ;
        public static bool InCourt(float x, float z, float margin = 0f) =>
            x >= -margin && x <= Width + margin && z >= -margin && z <= Length + margin;
    }

    public enum MatchPhase : byte { Lobby = 0, Countdown = 1, Serve = 2, Rally = 3, Point = 4, End = 5 }

    public enum PoseId : byte
    {
        Idle, Ready, Run, Bump, Set, Jump, SpikeWind, Spike, Block, Dive, ServeToss, ServeHit, Celebrate, Sad, Stun
    }

    public enum PlanKind : byte { None, Receive, Set, Attack, Over, Leave }

    public enum FinisherKind : byte { Spike, Serve, Block, Dig, Set }

    public enum PlayStyle : byte { Setter = 0, Outside = 1, Middle = 2, Opposite = 3, Libero = 4 }

    [Serializable]
    public struct Stats
    {
        public float spike, jump, receive, set, block, serve, speed; // 1..10
        public Stats(float spike, float jump, float receive, float set, float block, float serve, float speed)
        { this.spike = spike; this.jump = jump; this.receive = receive; this.set = set; this.block = block; this.serve = serve; this.speed = speed; }
    }

    public sealed class StyleDef
    {
        public PlayStyle style;
        public string name, desc;
        public Stats stats;
        public string finisherName, finisherQuote;
        public FinisherKind finisherKind;
        public Color c1, c2;
    }

    /// <summary>Play styles (Rematch-like: no fixed heroes, you pick a style + cosmetics).</summary>
    public static class Styles
    {
        public static readonly StyleDef[] All =
        {
            new StyleDef { style = PlayStyle.Setter, name = "Связующий", desc = "Мозг команды: точные передачи, быстрые комбинации.",
                stats = new Stats(5, 6, 6, 10, 5, 8, 7), finisherName = "Пас-гильотина", finisherQuote = "Я отдам мяч туда, где ты будешь!",
                finisherKind = FinisherKind.Set, c1 = new Color(0.2f, 0.45f, 1f), c2 = new Color(0.8f, 0.95f, 1f) },
            new StyleDef { style = PlayStyle.Outside, name = "Доигровщик", desc = "Универсал: сильная атака и надёжный приём.",
                stats = new Stats(8, 7, 7, 5, 6, 7, 7), finisherName = "Удар аса", finisherQuote = "Дай мне мяч. Я пробью.",
                finisherKind = FinisherKind.Spike, c1 = new Color(1f, 0.45f, 0.1f), c2 = new Color(1f, 0.85f, 0.3f) },
            new StyleDef { style = PlayStyle.Middle, name = "Центральный", desc = "Прыжок выше всех: быстрые атаки и стена на блоке.",
                stats = new Stats(7, 10, 4, 4, 9, 5, 9), finisherName = "Странная быстрая", finisherQuote = "Я могу взлететь!",
                finisherKind = FinisherKind.Spike, c1 = new Color(1f, 0.55f, 0f), c2 = new Color(1f, 1f, 0.6f) },
            new StyleDef { style = PlayStyle.Opposite, name = "Диагональный", desc = "Пушка команды: убойная атака и подача.",
                stats = new Stats(10, 8, 4, 4, 7, 9, 6), finisherName = "Левая пушка", finisherQuote = "Сила — это всё, что нужно.",
                finisherKind = FinisherKind.Serve, c1 = new Color(0.6f, 0.2f, 1f), c2 = new Color(1f, 0.3f, 0.5f) },
            new StyleDef { style = PlayStyle.Libero, name = "Либеро", desc = "Страж площадки: поднимает невозможные мячи.",
                stats = new Stats(2, 6, 10, 7, 2, 6, 10), finisherName = "Роллинг Сандер", finisherQuote = "За моей спиной мяч не упадёт!",
                finisherKind = FinisherKind.Dig, c1 = new Color(0.2f, 1f, 0.6f), c2 = new Color(1f, 1f, 1f) },
        };
        public static StyleDef Get(PlayStyle s) => All[(int)s];
    }

    public static class TeamLook
    {
        public static readonly string[] Names = { "КАРАСУ", "АОБА" };
        public static readonly Color[] Shirt = { new Color(0.14f, 0.14f, 0.17f), new Color(0.95f, 0.95f, 0.97f) };
        public static readonly Color[] Trim = { new Color(1f, 0.5f, 0.05f), new Color(0.1f, 0.7f, 0.7f) };
        public static readonly Color[] Shorts = { new Color(0.14f, 0.14f, 0.17f), new Color(0.1f, 0.35f, 0.45f) };
        public static readonly Color[] SkinTones =
        {
            new Color(1f, 0.87f, 0.77f), new Color(0.98f, 0.8f, 0.68f), new Color(0.9f, 0.7f, 0.55f), new Color(0.76f, 0.55f, 0.4f), new Color(0.55f, 0.38f, 0.27f),
        };
        public static readonly Color[] EyeColors =
        {
            new Color(0.35f, 0.22f, 0.12f), new Color(0.15f, 0.15f, 0.2f), new Color(0.2f, 0.45f, 0.85f), new Color(0.85f, 0.55f, 0.1f), new Color(0.25f, 0.6f, 0.35f), new Color(0.6f, 0.2f, 0.25f),
        };
        public static readonly Color[] HairPresets =
        {
            new Color(1f, 0.5f, 0.1f), new Color(0.08f, 0.08f, 0.1f), new Color(0.95f, 0.85f, 0.45f), new Color(0.75f, 0.75f, 0.8f),
            new Color(0.45f, 0.28f, 0.15f), new Color(0.9f, 0.2f, 0.2f), new Color(0.25f, 0.4f, 0.9f), new Color(0.95f, 0.6f, 0.75f),
        };
    }

    /// <summary>Cosmetic + gameplay choice a player sends when joining.</summary>
    [Serializable]
    public struct PlayerProfile
    {
        public string nick;
        public PlayStyle style;
        public byte model;      // index into available character models
        public byte hair;       // index into TeamLook.HairPresets
        public byte number;     // jersey number 1..99
        // --- appearance (character editor) ---
        public byte skin;       // index into TeamLook.SkinTones
        public byte height;     // 0..255 -> body scale 0.92..1.08
        public byte build;      // 0..255 -> slim..athletic (shoulder/limb thickness)
        public byte eyes;       // index into TeamLook.EyeColors
        public byte gear;       // bitmask of Gear flags
        public float HeightScale => 0.92f + height / 255f * 0.16f;
    }

    [Flags]
    public enum Gear : byte { None = 0, KneePads = 1, Headband = 2, Wristbands = 4, Glasses = 8, ArmSleeve = 16, AnkleTape = 32 }

    /// <summary>Per-player state the server replicates to every client.</summary>
    public struct PlayerSnap
    {
        public byte id, team, slot;
        public bool isBot, connected;
        public PlayerProfile profile;  // sent in roster, not every snapshot
        public Vector3 pos, vel;
        public float yaw;              // degrees, 0 = facing +Z
        public PoseId pose;
        public float poseT;            // remaining hold time of a one-shot pose (counts down; 0 for continuous poses)
        public bool air, armed, calling;
        public float energy;           // 0..100
        public float stamina;          // 0..100
    }

    public struct BallSnap
    {
        public Vector3 pos, vel;
        public bool live, held;
        public byte superBy;           // 255 = none; otherwise player id whose finisher is on the ball
        public bool mini;              // "perfect" (non-finisher) glow
    }

    public struct PlanSnap
    {
        public PlanKind kind;
        public byte playerId;          // 255 = none
        public Vector3 point;          // contact point
        public float timeLeft;
    }

    /// <summary>Everything the presentation layer needs to draw one frame.</summary>
    public sealed class MatchView
    {
        public MatchPhase phase;
        public int[] score = new int[2];
        public int servingTeam, targetScore = 15, teamSize = 6;
        public float phaseTime;        // seconds left in timed phases (lobby countdown, point pause)
        public string lobbyStatus = "";
        public PlayerSnap[] players = new PlayerSnap[0];
        public BallSnap ball;
        public PlanSnap[] plans = new PlanSnap[2];
        public bool landingValid;
        public Vector3 landing;        // predicted floor impact of the ball
        public int localPlayerId = -1; // which player this client controls (-1 = spectator / none yet)
        public float slowMo;           // 0 = normal speed, 0.8 = slow motion, 1 = frozen (finisher cut-in)
        public int serverPlayerId = -1; // player currently serving (Serve phase)
        public int winner = -1;

        public bool TryGetLocal(out PlayerSnap p)
        {
            for (int i = 0; i < players.Length; i++) if (players[i].id == localPlayerId) { p = players[i]; return true; }
            p = default; return false;
        }
        public bool TryGet(int id, out PlayerSnap p)
        {
            for (int i = 0; i < players.Length; i++) if (players[i].id == id) { p = players[i]; return true; }
            p = default; return false;
        }
    }

    public enum GameEventType : byte
    {
        Popup,        // text (big if intArg == 1), color
        Sound,        // text = clip name, pos, floatArg = volume
        Cutin,        // playerId: finisher cut-in (presentation shows ~1.2 s)
        Impact,       // pos on floor, intArg = 1 for finisher impact (cracks + shockwave), color
        Hit,          // pos, playerId, intArg = PoseId of the touch, floatArg = power 0..1
        Point,        // intArg = scoring team, text = reason, playerId = hero or 255
        Cinematic,    // pos = focus point, floatArg = duration
        Block,        // pos, playerId (blocker)
        MatchEnd,     // intArg = winning team
        Whistle,
    }

    [Serializable]
    public struct GameEvent
    {
        public GameEventType type;
        public string text;
        public Color color;
        public Vector3 pos;
        public byte playerId;
        public int intArg;
        public float floatArg;
    }

    /// <summary>Client-side hub: the network client fills <see cref="View"/> and raises events; view/UI read and subscribe.</summary>
    public static class GameHub
    {
        public static readonly MatchView View = new MatchView();
        public static event Action<GameEvent> OnEvent;
        public static event Action OnRosterChanged;
        public static void Raise(GameEvent e) => OnEvent?.Invoke(e);
        public static void RaiseRoster() => OnRosterChanged?.Invoke();

        /// <summary>Local aim point on the floor (written by the camera rig, read by input).</summary>
        public static Vector3 AimPoint;
        public static bool AimValid;
        /// <summary>Camera yaw in degrees (written by the camera rig, used for camera-relative movement).</summary>
        public static float CameraYaw;
        /// <summary>True while a UI screen owns the mouse (menus); gameplay input is ignored.</summary>
        public static bool UiBlocking = true;
        public static PlayerProfile LocalProfile = new PlayerProfile { nick = "Игрок", style = PlayStyle.Middle, model = 0, hair = 0, number = 10, skin = 1, height = 128, build = 140, eyes = 0, gear = (byte)(Gear.KneePads | Gear.Wristbands) };
        /// <summary>Names of character model files found in StreamingAssets/Characters (filled by CharacterLibrary).</summary>
        public static string[] ModelNames = new string[0];
    }

    /// <summary>Local player intent, sampled every frame by the input layer.</summary>
    public struct InputCmd
    {
        public Vector2 move;          // world XZ, already camera-relative, length <= 1
        public bool sprint;
        public Vector3 aim;           // floor aim point
        public ushort jumpCount, hitPressCount, hitReleaseCount, diveCount, callCount, specialCount; // edge counters
        public bool hitHeld;
        // Movement is simulated on the owning client (responsive, like Rematch) and validated by the server.
        public Vector3 clientPos, clientVel;
        public bool clientAir, clientDiving;
    }

    /// <summary>Network actions the UI can trigger. Implemented and assigned by the Net layer at startup.</summary>
    public static class NetApi
    {
        /// <summary>Quick match via Unity Gaming Services matchmaking (teamSize 3 or 6). Joins an open session or creates one.</summary>
        public static Func<int, System.Threading.Tasks.Task> QuickMatch;
        /// <summary>Create a private session; returns the join code.</summary>
        public static Func<int, System.Threading.Tasks.Task<string>> CreatePrivate;
        public static Func<string, System.Threading.Tasks.Task> JoinByCode;
        /// <summary>Offline practice: local host + bots, no online services required.</summary>
        public static Action<int> StartPractice;
        public static Action Leave;
        /// <summary>Host only: start the match now (bots fill empty slots).</summary>
        public static Action StartNow;
        /// <summary>Pause/resume the simulation. Only works in offline practice (online matches keep running).</summary>
        public static Action<bool> SetPaused;
        public static bool PauseAllowed;
        public static string Status = "";
        public static bool Busy;
        public static bool InSession;
        public static string SessionCode = "";
        public static event Action OnStatusChanged;
        public static void SetStatus(string s, bool busy) { Status = s; Busy = busy; OnStatusChanged?.Invoke(); }
    }
}
