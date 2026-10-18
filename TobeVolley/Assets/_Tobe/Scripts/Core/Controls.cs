// Управление: действия с отдельными привязками для клавиатуры+мыши и для геймпада (Xbox, PlayStation, Switch Pro и любой,
// который Input System видит как Gamepad). Устройство определяется само — по последнему нажатию. Привязки меняются в меню
// «Управление» и хранятся в PlayerPrefs (tobe.bind.k.* / tobe.bind.p.*).
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Tobe
{
    public enum Act { Jump, Block, Bump, Set, Attack, Dive, Sprint, Call, Special, Emote, ServeNext, Pause }

    public static class Controls
    {
        public static readonly string[] Names =
        {
            "Прыжок (атака)", "Блок у сетки", "Приём снизу", "Передача сверху", "Удар в прыжке / подача", "Нырок",
            "Рывок", "«Дай мне!»", "Добивание", "Дать пять", "Тип подачи (геймпад)", "Пауза / меню",
        };
        // клавиатура+мышь: «k:<Key>» или «m:left|right|middle»; геймпад: «p:<GamepadButton>»
        static readonly string[] DefK = { "k:Space", "k:C", "k:E", "m:right", "m:left", "k:LeftCtrl", "k:LeftShift", "k:Q", "k:R", "k:G", "k:Tab", "k:Escape" };
        static readonly string[] DefP = { "p:South", "p:LeftShoulder", "p:West", "p:North", "p:RightTrigger", "p:East", "p:LeftTrigger", "p:RightShoulder", "p:RightStick", "p:DpadUp", "p:DpadRight", "p:Start" };
        static readonly string[] k = new string[DefK.Length], p = new string[DefP.Length];
        public static bool UsingGamepad { get; private set; }
        public static float MouseSens = 0.1f, PadSens = 170f;   // мышь: градусы на пиксель; стик: градусы в секунду
        static int frame = -1;
        static bool loaded;

        static void Load()
        {
            if (loaded) return; loaded = true;
            for (int i = 0; i < DefK.Length; i++)
            {
                k[i] = DefK[i]; p[i] = DefP[i];
                try { k[i] = PlayerPrefs.GetString("tobe.bind.k." + (Act)i, DefK[i]); p[i] = PlayerPrefs.GetString("tobe.bind.p." + (Act)i, DefP[i]); } catch (Exception) { }
            }
        }

        public static string Binding(Act a, bool pad) { Load(); return pad ? p[(int)a] : k[(int)a]; }
        public static void SetBinding(Act a, bool pad, string b)
        {
            Load();
            if (pad) p[(int)a] = b; else k[(int)a] = b;
            try { PlayerPrefs.SetString((pad ? "tobe.bind.p." : "tobe.bind.k.") + a, b); PlayerPrefs.Save(); } catch (Exception) { }
        }
        public static void ResetDefaults(bool pad)
        {
            Load();
            for (int i = 0; i < DefK.Length; i++) SetBinding((Act)i, pad, pad ? DefP[i] : DefK[i]);
        }

        /// <summary>Раз в кадр: кто сейчас играет — клавиатура/мышь или геймпад.</summary>
        static void Tick()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount; Load();
            var g = Gamepad.current;
            if (g != null)
            {
                bool padUsed = g.leftStick.ReadValue().sqrMagnitude > 0.25f || g.rightStick.ReadValue().sqrMagnitude > 0.25f;
                if (!padUsed) foreach (var c in g.allControls) if (c is ButtonControl b && b.wasPressedThisFrame) { padUsed = true; break; }
                if (padUsed) UsingGamepad = true;
            }
            var kb = Keyboard.current; var ms = Mouse.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame || ms != null && (ms.delta.ReadValue().sqrMagnitude > 9f || ms.leftButton.wasPressedThisFrame || ms.rightButton.wasPressedThisFrame))
                UsingGamepad = false;
        }

        static ButtonControl Control(string b)
        {
            if (string.IsNullOrEmpty(b) || b.Length < 3) return null;
            string v = b.Substring(2);
            switch (b[0])
            {
                case 'k':
                    var kb = Keyboard.current; if (kb == null) return null;
                    return Enum.TryParse(v, out Key key) && key != Key.None ? kb[key] : null;
                case 'm':
                    var ms = Mouse.current; if (ms == null) return null;
                    return v == "left" ? ms.leftButton : v == "right" ? ms.rightButton : v == "middle" ? ms.middleButton : null;
                case 'p':
                    var g = Gamepad.current; if (g == null) return null;
                    return Enum.TryParse(v, out GamepadButton gb) ? g[gb] : null;
            }
            return null;
        }

        public static bool Down(Act a) { Tick(); var c1 = Control(k[(int)a]); var c2 = Control(p[(int)a]); return c1 != null && c1.wasPressedThisFrame || c2 != null && c2.wasPressedThisFrame; }
        public static bool Up(Act a) { Tick(); var c1 = Control(k[(int)a]); var c2 = Control(p[(int)a]); return c1 != null && c1.wasReleasedThisFrame || c2 != null && c2.wasReleasedThisFrame; }
        public static bool Held(Act a) { Tick(); var c1 = Control(k[(int)a]); var c2 = Control(p[(int)a]); return c1 != null && c1.isPressed || c2 != null && c2.isPressed; }

        /// <summary>Движение: WASD/стрелки + левый стик (мёртвая зона 0,2), длина ≤ 1.</summary>
        public static Vector2 Move()
        {
            Tick();
            Vector2 m = Vector2.zero;
            var kb = Keyboard.current;
            if (kb != null)
            {
                m.x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0);
                m.y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0);
            }
            var g = Gamepad.current;
            if (g != null) { var s = g.leftStick.ReadValue(); if (s.magnitude > 0.2f) m += s; }
            return m.sqrMagnitude > 1f ? m.normalized : m;
        }

        /// <summary>Поворот камеры за кадр в градусах: мышь + правый стик (с плавной кривой — точнее в центре).</summary>
        public static Vector2 Look(float dt)
        {
            Tick();
            Vector2 d = Vector2.zero;
            var ms = Mouse.current; if (ms != null) d += ms.delta.ReadValue() * MouseSens;
            var g = Gamepad.current;
            if (g != null) { var s = g.rightStick.ReadValue(); float mg = s.magnitude; if (mg > 0.15f) d += s / mg * ((mg - 0.15f) / 0.85f) * ((mg - 0.15f) / 0.85f) * PadSens * dt; }
            return d;
        }

        // ---------- подписи кнопок для подсказок: под текущее устройство и тип геймпада
        public static string Glyph(Act a) => Label(UsingGamepad ? p[(int)a] : k[(int)a]);

        public static string Label(string b)
        {
            Load();
            if (string.IsNullOrEmpty(b) || b.Length < 3) return "?";
            string v = b.Substring(2);
            if (b[0] == 'm') return v == "left" ? "ЛКМ" : v == "right" ? "ПКМ" : "СКМ";
            if (b[0] == 'k')
            {
                switch (v)
                {
                    case "Space": return "ПРОБЕЛ"; case "LeftCtrl": case "RightCtrl": return "CTRL"; case "LeftShift": case "RightShift": return "SHIFT";
                    case "Escape": return "ESC"; case "Tab": return "TAB"; case "Enter": return "ENTER";
                }
                return v.StartsWith("Digit") ? v.Substring(5) : v.ToUpperInvariant();
            }
            string pad = Gamepad.current != null ? Gamepad.current.GetType().Name : "";
            int style = pad.Contains("DualShock") || pad.Contains("DualSense") ? 1 : pad.Contains("Switch") ? 2 : 0;   // 0 Xbox, 1 PlayStation, 2 Nintendo
            switch (v)
            {
                case "South": return style == 1 ? "×" : style == 2 ? "B" : "A";
                case "East": return style == 1 ? "○" : style == 2 ? "A" : "B";
                case "West": return style == 1 ? "□" : style == 2 ? "Y" : "X";
                case "North": return style == 1 ? "△" : style == 2 ? "X" : "Y";
                case "LeftShoulder": return style == 1 ? "L1" : style == 2 ? "L" : "LB";
                case "RightShoulder": return style == 1 ? "R1" : style == 2 ? "R" : "RB";
                case "LeftTrigger": return style == 1 ? "L2" : style == 2 ? "ZL" : "LT";
                case "RightTrigger": return style == 1 ? "R2" : style == 2 ? "ZR" : "RT";
                case "LeftStick": return style == 1 ? "L3" : "LS";
                case "RightStick": return style == 1 ? "R3" : "RS";
                case "Start": return style == 1 ? "OPTIONS" : style == 2 ? "+" : "MENU";
                case "Select": return style == 1 ? "SHARE" : style == 2 ? "−" : "VIEW";
                case "DpadUp": return "↑"; case "DpadDown": return "↓"; case "DpadLeft": return "←"; case "DpadRight": return "→";
            }
            return v;
        }

        /// <summary>Переназначение: первая нажатая кнопка нужного устройства (null — ещё ничего не нажато).</summary>
        public static string CaptureNext(bool pad)
        {
            if (pad)
            {
                var g = Gamepad.current; if (g == null) return null;
                foreach (GamepadButton gb in Enum.GetValues(typeof(GamepadButton))) { var c = g[gb]; if (c != null && c.wasPressedThisFrame) return "p:" + gb; }
                return null;
            }
            var ms = Mouse.current;
            if (ms != null) { if (ms.leftButton.wasPressedThisFrame) return "m:left"; if (ms.rightButton.wasPressedThisFrame) return "m:right"; if (ms.middleButton.wasPressedThisFrame) return "m:middle"; }
            var kb = Keyboard.current; if (kb == null) return null;
            foreach (var key in kb.allKeys) if (key != null && key.wasPressedThisFrame) return "k:" + key.keyCode;
            return null;
        }
    }
}
