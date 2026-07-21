using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Source: docs.unity3d.com/6000.3/Documentation/ScriptReference/Object.FindAnyObjectByType.html (checked 2026-07-20)
public class PlayTestRunner : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/PlayTest/Dash I-Frame")]
    private static void RunDashIFrameTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[PlayTestRunner] Enter Play mode first.");
            return;
        }

        var runner = FindAnyObjectByType<PlayTestRunner>();
        if (runner == null)
        {
            var go = new GameObject("PlayTestRunner_Temp");
            runner = go.AddComponent<PlayTestRunner>();
        }
        // Guards against stale virtual devices piling up when this menu item is triggered
        // more than once within the same Play session (found via a reproducible dash-detection
        // failure caused by leaked duplicate Keyboard devices - checked 2026-07-21).
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        TestRecorder.StartRecording("DashIFrameTest");
        runner.StartCoroutine(runner.DashIFrameTest());
    }

    // Default Input System settings only route keyboard input to the game while the Game View
    // has OS focus, and disable devices that aren't focused. Automated Play sessions triggered
    // via MCP/menu items rarely have real OS focus, so injected key presses were silently
    // dropped or caused the virtual Keyboard device to be repeatedly reset/recreated mid-test -
    // this made dash detection non-deterministic depending on focus timing. Re-applied on every
    // test run (not just once) because a script recompile reloads InputSystem.settings from
    // disk, silently reverting an in-memory-only change.
    // Source: docs.unity3d.com/Packages/com.unity.inputsystem/manual/Settings.html
    // (EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView, BackgroundBehavior.IgnoreFocus - checked 2026-07-21)
    private static void EnsureDeterministicInputSettings()
    {
        var settings = InputSystem.settings;
        settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    }
#endif

    public IEnumerator DashIFrameTest()
    {
        const string channel = "dash_iframe";
        TestLog.Step(channel, "spawned");

        var player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            TestLog.Assert(channel, false, "NOT_FOUND: no PlayerController in scene");
            yield break;
        }

        // 데미지/적 시스템이 아직 없어서 실제 피격 카운트 대신, 대시 스펙("레이어 스왑 기반 무적")대로
        // 대시 중 레이어가 무적 레이어로 바뀌었다가 종료 후 원래 레이어로 복귀하는지 검증한다.
        int invincibleLayer = LayerMask.NameToLayer(player.invincibleLayerName);
        if (invincibleLayer == -1)
        {
            TestLog.Assert(channel, false, $"NOT_FOUND: layer '{player.invincibleLayerName}' missing");
            yield break;
        }

        int normalLayer = player.gameObject.layer;

        // 녹화 영상이 유튜브 처리 파이프라인에 너무 짧게 걸리지 않도록(실측: <1s 영상은 "처리 중단됨" 오류)
        // 시작/종료에 여유 구간을 둔다. 판정 로직 자체와는 무관.
        yield return new WaitForSeconds(1.5f);

        InputInjector.SetMoveX(1f);
        yield return null;

        InputInjector.PressDash();
        yield return null;
        InputInjector.ReleaseDash();
        TestLog.Step(channel, "dash_pressed");

        yield return new WaitForSeconds(player.dashDuration * 0.5f);
        bool duringDashInvincible = player.gameObject.layer == invincibleLayer;
        TestLog.Step(channel, $"mid_dash layer_invincible={duringDashInvincible}");

        yield return new WaitForSeconds(player.dashDuration * 0.5f + 0.05f);
        bool afterDashRestored = player.gameObject.layer == normalLayer;
        TestLog.Step(channel, $"post_dash layer_restored={afterDashRestored}");

        InputInjector.SetMoveX(0f);

        bool pass = duringDashInvincible && afterDashRestored;
        TestLog.Assert(channel, pass, $"during={duringDashInvincible} restored={afterDashRestored}");

        yield return new WaitForSeconds(1.5f);

#if UNITY_EDITOR
        string recordingPath = TestRecorder.StopRecording();
        TestLog.Event(channel, $"recording_saved={recordingPath}");
#endif
    }
}
