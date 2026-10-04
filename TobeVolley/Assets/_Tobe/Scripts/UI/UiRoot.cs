using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Tobe.UI
{
    /// <summary>Owns the canvas and all screens; picks the visible screen from NetApi / GameHub.View state.</summary>
    public sealed class UiRoot : MonoBehaviour
    {
        public static UiRoot Instance { get; private set; }

        public MainMenuScreen Menu { get; private set; }
        public CharacterScreen Character { get; private set; }
        public LobbyScreen Lobby { get; private set; }
        public HudScreen Hud { get; private set; }
        public VsIntroScreen Vs { get; private set; }
        public PauseScreen Pause { get; private set; }
        public EndScreen End { get; private set; }

        public bool EndDismissed;
        bool paused, pauseSent;
        MatchPhase prevPhase = MatchPhase.Lobby;

        public static UiRoot Create()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("TobeUI", typeof(RectTransform));
            DontDestroyOnLoad(go);
            return go.AddComponent<UiRoot>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            ProfileStore.Load();

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            EnsureEventSystem();

            var t = transform;
            Menu = UiScreen.Create<MainMenuScreen>(t, "MainMenu");
            Character = UiScreen.Create<CharacterScreen>(t, "Character");
            Lobby = UiScreen.Create<LobbyScreen>(t, "Lobby");
            Hud = UiScreen.Create<HudScreen>(t, "Hud");
            Vs = UiScreen.Create<VsIntroScreen>(t, "VsIntro");
            End = UiScreen.Create<EndScreen>(t, "End");
            Pause = UiScreen.Create<PauseScreen>(t, "Pause");
        }

        static void EnsureEventSystem()
        {
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                DontDestroyOnLoad(go);
                es = go.AddComponent<EventSystem>();
            }
            var old = es.GetComponent<StandaloneInputModule>();
            if (old != null) Destroy(old);
            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                var m = es.gameObject.AddComponent<InputSystemUIInputModule>();
                m.AssignDefaultActions();
            }
        }

        void OnEnable() { GameHub.OnEvent += OnGameEvent; }
        void OnDisable() { GameHub.OnEvent -= OnGameEvent; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnGameEvent(GameEvent e)
        {
            if (Hud != null) Hud.HandleEvent(e);
        }

        public void OpenCharacter() { if (Character != null) Character.Show(true); }

        void Update()
        {
            var v = GameHub.View;
            var ph = v.phase;
            bool inMatch = ph == MatchPhase.Serve || ph == MatchPhase.Rally || ph == MatchPhase.Point;
            if (ph != MatchPhase.End) EndDismissed = false;

            UiScreen target;
            if (inMatch) target = Hud;
            else if (ph == MatchPhase.End && !EndDismissed) target = End;
            else if (NetApi.InSession && (ph == MatchPhase.Lobby || ph == MatchPhase.Countdown)) target = Lobby;
            else target = Menu;

            if (target == Hud && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) paused = !paused;
            if (target != Hud) paused = false;

            Menu.Show(target == Menu);
            Lobby.Show(target == Lobby);
            Hud.Show(target == Hud);
            End.Show(target == End);
            Pause.Show(paused);
            if (paused != pauseSent) { pauseSent = paused; NetApi.SetPaused?.Invoke(paused); }
            if ((prevPhase == MatchPhase.Lobby || prevPhase == MatchPhase.Countdown) && ph == MatchPhase.Serve && target == Hud) Vs.Play();
            if (target != Hud && Vs.Visible) Vs.Show(false);
            prevPhase = ph;
            if (target != Menu && Character.Visible) Character.Show(false);

            bool block = target != Hud || paused;
            GameHub.UiBlocking = block;
            var wantLock = block ? CursorLockMode.None : CursorLockMode.Locked;
            if (Cursor.lockState != wantLock) Cursor.lockState = wantLock;
            if (Cursor.visible != block) Cursor.visible = block;
        }

        public void ClosePause() { paused = false; }
    }
}
