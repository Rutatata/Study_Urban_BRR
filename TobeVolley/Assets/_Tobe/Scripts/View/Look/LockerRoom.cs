// Раздевалка главного меню: персонаж игрока сидит на скамейке и ждёт; по кнопке матча встаёт, идёт к светящейся двери
// и уходит в свет (экран заливает белым), после чего запускается само действие меню. Комната строится кодом в стороне от зала.
using System;
using UnityEngine;

namespace Tobe.View
{
    public sealed class LockerRoom : MonoBehaviour
    {
        public static LockerRoom Instance { get; private set; }
        /// <summary>0..1 — засветка экрана при выходе в дверь (CameraRig добавляет к экспозиции).</summary>
        public static float Whiteout { get; private set; }
        /// <summary>0..1 — ход выхода (меню по нему гаснет).</summary>
        public static float ExitProgress { get; private set; }
        public static float DevSpeed = 1f;   // проверка по кадрам: замедлить выход

        static readonly Vector3 Origin = new Vector3(140f, 0f, 0f);   // далеко за стенами зала
        static readonly Vector3 SeatLocal = new Vector3(-1.86f, 0f, 0f), DoorLocal = new Vector3(0.4f, 0f, 5.6f);   // таз на сиденье (скамейка x −2,16…−1,74)
        const float StandT = 0.6f, RunSpeed = 3.4f, Accel = 6.8f, WhiteStart = 1.7f, WhiteDur = 0.7f;   // скорость = скорость записи бега (иначе ноги скользят), разгон за 0,5 с

        Transform roomRoot; PlayerView pv; Light doorLight; Material doorMat;
        bool exiting; float exitT; Action onExit; bool fired;
        Vector3 camPos; Quaternion camRot; bool camInit;

        public static LockerRoom Create(Transform parent)
        {
            var go = new GameObject("LockerRoom");
            go.transform.SetParent(parent, false);
            return go.AddComponent<LockerRoom>();
        }

        /// <summary>Меню показывается без сессии матча — тогда камера смотрит в раздевалку.</summary>
        public static bool Showing => Instance != null && !NetApi.InSession;

        /// <summary>Выйти на площадку: персонаж уходит в дверь, затем action. Если раздевалки нет — action сразу.</summary>
        public static void Exit(Action action)
        {
            if (!Showing || Instance.exiting) { action?.Invoke(); return; }
            var r = Instance; r.exiting = true; r.exitT = 0f; r.onExit = action; r.fired = false;
        }

        void Awake()
        {
            Instance = this;
            transform.position = Origin;
            roomRoot = new GameObject("Room").transform; roomRoot.SetParent(transform, false);
            BuildRoom();
        }

