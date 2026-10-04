// STUB (compile-check only): subset of com.unity.inputsystem (UnityEngine.InputSystem). Not functional.
using System;
using UnityEngine;

namespace UnityEngine.InputSystem
{
    public enum Key { None, Space, Enter, Tab, Backquote, Quote, Semicolon, Comma, Period, Slash, Backslash, LeftBracket, RightBracket, Minus, Equals, A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z, Digit0, Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9, LeftShift, RightShift, LeftAlt, RightAlt, LeftCtrl, RightCtrl, LeftMeta, RightMeta, ContextMenu, Escape, LeftArrow, RightArrow, UpArrow, DownArrow, Backspace, PageDown, PageUp, Home, End, Insert, Delete, CapsLock, NumLock, PrintScreen, ScrollLock, Pause, Numpad0, Numpad1, Numpad2, Numpad3, Numpad4, Numpad5, Numpad6, Numpad7, Numpad8, Numpad9, NumpadEnter, NumpadDivide, NumpadMultiply, NumpadPlus, NumpadMinus, NumpadPeriod, NumpadEquals, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12 }

    public class InputControl { public string name { get; } public string path { get; } public bool noisy { get; } public InputDevice device { get; } public bool IsPressed(float pressPoint = 0) => false; }
    public class InputControl<TValue> : InputControl where TValue : struct
    {
        public TValue ReadValue() => default;
        public TValue ReadValueFromPreviousFrame() => default;
        public TValue ReadUnprocessedValue() => default;
    }
    public class AxisControl : InputControl<float> { }
    public class ButtonControl : AxisControl
    {
        public bool isPressed { get; } public bool wasPressedThisFrame { get; } public bool wasReleasedThisFrame { get; } public float pressPoint { get; }
    }
    public class KeyControl : ButtonControl { public Key keyCode { get; } }
    public class Vector2Control : InputControl<Vector2> { public AxisControl x { get; } public AxisControl y { get; } }
    public class StickControl : Vector2Control { public ButtonControl up { get; } public ButtonControl down { get; } public ButtonControl left { get; } public ButtonControl right { get; } }
    public class DpadControl : Vector2Control { public ButtonControl up { get; } public ButtonControl down { get; } public ButtonControl left { get; } public ButtonControl right { get; } }
    public class AnyKeyControl : ButtonControl { }

