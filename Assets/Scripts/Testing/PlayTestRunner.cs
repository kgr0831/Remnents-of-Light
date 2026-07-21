using System.Collections;
using UnityEngine;
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
        runner.StartCoroutine(runner.DashIFrameTest());
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
    }
}