        void BuildRoom()
        {
            var floor = Mats.Lit("lr_floor", new Color(0.16f, 0.13f, 0.12f), 0.45f);
            var wall = Mats.Lit("lr_wall", new Color(0.2f, 0.23f, 0.3f), 0.2f);
            var locker = Mats.Lit("lr_locker", new Color(0.95f, 0.45f, 0.1f), 0.35f, 0.4f);
            var lockerDark = Mats.Lit("lr_locker_d", new Color(0.12f, 0.12f, 0.15f), 0.3f, 0.5f);
            var wood = Mats.Lit("lr_bench", new Color(0.55f, 0.36f, 0.2f), 0.35f);
            var metal = Mats.Lit("lr_metal", new Color(0.35f, 0.36f, 0.4f), 0.6f, 0.7f);
            Vector3 P(float x, float y, float z) => new Vector3(x, y, z);
            Mats.Prim(PrimitiveType.Cube, roomRoot, "Floor", P(0, -0.05f, 0.5f), P(7, 0.1f, 13), floor, false).GetComponent<Renderer>().receiveShadows = true;
            Mats.Prim(PrimitiveType.Cube, roomRoot, "Ceiling", P(0, 3.25f, 0.5f), P(7, 0.1f, 13), wall);
            Mats.Prim(PrimitiveType.Cube, roomRoot, "WallL", P(-3.55f, 1.6f, 0.5f), P(0.1f, 3.3f, 13), wall);
            Mats.Prim(PrimitiveType.Cube, roomRoot, "WallR", P(3.55f, 1.6f, 0.5f), P(0.1f, 3.3f, 13), wall);
            Mats.Prim(PrimitiveType.Cube, roomRoot, "WallBack", P(0, 1.6f, -6f), P(7, 3.3f, 0.1f), wall);
            // торцевая стена с проёмом двери
            Mats.Prim(PrimitiveType.Cube, roomRoot, "WallDoorL", P(-2.05f, 1.6f, 6.95f), P(3.1f, 3.3f, 0.1f), wall);
            Mats.Prim(PrimitiveType.Cube, roomRoot, "WallDoorR", P(2.4f, 1.6f, 6.95f), P(2.4f, 3.3f, 0.1f), wall);
            Mats.Prim(PrimitiveType.Cube, roomRoot, "WallDoorTop", P(0.35f, 2.85f, 6.95f), P(1.7f, 0.9f, 0.1f), wall);
            Mats.Prim(PrimitiveType.Cube, roomRoot, "DoorFrame", P(0.35f, 1.2f, 6.9f), P(1.8f, 2.5f, 0.06f), metal);
            doorMat = Mats.Instance(Mats.Unlit("lr_doorlight", new Color(1f, 0.95f, 0.85f) * 1.6f));
            Mats.Quad("DoorLight", roomRoot, P(0.35f, 1.2f, 6.86f), P(0, 180, 0), P(1.5f, 2.35f, 1f), doorMat);
            // шкафчики вдоль левой стены
            for (int i = 0; i < 8; i++)
            {
                float z = -4.6f + i * 0.72f;
                Mats.Prim(PrimitiveType.Cube, roomRoot, "Locker", P(-3.2f, 1.0f, z), P(0.55f, 2.0f, 0.68f), i % 3 == 1 ? lockerDark : locker, true);
                Mats.Prim(PrimitiveType.Cube, roomRoot, "Vent", P(-2.92f, 1.65f, z), P(0.02f, 0.22f, 0.4f), lockerDark);
                Mats.Prim(PrimitiveType.Cube, roomRoot, "Handle", P(-2.91f, 1.0f, z + 0.22f), P(0.03f, 0.18f, 0.03f), metal);
            }
            // скамейка (сиденье на 0,45 м — под длину голени VRoid)
            Mats.Prim(PrimitiveType.Cube, roomRoot, "Bench", P(-1.95f, 0.42f, 0f), P(0.42f, 0.06f, 4.2f), wood, true);
            for (int i = -1; i <= 1; i++) Mats.Prim(PrimitiveType.Cube, roomRoot, "BenchLeg", P(-1.95f, 0.2f, i * 1.8f), P(0.32f, 0.4f, 0.08f), metal, true);
            // полотенце и бутылка на скамейке — чтобы комната выглядела обжитой
            Mats.Prim(PrimitiveType.Cube, roomRoot, "Towel", P(-1.95f, 0.47f, 1.2f), P(0.36f, 0.04f, 0.5f), Mats.Lit("lr_towel", new Color(0.9f, 0.92f, 0.95f), 0.1f));
            Mats.Prim(PrimitiveType.Cylinder, roomRoot, "Bottle", P(-1.9f, 0.58f, -1.1f), P(0.07f, 0.11f, 0.07f), Mats.Lit("lr_bottle", new Color(0.2f, 0.75f, 0.95f), 0.8f));
            // свет: две тёплые лампы под потолком и сам дверной проём
            AddLight("LampA", P(-0.8f, 3.0f, -1.5f), new Color(1f, 0.82f, 0.62f), 2.2f, 9f);
            AddLight("LampB", P(-0.8f, 3.0f, 3.0f), new Color(1f, 0.82f, 0.62f), 1.6f, 8f);
            doorLight = AddLight("DoorLight", P(0.35f, 1.4f, 6.3f), new Color(1f, 0.95f, 0.85f), 1.5f, 7f);
        }

        Light AddLight(string n, Vector3 local, Color c, float intensity, float range)
        {
            var go = new GameObject(n); go.transform.SetParent(roomRoot, false); go.transform.localPosition = local;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            return l;
        }

