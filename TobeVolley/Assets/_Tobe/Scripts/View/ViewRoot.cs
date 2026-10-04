// Entry point of the presentation layer. ViewRoot.Create() builds arena, camera, ball, fx and keeps PlayerViews in sync with GameHub.View.
using System.Collections.Generic;
using UnityEngine;

namespace Tobe.View
{
    public sealed class ViewRoot : MonoBehaviour
    {
        public static ViewRoot Instance { get; private set; }

        readonly Dictionary<int, PlayerView> views = new Dictionary<int, PlayerView>();
        readonly HashSet<int> seen = new HashSet<int>();
        readonly List<int> toRemove = new List<int>();
        Transform playersRoot;
        bool rosterDirty = true;

        public CameraRig Rig { get; private set; }
        public BallView Ball { get; private set; }
        public FxManager Fx { get; private set; }

        public static ViewRoot Create()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("TobeViewRoot");
            DontDestroyOnLoad(go);
            var vr = go.AddComponent<ViewRoot>();
            vr.Init();
            return vr;
        }

        void Init()
        {
            Instance = this;
            CharacterLibrary.Scan();
            ArenaBuilder.Build(transform);
            playersRoot = new GameObject("Players").transform;
            playersRoot.SetParent(transform, false);
            Fx = FxManager.Create(transform);
            Ball = BallView.Create(transform);
            Rig = CameraRig.Create(transform);
        }

        void OnEnable() { GameHub.OnRosterChanged += OnRoster; }
        void OnDisable() { GameHub.OnRosterChanged -= OnRoster; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnRoster() { rosterDirty = true; }

        /// <summary>Smoothed on-screen position of the local player (falls back to the snapshot position).</summary>
        public static bool TryGetLocalVisual(out Vector3 pos, out int team)
        {
            pos = Vector3.zero; team = 0;
            if (!GameHub.View.TryGetLocal(out var snap)) return false;
            team = snap.team;
            pos = snap.pos;
            if (Instance != null && Instance.views.TryGetValue(snap.id, out var pv) && pv != null) pos = pv.VisualPos;
            return true;
        }

        void Update()
        {
            var view = GameHub.View;
            var players = view.players ?? new PlayerSnap[0];
            float dt = Time.deltaTime;
            seen.Clear();
            for (int i = 0; i < players.Length; i++)
            {
                int id = players[i].id;
                seen.Add(id);
                if (!views.TryGetValue(id, out var pv) || pv == null)
                {
                    pv = PlayerView.Create(playersRoot, in players[i]);
                    views[id] = pv;
                }
                pv.Tick(in players[i], id == view.localPlayerId, dt);
            }
            if (views.Count != seen.Count || rosterDirty)
            {
                toRemove.Clear();
                foreach (var kv in views) if (!seen.Contains(kv.Key) || kv.Value == null) toRemove.Add(kv.Key);
                for (int i = 0; i < toRemove.Count; i++)
                {
                    if (views[toRemove[i]] != null) Destroy(views[toRemove[i]].gameObject);
                    views.Remove(toRemove[i]);
                }
                rosterDirty = false;
            }
        }
    }
}
