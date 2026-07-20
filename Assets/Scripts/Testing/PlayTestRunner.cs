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

        // TODO: dash system not implemented yet (PlayerController.cs has no Dash handling).
        // Once dash exists, fill in:
        // 1) InputInjector.PressDash() to inject the dash input
        // 2) During the dash duration (e.g. 0.1s), expose the player to a damage source and count hits
        // 3) TestLog.Assert(channel, hits == 0, $"hits={hits}")
        // 4) After the dash ends, verify normal hit-taking resumes

        TestLog.Assert(channel, false, "NOT_IMPLEMENTED: dash system missing, see task.md");
        yield break;
    }
}