        void Update()
        {
            bool show = Showing;
            if (roomRoot.gameObject.activeSelf != show) roomRoot.gameObject.SetActive(show);
            if (!show)
            {   // ушли в матч (или в лобби): в следующий раз снова сидит
                exiting = false; exitT = 0f; ExitProgress = 0f;
                Whiteout = Mathf.MoveTowards(Whiteout, 0f, Time.unscaledDeltaTime / 0.5f);
                if (pv != null) pv.gameObject.SetActive(false);
                return;
            }
            float dt = Time.unscaledDeltaTime;
            if (pv == null) { pv = PlayerView.Create(roomRoot, Snap(SeatLocal, Vector3.zero, 90f, PoseId.Sit)); pv.Showcase = true; }
            if (!pv.gameObject.activeSelf) pv.gameObject.SetActive(true);

            Vector3 pos = SeatLocal, vel = Vector3.zero; float yaw = 90f; PoseId pose = PoseId.Sit;
            if (exiting)
            {
                exitT += dt * DevSpeed;
                float walkT = exitT - StandT;
                if (exitT < StandT) { pose = PoseId.Idle; yaw = Mathf.LerpAngle(90f, YawTo(SeatLocal, DoorLocal), exitT / StandT); }
                else
                {
                    // равноускоренный разгон до RunSpeed, затем ровный бег
                    float tAcc = RunSpeed / Accel, dist = walkT < tAcc ? 0.5f * Accel * walkT * walkT : 0.5f * Accel * tAcc * tAcc + RunSpeed * (walkT - tAcc);
                    float spd = Mathf.Min(RunSpeed, Accel * walkT);
                    Vector3 path = DoorLocal - SeatLocal; float len = path.magnitude, d = Mathf.Min(len, dist);
                    pos = SeatLocal + path / len * d; vel = d < len ? path / len * spd : Vector3.zero;
                    pose = PoseId.Run; yaw = YawTo(SeatLocal, DoorLocal);
                }
                Whiteout = Mathf.Clamp01((exitT - WhiteStart) / WhiteDur);
                ExitProgress = Mathf.Clamp01(exitT / 0.4f);
                if (!fired && exitT >= WhiteStart + WhiteDur) { fired = true; var a = onExit; onExit = null; a?.Invoke(); }
                if (fired && exitT > WhiteStart + WhiteDur + 1.5f) { exiting = false; exitT = 0f; ExitProgress = 0f; }   // действие не увело в матч (ошибка сети) — вернуть меню
            }
            else
            {
                Whiteout = Mathf.MoveTowards(Whiteout, 0f, dt / 0.5f);
                ExitProgress = 0f;
            }
            pv.Tick(Snap(pos, vel, yaw, pose), true, dt);
            float glow = 1f + 3f * Whiteout + 0.15f * Mathf.Sin(Time.unscaledTime * 2f);
            Mats.SetColor(doorMat, new Color(1f, 0.95f, 0.85f) * (1.6f * glow));
            doorLight.intensity = 1.5f + 9f * Whiteout;
        }

        static float YawTo(Vector3 a, Vector3 b) => Mathf.Atan2(b.x - a.x, b.z - a.z) * Mathf.Rad2Deg;

        PlayerSnap Snap(Vector3 local, Vector3 vel, float yaw, PoseId pose) => new PlayerSnap
        {
            id = 250, team = 0, slot = 0, isBot = false, connected = true, profile = GameHub.LocalProfile,
            pos = Origin + local, vel = vel, yaw = yaw, pose = pose, poseT = 0f, air = false, energy = 0f, stamina = 100f,
        };

        /// <summary>Поза камеры меню: сбоку от скамейки, персонаж в правой трети кадра; на выходе — провожает к двери.</summary>
        public static bool TryCamera(out Vector3 pos, out Quaternion rot)
        {
            pos = default; rot = Quaternion.identity;
            if (!Showing) return false;
            var r = Instance; float dt = Time.unscaledDeltaTime;
            Vector3 head = Origin + SeatLocal + new Vector3(0f, 0.95f, 0f);
            Vector3 wantPos = Origin + new Vector3(1.9f, 1.45f, -2.6f);
            Quaternion look = Quaternion.LookRotation(head - wantPos);
            Quaternion want = Quaternion.LookRotation(head - look * Vector3.right * 0.25f - wantPos);   // персонаж между кнопками меню и карточкой игрока
            if (r.exiting && r.pv != null)
            {   // провожаем взглядом к двери, чуть наезжая
                Vector3 target = r.pv.transform.position + new Vector3(0f, 1.1f, 0f);
                wantPos = Vector3.Lerp(wantPos, Origin + new Vector3(1.2f, 1.4f, -1.2f), Mathf.Clamp01(r.exitT / 2f));
                want = Quaternion.LookRotation(target - wantPos);
            }
            if (!r.camInit) { r.camPos = wantPos; r.camRot = want; r.camInit = true; }
            float k = 1f - Mathf.Exp(-dt * 4f);
            r.camPos = Vector3.Lerp(r.camPos, wantPos, k); r.camRot = Quaternion.Slerp(r.camRot, want, k);
            pos = r.camPos; rot = r.camRot;
            return true;
        }
    }
}
