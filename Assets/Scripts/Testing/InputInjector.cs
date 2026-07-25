using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Runtime virtual-device input injection for PlayTestRunner (STEP4).
// Uses InputSystem.AddDevice + QueueStateEvent so existing bindings
// (<Keyboard>/space, <Keyboard>/leftShift, <Mouse>/leftButton, <Mouse>/rightButton,
// <Keyboard>/w,a,s,d via the "Dpad" composite on Move) pick this up as real input,
// without OS-level SendInput.
// Source: docs.unity3d.com/Packages/com.unity.inputsystem@1.19/api/UnityEngine.InputSystem.InputSystem.html (checked 2026-07-20)
// Keyboard keys are bitfield-packed controls, so QueueDeltaStateEvent throws
// InvalidOperationException ("Cannot send delta state events against bitfield controls").
// Keys must go through a full KeyboardState + QueueStateEvent instead.
// Source: docs.unity3d.com/Packages/com.unity.inputsystem@1.19/api/UnityEngine.InputSystem.LowLevel.KeyboardState.html (checked 2026-07-20)
// Mouse buttons have the exact same bitfield problem (MouseState.buttons is a ushort bitmask) —
// found live via execute_code while wiring up the player attack test (checked 2026-07-23):
// QueueDeltaStateEvent(leftButton, 1f) throws ArgumentException "Size 4 of delta state of type
// Single ... does not match size 1 of control". Fixed the same way as keyboard: build a full
// MouseState via MouseState.WithButton(MouseButton, bool) + QueueStateEvent.
public static class InputInjector
{
    private static Keyboard _keyboard;
    private static Mouse _mouse;
    private static readonly HashSet<Key> _heldKeys = new HashSet<Key>();
    private static readonly HashSet<MouseButton> _heldMouseButtons = new HashSet<MouseButton>();

    private static Keyboard Kb
    {
        get
        {
            if (_keyboard == null || !_keyboard.added)
                _keyboard = InputSystem.AddDevice<Keyboard>();
            return _keyboard;
        }
    }

    private static Mouse Ms
    {
        get
        {
            if (_mouse == null || !_mouse.added)
                _mouse = InputSystem.AddDevice<Mouse>();
            return _mouse;
        }
    }

    public static void PressKey(Key key)
    {
        _heldKeys.Add(key);
        InputSystem.QueueStateEvent(Kb, new KeyboardState(_heldKeys.ToArray()));
    }

    public static void ReleaseKey(Key key)
    {
        _heldKeys.Remove(key);
        InputSystem.QueueStateEvent(Kb, new KeyboardState(_heldKeys.ToArray()));
    }

    public static void PressMouseButton(MouseButton button)
    {
        _heldMouseButtons.Add(button);
        SendMouseState();
    }

    public static void ReleaseMouseButton(MouseButton button)
    {
        _heldMouseButtons.Remove(button);
        SendMouseState();
    }

    private static void SendMouseState()
    {
        MouseState state = default;
        foreach (var b in _heldMouseButtons) state = state.WithButton(b, true);
        InputSystem.QueueStateEvent(Ms, state);
    }

    // PlayerActions.inputactions (map "Player") wrappers — matches current bindings exactly.
    public static void PressJump() => PressKey(Key.Space);
    public static void ReleaseJump() => ReleaseKey(Key.Space);
    public static void PressDash() => PressKey(Key.LeftShift);
    public static void ReleaseDash() => ReleaseKey(Key.LeftShift);
    public static void PressAttack() => PressMouseButton(MouseButton.Left);
    public static void ReleaseAttack() => ReleaseMouseButton(MouseButton.Left);
    public static void PressParry() => PressMouseButton(MouseButton.Right);
    public static void ReleaseParry() => ReleaseMouseButton(MouseButton.Right);
    // 일섬 차지 = "Charge" 액션(<Mouse>/rightButton). Parry와 같은 버튼에 걸려 있어 물리적으로 동일한 입력이다
    // (회피-카운터 대기 중이 아니면 Parry 쪽은 아무 일도 하지 않는다).
    public static void PressCharge() => PressMouseButton(MouseButton.Right);
    public static void ReleaseCharge() => ReleaseMouseButton(MouseButton.Right);

    // Move is a "Dpad" composite bound to W/S/A/D.
    public static void SetMoveX(float x)
    {
        if (x > 0.01f) { PressKey(Key.D); ReleaseKey(Key.A); }
        else if (x < -0.01f) { PressKey(Key.A); ReleaseKey(Key.D); }
        else { ReleaseKey(Key.A); ReleaseKey(Key.D); }
    }

    public static void Cleanup()
    {
        if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
        if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
        _keyboard = null;
        _mouse = null;
        _heldKeys.Clear();
        _heldMouseButtons.Clear();
    }
}
