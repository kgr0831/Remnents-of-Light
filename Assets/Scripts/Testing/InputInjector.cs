using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

// Runtime virtual-device input injection for PlayTestRunner (STEP4).
// Uses InputSystem.AddDevice + QueueDeltaStateEvent so existing bindings
// (<Keyboard>/space, <Keyboard>/leftShift, <Mouse>/leftButton, <Mouse>/rightButton,
// <Keyboard>/w,a,s,d via the "Dpad" composite on Move) pick this up as real input,
// without OS-level SendInput.
// Source: docs.unity3d.com/Packages/com.unity.inputsystem@1.19/api/UnityEngine.InputSystem.InputSystem.html (checked 2026-07-20)
public static class InputInjector
{
    private static Keyboard _keyboard;
    private static Mouse _mouse;

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

    public static void PressKey(Key key) => InputSystem.QueueDeltaStateEvent(Kb[key], 1f);
    public static void ReleaseKey(Key key) => InputSystem.QueueDeltaStateEvent(Kb[key], 0f);
    public static void PressButton(ButtonControl button) => InputSystem.QueueDeltaStateEvent(button, 1f);
    public static void ReleaseButton(ButtonControl button) => InputSystem.QueueDeltaStateEvent(button, 0f);

    // PlayerActions.inputactions (map "Player") wrappers — matches current bindings exactly.
    public static void PressJump() => PressKey(Key.Space);
    public static void ReleaseJump() => ReleaseKey(Key.Space);
    public static void PressDash() => PressKey(Key.LeftShift);
    public static void ReleaseDash() => ReleaseKey(Key.LeftShift);
    public static void PressAttack() => PressButton(Ms.leftButton);
    public static void ReleaseAttack() => ReleaseButton(Ms.leftButton);
    public static void PressParry() => PressButton(Ms.rightButton);
    public static void ReleaseParry() => ReleaseButton(Ms.rightButton);

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
    }
}