    public class InputDevice : InputControl { public bool enabled { get; } public int deviceId { get; } }
    public class Pointer : InputDevice { public Vector2Control position { get; } public Vector2Control delta { get; } public ButtonControl press { get; } }
    public class Mouse : Pointer
    {
        public static Mouse current { get; }
        public ButtonControl leftButton { get; } public ButtonControl rightButton { get; } public ButtonControl middleButton { get; } public ButtonControl backButton { get; } public ButtonControl forwardButton { get; }
        public Vector2Control scroll { get; }
        public void WarpCursorPosition(Vector2 position) { }
    }
    public class Keyboard : InputDevice
    {
        public static Keyboard current { get; }
        public KeyControl this[Key key] => null;
        public AnyKeyControl anyKey { get; }
        public KeyControl spaceKey => null;
        public KeyControl enterKey => null;
        public KeyControl tabKey => null;
        public KeyControl escapeKey => null;
        public KeyControl leftArrowKey => null;
        public KeyControl rightArrowKey => null;
        public KeyControl upArrowKey => null;
        public KeyControl downArrowKey => null;
        public KeyControl backspaceKey => null;
        public KeyControl leftShiftKey => null;
        public KeyControl rightShiftKey => null;
        public KeyControl leftCtrlKey => null;
        public KeyControl rightCtrlKey => null;
        public KeyControl leftAltKey => null;
        public KeyControl rightAltKey => null;
        public KeyControl deleteKey => null;
        public KeyControl aKey => null;
        public KeyControl bKey => null;
        public KeyControl cKey => null;
        public KeyControl dKey => null;
        public KeyControl eKey => null;
        public KeyControl fKey => null;
        public KeyControl gKey => null;
        public KeyControl hKey => null;
        public KeyControl iKey => null;
        public KeyControl jKey => null;
        public KeyControl kKey => null;
        public KeyControl lKey => null;
        public KeyControl mKey => null;
        public KeyControl nKey => null;
        public KeyControl oKey => null;
        public KeyControl pKey => null;
        public KeyControl qKey => null;
        public KeyControl rKey => null;
        public KeyControl sKey => null;
        public KeyControl tKey => null;
        public KeyControl uKey => null;
        public KeyControl vKey => null;
        public KeyControl wKey => null;
        public KeyControl xKey => null;
        public KeyControl yKey => null;
        public KeyControl zKey => null;
        public KeyControl digit0Key => null;
        public KeyControl digit1Key => null;
        public KeyControl digit2Key => null;
        public KeyControl digit3Key => null;
        public KeyControl digit4Key => null;
        public KeyControl digit5Key => null;
        public KeyControl digit6Key => null;
        public KeyControl digit7Key => null;
        public KeyControl digit8Key => null;
        public KeyControl digit9Key => null;
        public KeyControl f1Key => null;
        public KeyControl f2Key => null;
        public KeyControl f3Key => null;
        public KeyControl f4Key => null;
        public KeyControl f5Key => null;
        public KeyControl f6Key => null;
        public KeyControl f7Key => null;
        public KeyControl f8Key => null;
        public KeyControl f9Key => null;
        public KeyControl f10Key => null;
        public KeyControl f11Key => null;
        public KeyControl f12Key => null;
        public event Action<char> onTextInput;
    }
    public class Gamepad : InputDevice
    {
        public static Gamepad current { get; }
        public StickControl leftStick { get; } public StickControl rightStick { get; } public DpadControl dpad { get; }
        public ButtonControl buttonSouth { get; } public ButtonControl buttonNorth { get; } public ButtonControl buttonEast { get; } public ButtonControl buttonWest { get; }
        public ButtonControl startButton { get; } public ButtonControl selectButton { get; }
        public ButtonControl leftShoulder { get; } public ButtonControl rightShoulder { get; } public ButtonControl leftTrigger { get; } public ButtonControl rightTrigger { get; }
    }
    public class Touchscreen : Pointer { public static Touchscreen current { get; } }

    public enum InputActionPhase { Disabled, Waiting, Started, Performed, Canceled }
    public class InputAction : IDisposable
    {
        public struct CallbackContext
        {
            public InputActionPhase phase { get; } public InputAction action { get; } public bool started { get; } public bool performed { get; } public bool canceled { get; }
            public TValue ReadValue<TValue>() where TValue : struct => default;
            public bool ReadValueAsButton() => false;
        }
        public InputAction() { }
        public InputAction(string name = null, InputActionType type = default, string binding = null, string interactions = null, string processors = null, string expectedControlType = null) { }
        public string name { get; } public InputActionPhase phase { get; } public bool enabled { get; }
        public event Action<CallbackContext> started; public event Action<CallbackContext> performed; public event Action<CallbackContext> canceled;
        public void Enable() { } public void Disable() { }
        public TValue ReadValue<TValue>() where TValue : struct => default;
        public bool IsPressed() => false; public bool WasPressedThisFrame() => false; public bool WasReleasedThisFrame() => false; public bool WasPerformedThisFrame() => false;
        public bool triggered => false;
        public void Dispose() { }
    }
    public enum InputActionType { Value, Button, PassThrough }
    public class InputActionMap { public string name { get; } public void Enable() { } public void Disable() { } public InputAction FindAction(string name, bool throwIfNotFound = false) => null; public InputAction this[string name] => null; }
    public class InputActionAsset : ScriptableObject { public InputAction FindAction(string name, bool throwIfNotFound = false) => null; public InputActionMap FindActionMap(string name, bool throwIfNotFound = false) => null; public void Enable() { } public void Disable() { } }
    [Serializable] public class InputActionReference : ScriptableObject { public InputAction action { get; } }
    public static class InputSystem { public static event Action<InputDevice, InputDeviceChange> onDeviceChange; }
    public enum InputDeviceChange { Added, Removed, Disconnected, Reconnected, Enabled, Disabled }
}
