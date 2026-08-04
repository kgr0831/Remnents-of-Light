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

    [MenuItem("Tools/PlayTest/Dash Afterimage Shape")]
    private static void RunDashAfterimageShapeTest()
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        string date = System.DateTime.Now.ToString("yyyyMMdd");
        TestRecorder.StartRecording($"DashAfterimageShape_{date}");
        runner.StartCoroutine(runner.DashAfterimageShapeTest());
    }

    [MenuItem("Tools/PlayTest/Wall Real Map")]
    private static void RunWallRealMapTest()
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        runner.StartCoroutine(runner.WallRealMapTest());
    }

    [MenuItem("Tools/PlayTest/Slope Real Map")]
    private static void RunSlopeRealMapTest()
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        runner.StartCoroutine(runner.SlopeRealMapTest());
    }

    [MenuItem("Tools/PlayTest/Slope Walk")]
    private static void RunSlopeWalkTest()
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        runner.StartCoroutine(runner.SlopeWalkTest());
    }

    [MenuItem("Tools/PlayTest/Wall Climb Gate")]
    private static void RunWallClimbGateTest()
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        runner.StartCoroutine(runner.WallClimbGateTest());
    }

    [MenuItem("Tools/PlayTest/Dodge Counter x Time Accel")]
    private static void RunDodgeCounterTimeAccelTest()
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        runner.StartCoroutine(runner.DodgeCounterTimeAccelTest());
    }

    [MenuItem("Tools/PlayTest/Time Accel")]
    private static void RunTimeAccelTest()
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        // ⚠️ 녹화를 걸지 않는다 — TestRecorder는 captureDeltaTime을 고정해 실측 1.5fps로 떨어뜨리는데,
        // 이 시나리오는 "실시간 이동거리"와 "실시간 광원 소모"를 재기 때문에 그러면 전부 무의미해진다.
        runner.StartCoroutine(runner.TimeAccelTest());
    }

    [MenuItem("Tools/PlayTest/Dummy Enemy Combat")]
    private static void RunDummyEnemyCombatTest()
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        string date = System.DateTime.Now.ToString("yyyyMMdd");
        TestRecorder.StartRecording($"DummyEnemyCombat_{date}");
        runner.StartCoroutine(runner.DummyEnemyCombatTest());
    }

    [MenuItem("Tools/PlayTest/Ilseom")]
    private static void RunIlseomTest()
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        // 일섬은 기능 검증(ASSERT)용이라 녹화하지 않는다. Recorder는 captureDeltaTime을 1/30로 고정해
        // 프레임을 ~1.5fps로 스로틀하는데, 그러면 WaitForSeconds와 입력 주입(Press/Release) 폴링 타이밍이
        // 어긋나 릴리즈가 1~2초 늦게 인식돼 테스트가 비결정적으로 FAIL한다(파일 잠금 시 더 심함). 실시간
        // 프레임에서는 모든 판정이 통과함을 실측 확인(2026-07-25). 영상이 필요하면 Dash 계열 showcase를 쓸 것.
        runner.StartCoroutine(runner.IlseomTest());
    }

    [MenuItem("Tools/PlayTest/Parry")]
    private static void RunParryTest()
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        // 일섬 테스트와 같은 이유로 녹화하지 않는다 — Recorder가 captureDeltaTime을 1/30로 고정하면
        // 우클릭 탭(한 프레임 press→release)이 입력 폴링과 어긋나 비결정적으로 실패한다.
        runner.StartCoroutine(runner.ParryTest());
    }

    [MenuItem("Tools/PlayTest/Execution")]
    private static void RunExecutionTest()
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        // 일섬/패링 테스트와 같은 이유로 녹화하지 않는다 — Recorder가 프레임을 ~1.5fps로 스로틀하면
        // 커서 주입과 R키 한 프레임 입력이 폴링 타이밍과 어긋나 비결정적으로 실패한다.
        runner.StartCoroutine(runner.ExecutionTest());
    }

    [MenuItem("Tools/PlayTest/Execution VFX Showcase")]
    private static void RunExecutionVfxShowcase()
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        string date = System.DateTime.Now.ToString("yyyyMMdd");
        TestRecorder.StartRecording($"ExecutionVfxShowcase_{date}");
        runner.StartCoroutine(runner.ExecutionVfxShowcase());
    }

    [MenuItem("Tools/PlayTest/Player HUD")]
    private static void RunPlayerHudTest()
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        string hudDate = System.DateTime.Now.ToString("yyyyMMdd");
        TestRecorder.StartRecording($"PlayerHud_{hudDate}");
        runner.StartCoroutine(runner.PlayerHudTest());
    }

    [MenuItem("Tools/PlayTest/Game Data (Save & Load)")]
    private static void RunGameDataTest()
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        string dataDate = System.DateTime.Now.ToString("yyyyMMdd");
        TestRecorder.StartRecording($"GameDataSaveLoad_{dataDate}");
        runner.StartCoroutine(runner.GameDataTest());
    }

    [MenuItem("Tools/PlayTest/Trap Crumbling Platform")]
    private static void RunTrapCrumbleTest()
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        string trapDate = System.DateTime.Now.ToString("yyyyMMdd");
        TestRecorder.StartRecording($"TrapCrumble_{trapDate}");
        runner.StartCoroutine(runner.TrapCrumbleTest());
    }

    [MenuItem("Tools/PlayTest/Trap Press")]
    private static void RunTrapPressTest()
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        string pressDate = System.DateTime.Now.ToString("yyyyMMdd");
        TestRecorder.StartRecording($"TrapPress_{pressDate}");
        runner.StartCoroutine(runner.TrapPressTest());
    }

    [MenuItem("Tools/PlayTest/Rampage")]
    private static void RunRampageTest()
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        string rampageDate = System.DateTime.Now.ToString("yyyyMMdd");
        TestRecorder.StartRecording($"Rampage_{rampageDate}");
        runner.StartCoroutine(runner.RampageTest());
    }

    [MenuItem("Tools/PlayTest/Rampage Vision")]
    private static void RunRampageVisionTest()
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        string visionDate = System.DateTime.Now.ToString("yyyyMMdd");
        TestRecorder.StartRecording($"RampageVision_{visionDate}");
        runner.StartCoroutine(runner.RampageVisionTest());
    }

    [MenuItem("Tools/PlayTest/Dash VFX Showcase (Slowmo)")]
    private static void RunDashVfxShowcaseSlowmo() => StartShowcase(true);

    [MenuItem("Tools/PlayTest/Dash VFX Showcase (Realtime)")]
    private static void RunDashVfxShowcaseRealtime() => StartShowcase(false);

    private static void StartShowcase(bool slowMo)
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
        InputInjector.Cleanup();
        EnsureDeterministicInputSettings();
        string date = System.DateTime.Now.ToString("yyyyMMdd");
        string file = slowMo ? $"DashVfxFancy_slowmo_{date}" : $"DashVfxFancy_realtime_{date}";
        TestRecorder.StartRecording(file);
        runner.StartCoroutine(runner.ShowcaseRoutine(slowMo));
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

    // 실제 맵에 배치된 Wall 콜라이더에서 벽타기가 붙는지 — 지상·상승 중·낙하 중 세 가지로 확인한다
    // (사용자 리포트 2026-08-05 "점프중/공중에 떠있을 때 벽타기가 안 발동"). 합성 지형이 아니라
    // 사용자가 실제로 배치한 콜라이더를 그대로 쓴다.
    public IEnumerator WallRealMapTest()
    {
        const string channel = "wall_climb";
        TestLog.Step(channel, "real_map 시작");

        var player = FindAnyObjectByType<PlayerController>();
        if (player == null) { TestLog.Assert(channel, false, "NOT_FOUND: player"); yield break; }
        var rb = player.GetComponent<Rigidbody2D>();
        int wallIdx = LayerMask.NameToLayer("Wall");

        // 사용자가 지목한 두 벽을 높이별로 훑는다 — "어느 높이에서 안 붙는가"를 특정하기 위해서다.
        string[] targets = { "Wall (3)", "Wall (4)" };
        bool allOk = true; string detail = "";
        for (int w = 0; w < targets.Length; w++)
        {
            Collider2D wall = null;
            foreach (var c in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
                if (c.gameObject.layer == wallIdx && c.gameObject.name == targets[w]) { wall = c; break; }
            if (wall == null) { TestLog.Step(channel, targets[w] + ": 없음(건너뜀)"); continue; }
            Bounds wb = wall.bounds;
            TestLog.Step(channel, $"=== {targets[w]} {wall.GetType().Name} x=[{wb.min.x:F2},{wb.max.x:F2}] " +
                                  $"y=[{wb.min.y:F2},{wb.max.y:F2}] trigger={wall.isTrigger} ===");

            for (int h = 0; h < 6; h++)
            {
                float y = Mathf.Lerp(wb.min.y + 0.5f, wb.max.y - 1.5f, h / 5f);
                // 좌우 양쪽에서 접근해 본다(어느 쪽이 뚫려 있는지 모르므로)
                for (int side = 0; side < 2; side++)
                {
                    float dir = side == 0 ? 1f : -1f;
                    float startX = side == 0 ? wb.min.x - 0.7f : wb.max.x + 0.7f;
                    player.transform.position = new Vector3(startX, y, 0f);
                    rb.linearVelocity = Vector2.zero;
                    InputInjector.SetMoveX(0f);
                    yield return null;
                    rb.linearVelocity = new Vector2(0f, 12f); // 점프 상승 상태로 접근
                    InputInjector.SetMoveX(dir);
                    bool grabbed = false;
                    float t0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t0 < 0.5f)
                    {
                        if (player.IsWallSliding) { grabbed = true; break; }
                        yield return null;
                    }
                    InputInjector.SetMoveX(0f);
                    if (side == 0 || !grabbed)
                        TestLog.Step(channel, $"  y={y:F1} {(side == 0 ? "왼→오" : "오→왼")} 붙음={grabbed} " +
                                              $"최종x={player.transform.position.x:F2}");
                    if (grabbed) { allOk &= true; break; } // 한쪽에서라도 붙으면 그 높이는 통과
                    if (side == 1) { allOk = false; detail += $"[{targets[w]} y={y:F1} 실패] "; }
                    yield return null;
                }
            }
        }
        TestLog.Assert(channel, allOk, string.IsNullOrEmpty(detail) ? "real_map_wall_grab 모든 높이 통과" : "real_map_wall_grab " + detail);
        TestLog.Step(channel, "real_map done");
    }

    // 실제 맵(Map1) 지형의 경사에서 미끄러지는지 확인한다(사용자 리포트 "오르막길에서 점점 미끄러집니다").
    // 합성 테스트 지형(30° 단일 박스)에서는 안 미끄러졌으므로, 타일 컴포지트 특유의 형상이 원인인지
    // 그 자리에서 직접 재현해 법선·각도·접촉점을 같이 찍는다.
    public IEnumerator SlopeRealMapTest()
    {
        const string channel = "slope_walk";
        TestLog.Step(channel, "real_map 시작");

        var player = FindAnyObjectByType<PlayerController>();
        if (player == null) { TestLog.Assert(channel, false, "NOT_FOUND: player"); yield break; }
        var rb = player.GetComponent<Rigidbody2D>();
        var col = player.GetComponent<Collider2D>();

        // 지형에서 가장 긴 경사변을 찾아 그 중앙 위에 세운다(맵이 바뀌어도 따라간다).
        Vector2 bestA = Vector2.zero, bestB = Vector2.zero; float bestLen = 0f;
        foreach (var cc in FindObjectsByType<CompositeCollider2D>(FindObjectsSortMode.None))
        {
            if (((1 << cc.gameObject.layer) & player.groundLayer.value) == 0) continue;
            for (int p = 0; p < cc.pathCount; p++)
            {
                var pts = new Vector2[cc.GetPathPointCount(p)];
                cc.GetPath(p, pts);
                for (int i = 0; i < pts.Length; i++)
                {
                    Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
                    Vector2 d = b - a;
                    float len = d.magnitude;
                    if (len < 0.05f) continue;
                    float ang = Mathf.Atan2(Mathf.Abs(d.y), Mathf.Abs(d.x)) * Mathf.Rad2Deg;
                    if (ang > 15f && ang < player.maxSlopeAngle && len > bestLen) { bestLen = len; bestA = a; bestB = b; }
                }
            }
        }
        if (bestLen <= 0f) { TestLog.Assert(channel, false, "NOT_FOUND: 걸을 수 있는 경사변이 없음"); yield break; }
        Vector2 mid = (bestA + bestB) * 0.5f;
        float slopeAng = Mathf.Atan2(Mathf.Abs(bestB.y - bestA.y), Mathf.Abs(bestB.x - bestA.x)) * Mathf.Rad2Deg;
        TestLog.Step(channel, $"대상 경사변 a={bestA.ToString("F1")} b={bestB.ToString("F1")} 길이={bestLen:F1} 각도={slopeAng:F1}");

        player.transform.position = new Vector3(mid.x, mid.y + 1.2f, 0f);
        rb.linearVelocity = Vector2.zero;
        InputInjector.SetMoveX(0f);
        yield return new WaitForSecondsRealtime(0.8f); // 착지 대기

        Vector3 settled = player.transform.position;
        var contacts = new ContactPoint2D[16];
        for (int i = 0; i < 5; i++)
        {
            yield return new WaitForSecondsRealtime(0.3f);
            int n = rb.GetContacts(contacts);
            string ns = "";
            for (int c = 0; c < n && c < 3; c++) ns += contacts[c].normal.ToString("F2") + " ";
            TestLog.Step(channel, $"  t={i * 0.3f + 0.3f:F1}s pos={player.transform.position.ToString("F3")} " +
                $"이동={Vector2.Distance(settled, player.transform.position):F3} vel={rb.linearVelocity.ToString("F2")} " +
                $"grounded={player.IsGrounded} angle={player.GroundAngle:F1} 접촉={n}개 {ns}");
        }
        float slide = Vector2.Distance(settled, player.transform.position);
        bool ok = slide < 0.2f;
        TestLog.Assert(channel, ok, $"real_map_no_slide 1.5초 동안 이동={slide:F3}(<0.2여야 함) 경사={slopeAng:F1}도");
        TestLog.Step(channel, "real_map done");
    }

    // 오르막·내리막(2026-08-04) — 새 맵 지형에 경사면이 많은데(실측: 지형 변 702개 중 259개가 5~85°)
    // 수평 속도만 주면 오르막에선 벽처럼 걸리고 내리막에선 붕 떠서 통통 튄다. 접선 이동이 실제로
    // 그 둘을 해결하는지 런타임 전용 경사 지형(30°)을 세워 확인한다(씬 파일 무변경).
    public IEnumerator SlopeWalkTest()
    {
        const string channel = "slope_walk";
        TestLog.Step(channel, "spawned");

        var player = FindAnyObjectByType<PlayerController>();
        if (player == null) { TestLog.Assert(channel, false, "NOT_FOUND: no PlayerController"); yield break; }
        var rb = player.GetComponent<Rigidbody2D>();
        int groundLayer = LayerMask.NameToLayer("Ground");

        Vector3 origin = player.transform.position + new Vector3(0f, 500f, 0f);
        var temp = new System.Collections.Generic.List<GameObject>();
        temp.Add(MakeBox("SWT_Floor", origin + new Vector3(0f, -0.5f, 0f), new Vector2(14f, 1f), groundLayer, false));
        // 30° 경사면 — 긴 박스를 회전시켜 만든다(x=6 부근에서 시작해 위로)
        var slope = MakeBox("SWT_Slope", origin + new Vector3(11.5f, 2.35f, 0f), new Vector2(12f, 1f), groundLayer, false);
        slope.transform.rotation = Quaternion.Euler(0f, 0f, 30f);
        temp.Add(slope);
        temp.Add(MakeBox("SWT_Top", origin + new Vector3(21.5f, 5.5f, 0f), new Vector2(8f, 1f), groundLayer, false));

        player.transform.position = origin + new Vector3(2f, 0.3f, 0f);
        rb.linearVelocity = Vector2.zero;
        InputInjector.SetMoveX(0f);
        yield return new WaitForSecondsRealtime(0.5f);

        // ── 오르막: 오른쪽으로 걸어 올라간다 ──────────────────────────────────────────────
        float startY = player.transform.position.y - origin.y;
        float startX = player.transform.position.x - origin.x;
        InputInjector.SetMoveX(1f);
        int airFrames = 0, frames = 0;
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < 3.5f)
        {
            frames++;
            if (!player.IsGrounded) airFrames++;
            yield return null;
        }
        float upY = player.transform.position.y - origin.y;
        float upX = player.transform.position.x - origin.x;
        InputInjector.SetMoveX(0f);
        yield return new WaitForSecondsRealtime(0.3f);
        // 경사를 실제로 올라갔는가 + 올라가는 내내 지면에 붙어 있었는가
        bool climbed = upY > startY + 2f;
        bool stuckToGround = frames > 0 && (float)airFrames / frames < 0.15f;
        TestLog.Step(channel, $"uphill y {startY:F2}->{upY:F2} x {startX:F2}->{upX:F2} " +
                              $"공중프레임={airFrames}/{frames}");
        TestLog.Assert(channel, climbed && stuckToGround,
            $"uphill 올라감={climbed}(Δy={upY - startY:F2}) 접지유지={stuckToGround}({airFrames}/{frames})");

        // ── 경사면에서 정지: 미끄러지지 않아야 한다 ────────────────────────────────────────
        float holdX = player.transform.position.x;
        float holdY = player.transform.position.y;
        yield return new WaitForSecondsRealtime(0.8f);
        float drift = Vector2.Distance(new Vector2(holdX, holdY),
                                       new Vector2(player.transform.position.x, player.transform.position.y));
        bool noSlide = drift < 0.15f;
        TestLog.Step(channel, $"idle_on_slope drift={drift:F3}");
        TestLog.Assert(channel, noSlide, $"idle_no_slide drift={drift:F3}(<0.15)");

        // ── 내리막: 왼쪽으로 걸어 내려온다 ────────────────────────────────────────────────
        float downStartY = player.transform.position.y - origin.y;
        InputInjector.SetMoveX(-1f);
        airFrames = 0; frames = 0;
        int logged = 0;
        int groundMask = 1 << LayerMask.NameToLayer("Ground");
        var pcoll = player.GetComponent<Collider2D>();
        t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < 2.5f)
        {
            frames++;
            if (!player.IsGrounded)
            {
                airFrames++;
                // 공중에 뜬 순간의 실제 값을 몇 개만 남긴다 — 추측 대신 원인을 지목하기 위해.
                if (logged < 6)
                {
                    var b = pcoll.bounds;
                    var below = Physics2D.BoxCast(b.center, b.size, 0f, Vector2.down, 3f, groundMask);
                    TestLog.Step(channel, $"  air#{logged} y={player.transform.position.y - origin.y:F2} " +
                        $"vel={rb.linearVelocity.ToString("F2")} 아래지면거리=" +
                        (below.collider != null ? below.distance.ToString("F3") : "없음") +
                        $" angle={player.GroundAngle:F1}");
                    logged++;
                }
            }
            yield return null;
        }
        float downY = player.transform.position.y - origin.y;
        InputInjector.SetMoveX(0f);
        bool descended = downY < downStartY - 1f;
        // 내리막에서 붕 뜨면 공중 프레임이 확 늘어난다 — 그게 "통통 튀는" 증상의 지표다.
        bool smoothDown = frames > 0 && (float)airFrames / frames < 0.15f;
        TestLog.Step(channel, $"downhill y {downStartY:F2}->{downY:F2} 공중프레임={airFrames}/{frames}");
        TestLog.Assert(channel, descended && smoothDown,
            $"downhill 내려옴={descended}(Δy={downY - downStartY:F2}) 접지유지={smoothDown}({airFrames}/{frames})");

        // ── 점프 회귀 확인 ────────────────────────────────────────────────────────────────
        // 슬로프 런치 억제는 "접지 중 위로 솟는 속도"를 깎기 때문에, 점프까지 같이 죽이면 안 된다.
        // 평지와 경사면 두 곳에서 실제로 떠오르는지 본다(jumpSuppressTimer 면제가 동작하는지).
        for (int phase = 0; phase < 2; phase++)
        {
            bool onSlope = phase == 1;
            player.transform.position = origin + new Vector3(onSlope ? 11.5f : 2f, onSlope ? 3.2f : 0.3f, 0f);
            rb.linearVelocity = Vector2.zero;
            InputInjector.SetMoveX(0f);
            yield return new WaitForSecondsRealtime(0.5f);
            float jy0 = player.transform.position.y;
            InputInjector.PressJump();
            yield return null;
            InputInjector.ReleaseJump();
            float peak = jy0;
            float jt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - jt < 0.6f)
            {
                peak = Mathf.Max(peak, player.transform.position.y);
                yield return null;
            }
            float rise = peak - jy0;
            bool jumped = rise > 1.5f;
            TestLog.Step(channel, (onSlope ? "jump_on_slope" : "jump_on_flat") + " 상승=" + rise.ToString("F2"));
            TestLog.Assert(channel, jumped,
                (onSlope ? "jump_on_slope" : "jump_on_flat") + " 상승=" + rise.ToString("F2") + "(>1.5여야 함)");
            yield return new WaitForSecondsRealtime(0.4f);
        }

        for (int i = 0; i < temp.Count; i++) if (temp[i] != null) Destroy(temp[i]);
        TestLog.Step(channel, "done");
    }

    // 벽타기·자동 오르기 규칙(2026-08-04 최종) — 사용자 지시로 "추정"을 전부 걷어내고 명시적 지정으로 바꿨다.
    //   · 벽타기는 **Wall 레이어 콜라이더(climbWallLayer)** 에만 붙는다. 지형(Ground)은 아무리 높아도 벽이 아니다.
    //   · 자동으로 올라가는 건 **벽타기 중에만**(TryLedgeClimb). 접지 상태 걸어 올라가기(TryStepUpShortWall)는 제거.
    //   · 벽 꼭대기 오르기는 1프레임 순간이동이 아니라 ledgeClimbDuration 동안 보간한다.
    //   · 떨어지면서 벽을 잡으면 그 순간 하강이 멎는다.
    // 씬 지형에 기대지 않고 **런타임 전용 테스트 지형**을 세워서 검사한다(끝나면 파괴, 씬 파일 무변경).
    public IEnumerator WallClimbGateTest()
    {
        const string channel = "wall_climb";
        TestLog.Step(channel, "spawned");

        var player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            TestLog.Assert(channel, false, "NOT_FOUND: no PlayerController in scene");
            yield break;
        }
        var rb = player.GetComponent<Rigidbody2D>();
        var pcoll = player.GetComponent<Collider2D>();
        float ph = pcoll.bounds.size.y; // 플레이어 키
        int groundLayer = LayerMask.NameToLayer("Ground");
        int wallLayerIdx = LayerMask.NameToLayer("Wall");
        if (wallLayerIdx < 0)
        {
            TestLog.Assert(channel, false, "NOT_FOUND: \"Wall\" 레이어가 프로젝트에 없음");
            yield break;
        }

        Vector3 origin = player.transform.position + new Vector3(0f, 400f, 0f);
        var temp = new System.Collections.Generic.List<GameObject>();
        temp.Add(MakeBox("WCT_Floor", origin + new Vector3(13f, -0.5f, 0f), new Vector2(50f, 1f), groundLayer, false));

        // ① 지형(Ground)만 있는 높은 기둥 — 벽이 아니므로 붙으면 안 된다
        temp.Add(MakeBox("WCT_GroundPillar", origin + new Vector3(7f, ph * 1.25f, 0f), new Vector2(6f, ph * 2.5f), groundLayer, false));
        // ② 낮은 턱 — 걸어 올라가기가 제거됐으므로 순간이동도 벽타기도 없어야 한다
        temp.Add(MakeBox("WCT_LowLedge", origin + new Vector3(17f, ph * 0.2f, 0f), new Vector2(6f, ph * 0.4f), groundLayer, false));
        // ③ 진짜 벽 — 실제 맵과 같은 구성: 막아주는 Ground 기둥 + 그 왼쪽 면에 겹쳐 놓은 Wall 트리거
        float wallH = 3f;
        temp.Add(MakeBox("WCT_WallPillar", origin + new Vector3(27f, wallH * 0.5f, 0f), new Vector2(6f, wallH), groundLayer, false));
        temp.Add(MakeBox("WCT_WallFace", origin + new Vector3(24.1f, wallH * 0.5f, 0f), new Vector2(0.3f, wallH), wallLayerIdx, true));

        float[] approachX = { 4f, 14f, 24f };
        string[] labels = { "지형기둥(Ground 2.5키)", "낮은턱(Ground 0.4키)", "진짜벽(Wall 트리거)" };
        bool[] expectClimb = { false, false, true };
        bool allOk = true;
        string detail = "";

        for (int i = 0; i < approachX.Length; i++)
        {
            player.transform.position = origin + new Vector3(approachX[i] - 0.45f, 0.2f, 0f);
            rb.linearVelocity = Vector2.zero;
            InputInjector.SetMoveX(0f);
            yield return new WaitForSecondsRealtime(0.35f);

            float startY = player.transform.position.y - origin.y;
            InputInjector.SetMoveX(1f);
            bool climbed = false;
            float maxY = startY;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 0.8f)
            {
                maxY = Mathf.Max(maxY, player.transform.position.y - origin.y);
                if (player.IsWallSliding) { climbed = true; break; }
                yield return null;
            }
            InputInjector.SetMoveX(0f);
            yield return new WaitForSecondsRealtime(0.2f);

            bool ok = climbed == expectClimb[i];
            // ②는 "순간이동으로 올라가지 않았는가"까지 본다 — 걸어 올라가기 제거의 핵심 검증.
            bool teleported = maxY > startY + 0.2f;
            if (i == 1) ok &= !teleported;
            allOk &= ok;
            string extra = i == 1 ? $" teleported={teleported}" : "";
            detail += $"[{labels[i]} climb={climbed} expect={expectClimb[i]} {(ok ? "OK" : "MISMATCH")}{extra}] ";
            TestLog.Step(channel, $"{labels[i]} climbed={climbed} expect={expectClimb[i]} max_y={maxY:F2}{extra}");
        }
        TestLog.Assert(channel, allOk, $"wall_only_climb {detail}");

        // ── 벽 "윗면"에서는 붙지 않아야 한다 ───────────────────────────────────────────────
        // 사용자가 폴리곤으로 벽 실루엣을 통째로 감싸면 윗면도 같은 Wall 콜라이더다. 그 위에 서서
        // 벽 쪽으로 밀어도 벽타기가 붙으면 안 된다(맞은 면의 법선이 위를 향하므로 벽면이 아니다).
        // 재현: Ground 기둥과 **완전히 같은 범위**를 Wall 트리거로 덮고, 그 꼭대기에 서서 밀어본다.
        temp.Add(MakeBox("WCT_TopWallFace", origin + new Vector3(34f, wallH * 0.5f, 0f),
                         new Vector2(6f, wallH), wallLayerIdx, true));
        temp.Add(MakeBox("WCT_TopGround", origin + new Vector3(34f, wallH * 0.5f, 0f),
                         new Vector2(6f, wallH), groundLayer, false));
        player.transform.position = origin + new Vector3(33f, wallH + 0.3f, 0f);
        rb.linearVelocity = Vector2.zero;
        InputInjector.SetMoveX(0f);
        yield return new WaitForSecondsRealtime(0.5f);
        InputInjector.SetMoveX(1f);
        bool climbedOnTop = false;
        float topT = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - topT < 0.8f)
        {
            if (player.IsWallSliding) { climbedOnTop = true; break; }
            yield return null;
        }
        InputInjector.SetMoveX(0f);
        TestLog.Step(channel, $"wall_top 위에 서서 밀기 → 벽타기={climbedOnTop} (False여야 함) " +
                              $"grounded={player.IsGrounded}");
        TestLog.Assert(channel, !climbedOnTop, $"no_climb_on_wall_top climbed={climbedOnTop}");
        yield return new WaitForSecondsRealtime(0.2f);

        // ── 낙하 중 벽 잡기: 붙는 순간 하강이 멎어야 한다 ──────────────────────────────────
        // ⚠️ 먼저 앞 케이스에서 벽에 붙어 있던 상태를 확실히 털어낸다 — 붙어 있으면 중력이 0이라
        // 낙하 자체가 시작되지 않아 "낙하속도 0"으로 측정되는 헛검사가 된다(1차 실행에서 실측).
        player.transform.position = origin + new Vector3(0f, 1f, 0f);
        rb.linearVelocity = Vector2.zero;
        InputInjector.SetMoveX(0f);
        yield return new WaitForSecondsRealtime(0.4f);

        // 벽면 옆 공중에 놓고 하강 속도를 직접 실어 떨어뜨린다(자유낙하 대기보다 결정적이다).
        player.transform.position = origin + new Vector3(approachX[2] - 0.45f, wallH - 0.5f, 0f);
        rb.linearVelocity = new Vector2(0f, -20f);
        InputInjector.SetMoveX(1f);
        bool grabbed = false;
        float fallVel = 0f;
        float grabT = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - grabT < 1.5f)
        {
            if (player.IsWallSliding) { grabbed = true; break; }
            fallVel = Mathf.Abs(rb.linearVelocity.y); // 붙기 직전 프레임의 하강 속도
            yield return null;
        }
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        float velAfterGrab = rb.linearVelocity.y;
        bool fallStopped = grabbed && Mathf.Abs(velAfterGrab) < 1f;
        TestLog.Step(channel, $"fall_grab fall_vel={fallVel:F1} grabbed={grabbed} vel_after={velAfterGrab:F2}");
        TestLog.Assert(channel, fallStopped,
            $"fall_grab_stops 낙하 {fallVel:F1} -> 붙은 뒤 {velAfterGrab:F2} (|v|<1, grabbed={grabbed})");

        // ── 상승 중(점프 중) 벽 잡기 ──────────────────────────────────────────────────────
        // 사용자 리포트 2026-08-05: "점프중 / 공중에 떠있을 때 벽타기가 안 발동한다".
        // 낙하 중(-20)은 위 케이스에서 붙는 것이 확인됐으므로, 위로 솟는 중을 따로 재현한다.
        player.transform.position = origin + new Vector3(approachX[2] - 0.45f, 0.3f, 0f);
        rb.linearVelocity = Vector2.zero;
        InputInjector.SetMoveX(0f);
        yield return new WaitForSecondsRealtime(0.4f);
        rb.linearVelocity = new Vector2(0f, 15f);   // 점프 상승과 같은 상태
        InputInjector.SetMoveX(1f);                 // 벽 쪽으로
        bool grabbedRising = false;
        float riseVelAtGrab = 0f, riseY = 0f;
        float riseT = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - riseT < 1.2f)
        {
            if (player.IsWallSliding)
            {
                grabbedRising = true;
                riseVelAtGrab = rb.linearVelocity.y;
                riseY = player.transform.position.y - origin.y;
                break;
            }
            yield return null;
        }
        InputInjector.SetMoveX(0f);
        TestLog.Step(channel, $"rising_grab 붙음={grabbedRising} y={riseY:F2} vel_y={riseVelAtGrab:F2}");
        TestLog.Assert(channel, grabbedRising, $"rising_grab 상승 중 벽 잡기={grabbedRising}");
        yield return new WaitForSecondsRealtime(0.3f);

        // ── 벽 꼭대기 자동 오르기: 1프레임 순간이동이 아니라 보간이어야 한다 ────────────────
        // ⚠️ 벽 **아래쪽**에서 다시 붙는다 — 위에서 잡으면 머리가 이미 꼭대기보다 높아, 꼭대기 판정은
        // 바로 통과하지만 착지 탐색(머리 위 0.5에서 아래로 0.8)이 실제 바닥까지 닿지 않아 그냥 무시된다
        // (1차 실행에서 saw=False로 실측). 실제 플레이처럼 아래에서 타고 올라가야 재현된다.
        player.transform.position = origin + new Vector3(approachX[2] - 0.45f, 0.3f, 0f);
        rb.linearVelocity = Vector2.zero;
        InputInjector.SetMoveX(1f);
        float reGrabT = Time.realtimeSinceStartup;
        while (!player.IsWallSliding && Time.realtimeSinceStartup - reGrabT < 1f) yield return null;
        bool reGrabbed = player.IsWallSliding;

        bool sawLedgeClimb = false;
        if (reGrabbed)
        {
            InputInjector.PressKey(Key.W); // 벽을 타고 위로
            float climbT = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - climbT < 3f)
            {
                if (player.IsLedgeClimbing) { sawLedgeClimb = true; break; }
                yield return null;
            }
            InputInjector.ReleaseKey(Key.W);
        }
        InputInjector.SetMoveX(0f);
        // 보간 중이라면 다음 프레임에도 여전히 올라타는 중이어야 한다(1프레임 순간이동이면 이미 끝나 있다).
        yield return null;
        bool stillClimbingNextFrame = player.IsLedgeClimbing;
        TestLog.Step(channel, $"ledge_climb re_grabbed={reGrabbed} saw={sawLedgeClimb} " +
                              $"still_next_frame={stillClimbingNextFrame} duration={player.ledgeClimbDuration:F2}");
        TestLog.Assert(channel, sawLedgeClimb && stillClimbingNextFrame,
            $"ledge_climb_smooth re_grabbed={reGrabbed} saw={sawLedgeClimb} still_next_frame={stillClimbingNextFrame}(보간이면 True)");

        // 올라선 뒤 그 자리에 서 있는가 — 예전엔 도착 지점이 모서리라 몸 절반이 허공에 걸려 바로 다시
        // 떨어졌고, 떨어질 때마다 착지 애니메이션이 다시 재생됐다(사용자 리포트 2026-08-04).
        if (sawLedgeClimb)
        {
            InputInjector.SetMoveX(0f);
            float climbEnd = Time.realtimeSinceStartup;
            while (player.IsLedgeClimbing && Time.realtimeSinceStartup - climbEnd < 1f) yield return null;
            float landedY = player.transform.position.y;
            yield return new WaitForSecondsRealtime(0.7f);
            float dropped = landedY - player.transform.position.y;
            bool stayedUp = player.IsGrounded && dropped < 0.3f;
            TestLog.Step(channel, $"ledge_landing 착지 후 0.7초: 낙하={dropped:F2} grounded={player.IsGrounded} " +
                                  $"pos={player.transform.position.ToString("F2")}");
            TestLog.Assert(channel, stayedUp,
                $"ledge_landing_stable 낙하={dropped:F2}(<0.3) grounded={player.IsGrounded}");
        }

        yield return new WaitForSecondsRealtime(0.4f);
        for (int i = 0; i < temp.Count; i++) if (temp[i] != null) Destroy(temp[i]);
        TestLog.Step(channel, "done");
    }

    // 런타임 전용 테스트 지형 한 조각(씬에 저장되지 않는다).
    static GameObject MakeBox(string name, Vector3 center, Vector2 size, int layer, bool trigger)
    {
        var go = new GameObject(name);
        if (layer >= 0) go.layer = layer;
        go.transform.position = center;
        var c = go.AddComponent<BoxCollider2D>();
        c.size = size;
        c.isTrigger = trigger; // Wall 면은 판정 전용 트리거(WallColliderTool이 만드는 것과 같은 구성)
        return go;
    }

    // 회피(대시)-카운터가 시간 가속 중에도 평상시와 같은 타이밍으로 발동하는지(2026-08-04 회귀).
    // 실패했던 원인: 판정 유예(dodgeCounterGraceTimer)를 플레이어 타이머로 보고 실시간으로 보정했더니,
    // 적의 예비동작은 2.5배로 늘어나는데 유예만 그대로라 창이 열리기 전에 만료됐다.
    // 두 번 돌린다 — ① 가속 OFF(기준선, 원래 되던 것) ② 가속 ON(회귀 지점). 둘 다 "예비동작을 보는
    // 순간 대시"라는 같은 플레이를 재현한다.
    public IEnumerator DodgeCounterTimeAccelTest()
    {
        const string channel = "dodge_counter";
        TestLog.Step(channel, "spawned");

        var player = FindAnyObjectByType<PlayerController>();
        var enemy = FindAnyObjectByType<DummyEnemy>();
        if (player == null || enemy == null)
        {
            TestLog.Assert(channel, false, $"NOT_FOUND: player={player != null} enemy={enemy != null}");
            yield break;
        }

        // 적을 세워두고 알려진 거리에 플레이어를 놓는다(SKILL: 추적하게 두면 발동 거리가 매번 달라진다).
        float enemyMoveBackup = enemy.moveSpeed;
        float dashSpeedBackup = player.dashSpeed;
        int energyBackup = player.currentEnergy;
        int hpBackup = player.currentHealth;
        var psr = player.GetComponent<SpriteRenderer>();
        enemy.moveSpeed = 0f;
        player.currentEnergy = Mathf.RoundToInt(player.maxEnergy * 0.4f); // 초월·폭주 밖
        // ⚠️ 1차 실행에서 기준선까지 FAIL이었는데 원인은 타이밍이 아니라 거리였다 — 기본 대시는 3.6유닛
        // (20×0.18)을 날아가 창 사거리(2.28) 밖으로 빠져나가서 FindPlayerAtHitPoint에 안 걸린다.
        // 이 테스트가 재려는 건 "판정 유예가 적의 공격 타임라인과 겹치는가" 하나뿐이므로, 대시 거리를
        // 줄여 위치 변수를 제거한다(런타임 전용, 끝나면 원복). 유예·무적·타이머는 전부 그대로다.
        player.dashSpeed = 2f;

        bool accelPass = false, basePass = false;
        for (int pass = 0; pass < 2; pass++)
        {
            bool useAccel = pass == 1;
            // 창 사거리(2.4) 안쪽에 세운다 — 창 캡슐이 밑동~창끝 전체라 이 거리면 판정에 확실히 걸린다.
            Vector3 ep = enemy.transform.position;
            player.transform.position = new Vector3(ep.x - 1.5f, ep.y + 0.5f, player.transform.position.z);
            if (psr != null) psr.flipX = false; // 적(오른쪽)을 바라보게 — 대시 방향이 이 값을 따른다
            player.currentHealth = player.maxHealth;
            InputInjector.SetMoveX(0f);
            yield return new WaitForSecondsRealtime(0.6f); // 착지 + 적이 다음 공격을 준비할 여유

            if (useAccel)
            {
                InputInjector.PressTimeAccel();
                yield return null;
                InputInjector.ReleaseTimeAccel();
                yield return new WaitForSecondsRealtime(0.1f);
            }
            bool wasAttacking = enemy.IsAttacking;
            TestLog.Step(channel, $"pass{pass} accel={player.IsTimeAccelActive} " +
                                  $"dist={Mathf.Abs(player.transform.position.x - enemy.transform.position.x):F2} " +
                                  $"already_attacking={wasAttacking}");

            // 예비동작이 "시작되는" 순간(IsAttacking의 상승 엣지 = StartAttack)을 기다렸다가 그 즉시 대시 —
            // 예고를 보고 회피하는 실제 플레이와 같은 입력이다. AttackTelegraphProgress로 기다리면
            // 진행 중인 공격의 잔여 구간에도 걸려 너무 이르게 눌린다(1차 실행의 또 다른 결함).
            float waitStart = Time.realtimeSinceStartup;
            while ((enemy.IsAttacking || wasAttacking) && Time.realtimeSinceStartup - waitStart < 8f)
            {
                if (!enemy.IsAttacking) wasAttacking = false; // 진행 중이던 공격이 끝날 때까지 먼저 흘려보낸다
                yield return null;
            }
            while (!enemy.IsAttacking && Time.realtimeSinceStartup - waitStart < 8f) yield return null;
            bool sawTelegraph = enemy.IsAttacking;
            int hpAtDash = player.currentHealth;
            InputInjector.PressDash();
            yield return null;
            InputInjector.ReleaseDash();

            // 판정 창이 닫힐 때까지(가속 중엔 실시간으로 2.5배 길다) 카운터 발동을 관찰한다.
            bool countered = false;
            float watchStart = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - watchStart < 3f)
            {
                if (player.IsDodgeCountering) { countered = true; break; }
                yield return null;
            }
            // HP가 깎였는지를 같이 남긴다 — 실패했을 때 "공격이 닿았는데 회피가 안 잡힘"(타이밍 문제)인지
            // "공격 자체가 안 닿음"(배치 문제)인지 한 줄로 구분된다(SKILL 8번).
            bool tookHit = player.currentHealth < hpAtDash;
            TestLog.Step(channel, $"pass{pass} accel={useAccel} saw_telegraph={sawTelegraph} " +
                                  $"countered={countered} took_hit={tookHit}");
            if (useAccel) accelPass = countered && sawTelegraph;
            else basePass = countered && sawTelegraph;

            // 다음 패스를 위해 정리 — 카운터 시퀀스가 끝나고 가속도 확실히 꺼진 상태로 돌아간다.
            float cleanupStart = Time.realtimeSinceStartup;
            while (player.IsDodgeCountering && Time.realtimeSinceStartup - cleanupStart < 5f) yield return null;
            if (player.IsTimeAccelActive)
            {
                InputInjector.PressTimeAccel();
                yield return null;
                InputInjector.ReleaseTimeAccel();
            }
            yield return new WaitForSecondsRealtime(0.4f);
        }

        // 기준선이 실패하면 가속과 무관한 문제다 — 둘을 따로 못박아 둔다(음성 판정 방지, SKILL 8번).
        TestLog.Assert(channel, basePass, $"baseline(가속 OFF) countered={basePass}");
        TestLog.Assert(channel, accelPass, $"time_accel(가속 ON) countered={accelPass}");

        enemy.moveSpeed = enemyMoveBackup;
        player.dashSpeed = dashSpeedBackup;
        player.currentEnergy = energyBackup;
        player.currentHealth = hpBackup;
        TestLog.Step(channel, "done");
    }

    // 시간 가속(2026-08-04) — Shift 탭/홀드 분리, 플레이어 보정(가속 중에도 실시간 이동이 평소와 동일),
    // 세계 감속(게임시간이 실시간의 timeAccelTimeScale배로 흐름), 실시간 광원 드레인, 10% 강제 해제 +
    // 재진입 차단, 잔상 수명 절반, 종료 후 전역 시간·중력 복원까지 한 번에 확인한다.
    // 판정값은 전부 player.<필드>에서 읽는다(튜닝이 바뀌어도 테스트가 따라가도록 — SKILL 7번).
    public IEnumerator TimeAccelTest()
    {
        const string channel = "time_accel";
        TestLog.Step(channel, "spawned");

        var player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            TestLog.Assert(channel, false, "NOT_FOUND: no PlayerController in scene");
            yield break;
        }
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            TestLog.Assert(channel, false, "NOT_FOUND: no Rigidbody2D on player");
            yield break;
        }

        float baselineFixedDelta = Time.fixedDeltaTime;
        float baselineGravity = rb.gravityScale;
        float baselineMaxTranslation = Physics2D.maxTranslationSpeed;
        int energyBackup = player.currentEnergy;
        // 초월(광원 100%)·폭주(광원 0) 어느 쪽도 아닌 중간값으로 고정 — 그 두 상태가 끼면 드레인이
        // 섞여 실시간 소모 측정이 오염된다(런타임 전용 변경, 끝나면 원복).
        // 진입 보너스(+20%)가 붙어도 초월(100%)에 닿지 않도록 절반보다 낮게 잡는다.
        player.currentEnergy = Mathf.RoundToInt(player.maxEnergy * 0.4f);

        yield return new WaitForSecondsRealtime(0.3f);

        // ── Phase 1: Shift = 대시(누른 즉시), 시간 가속은 안 켜짐 ─────────────────────────
        InputInjector.SetMoveX(1f);
        yield return null;
        InputInjector.PressDash();
        yield return null;                 // 입력 이벤트 전달 + 같은 프레임 HandleDash 소비
        yield return null;
        bool tapDashed = player.IsInvincible;      // 대시 중 무적(일섬·처형은 안 도는 구간이라 곧 isDashing)
        bool tapNoAccel = !player.IsTimeAccelActive;
        InputInjector.ReleaseDash();
        TestLog.Step(channel, $"dash_press dashed={tapDashed} accel_off={tapNoAccel}");
        TestLog.Assert(channel, tapDashed && tapNoAccel, $"phase1_dash_key dash={tapDashed} accel_off={tapNoAccel}");

        InputInjector.SetMoveX(0f);
        yield return new WaitForSecondsRealtime(player.dashCooldown + 0.3f);

        // ── Phase 2: 평상시 실시간 이동거리 기준선 ────────────────────────────────────────
        // ⚠️ 스폰 지점에서 그냥 재면 안 된다 — 1차 실행에서 플레이어가 벽에 붙은 채로 시작해
        // 기준선이 0.000이 나왔다(STALL 로그와 일치). 다른 대시 시나리오가 쓰는 "하부 넓은 바닥
        // 중앙"(좌우로 충분히 트여 있음)으로 옮겨서 잰다. 런타임 전용 이동이라 씬은 안 바뀐다.
        const float flatCenterX = 10f;
        const float flatCenterY = 23f;
        player.transform.position = new Vector3(flatCenterX, flatCenterY + 1f, player.transform.position.z);
        rb.linearVelocity = Vector2.zero;
        yield return new WaitForSecondsRealtime(0.4f); // 착지 대기

        const float sampleSeconds = 0.5f;
        InputInjector.SetMoveX(1f);
        yield return new WaitForSecondsRealtime(0.15f);   // 속도가 목표치에 오를 여유
        float baseStartX = player.transform.position.x;
        float baseVel = 0f;
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < sampleSeconds)
        {
            baseVel = Mathf.Max(baseVel, Mathf.Abs(rb.linearVelocity.x) * Time.timeScale); // 실시간 환산 속도
            yield return null;
        }
        float baseDist = Mathf.Abs(player.transform.position.x - baseStartX);
        InputInjector.SetMoveX(0f);
        TestLog.Step(channel, $"baseline dist={baseDist:F3} realtime_speed={baseVel:F2}");
        // 기준선이 0에 가까우면 지형에 막힌 것이다 — 보정 수식과 무관한 실패이므로 그렇게 못박아 둔다
        // (음성 판정으로 조용히 넘어가지 않게, SKILL 8번).
        if (baseDist < 0.5f)
        {
            TestLog.Assert(channel, false,
                $"SETUP_FAILED: 기준선 이동이 막힘(dist={baseDist:F3} at x={baseStartX:F2}) — 측정 지점을 다시 잡을 것");
            player.currentEnergy = energyBackup;
            yield break;
        }
        yield return new WaitForSecondsRealtime(0.3f);

        // ── Phase 3: Alt 토글로 진입 + 진입 보너스 + 플레이어는 그대로, 세계만 느려짐 ─────
        InputInjector.SetMoveX(-1f);                     // 왔던 길을 되돌아가 같은 지형에서 잰다
        InputInjector.PressTimeAccel();
        yield return null;
        InputInjector.ReleaseTimeAccel();                // 토글이라 누른 순간 켜지고, 뗌은 아무 영향 없음
        yield return new WaitForSecondsRealtime(0.15f);

        bool accelOn = player.IsTimeAccelActive;
        bool scaleOk = Mathf.Abs(Time.timeScale - player.timeAccelTimeScale) < 0.001f;
        bool fixedOk = Mathf.Abs(Time.fixedDeltaTime - baselineFixedDelta * player.timeAccelTimeScale) < 1e-6f;
        TestLog.Step(channel, $"toggle_on accel={accelOn} timeScale={Time.timeScale:F2} fixedDelta={Time.fixedDeltaTime:F5}");
        TestLog.Assert(channel, accelOn && scaleOk && fixedOk,
            $"phase2_enter accel={accelOn} scale={Time.timeScale:F2}/{player.timeAccelTimeScale:F2} fixed={Time.fixedDeltaTime:F5}");

        // 플레이어 이동거리(실시간 기준)와 "세계 시간이 얼마나 느리게 흘렀는지"를 같은 구간에서 잰다.
        float accStartX = player.transform.position.x;
        float gameT0 = Time.time;
        float accVel = 0f;
        t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < sampleSeconds)
        {
            accVel = Mathf.Max(accVel, Mathf.Abs(rb.linearVelocity.x) * Time.timeScale);
            yield return null;
        }
        float accDist = Mathf.Abs(player.transform.position.x - accStartX);
        float worldRate = (Time.time - gameT0) / (Time.realtimeSinceStartup - t0); // 세계 시간 / 실시간

        // 이동거리 5% 이내(요구: "플레이어는 변화하지 않는다"), 세계는 timeAccelTimeScale배(±5%p).
        float distErr = baseDist > 0.01f ? Mathf.Abs(accDist - baseDist) / baseDist : 999f;
        float speedErr = baseVel > 0.01f ? Mathf.Abs(accVel - baseVel) / baseVel : 999f;
        bool playerUnchanged = distErr <= 0.05f;
        bool worldSlowed = Mathf.Abs(worldRate - player.timeAccelTimeScale) <= 0.05f;
        // 거리와 속도를 함께 남긴다 — 실패했을 때 "보정 수식이 틀림"인지 "지형에 막혀 못 감"인지 구분된다.
        TestLog.Step(channel, $"accel_move dist={accDist:F3} vs base={baseDist:F3} err={distErr:P1} " +
                              $"speed={accVel:F2} vs base={baseVel:F2} speed_err={speedErr:P1} world_rate={worldRate:F2}");
        TestLog.Assert(channel, playerUnchanged && worldSlowed,
            $"phase3_compensation dist_err={distErr:P1}(≤5%) world_rate={worldRate:F2}(={player.timeAccelTimeScale:F2}) speed_err={speedErr:P1}");

        // ── Phase 4: 잔상 수명이 절반 ─────────────────────────────────────────────────────
        float expectedLifetime = player.afterImageLifetime / player.fastAfterImageFadeMultiplier;
        float foundLifetime = -1f;
        var lifetimeField = typeof(DashAfterImage).GetField("lifetime",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (var ghost in FindObjectsByType<DashAfterImage>(FindObjectsSortMode.None))
        {
            if (ghost.gameObject.name != "DashAfterImage" || lifetimeField == null) continue;
            foundLifetime = (float)lifetimeField.GetValue(ghost);
            break;
        }
        bool ghostFast = foundLifetime > 0f && Mathf.Abs(foundLifetime - expectedLifetime) < 0.01f;
        TestLog.Step(channel, $"afterimage lifetime={foundLifetime:F3} expected={expectedLifetime:F3}");
        TestLog.Assert(channel, ghostFast, $"phase4_afterimage lifetime={foundLifetime:F3} expected={expectedLifetime:F3}");

        // ── Phase 5: 실시간 광원 드레인(초당 transcendDrainPerSecond × timeAccelDrainMultiplier) ──
        int drainStart = player.currentEnergy;
        float drainSeconds = 1.5f;
        yield return new WaitForSecondsRealtime(drainSeconds);
        int drained = drainStart - player.currentEnergy;
        float expectedDrain = player.transcendDrainPerSecond * player.timeAccelDrainMultiplier * drainSeconds;
        bool drainOk = Mathf.Abs(drained - expectedDrain) <= 1.5f;
        TestLog.Step(channel, $"drain {drained} in {drainSeconds}s (expected≈{expectedDrain:F1})");
        TestLog.Assert(channel, drainOk, $"phase5_drain drained={drained} expected≈{expectedDrain:F1} (±1.5)");

        // ── Phase 6: 광원 10%에서 강제 해제 + 재진입 차단 ──────────────────────────────────
        player.currentEnergy = player.TimeAccelMinEnergy + 3; // 곧 임계치를 넘어 스스로 풀려야 한다
        float waitStart = Time.realtimeSinceStartup;
        while (player.IsTimeAccelActive && Time.realtimeSinceStartup - waitStart < 3f) yield return null;
        bool autoReleased = !player.IsTimeAccelActive;
        bool scaleRestored = Mathf.Abs(Time.timeScale - 1f) < 0.001f;
        TestLog.Step(channel, $"low_energy_release released={autoReleased} energy={player.currentEnergy} " +
                              $"min={player.TimeAccelMinEnergy} timeScale={Time.timeScale:F2}");

        yield return new WaitForSecondsRealtime(0.2f);
        InputInjector.PressTimeAccel();                   // 10% 이하에서 다시 토글 → 진입 자체가 막혀야 함
        yield return null;
        InputInjector.ReleaseTimeAccel();
        yield return new WaitForSecondsRealtime(0.15f);
        bool reentryBlocked = !player.IsTimeAccelActive;
        TestLog.Assert(channel, autoReleased && scaleRestored && reentryBlocked,
            $"phase6_low_energy released={autoReleased} scale_restored={scaleRestored} reentry_blocked={reentryBlocked}");

        // ── Phase 7: 토글 off → 베이스라인 복원 ───────────────────────────────────────────
        // 광원을 되돌려 놓고 켰다 끄는 "정상 토글 종료" 경로로 복원까지 확인한다(위 Phase 6은 강제 해제).
        player.currentEnergy = Mathf.RoundToInt(player.maxEnergy * 0.4f);
        InputInjector.PressTimeAccel();
        yield return null;
        InputInjector.ReleaseTimeAccel();
        yield return new WaitForSecondsRealtime(0.2f);
        bool toggledBackOn = player.IsTimeAccelActive;
        InputInjector.PressTimeAccel();                   // 같은 키를 다시 → 꺼져야 한다
        yield return null;
        InputInjector.ReleaseTimeAccel();
        yield return new WaitForSecondsRealtime(0.15f);
        bool toggledOff = !player.IsTimeAccelActive;
        TestLog.Step(channel, $"toggle_cycle on={toggledBackOn} off={toggledOff}");
        TestLog.Assert(channel, toggledBackOn && toggledOff, $"phase7a_toggle on={toggledBackOn} off={toggledOff}");

        InputInjector.SetMoveX(0f);
        yield return new WaitForSecondsRealtime(player.dodgeGrayscaleRampOut + 0.4f);

        bool timeRestored = Mathf.Abs(Time.timeScale - 1f) < 0.001f
                            && Mathf.Abs(Time.fixedDeltaTime - baselineFixedDelta) < 1e-6f
                            && Mathf.Abs(Physics2D.maxTranslationSpeed - baselineMaxTranslation) < 0.001f;
        bool gravityRestored = Mathf.Abs(rb.gravityScale - baselineGravity) < 0.001f;
        float grayscale = GrayscaleRendererFeature.Instance != null ? GrayscaleRendererFeature.Instance.Intensity : 0f;
        bool grayscaleCleared = grayscale <= 0.001f;
        TestLog.Step(channel, $"restored timeScale={Time.timeScale:F2} fixedDelta={Time.fixedDeltaTime:F5} " +
                              $"gravity={rb.gravityScale:F2}/{baselineGravity:F2} grayscale={grayscale:F3}");
        TestLog.Assert(channel, timeRestored && gravityRestored && grayscaleCleared,
            $"phase7b_restore time={timeRestored} gravity={gravityRestored} grayscale={grayscale:F3}");

        player.currentEnergy = energyBackup; // 런타임 전용 변경 원복
        TestLog.Step(channel, "done");
    }

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

    // 일섬 3단 검증.
    //  Phase 1 — 2초 전에 떼면 취소되고 이동/무적이 전혀 없다.
    //  Phase 2 — 2초 넘게 모아서 떼면 발동: 시퀀스 중 무적, 경로 위 적에게 4배(처형) 피해,
    //            "가장 먼 적보다 ilseomPastEnemyDistance만큼 더" 지점에 정확히 멈춘다.
    //  Phase 3 — 성공 직후 쿨타임 동안은 아무리 눌러도 차지가 시작되지 않는다.
    // 적이 추적하면 발동 시점의 거리가 매번 달라져 멈출 위치를 예측할 수 없으므로,
    // Phase 2 동안만 moveSpeed=0으로 묶고 알려진 위치에 세워둔다(런타임 전용, 끝나면 원복).
    // 패링 3단 검증. 판정값은 전부 player/dummy의 튜닝 필드에서 읽어 온다(하드코딩 금지 — 수치가 바뀌어도
    // 테스트가 따라가야 하므로). 적은 moveSpeed=0으로 고정해 발동 시점의 거리가 매번 같게 만든다.
    //
    // ★ 거짓 통과 방지: "HP가 안 줄었다"는 음성 판정이라 기능이 아예 안 걸려도 통과한다(적이 그냥 빗나가도
    //   HP는 그대로). 그래서 로그 콜백으로 [EVENT] parry_timing이 실제로 찍혔는지도 함께 확인한다.
    public IEnumerator ParryTest()
    {
        const string channel = "parry_timing";
        TestLog.Step(channel, "spawned");

        var player = FindAnyObjectByType<PlayerController>();
        var dummy = FindAnyObjectByType<DummyEnemy>(FindObjectsInactive.Include);
        if (player == null || dummy == null)
        {
            TestLog.Assert(channel, false, $"NOT_FOUND: player={player != null} dummy={dummy != null}");
            yield break;
        }

        var events = new System.Collections.Generic.List<string>();
        Application.LogCallback capture = (msg, stack, type) =>
        {
            if (msg.Contains("[EVENT] " + channel)) events.Add(msg);
        };
        Application.logMessageReceived += capture;

        var isParryingField = typeof(PlayerController).GetField("isParrying",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        bool dummyWasActive = dummy.gameObject.activeSelf;
        float dummySpeed = dummy.moveSpeed;
        var playerRb = player.GetComponent<Rigidbody2D>();
        var dummyRb = dummy.GetComponent<Rigidbody2D>();

        try
        {
            dummy.gameObject.SetActive(true);
            dummy.moveSpeed = 0f; // 추적 금지 — 발동 시점의 거리를 고정해야 판정이 재현된다

            // 적은 "사거리 안쪽 끝"에 세운다. 창끝 판정원은 적 몸에서 spearThrustLocalPos.x만큼 앞이라
            // 적이 너무 가까우면 판정원이 플레이어를 지나쳐 1타 박스 왼쪽 밖으로 빠진다.
            Vector3 basePos = new Vector3(1.61f, 0.05f, 0f);
            float standoff = dummy.attackRange - 0.05f;
            player.transform.position = basePos;
            if (playerRb != null) playerRb.linearVelocity = Vector2.zero;
            dummy.transform.position = new Vector3(basePos.x + standoff, 0.15f, 0f);
            if (dummyRb != null) dummyRb.linearVelocity = Vector2.zero;
            InputInjector.ReleaseCharge();
            InputInjector.ReleaseAttack();

            InputInjector.SetMoveX(1f); // 오른쪽(적 쪽)을 보게 만든다
            yield return null;
            yield return null;
            InputInjector.SetMoveX(0f);
            yield return null;
            yield return null;
            // 정렬 입력이 실제 이동으로도 이어져 플레이어가 적 쪽으로 끌려간다(실측 +0.30). 그만큼 거리가
            // 줄면 창끝이 1타 박스 뒤로 빠져 판정 자체가 성립하지 않으므로 좌표를 원위치로 되돌린다.
            player.transform.position = basePos;
            if (playerRb != null) playerRb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.25f); // 착지 안정화(적도 이 사이에 지면에 안착)

            // 적은 중력으로 지면에 안착하며 y가 바뀐다 → 최종 y는 그대로 두고 x만 사거리 안쪽 끝으로 재정렬.
            dummy.transform.position = new Vector3(basePos.x + standoff, dummy.transform.position.y, 0f);
            if (dummyRb != null) dummyRb.linearVelocity = Vector2.zero;
            yield return null;

            // 판정 기하를 미리 찍어둔다 — 실패했을 때 "겹쳤는데 안 됨"인지 "애초에 안 겹침"인지 구분용.
            Vector2 boxCenter = (Vector2)player.transform.position + Vector2.right * player.attackHitboxDistance;
            Vector2 boxHalf = player.attackHitboxSize * 0.5f;
            Vector2 hp = dummy.AttackHitPoint;
            Vector2 nearest = new Vector2(
                Mathf.Clamp(hp.x, boxCenter.x - boxHalf.x, boxCenter.x + boxHalf.x),
                Mathf.Clamp(hp.y, boxCenter.y - boxHalf.y, boxCenter.y + boxHalf.y));
            float gap = Vector2.Distance(hp, nearest);
            TestLog.Step(channel, $"geometry box=({boxCenter.x:F2},{boxCenter.y:F2}) size={player.attackHitboxSize} " +
                $"hitPoint=({hp.x:F2},{hp.y:F2}) r={dummy.AttackHitRadius:F2} gap={gap:F2} overlaps={gap <= dummy.AttackHitRadius}");

            // ── Phase 1: 적 공격 모션 중 우클릭 탭 → 패링 성공 + 실드 생성 ──────────────
            // ★ IsAttacking만 보면 안 된다 — 이미 판정이 끝난(attackHitDone) 공격의 Recover 구간도 True라,
            //   정지 없이 이어진 Play 세션에서는 "막을 수 없는 공격"을 잡아 비결정적으로 FAIL한다(실측).
            //   아직 판정이 남아 있는(IsAttackUnresolved) 공격이 시작될 때까지 기다린다.
            float wait = 0f;
            while (wait < 4f && !(dummy.IsAttacking && dummy.IsAttackUnresolved))
            {
                wait += Time.deltaTime;
                yield return null;
            }
            TestLog.Step(channel, $"phase1 enemy_attacking={dummy.IsAttacking} unresolved={dummy.IsAttackUnresolved} after={wait:F2}s");

            int hpBefore = player.currentHealth;
            InputInjector.PressCharge();
            yield return null;
            InputInjector.ReleaseCharge();
            yield return null;
            yield return null; // 릴리즈가 폴링되고 TryParry가 도는 데 한 프레임 더

            bool motionPlaying = isParryingField != null && (bool)isParryingField.GetValue(player);
            bool shieldUp = player.HasParryShield;
            bool successLogged = events.Exists(e => e.Contains("parry_success"));
            TestLog.Step(channel, $"phase1 motion={motionPlaying} shield={shieldUp} success_logged={successLogged}");

            // 패링한 그 공격이 실제로 무피해로 끝나는지(스펙 5) — 창이 회수될 때까지 지켜본다.
            yield return new WaitForSeconds(dummy.windupDuration + dummy.thrustDuration + dummy.recoverDuration + 0.2f);
            bool noDamage = player.currentHealth == hpBefore;
            bool phase1 = shieldUp && successLogged && noDamage && motionPlaying;
            TestLog.Step(channel, $"phase1_result hp={player.currentHealth}/{hpBefore} noDamage={noDamage} pass={phase1}");

            // ── Phase 2: 실드가 다음 공격 1회를 막고 깨진다 ────────────────────────────
            int hpBeforeShield = player.currentHealth;
            float t2 = 0f;
            while (t2 < 4f && player.HasParryShield) { t2 += Time.deltaTime; yield return null; }
            bool shieldConsumed = !player.HasParryShield;
            bool blockLogged = events.Exists(e => e.Contains("shield_blocked"));
            yield return null;
            bool stillNoDamage = player.currentHealth == hpBeforeShield;
            bool phase2 = shieldConsumed && blockLogged && stillNoDamage;
            TestLog.Step(channel, $"phase2_result consumed={shieldConsumed} block_logged={blockLogged} " +
                $"hp={player.currentHealth}/{hpBeforeShield} pass={phase2}");

            // ── Phase 3: 탭보다 길게 누르면 패링이 아니라 일섬 차지로 넘어간다 ──────────
            events.Clear();
            InputInjector.PressCharge();
            yield return new WaitForSeconds(player.parryTapMaxHold + 0.2f);
            InputInjector.ReleaseCharge();
            yield return null;
            yield return null;

            bool parriedOnHold = isParryingField != null && (bool)isParryingField.GetValue(player);
            bool noParryEvent = !events.Exists(e => e.Contains("parry_success") || e.Contains("parry_miss"));
            bool phase3 = !parriedOnHold && noParryEvent;
            TestLog.Step(channel, $"phase3_result parriedOnHold={parriedOnHold} noParryEvent={noParryEvent} pass={phase3}");

            TestLog.Assert(channel, phase1 && phase2 && phase3,
                $"phase1={phase1} phase2={phase2} phase3={phase3}");
        }
        finally
        {
            Application.logMessageReceived -= capture;
            InputInjector.ReleaseCharge();
            dummy.moveSpeed = dummySpeed;
            dummy.gameObject.SetActive(dummyWasActive);
        }
    }

    public IEnumerator IlseomTest()
    {
        const string channel = "ilseom";
        TestLog.Step(channel, "spawned");

        var player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            TestLog.Assert(channel, false, "NOT_FOUND: no PlayerController in scene");
            yield break;
        }

        int invincibleLayer = LayerMask.NameToLayer(player.invincibleLayerName);
        if (invincibleLayer == -1)
        {
            TestLog.Assert(channel, false, $"NOT_FOUND: layer '{player.invincibleLayerName}' missing");
            yield break;
        }
        int normalLayer = player.gameObject.layer;
        var enemy = FindAnyObjectByType<DummyEnemy>();

        yield return new WaitForSeconds(0.8f);

        // ── Phase 1: 조기 릴리즈 → 취소 ────────────────────────────────────────────
        InputInjector.SetMoveX(1f);                     // 오른쪽을 보게 만든 뒤
        yield return null;
        InputInjector.SetMoveX(0f);
        yield return null;

        Vector3 cancelStart = player.transform.position;
        InputInjector.PressCharge();
        yield return new WaitForSeconds(player.ilseomChargeTime * 0.35f);
        InputInjector.ReleaseCharge();
        yield return new WaitForSeconds(0.35f);

        float movedOnCancel = Mathf.Abs(player.transform.position.x - cancelStart.x);
        bool cancelOk = movedOnCancel < 0.2f && player.gameObject.layer == normalLayer && !player.IsInvincible;
        TestLog.Step(channel, $"phase1_cancel moved={movedOnCancel:F2} layer_ok={player.gameObject.layer == normalLayer} pass={cancelOk}");

        // ── Phase 2: 완충 후 릴리즈 → 발동 ─────────────────────────────────────────
        Vector3 start = player.transform.position;
        float savedEnemySpeed = 0f;
        float enemyOffset = 5f;                          // maxDist(7.2) 안쪽의 알려진 거리
        int enemyHpBefore = -1;
        if (enemy != null)
        {
            savedEnemySpeed = enemy.moveSpeed;
            enemy.moveSpeed = 0f;
            enemy.transform.position = new Vector3(start.x + enemyOffset, enemy.transform.position.y, enemy.transform.position.z);
            enemyHpBefore = enemy.currentHp;
        }
        yield return null;

        InputInjector.PressCharge();
        yield return new WaitForSeconds(player.ilseomChargeTime + 0.25f);
        InputInjector.ReleaseCharge();

        // Glitch Out 재생 중간 — 이 시점엔 무적 + 무적 레이어여야 한다
        yield return new WaitForSeconds(player.ilseomGlitchOutDuration * 0.5f);
        bool invincibleDuring = player.gameObject.layer == invincibleLayer && player.IsInvincible;
        TestLog.Step(channel, $"phase2_mid invincible={invincibleDuring}");

        // 이동 + Sweep까지 전부 끝날 때까지 대기
        yield return new WaitForSeconds(player.ilseomGlitchOutDuration * 0.5f
            + player.ilseomMoveDuration + player.ilseomSweepDuration + 0.25f);

        // 정지 위치는 이제 적과 무관하다 — 경로에 벽이 없으면 항상 최대 사거리까지 간다(사용자 변경).
        // 테스트 지형엔 경로 상 활성 벽이 없으므로 최대 사거리를 기대한다.
        float traveled = player.transform.position.x - start.x;
        float expectedTravel = player.dashSpeed * player.dashDuration * player.ilseomDistanceMultiplier;
        bool stopOk = Mathf.Abs(traveled - expectedTravel) < 0.6f;
        bool layerRestored = player.gameObject.layer == normalLayer && !player.IsInvincible;

        int expectedDmg = Mathf.RoundToInt(player.attack1Damage * player.ilseomDamageMultiplier);
        int hpDelta = enemy != null ? enemyHpBefore - enemy.currentHp : expectedDmg;
        bool damageOk = hpDelta == expectedDmg;

        TestLog.Step(channel, $"phase2_done traveled={traveled:F2} expected={expectedTravel:F2} stopOk={stopOk} " +
            $"layerRestored={layerRestored} hpDelta={hpDelta}/{expectedDmg}");

        if (enemy != null) enemy.moveSpeed = savedEnemySpeed;

        // ── Phase 3: 쿨타임 중엔 차지 자체가 시작되지 않음 ─────────────────────────
        Vector3 cdStart = player.transform.position;
        InputInjector.PressCharge();
        yield return new WaitForSeconds(player.ilseomChargeTime + 0.3f);
        InputInjector.ReleaseCharge();
        yield return new WaitForSeconds(0.4f);

        float movedOnCooldown = Mathf.Abs(player.transform.position.x - cdStart.x);
        bool cooldownOk = movedOnCooldown < 0.5f;
        TestLog.Step(channel, $"phase3_cooldown moved={movedOnCooldown:F2} blocked={cooldownOk}");

        bool pass = cancelOk && invincibleDuring && stopOk && layerRestored && damageOk && cooldownOk;
        TestLog.Assert(channel, pass, $"cancel={cancelOk} invincible={invincibleDuring} stop={stopOk} " +
            $"restore={layerRestored} dmg={damageOk} cooldown={cooldownOk}");

        yield return new WaitForSeconds(0.8f);
        // 녹화하지 않으므로(RunIlseomTest 주석 참고) StopRecording 호출 없음.
    }

    // 처형(Execution) 4단 검증:
    //   Phase 1 — HP가 임계값 위면 호버해도 글로우·프롬프트가 뜨지 않는다(임계값이 실제로 게이트인지)
    //   Phase 2 — HP를 임계값 아래로 내리고 호버하면 붉은 글로우 + "R/처형" 프롬프트가 뜬다(스펙 1)
    //   Phase 3 — 커서를 떼면 둘 다 사라진다(스펙 1의 페이드 아웃)
    //   Phase 4 — 다시 호버하고 R을 누르면 무적 → 적 위치로 이동 → 적 사망, 끝나면 레이어 복구(스펙 2·3)
    public IEnumerator ExecutionTest()
    {
        const string channel = "execution";
        TestLog.Step(channel, "spawned");

        var player = FindAnyObjectByType<PlayerController>();
        var enemy = FindAnyObjectByType<DummyEnemy>();
        if (player == null || enemy == null)
        {
            TestLog.Assert(channel, false, $"NOT_FOUND: player={player != null} dummy={enemy != null}");
            yield break;
        }

        var cam = Camera.main;
        if (cam == null)
        {
            TestLog.Assert(channel, false, "NOT_FOUND: no Camera.main (커서→월드 변환 불가)");
            yield break;
        }

        int normalLayer = player.gameObject.layer;
        int invincibleLayer = LayerMask.NameToLayer(player.invincibleLayerName);
        if (invincibleLayer == -1)
        {
            TestLog.Assert(channel, false, $"NOT_FOUND: layer '{player.invincibleLayerName}' missing");
            yield break;
        }

        // 이 테스트는 적을 죽이고 끝난다(respawnDelay=0이면 비활성화된다) → 같은 Play 세션에서 두 번째
        // 실행하면 이미 죽은 적을 잡는다. 되살릴 수 없으므로 여기서 명확히 끊고 stop→play를 요구한다.
        if (!enemy.IsAlive)
        {
            TestLog.Assert(channel, false, "STALE: 적이 이미 죽어 있음 — stop→play로 새 세션에서 다시 실행할 것");
            yield break;
        }

        // 적을 알려진 자리에 세워 둔다 — 추적하게 두면 커서를 맞출 좌표가 매 프레임 흔들린다.
        // 4유닛은 적의 attackRange(2.4) 밖이라 테스트 중 적이 먼저 찌르지도 않는다. (런타임 전용, 끝나고 원복)
        float savedSpeed = enemy.moveSpeed;
        enemy.moveSpeed = 0f;
        enemy.transform.position = player.transform.position + new Vector3(4f, 0f, 0f);
        yield return new WaitForSeconds(0.6f);
        Vector3 enemyPos = enemy.transform.position;

        var ui = ExecutionUI.GetOrCreate();
        float hoverSettle = player.executionGlowFadeIn + ui.fadeDuration + 0.2f;

        // ── Phase 1: 체력이 충분하면 호버해도 반응이 없어야 한다 ─────────────────────
        enemy.currentHp = enemy.maxHp;
        InputInjector.SetMousePosition(cam.WorldToScreenPoint(enemy.transform.position));
        yield return new WaitForSeconds(hoverSettle);

        bool healthyNoPrompt = !ui.IsVisible && enemy.GetComponentInChildren<EnemyExecutionGlowFx>() == null;
        TestLog.Step(channel, $"phase1_above_threshold hpRatio={enemy.HpRatio:F2} " +
            $"threshold={player.executionHpThreshold:F2} quiet={healthyNoPrompt}");

        // ── Phase 2: 임계값 아래로 내리면 글로우 + 프롬프트 ──────────────────────────
        enemy.currentHp = Mathf.Max(1, Mathf.FloorToInt(enemy.maxHp * player.executionHpThreshold * 0.5f));
        InputInjector.SetMousePosition(cam.WorldToScreenPoint(enemy.transform.position));
        yield return new WaitForSeconds(hoverSettle);

        bool glowOn = enemy.GetComponentInChildren<EnemyExecutionGlowFx>() != null;
        bool promptOn = ui.IsVisible;
        TestLog.Step(channel, $"phase2_hover hpRatio={enemy.HpRatio:F2} glow={glowOn} prompt={promptOn}");

        // ── Phase 3: 커서를 떼면 둘 다 사라진다 ────────────────────────────────────
        InputInjector.SetMousePosition(new Vector2(4f, 4f)); // 적이 없는 화면 구석
        yield return new WaitForSeconds(player.executionGlowFadeOut + ui.fadeDuration + 0.3f);

        bool promptOff = !ui.IsVisible;
        bool glowOff = enemy.GetComponentInChildren<EnemyExecutionGlowFx>() == null;
        TestLog.Step(channel, $"phase3_unhover glow_gone={glowOff} prompt_gone={promptOff}");

        // ── Phase 4: 다시 호버 + R → 무적 → 이동 → 즉사 ────────────────────────────
        InputInjector.SetMousePosition(cam.WorldToScreenPoint(enemy.transform.position));
        yield return new WaitForSeconds(hoverSettle);

        float startY = player.transform.position.y;
        int hpBefore = enemy.currentHp;
        InputInjector.PressExecute();
        yield return null;
        yield return null;
        InputInjector.ReleaseExecute();

        // Glitch Out 재생 중간에 무적을 확인(시퀀스 전체가 무적이어야 한다 — 스펙 2)
        yield return new WaitForSeconds(player.ilseomGlitchOutDuration * 0.5f);
        bool invincibleDuring = player.IsInvincible && player.gameObject.layer == invincibleLayer;
        TestLog.Step(channel, $"phase4_mid invincible={invincibleDuring} layer={player.gameObject.layer}");

        // 시퀀스 종료까지 대기(Glitch Out 남은 구간 + Sweep + 여운 + 히트스톱 여유)
        yield return new WaitForSeconds(player.ilseomGlitchOutDuration + player.ilseomSweepDuration
            + player.executionHold + 0.8f);

        bool killed = enemy.currentHp <= 0 || !enemy.gameObject.activeInHierarchy;
        bool arrived = Mathf.Abs(player.transform.position.x - enemyPos.x) < 0.6f;
        bool yKept = Mathf.Abs(player.transform.position.y - startY) < 0.3f;
        bool restored = player.gameObject.layer == normalLayer && !player.IsInvincible;
        TestLog.Step(channel, $"phase4_done hp={hpBefore}→{enemy.currentHp} killed={killed} " +
            $"arrived={arrived} dx={Mathf.Abs(player.transform.position.x - enemyPos.x):F2} " +
            $"yKept={yKept} restored={restored}");

        bool pass = healthyNoPrompt && glowOn && promptOn && promptOff && glowOff
            && invincibleDuring && killed && arrived && yKept && restored;
        TestLog.Assert(channel, pass,
            $"quiet={healthyNoPrompt} glow={glowOn} prompt={promptOn} unhover={promptOff && glowOff} " +
            $"invincible={invincibleDuring} killed={killed} arrived={arrived} yKept={yKept} restored={restored}");

        enemy.moveSpeed = savedSpeed;
        yield return new WaitForSeconds(0.5f);
        // 일섬/패링 테스트와 같은 이유로 녹화하지 않는다(RunIlseomTest 주석 참고).
    }

    // 처형(Execution)의 카메라 쉐이크 + VFX(글리치 슬라이스 · Hit03 · "처형됨!!" 텍스트 · FocusPulse 줌)를
    // 영상으로 보여주는 showcase. 기능 검증(Tools/PlayTest/Execution)은 커서 주입 + R키 한 프레임 폴링이
    // Recorder의 프레임 스로틀과 어긋나 비결정적으로 실패할 수 있어(일섬/패링과 같은 이유) 녹화하지 않는다 —
    // 이 showcase는 같은 발동 시퀀스를 그대로 타되, 판정 성패와 무관하게 영상 확보만이 목적이라 안전하다.
    public IEnumerator ExecutionVfxShowcase()
    {
        const string channel = "execution";
        TestLog.Step(channel, "showcase_spawned");

        var player = FindAnyObjectByType<PlayerController>();
        var enemy = FindAnyObjectByType<DummyEnemy>();
        if (player == null || enemy == null)
        {
            TestLog.Assert(channel, false, $"NOT_FOUND: player={player != null} dummy={enemy != null}");
            yield break;
        }
        var cam = Camera.main;
        if (cam == null)
        {
            TestLog.Assert(channel, false, "NOT_FOUND: no Camera.main");
            yield break;
        }
        if (!enemy.IsAlive)
        {
            TestLog.Assert(channel, false, "STALE: 적이 이미 죽어 있음 — stop→play로 새 세션에서 다시 실행할 것");
            yield break;
        }

        float savedSpeed = enemy.moveSpeed;
        enemy.moveSpeed = 0f;
        enemy.transform.position = player.transform.position + new Vector3(4f, 0f, 0f);
        enemy.currentHp = Mathf.Max(1, Mathf.FloorToInt(enemy.maxHp * player.executionHpThreshold * 0.5f));

        // 영상 처리 파이프라인이 <1s 클립을 거부하는 사례가 있어(DashIFrameTest 주석 참고) 리드인을 둔다.
        yield return new WaitForSeconds(1.5f);

        InputInjector.SetMousePosition(cam.WorldToScreenPoint(enemy.transform.position));
        // 아웃라인이 얇아(수 px) ~1fps 샘플링 영상 판정기가 놓치기 쉬워 호버 구간을 넉넉히 늘렸다
        // (2026-07-26 아웃라인 판정 FAIL 이후 조정 — 판정기가 최소 여러 프레임 샘플할 시간을 준다).
        yield return new WaitForSeconds(player.executionGlowFadeIn + 2.5f);

        InputInjector.PressExecute();
        yield return null;
        yield return null;
        InputInjector.ReleaseExecute();

        // 시퀀스 전체(Glitch Out + Sweep + 여운) + 카메라 FocusPulse/쉐이크 잔향까지 넉넉히 대기.
        yield return new WaitForSeconds(player.ilseomGlitchOutDuration + player.ilseomSweepDuration
            + player.executionHold + 1f);

        bool killed = enemy.currentHp <= 0 || !enemy.gameObject.activeInHierarchy;
        TestLog.Assert(channel, killed, $"showcase_recorded killed={killed}");

        enemy.moveSpeed = savedSpeed;
        yield return new WaitForSeconds(1f); // 테일

#if UNITY_EDITOR
        string recordingPath = TestRecorder.StopRecording();
        TestLog.Event(channel, $"recording_saved={recordingPath}");
#endif
    }

    // 체력 칸(갯수) + 빛 에너지 게이지 HUD 검증 & 녹화(일정표 1주차 "체력/마나 게이지 UI 스무딩(Lerp)").
    // 검증 대상은 "수치"가 아니라 **화면에 그려지는 칸** — PlayerHudUI가 노출하는 칸 표시값을 읽어
    //   ① 한 대 맞으면 그 칸이 한 프레임 만에 꺼지지 않고(스무딩), ② 결국 꺼져서 켜진 칸 수가 정확히
    //   1 줄고, ③ 에너지 게이지는 연속 게이지로 차오르며, ④ 회복하면 칸이 다시 켜지는지 본다.
    // 수렴 대기를 초가 아니라 **프레임 수**로 하는 이유: 원격/비포커스 에디터는 Play 루프가 심하게
    // 스로틀돼(task.md의 녹화 주석 참고) 같은 초라도 실제 경과 프레임 수가 들쭉날쭉하다.
    public IEnumerator PlayerHudTest()
    {
        const string channel = "player_hud";
        TestLog.Step(channel, "spawned");

        var player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            TestLog.Assert(channel, false, "NOT_FOUND: no PlayerController in scene");
            yield break;
        }

        // HUD는 씬에 배치하지 않고 RuntimeInitializeOnLoadMethod로 자동 생성된다 — 그게 실제로 됐는지 먼저 본다.
        bool autoCreated = PlayerHudUI.Instance != null;
        var hud = PlayerHudUI.GetOrCreate();

        // 더미가 다가와 때리면 HP가 측정 도중 바뀐다 → 테스트 동안만 멈춰 세운다(Play 종료 시 원복).
        var dummies = FindObjectsByType<DummyEnemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var savedSpeeds = new float[dummies.Length];
        for (int i = 0; i < dummies.Length; i++) { savedSpeeds[i] = dummies[i].moveSpeed; dummies[i].moveSpeed = 0f; }

        yield return new WaitForSeconds(1.5f); // 리드인(가득 찬 바를 보여준다)

        // 칸 줄이 실제 최대 체력만큼 만들어졌고 전부 켜져 있는지(수치가 아니라 그려진 칸을 센다).
        bool hudReady = hud.HasPlayer && hud.PipCount == player.maxHealth && hud.LitPipCount == player.maxHealth;
        TestLog.Step(channel, $"ready auto={autoCreated} pips={hud.PipCount}/{player.maxHealth} " +
            $"lit={hud.LitPipCount} energy_display={hud.EnergyDisplayRatio:F2}");

        // ── ① 1칸 피해 → 그 칸이 한 프레임 만에 꺼지지 않는다(스무딩) ──────────
        int lastIndex = player.currentHealth - 1;   // 오른쪽 끝(마지막) 칸이 먼저 꺼진다
        player.TakeDamage(1);
        yield return null;

        float firstFrame = hud.PipDisplay(lastIndex);
        bool smoothed = firstFrame > 0.2f;          // 아직 거의 켜진 채로 남아 있다
        bool countDropped = player.currentHealth == lastIndex;
        TestLog.Step(channel, $"damaged hp={player.currentHealth}/{player.maxHealth} " +
            $"pip[{lastIndex}]_first_frame={firstFrame:F2} smoothed={smoothed}");

        // ── ② 그 칸이 결국 꺼지고, 켜진 칸 수가 정확히 1 줄어든다 ──────────────
        bool converged = false;
        for (int i = 0; i < 600 && !converged; i++)
        {
            if (hud.PipDisplay(lastIndex) < 0.05f) converged = true;
            else yield return null;
        }
        bool litMatches = hud.LitPipCount == player.currentHealth;
        TestLog.Step(channel, $"converged={converged} lit={hud.LitPipCount} hp={player.currentHealth} match={litMatches}");

        // 영상용: 한 칸 더 깎아 칸이 확실히 줄어드는 구간을 만든다(판정기가 ~1fps로 샘플링한다).
        yield return new WaitForSeconds(0.8f);
        player.TakeDamage(1);
        yield return new WaitForSeconds(1.2f);

        // ── ③ 에너지 게이지 ────────────────────────────────────────────────
        int gain = Mathf.Max(1, Mathf.RoundToInt(player.maxEnergy * 0.4f));
        player.AddEnergy(gain);
        yield return null;

        float energyTarget = (float)player.currentEnergy / player.maxEnergy;
        bool energyLag = hud.EnergyDisplayRatio < energyTarget - 0.05f; // 차오르는 중(즉시 점프 아님)
        bool energyConverged = false;
        for (int i = 0; i < 600 && !energyConverged; i++)
        {
            if (Mathf.Abs(hud.EnergyDisplayRatio - energyTarget) < 0.01f) energyConverged = true;
            else yield return null;
        }
        TestLog.Step(channel, $"energy target={energyTarget:F2} lag={energyLag} converged={energyConverged}");

        // ── ④ 회복(처형 성공과 같은 경로) → 꺼졌던 칸이 다시 켜진다 ────────────
        yield return new WaitForSeconds(0.8f);
        int beforeHeal = hud.LitPipCount;
        player.Heal(1);
        bool healed = false;
        for (int i = 0; i < 600 && !healed; i++)
        {
            if (hud.LitPipCount == beforeHeal + 1) healed = true;
            else yield return null;
        }
        TestLog.Step(channel, $"healed lit={beforeHeal}→{hud.LitPipCount} pass={healed}");
        yield return new WaitForSeconds(1.5f); // 테일

        bool pass = hudReady && smoothed && countDropped && converged && litMatches && energyLag && energyConverged && healed;
        TestLog.Assert(channel, pass,
            $"auto={autoCreated} ready={hudReady} smoothed={smoothed} count_dropped={countDropped} " +
            $"converged={converged} lit_matches={litMatches} energy_lag={energyLag} " +
            $"energy_converged={energyConverged} healed={healed}");

        for (int i = 0; i < dummies.Length; i++) dummies[i].moveSpeed = savedSpeeds[i];

#if UNITY_EDITOR
        string recordingPath = TestRecorder.StopRecording();
        TestLog.Event(channel, $"recording_saved={recordingPath}");
#endif
    }

    // 데이터 저장/불러오기 검증 & 녹화(기능_구현_명세서 1장 "데이터 저장/불러오기").
    // 흐름: 저장 → 런타임 값을 일부러 망가뜨림 → 불러오기 → 값이 되돌아오는가.
    // 화면으로도 판단할 수 있게 체력 칸이 "줄었다가 불러오기로 다시 켜지는" 순서로 짰다.
    public IEnumerator GameDataTest()
    {
        const string channel = "game_data";
        TestLog.Step(channel, "spawned");

        var player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            TestLog.Assert(channel, false, "NOT_FOUND: no PlayerController in scene");
            yield break;
        }

        // 더미가 다가와 때리면 측정 도중 HP가 바뀐다 → 테스트 동안만 멈춰 세운다(Play 종료 시 원복).
        var dummies = FindObjectsByType<DummyEnemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var savedSpeeds = new float[dummies.Length];
        for (int i = 0; i < dummies.Length; i++) { savedSpeeds[i] = dummies[i].moveSpeed; dummies[i].moveSpeed = 0f; }

        // 각 상태(칸 5→4→2→4)를 2초 이상 유지한다 — 영상 판정기가 ~1fps로 샘플링해서, 상태가 짧으면
        // 샘플 프레임 사이로 통째로 사라진다(대시 잔상 slowmo 워크어라운드와 같은 이유).
        yield return new WaitForSeconds(2f); // 리드인(칸이 가득 찬 상태를 보여준다)

        // ── ① 저장할 상태를 만든다 ──────────────────────────────────────────
        player.TakeDamage(1);
        player.AddEnergy(-20);
        yield return new WaitForSeconds(2f);

        int savedHp = player.currentHealth;
        int savedEnergy = player.currentEnergy;
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        Vector2 checkpoint = player.transform.position;

        bool saved = GameDataManager.SaveCheckpoint(sceneName, checkpoint);
        bool fileExists = SaveSystem.Exists();
        TestLog.Step(channel, $"saved={saved} file={fileExists} hp={savedHp}/{player.maxHealth} " +
            $"energy={savedEnergy} path={SaveSystem.SavePath}");

        // ── ② 런타임 값을 일부러 망가뜨린다 ────────────────────────────────
        yield return new WaitForSeconds(1f);
        player.TakeDamage(2);
        player.AddEnergy(-30);
        yield return new WaitForSeconds(2.5f); // 칸이 두 개 더 꺼진 상태를 눈으로 확인할 시간

        bool mutated = player.currentHealth != savedHp && player.currentEnergy != savedEnergy;
        TestLog.Step(channel, $"mutated hp={player.currentHealth} energy={player.currentEnergy} pass={mutated}");

        // ── ③ 불러오기 → 값이 되돌아온다 ───────────────────────────────────
        bool loaded = GameDataManager.LoadGame();
        yield return null;

        bool hpRestored = player.currentHealth == savedHp;
        bool energyRestored = player.currentEnergy == savedEnergy;
        var progress = GameDataManager.Current.progress;
        bool checkpointRestored = progress.hasCheckpoint
            && progress.checkpointScene == sceneName
            && Vector2.Distance(progress.checkpointPosition, checkpoint) < 0.01f;
        TestLog.Step(channel, $"loaded={loaded} hp={player.currentHealth}(want {savedHp}) " +
            $"energy={player.currentEnergy}(want {savedEnergy}) checkpoint={checkpointRestored}");

        // ── ④ HUD가 복원된 칸 수를 다시 그린다 ─────────────────────────────
        var hud = PlayerHudUI.GetOrCreate();
        bool hudRestored = false;
        for (int i = 0; i < 600 && !hudRestored; i++)
        {
            if (hud.LitPipCount == savedHp) hudRestored = true;
            else yield return null;
        }
        TestLog.Step(channel, $"hud lit={hud.LitPipCount} want={savedHp} pass={hudRestored}");
        yield return new WaitForSeconds(2.5f); // 테일(복원된 칸 수를 충분히 보여준다)

        bool pass = saved && fileExists && mutated && loaded && hpRestored && energyRestored
                    && checkpointRestored && hudRestored;
        TestLog.Assert(channel, pass,
            $"saved={saved} file={fileExists} mutated={mutated} loaded={loaded} hp_restored={hpRestored} " +
            $"energy_restored={energyRestored} checkpoint={checkpointRestored} hud_restored={hudRestored}");

        for (int i = 0; i < dummies.Length; i++) dummies[i].moveSpeed = savedSpeeds[i];

#if UNITY_EDITOR
        string recordingPath = TestRecorder.StopRecording();
        TestLog.Event(channel, $"recording_saved={recordingPath}");
#endif
    }

    // 산데비스탄 대시 VFX(컬러 에코 + 모션 스트리크 + 히트스톱)를 영상으로 보여주는 showcase.
    // ~1fps로 샘플링하는 영상 판정기가 짧은 잔상을 놓치지 않도록:
    //   - 넓은 하부 바닥 중앙(x≈10, y≈23)에 배치하고 좌우 교대 대시로 **화면 중앙을 벗어나지 않게** 한다(낙하 방지).
    //   - slowMo=true: timeScale=0.05로 늘려 한 번의 대시 잔상 궤적이 실시간 수 초 지속.
    //   - slowMo=false: 기본 속도. 대신 녹화 동안만 잔상 수명을 늘려(대시 속도는 불변) 컬러가 샘플 사이로
    //     사라지지 않게 하고, 여러 번 대시한다.
    // 배치/입력/timeScale/수명 조정은 모두 런타임 전용 → Play 종료 시 원복.
    public IEnumerator ShowcaseRoutine(bool slowMo)
    {
        const string channel = "dash_vfx";
        TestLog.Step(channel, slowMo ? "spawned_slowmo" : "spawned_realtime");

        var player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            TestLog.Assert(channel, false, "NOT_FOUND: no PlayerController in scene");
            yield break;
        }

        // 대시 구간을 화면 정중앙에 확실히 담는다. SectionCamera 세로 섹션 경계가 y≈23.17이라
        // 그냥 두면 바닥(y≈23)에 선 플레이어가 화면 위로 잘려 효과가 아예 녹화되지 않는다(FAIL 원인).
        // → 녹화 동안만 SectionCamera를 끄고 카메라를 대시 중앙에 고정 + 살짝 줌인(캐릭터·컬러 잔상 크게).
        const float dashCenterX = 10f;   // 하부 넓은 바닥 중앙(좌우 교대 대시가 벼랑/벽에 안 닿음)
        const float dashCenterY = 23f;   // 바닥 높이
        var cam = Camera.main;
        var sc = cam != null ? cam.GetComponent<SectionCamera>() : null;
        bool scWasEnabled = sc != null && sc.enabled;
        Vector3 camOrigPos = cam != null ? cam.transform.position : Vector3.zero;
        float camOrigOrtho = cam != null ? cam.orthographicSize : 6f;

        var rb = player.GetComponent<Rigidbody2D>();
        player.transform.position = new Vector3(dashCenterX, dashCenterY + 1f, player.transform.position.z);
        if (rb != null) rb.linearVelocity = Vector2.zero;
        InputInjector.SetMoveX(0f);
        InputInjector.ReleaseDash();

        if (cam != null)
        {
            if (sc != null) sc.enabled = false;
            cam.transform.position = new Vector3(dashCenterX, dashCenterY + 0.8f, camOrigPos.z);
            cam.orthographicSize = 3.5f; // 강하게 줌인 → 캐릭터·컬러 잔상이 압축·저해상 샘플링에도 크게
        }

        yield return new WaitForSeconds(0.4f); // 리드인 + 착지(짧게 — 컬러가 영상을 지배하도록)

        float prevScale = Time.timeScale;
        float prevLife = player.afterImageLifetime;
        float prevCd = player.dashCooldown;
        float prevAlpha = player.afterImageAlpha;
        float prevInterval = player.afterImageInterval;
        bool dashed = false;

        // 두 모드 공통: 컬러 잔상이 영상 대부분을 차지하고 유튜브 압축에도 뭉개지지 않도록 조정
        // (대시 속도·개수는 불변, 녹화 후 원복).
        player.afterImageLifetime = 2.5f;   // 길게 남김 → 대시 후에도 컬러 궤적이 화면에 계속 → 1fps 샘플 적중률↑
        player.afterImageAlpha = 1.0f;      // 진하게
        player.afterImageInterval = 0.01f;  // 촘촘하게 (새 기본값과 동일 → 잔상 개수↑)

        try
        {
            if (slowMo)
            {
                Time.timeScale = 0.12f; // 슬로모(대시를 늘려 프레임당 컬러 확보). 0.05보다 완주가 빠름
                yield return DashOnce(player, +1, channel, "dash_slowmo");
                // 긴 잔상 수명(2.5s)이라 이 대기 내내 컬러 궤적이 화면을 채운다(다 페이드될 때까지 안 기다림)
                yield return new WaitForSeconds(1.2f);
                dashed = true;
            }
            else
            {
                // 기본 속도: 1fps 판정기가 컬러를 놓치지 않게 쿨다운을 줄여 좌우 교대로 여러 번 대시.
                player.dashCooldown = 0.25f;
                for (int i = 0; i < 5; i++)
                {
                    int dir = (i % 2 == 0) ? +1 : -1; // 좌우 교대 → 중앙 유지
                    yield return DashOnce(player, dir, channel, $"dash_realtime_{i}");
                    yield return new WaitForSeconds(player.dashCooldown + 0.05f); // 쿨다운 해제 대기
                }
                dashed = true;
            }
        }
        finally
        {
            Time.timeScale = prevScale;
            player.afterImageLifetime = prevLife;
            player.afterImageAlpha = prevAlpha;
            player.afterImageInterval = prevInterval;
            player.dashCooldown = prevCd;
            InputInjector.SetMoveX(0f);
        }

        yield return new WaitForSeconds(0.5f); // 테일(짧게)

        TestLog.Assert(channel, dashed, $"showcase_recorded slowMo={slowMo} dashed={dashed}");

#if UNITY_EDITOR
        string recordingPath = TestRecorder.StopRecording();
        TestLog.Event(channel, $"recording_saved={recordingPath}");
#endif

        // 카메라 원복(녹화가 끝난 뒤 → 프레이밍이 tail까지 유지된다). SectionCamera 재활성 시 다음 프레임에 재정착.
        if (cam != null)
        {
            if (sc != null) sc.enabled = scWasEnabled;
            cam.transform.position = camOrigPos;
            cam.orthographicSize = camOrigOrtho;
        }
    }

    // 더미 몬스터 AI(추적→사거리 진입시 정지+창찌르기→플레이어 피격, 공격중 피격시 리셋)와
    // 플레이어 1-2타 콤보(Slash1/Slash2) 판정+데미지를 한 녹화로 순서대로 검증한다.
    public IEnumerator DummyEnemyCombatTest()
    {
        const string dummyChannel = "dummy_attack";
        const string playerChannel = "player_attack";
        TestLog.Step(dummyChannel, "spawned");

        var player = FindAnyObjectByType<PlayerController>();
        var dummy = FindAnyObjectByType<DummyEnemy>();
        if (player == null || dummy == null)
        {
            TestLog.Assert(dummyChannel, false, $"NOT_FOUND: player={player != null} dummy={dummy != null}");
            yield break;
        }

        var playerRb = player.GetComponent<Rigidbody2D>();
        var dummyRb = dummy.GetComponent<Rigidbody2D>();
        var stateField = typeof(DummyEnemy).GetField("state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // 이 좌표(y=29.5)는 원래 캐슬맵(TiledMap_Exterior) 기준이었으나, 같은 날 세션에서 캐슬맵을
        // 비활성화하고 TestFlatMap(지면 y=0, groundLayer, execute_code 레이캐스트로 실측 확인)으로
        // 교체됨 - 옛 y값 그대로 두면 플레이어/dummy가 허공에서 낙하하며 테스트가 돌아 stage2 판정
        // 타이밍에 세로 오프셋이 벌어져 히트가 빗나감(checked 2026-07-23, execute_code로 낙하 확인).
        Vector3 basePos = new Vector3(1.61f, 0.05f, 0f);
        player.transform.position = basePos;
        if (playerRb != null) playerRb.linearVelocity = Vector2.zero;
        // 추적 구간이 보이도록 사거리보다 확실히 멀리서 시작한다(attackRange가 바뀌어도 따라가게 유도값 사용).
        dummy.transform.position = new Vector3(basePos.x + dummy.attackRange + 1.2f, 0.15f, 0f);
        if (dummyRb != null) dummyRb.linearVelocity = Vector2.zero;
        InputInjector.SetMoveX(0f);
        InputInjector.ReleaseAttack();

        // 배치 직후(같은 프레임, yield 전)에 재야 chase가 baseline을 갉아먹지 않는다 - 조금이라도
        // WaitForSeconds를 먼저 걸면 그새 dummy가 사거리까지 다가가 버려 시작 거리가 이미 줄어든 값으로
        // 측정됨(checked 2026-07-23, 실측으로 확인: 0.2s만 기다려도 2.8->2.20으로 이미 감소).
        float startDist = Mathf.Abs(dummy.transform.position.x - player.transform.position.x);
        TestLog.Step(dummyChannel, $"start_dist={startDist:F2}");

        yield return new WaitForSeconds(0.2f); // 착지 안정화(측정에는 영향 없음, startDist는 이미 잼)

        // 1) 추적: 사거리(attackRange) 진입으로 멈추기 전 구간에서 거리가 줄어드는지 확인.
        yield return new WaitForSeconds(0.4f);
        float midDist = Mathf.Abs(dummy.transform.position.x - player.transform.position.x);
        bool chased = midDist < startDist - 0.5f;
        TestLog.Step(dummyChannel, $"mid_dist={midDist:F2} chased={chased}");

        // 2) 사거리 진입 후 창 찌르기 공격이 완료되어 플레이어가 맞는지 대기
        int startHp = player.currentHealth;
        float waitBudget = 4f;
        float t = 0f;
        while (t < waitBudget && player.currentHealth >= startHp)
        {
            t += Time.deltaTime;
            yield return null;
        }
        bool playerHit = player.currentHealth < startHp;
        TestLog.Step(dummyChannel, $"player_hp={player.currentHealth} playerHit={playerHit}");

        // 3) 공격 중 피격 리셋: 다음 windup 진입을 기다렸다가 그 순간 데미지를 줘서 확인
        float waitReset = 3f;
        float tr = 0f;
        bool enteredWindup = false;
        while (tr < waitReset)
        {
            if (stateField.GetValue(dummy).ToString() == "Windup") { enteredWindup = true; break; }
            tr += Time.deltaTime;
            yield return null;
        }

        bool resetWorks = false;
        if (enteredWindup)
        {
            int hpBeforeReset = dummy.currentHp;
            dummy.TakeDamage(5);
            yield return null;
            bool spearReset = Vector2.Distance(dummy.spear.localPosition, dummy.spearIdleLocalPos) < 0.01f;
            bool isHitstun = stateField.GetValue(dummy).ToString() == "Hitstun";
            resetWorks = spearReset && isHitstun && dummy.currentHp == hpBeforeReset - 5;
            TestLog.Step(dummyChannel, $"reset_check spearReset={spearReset} isHitstun={isHitstun} hp={dummy.currentHp}");
        }

        bool dummyPass = chased && playerHit && enteredWindup && resetWorks;
        TestLog.Assert(dummyChannel, dummyPass,
            $"chased={chased} playerHit={playerHit} enteredWindup={enteredWindup} resetWorks={resetWorks}");

        yield return new WaitForSeconds(dummy.hitstunDuration + 0.2f); // hitstun 해제 대기

        // 4) 플레이어 1-2타 콤보로 dummy에게 데미지 (판정을 위해 근접 배치 + 오른쪽을 보게 정렬)
        player.transform.position = basePos;
        if (playerRb != null) playerRb.linearVelocity = Vector2.zero;
        dummy.transform.position = new Vector3(basePos.x + player.attackHitboxDistance + 0.3f, basePos.y + 0.4f, 0f);
        if (dummyRb != null) dummyRb.linearVelocity = Vector2.zero;
        InputInjector.SetMoveX(1f); // 오른쪽을 보도록(flipX=false) 한 프레임 입력 후 정지
        yield return null;
        InputInjector.SetMoveX(0f);
        if (playerRb != null) playerRb.linearVelocity = Vector2.zero;

        // 판정이 Animation Event(AttackHitFrame) 기반으로 바뀌어 프레임 스킵에는 안전해졌지만,
        // 원격·비포커스 에디터의 큰 프레임 스로틀 자체는 여전히 존재(checked 2026-07-23) →
        // 이 구간만 timeScale을 낮춰 테스트 타이밍 해상도를 넉넉히 확보한다.
        float prevScale = Time.timeScale;
        int dummyHpBeforeCombo = dummy.currentHp;
        int dummyHpAfterCombo;
        try
        {
            Time.timeScale = 0.2f;

            InputInjector.PressAttack();
            yield return null;
            InputInjector.ReleaseAttack();
            TestLog.Step(playerChannel, "attack1_pressed");

            yield return new WaitForSeconds(player.attack1Duration * 0.6f); // 스윙 도중 눌러도 버퍼링돼 종료 즉시 2타로 이어지는지 검증

            InputInjector.PressAttack();
            yield return null;
            InputInjector.ReleaseAttack();
            TestLog.Step(playerChannel, "attack2_pressed");

            yield return new WaitForSeconds(player.attack2Duration + 0.15f);

            dummyHpAfterCombo = dummy.currentHp;
        }
        finally
        {
            Time.timeScale = prevScale;
        }

        bool bothStagesLanded = dummyHpAfterCombo <= dummyHpBeforeCombo - (player.attack1Damage + player.attack2Damage);
        TestLog.Assert(playerChannel, bothStagesLanded,
            $"hpBefore={dummyHpBeforeCombo} hpAfter={dummyHpAfterCombo} dmg1={player.attack1Damage} dmg2={player.attack2Damage}");

        yield return new WaitForSeconds(1.2f); // 테일

#if UNITY_EDITOR
        string recordingPath = TestRecorder.StopRecording();
        TestLog.Event(dummyChannel, $"recording_saved={recordingPath}");
#endif
    }

    // 대시 잔상(에코)의 형태가 플레이어 스프라이트와 일치하는지 검증 + 영상 기록.
    // (afterImageStretch 비균일 스케일 제거 후 회귀 확인용 - checked 2026-07-22)
    // 카메라 배치는 ShowcaseRoutine과 동일한 패턴(SectionCamera 임시 비활성 + 대시 중앙 줌인)을 재사용.
    public IEnumerator DashAfterimageShapeTest()
    {
        const string channel = "dash_afterimage_shape";
        TestLog.Step(channel, "spawned");

        var player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            TestLog.Assert(channel, false, "NOT_FOUND: no PlayerController in scene");
            yield break;
        }
        var sr = player.GetComponent<SpriteRenderer>();

        const float dashCenterX = 10f;
        const float dashCenterY = 23f;
        var cam = Camera.main;
        var sc = cam != null ? cam.GetComponent<SectionCamera>() : null;
        bool scWasEnabled = sc != null && sc.enabled;
        Vector3 camOrigPos = cam != null ? cam.transform.position : Vector3.zero;
        float camOrigOrtho = cam != null ? cam.orthographicSize : 6f;

        var rb = player.GetComponent<Rigidbody2D>();
        player.transform.position = new Vector3(dashCenterX, dashCenterY + 1f, player.transform.position.z);
        if (rb != null) rb.linearVelocity = Vector2.zero;
        InputInjector.SetMoveX(0f);
        InputInjector.ReleaseDash();

        if (cam != null)
        {
            if (sc != null) sc.enabled = false;
            cam.transform.position = new Vector3(dashCenterX, dashCenterY + 0.8f, camOrigPos.z);
            cam.orthographicSize = 3f; // 캐릭터·잔상 실루엣이 크게 보이도록 줌인
        }

        yield return new WaitForSeconds(0.3f); // 리드인

        float prevScale = Time.timeScale;
        float prevLife = player.afterImageLifetime;
        // 영상 판정기(~1fps 샘플링)가 짧은 잔상 구간을 놓치지 않도록 녹화 동안만 잔상 수명을 늘림
        // (대시 자체의 속도/개수는 불변, 녹화 후 원복 - dash_vfx 채널의 ShowcaseRoutine과 동일 패턴).
        player.afterImageLifetime = 2.0f;
        Time.timeScale = 0.12f; // 실시간 0.18s 대시를 늘려 실루엣이 눈에 보이게(과도한 스킵 없음)

        bool shapeMatch = true;
        int checkedGhosts = 0;
        string detail = "";

        try
        {
            InputInjector.SetMoveX(1);
            yield return null;
            InputInjector.PressDash();
            yield return null;
            InputInjector.ReleaseDash();
            TestLog.Step(channel, "dash_triggered");

            yield return new WaitForSeconds(player.dashDuration * 0.5f); // 대시 한복판

            // "DashAfterImage" 이름의 오브젝트만 실루엣 복제 에코 - "DashBurst"(원형 임팩트 플래시)는
            // 애초에 캐릭터 모양이 아니므로 형태 비교 대상에서 제외.
            var ghosts = FindObjectsByType<DashAfterImage>(FindObjectsSortMode.None);
            float aspectPlayer = sr.bounds.extents.x / Mathf.Max(0.0001f, sr.bounds.extents.y);
            foreach (var g in ghosts)
            {
                if (g.gameObject.name != "DashAfterImage") continue;
                var gr = g.GetComponent<SpriteRenderer>();
                if (gr == null || gr.sprite == null) continue;
                checkedGhosts++;
                float aspectGhost = gr.bounds.extents.x / Mathf.Max(0.0001f, gr.bounds.extents.y);
                if (Mathf.Abs(aspectPlayer - aspectGhost) > 0.01f)
                {
                    shapeMatch = false;
                    detail = $"aspectPlayer={aspectPlayer:F3} aspectGhost={aspectGhost:F3}";
                }
            }

            yield return new WaitForSeconds(player.dashDuration * 0.5f + 0.05f); // 대시 잔여
            InputInjector.SetMoveX(0f);
            Time.timeScale = prevScale;

            // 두 번째(반대 방향) 대시 - 영상 길이/에코 노출 시간을 늘려 판정기 샘플 적중률을 높임.
            yield return new WaitForSeconds(player.dashCooldown + 0.05f);
            InputInjector.SetMoveX(-1);
            yield return null;
            InputInjector.PressDash();
            yield return null;
            InputInjector.ReleaseDash();
            TestLog.Step(channel, "dash_triggered_2");
            yield return new WaitForSeconds(player.dashDuration + 0.05f);
            InputInjector.SetMoveX(0f);
        }
        finally
        {
            Time.timeScale = prevScale;
            player.afterImageLifetime = prevLife;
            InputInjector.SetMoveX(0f);
        }

        TestLog.Assert(channel, shapeMatch && checkedGhosts > 0,
            $"shape_match={shapeMatch} checkedGhosts={checkedGhosts} {detail}");

        yield return new WaitForSeconds(1.8f); // 테일(잔상들이 화면에 오래 남아 판정기가 놓치지 않게)

#if UNITY_EDITOR
        string recordingPath = TestRecorder.StopRecording();
        TestLog.Event(channel, $"recording_saved={recordingPath}");
#endif

        if (cam != null)
        {
            if (sc != null) sc.enabled = scWasEnabled;
            cam.transform.position = camOrigPos;
            cam.orthographicSize = camOrigOrtho;
        }
    }

    // 부서지는 바닥(2주차 함정 1): 밟으면 crumbleDelay 동안 버티다 무너져 플레이어가 통과해 떨어지고,
    // respawnDelay 후 복구되는지 검증한다.
    public IEnumerator TrapCrumbleTest()
    {
        const string channel = CrumblingPlatform.Channel;
        TestLog.Step(channel, "spawned");

        var player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            TestLog.Assert(channel, false, "NOT_FOUND: no PlayerController in scene");
            yield break;
        }
        var rb = player.GetComponent<Rigidbody2D>();

        var platform = BuildTestPlatform(new Vector3(player.transform.position.x, 3f, 0f));
        platform.respawnDelay = 1.5f; // 녹화 길이를 짧게(원격 에디터는 게임시간 1초당 실시간 수 분)

        // 녹화 프레이밍: 발판(y=3)과 착지 지면(y=0)이 모두 들어오게 줌인한다 — 기본 ortho 6에서는
        // 발판이 화면 폭의 14%밖에 안 돼 저해상·성긴 샘플링 판정에서 놓치기 쉽다.
        var cam = Camera.main;
        var sc = cam != null ? cam.GetComponent<SectionCamera>() : null;
        bool scWasEnabled = sc != null && sc.enabled;
        Vector3 camOrigPos = cam != null ? cam.transform.position : Vector3.zero;
        float camOrigOrtho = cam != null ? cam.orthographicSize : 6f;
        if (cam != null)
        {
            if (sc != null) sc.enabled = false;
            cam.transform.position = new Vector3(platform.transform.position.x, 2f, camOrigPos.z);
            cam.orthographicSize = 4f;
        }

        float platformTop = platform.transform.position.y + 0.25f;
        player.transform.position = new Vector3(platform.transform.position.x, platformTop + 1.2f, 0f);
        if (rb != null) rb.linearVelocity = Vector2.zero;
        InputInjector.SetMoveX(0f);

        // 1) 착지 → 밟힘 감지(CrumbleRoutine 시작)
        float t = 0f;
        while (t < 2f && !platform.IsCrumbling)
        {
            t += Time.deltaTime;
            yield return null;
        }
        bool stepped = platform.IsCrumbling;
        float yOnPlatform = player.transform.position.y;
        TestLog.Step(channel, $"stepped={stepped} y={yOnPlatform:F2}");

        // 2) 경고 구간: 아직은 버텨야 한다(즉시 사라지면 함정이 아니라 그냥 구멍)
        yield return new WaitForSeconds(platform.crumbleDelay * 0.5f);
        bool heldDuringWarning = !platform.IsBroken && player.transform.position.y > platform.transform.position.y;
        TestLog.Step(channel, $"warning held={heldDuringWarning} broken={platform.IsBroken}");

        // 3) 무너짐: 콜라이더가 꺼지고 파편이 튄다
        float tb = 0f;
        while (tb < 1.5f && !platform.IsBroken)
        {
            tb += Time.deltaTime;
            yield return null;
        }
        var col = platform.GetComponent<Collider2D>();
        bool broke = platform.IsBroken;
        bool colliderOff = col != null && !col.enabled;
        int debris = FindObjectsByType<CrumbleDebris>(FindObjectsSortMode.None).Length;
        TestLog.Step(channel, $"broke={broke} colliderOff={colliderOff} debris={debris}");

        // 4) 플레이어가 통과해 낙하
        yield return new WaitForSeconds(0.7f);
        bool fell = player.transform.position.y < yOnPlatform - 1f;
        TestLog.Step(channel, $"fell={fell} y={player.transform.position.y:F2}");

        // 5) 복구
        float tr = 0f;
        while (tr < platform.respawnDelay + 1f && platform.IsBroken)
        {
            tr += Time.deltaTime;
            yield return null;
        }
        bool respawned = !platform.IsBroken && col != null && col.enabled;

        bool pass = stepped && heldDuringWarning && broke && colliderOff && debris > 0 && fell && respawned;
        TestLog.Assert(channel, pass,
            $"stepped={stepped} heldDuringWarning={heldDuringWarning} broke={broke} colliderOff={colliderOff} " +
            $"debris={debris} fell={fell} respawned={respawned}");

        yield return new WaitForSeconds(0.8f); // 테일(복구된 발판이 화면에 남게)

#if UNITY_EDITOR
        string recordingPath = TestRecorder.StopRecording();
        TestLog.Event(channel, $"recording_saved={recordingPath}");
#endif

        if (cam != null)
        {
            if (sc != null) sc.enabled = scWasEnabled;
            cam.transform.position = camOrigPos;
            cam.orthographicSize = camOrigOrtho;
        }
        Destroy(platform.gameObject);
    }

    // 씬에는 아직 함정 오브젝트가 배치돼 있지 않으므로(레벨 조립은 2주차 뒷부분 작업) 검증용 발판을
    // 런타임에 만든다 — 씬/에셋을 건드리지 않고 Play 종료와 함께 사라진다.
    static CrumblingPlatform BuildTestPlatform(Vector3 pos)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        var go = new GameObject("TestCrumblingPlatform");
        go.transform.position = pos;
        go.layer = LayerMask.NameToLayer("Ground");
        var box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(3f, 0.5f);

        // 스프라이트를 자식에 두는 건 의도 — 경고 흔들림이 콜라이더째로 움직여 위에 선 플레이어를
        // 밀어내는 걸 막는다(CrumblingPlatform.warningShake 주석 참고).
        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = new Vector3(3f, 0.5f, 1f);
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(0.55f, 0.45f, 0.35f, 1f);
        sr.sortingOrder = 5; // 플레이어(10)보다 뒤

        return go.AddComponent<CrumblingPlatform>();
    }

    // 압착기(2주차 함정 2): 주기적으로 내려찍고 복귀하는지, 아래에 깔린 플레이어가 피해+넉백을 받는지,
    // 비켜서 있으면 안전한지 검증한다.
    public IEnumerator TrapPressTest()
    {
        const string channel = PressTrap.Channel;
        TestLog.Step(channel, "spawned");

        var player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            TestLog.Assert(channel, false, "NOT_FOUND: no PlayerController in scene");
            yield break;
        }
        var prb = player.GetComponent<Rigidbody2D>();
        var pcol = player.GetComponent<Collider2D>();
        InputInjector.SetMoveX(0f);

        // 씬마다 지형 높이가 달라 좌표를 박아둘 수 없다 — 발밑 지면을 찾아 그 위에 리그를 세운다.
        int groundMask = 1 << LayerMask.NameToLayer("Ground");
        var ground = Physics2D.Raycast((Vector2)player.transform.position + Vector2.up * 0.5f,
                                       Vector2.down, 60f, groundMask);
        float groundY = ground.collider != null ? ground.point.y : player.transform.position.y;
        float headHeight = pcol != null
            ? Mathf.Max(0.6f, pcol.bounds.max.y - player.transform.position.y)
            : 1.2f;
        TestLog.Step(channel, $"ground={(ground.collider != null ? ground.collider.name : "none")} " +
                              $"y={groundY:F2} head={headHeight:F2}");

        float pressX = player.transform.position.x;
        // 바닥까지 완전히 눌러버리면 플레이어가 지형에 짓이겨져 물리가 요동친다 — 머리 높이 안쪽까지만 내린다.
        float bottomFaceY = groundY + headHeight * 0.6f;
        const float halfHeight = 0.5f;
        const float travel = 3.2f;
        var press = BuildTestPress(new Vector3(pressX, bottomFaceY + halfHeight + travel, 0f), travel);

        // 녹화 프레이밍: 프레스의 위/아래 끝이 모두 들어오게 줌인(TrapCrumbleTest와 같은 이유).
        var cam = Camera.main;
        var sc = cam != null ? cam.GetComponent<SectionCamera>() : null;
        bool scWasEnabled = sc != null && sc.enabled;
        Vector3 camOrigPos = cam != null ? cam.transform.position : Vector3.zero;
        float camOrigOrtho = cam != null ? cam.orthographicSize : 6f;
        if (cam != null)
        {
            if (sc != null) sc.enabled = false;
            cam.transform.position = new Vector3(pressX, groundY + 2.5f, camOrigPos.z);
            cam.orthographicSize = 4.5f;
        }

        // 프레스 바로 아래에 세운다.
        player.transform.position = new Vector3(pressX, groundY + 0.15f, player.transform.position.z);
        if (prb != null) prb.linearVelocity = Vector2.zero;
        int hpBefore = player.currentHealth;
        yield return new WaitForSeconds(0.5f); // 리드인(프레스가 위에 떠 있는 그림)

        // 1) 내려찍기 → 깔린 플레이어 피격
        float t = 0f;
        while (t < 4f && press.HitCount == 0)
        {
            t += Time.deltaTime;
            yield return null;
        }
        bool crushed = press.HitCount > 0;
        int hpAfterHit = player.currentHealth;
        bool damaged = hpAfterHit == hpBefore - press.damageCount;
        TestLog.Step(channel, $"crushed={crushed} hp {hpBefore}->{hpAfterHit}");

        // 2) 옆으로 밀려나는가(넉백)
        yield return new WaitForSeconds(0.35f);
        float knockDist = Mathf.Abs(player.transform.position.x - pressX);
        bool knocked = knockDist > 0.8f;
        TestLog.Step(channel, $"knocked={knocked} dist={knockDist:F2}");

        // 3) 바닥을 찍고 다시 위로 복귀
        float tb = 0f;
        while (tb < 6f && press.CurrentPhase != PressTrap.Phase.WaitTop)
        {
            tb += Time.deltaTime;
            yield return null;
        }
        bool returned = Mathf.Abs(press.transform.position.y - press.TopY) < 0.05f;
        TestLog.Step(channel, $"returned={returned} y={press.transform.position.y:F2} top={press.TopY:F2}");

        // 4) 비켜서 있으면 안전한가 + 왕복이 반복되는가(주기성)
        player.transform.position = new Vector3(pressX + 3.5f, groundY + 0.15f, player.transform.position.z);
        if (prb != null) prb.linearVelocity = Vector2.zero;
        int hitsBeforeCycle2 = press.HitCount;
        int hpBeforeCycle2 = player.currentHealth;
        float tc = 0f;
        while (tc < 6f && press.CurrentPhase != PressTrap.Phase.HoldBottom)
        {
            tc += Time.deltaTime;
            yield return null;
        }
        bool cycled = press.CurrentPhase == PressTrap.Phase.HoldBottom; // 두 번째 내려찍기 도달
        bool safeAside = press.HitCount == hitsBeforeCycle2 && player.currentHealth == hpBeforeCycle2;
        TestLog.Step(channel, $"cycled={cycled} safeAside={safeAside} hits={press.HitCount}");

        bool pass = crushed && damaged && knocked && returned && cycled && safeAside;
        TestLog.Assert(channel, pass,
            $"crushed={crushed} damaged={damaged}(hp {hpBefore}->{hpAfterHit}) knocked={knocked}({knockDist:F2}) " +
            $"returned={returned} cycled={cycled} safeAside={safeAside}");

        yield return new WaitForSeconds(0.5f); // 테일

#if UNITY_EDITOR
        string recordingPath = TestRecorder.StopRecording();
        TestLog.Event(channel, $"recording_saved={recordingPath}");
#endif

        if (cam != null)
        {
            if (sc != null) sc.enabled = scWasEnabled;
            cam.transform.position = camOrigPos;
            cam.orthographicSize = camOrigOrtho;
        }
        Destroy(press.gameObject);
    }

    // 부서지는 바닥과 같은 이유로 검증용 프레스도 런타임에 만든다(씬/에셋 미변경, Play 종료와 함께 사라짐).
    static PressTrap BuildTestPress(Vector3 pos, float travel)
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        var go = new GameObject("TestPressTrap");
        go.SetActive(false); // PressTrap.Awake가 travelDistance를 읽으므로 값을 넣고 나서 켠다
        go.transform.position = pos;
        go.layer = LayerMask.NameToLayer("Ground");
        var box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(3f, 1f);

        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = new Vector3(3f, 1f, 1f);
        var sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(0.45f, 0.12f, 0.12f, 1f);
        sr.sortingOrder = 5; // 플레이어(10)보다 뒤

        var press = go.AddComponent<PressTrap>();
        press.travelDistance = travel;
        // 실전용 기본값(0.15s 내려찍기)은 ~1fps로 샘플링하는 영상 판정기에선 프레임 사이로 사라진다 —
        // 녹화용으로만 한 사이클을 느리게 늘린다.
        press.waitTop = 0.8f;
        press.slamSpeed = 10f;
        press.holdBottom = 0.8f;
        press.riseSpeed = 3f;
        go.SetActive(true);
        return press;
    }

    // 폭주(Rampage) — 자원(발동 조건 · 드레인 · 자동 종료)과 전투 수치(피해 배율 · 넉백 면역 ·
    // 피격 시 에너지 손실 · 이동 감속)를 한 번에 검증한다. 붉은 연출은 이번 범위 밖이라 화면에서
    // 확인 가능한 변화는 "에너지 게이지가 줄어드는 것"이다(영상 판정 기준도 그것).
    public IEnumerator RampageTest()
    {
        const string channel = "rampage";
        TestLog.Step(channel, "spawned");

        var player = FindAnyObjectByType<PlayerController>();
        var dummy = FindAnyObjectByType<DummyEnemy>();
        if (player == null || dummy == null)
        {
            TestLog.Assert(channel, false, $"NOT_FOUND: player={player != null} dummy={dummy != null}");
            yield break;
        }

        var rb = player.GetComponent<Rigidbody2D>();
        Vector3 basePos = new Vector3(1.61f, 0.05f, 0f);
        Vector3 nearPos = new Vector3(basePos.x + player.attackHitboxDistance + 0.3f, basePos.y + 0.4f, 0f);

        float prevCrit = player.critChance;
        float prevDummySpeed = dummy.moveSpeed;
        int prevDummyDamage = dummy.playerDamageCount;

        bool notRampagingWithEnergy = false, activated = false, sustained = false, slowed = false;
        bool damageAmplified = false, knockbackIgnored = false, knockbackWorksOff = false;
        bool endedOnRestore = false, reEntered = false;
        int baseHit = 0, rampageHit = 0;

        try
        {
            // 런타임 전용 세팅(테스트가 끝나면 원복, Stop해도 사라짐)
            player.critChance = 0f;          // 크리티컬 난수가 섞이면 "폭주 배율"만 따로 볼 수 없다
            dummy.moveSpeed = 0f;            // 추적하게 두면 거리가 매번 달라진다(스킬 규칙)
            dummy.playerDamageCount = 0;     // 적의 우발적 타격이 에너지/체력 측정을 오염시키지 않게

            player.transform.position = basePos;
            rb.linearVelocity = Vector2.zero;
            dummy.transform.position = new Vector3(basePos.x + 8f, basePos.y + 0.4f, 0f); // 자원 구간엔 멀리
            dummy.currentHp = dummy.maxHp;
            InputInjector.SetMoveX(1f); // 오른쪽을 보게(flipX=false)
            yield return null;
            InputInjector.SetMoveX(0f);
            yield return new WaitForSeconds(0.2f);

            // ① 빛이 남아 있으면 폭주가 아니다(발동 조건이 곧 상태다)
            player.currentEnergy = player.maxEnergy;
            yield return null;
            yield return null;
            notRampagingWithEnergy = !player.IsRampaging;
            TestLog.Step(channel, $"has_energy energy={player.currentEnergy} rampaging={player.IsRampaging}");

            // ② 광원이 0이 되면 입력 없이 자동으로 폭주에 들어간다
            player.currentEnergy = 0;
            yield return null;
            yield return null;
            activated = player.IsRampaging;
            TestLog.Step(channel, $"auto_entered={activated} energy={player.currentEnergy}");

            // ③ 폭주가 유지되는 동안 이동 감속 (오른쪽 0.4s → 왼쪽 0.5s로 제자리 왕복)
            InputInjector.SetMoveX(1f);
            yield return new WaitForSeconds(0.4f);
            float rampageSpeed = Mathf.Abs(rb.linearVelocity.x);
            yield return new WaitForSeconds(0.1f);
            InputInjector.SetMoveX(-1f);
            yield return new WaitForSeconds(0.5f);
            InputInjector.SetMoveX(0f);
            rb.linearVelocity = Vector2.zero;

            // 광원이 0인 동안엔 계속 폭주다(예전의 "초당 드레인 → 0에서 자동 종료"는 없어졌다)
            sustained = player.IsRampaging && player.currentEnergy == 0;
            float expectedSpeed = player.moveSpeed * player.rampageMoveSpeedMultiplier;
            slowed = rampageSpeed > 0.1f && rampageSpeed <= expectedSpeed + 0.05f;
            TestLog.Step(channel, $"sustained={sustained} energy={player.currentEnergy} speed={rampageSpeed:F2}/{expectedSpeed:F2}");

            // ④ 넉백 면역(슈퍼아머) — 폭주 중엔 함정/외력에 밀리지 않는다
            rb.linearVelocity = Vector2.zero;
            player.ApplyKnockback(new Vector2(8f, 4f), 0.2f);
            knockbackIgnored = Mathf.Abs(rb.linearVelocity.x) < 0.5f;
            TestLog.Step(channel, $"knockback_on_rampage vel={rb.linearVelocity.x:F2} ignored={knockbackIgnored}");

            // ⑤ 피해 배율 — 폭주 중 1타로 실제 깎인 HP를 잰다.
            //    ⚠️ 타격은 광원을 +3 준다(C-1). 그래서 스윙 직전에 다시 0으로 맞춰 폭주 상태를 확정한다.
            yield return PlaceForMelee(player, rb, dummy, basePos, nearPos);
            player.currentEnergy = 0;
            yield return null;
            bool rampagingAtSwing = player.IsRampaging;
            int hpBefore = dummy.currentHp;
            yield return AttackOnce(player);
            rampageHit = hpBefore - dummy.currentHp;

            // ⑥ 빛을 되찾으면 자동 해제 + 같은 공격의 기준값(baseHit)
            player.currentEnergy = player.maxEnergy;
            yield return null;
            yield return null;
            endedOnRestore = !player.IsRampaging;
            yield return PlaceForMelee(player, rb, dummy, basePos, nearPos);
            player.currentEnergy = player.maxEnergy; // 이동 중 사고로 0이 되어 다시 폭주하지 않게
            hpBefore = dummy.currentHp;
            yield return AttackOnce(player);
            baseHit = hpBefore - dummy.currentHp;
            damageAmplified = rampagingAtSwing && endedOnRestore && baseHit > 0
                && rampageHit == Mathf.Max(1, Mathf.RoundToInt(baseHit * player.rampageDamageMultiplier));
            TestLog.Step(channel, $"damage rampage={rampageHit} normal={baseHit} x{player.rampageDamageMultiplier:F1} atSwing={rampagingAtSwing} ended={endedOnRestore}");

            // ⑧ 폭주가 꺼진 상태에서 넉백은 정상 동작해야 한다 — ④가 "그냥 안 밀린 것"이 아님을 보장(거짓 통과 방지)
            rb.linearVelocity = Vector2.zero;
            player.ApplyKnockback(new Vector2(8f, 4f), 0.2f);
            knockbackWorksOff = rb.linearVelocity.x > 4f;
            TestLog.Step(channel, $"knockback_off_rampage vel={rb.linearVelocity.x:F2} works={knockbackWorksOff}");
            rb.linearVelocity = Vector2.zero;

            // ⑨ 광원이 다시 0이 되면 몇 번이든 자동으로 재진입한다(1회성이 아님)
            player.currentEnergy = 0;
            float budget = 1f;
            float t = 0f;
            while (t < budget && !player.IsRampaging)
            {
                t += Time.deltaTime;
                yield return null;
            }
            reEntered = player.IsRampaging;
            TestLog.Step(channel, $"re_entered={reEntered} after={t:F2}s energy={player.currentEnergy}");
        }
        finally
        {
            player.critChance = prevCrit;
            dummy.moveSpeed = prevDummySpeed;
            dummy.playerDamageCount = prevDummyDamage;
            InputInjector.SetMoveX(0f);
            // 가상 장치를 반드시 떼고 끝낸다 — 남으면 Keyboard.current가 그쪽을 가리켜 실제 키보드
            // 직접 폴링(R/E/F)이 죽는다(2026-08-01에 실제로 발생시킨 사고).
            InputInjector.Cleanup();
        }

        bool pass = notRampagingWithEnergy && activated && sustained && slowed && knockbackIgnored
            && damageAmplified && knockbackWorksOff && endedOnRestore && reEntered;
        TestLog.Assert(channel, pass,
            $"hasEnergy={notRampagingWithEnergy} autoEnter={activated} sustained={sustained} slowed={slowed} "
            + $"superarmor={knockbackIgnored} knockbackOff={knockbackWorksOff} endedOnRestore={endedOnRestore} "
            + $"damage={damageAmplified}({baseHit}->{rampageHit}) reEnter={reEntered}");

        yield return new WaitForSeconds(0.8f); // 테일

#if UNITY_EDITOR
        string recordingPath = TestRecorder.StopRecording();
        TestLog.Event(channel, $"recording_saved={recordingPath}");
#endif
    }

    // 폭주 시야 제한(B-2) — 어둠 · 적 실루엣 아웃라인 · 지형 글리치 라인 3층을 픽셀로 검증한다.
    //
    // ⚠️ 1차 증거는 영상이 아니라 픽셀이다. 이 프로젝트의 영상 판정기는 "얇은 색 경계"와 "어두운 화면"에서
    //    반복적으로 거짓 FAIL을 냈다(처형 아웃라인 3연속 · HUD 2연속). 시야 제한은 그 두 조건을 다 갖췄다.
    //    그래서 카메라를 RenderTexture로 직접 렌더해 같은 스크린 좌표를 폭주 전/중/후로 비교한다.
    public IEnumerator RampageVisionTest()
    {
        const string channel = "rampage_vision";
        TestLog.Step(channel, "spawned");

        var player = FindAnyObjectByType<PlayerController>();
        var cam = Camera.main;
        if (player == null || cam == null)
        {
            TestLog.Assert(channel, false, $"NOT_FOUND: player={player != null} cam={cam != null}");
            yield break;
        }

        var rb = player.GetComponent<Rigidbody2D>();
        var dummies = FindObjectsByType<DummyEnemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (dummies.Length < 2)
        {
            TestLog.Assert(channel, false, $"NEED_2_DUMMIES got={dummies.Length}");
            yield break;
        }
        var nearEnemy = dummies[0];
        var farEnemy = dummies[1];

        Vector3 basePos = new Vector3(1.61f, 0.05f, 0f);
        var prevSpeeds = new float[dummies.Length];
        var prevDamage = new int[dummies.Length];
        var prevPos = new Vector3[dummies.Length];
        for (int i = 0; i < dummies.Length; i++)
        {
            prevSpeeds[i] = dummies[i].moveSpeed;
            prevDamage[i] = dummies[i].playerDamageCount;
            prevPos[i] = dummies[i].transform.position;
        }

        int W = 480, H = 270;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        GameObject probe = null;

        bool darkened = false, centerKept = false, restored = false;
        bool nearHasOutline = false, farHasNoOutline = false;
        bool ringVisible = false, coreDark = false;
        bool terrainBuilt = false, glitchChanges = false, crumbleHides = false, noLeak = false;
        float cornerRatio = 1f;
        int ringPx = 0, segsWithProbe = 0, segsWithoutProbe = 0, distinctSigs = 0;

        try
        {
            for (int i = 0; i < dummies.Length; i++)
            {
                dummies[i].moveSpeed = 0f;        // 추적하게 두면 거리가 매번 달라진다
                dummies[i].playerDamageCount = 0; // 우발적 타격이 폭주를 끊지 않게
            }
            player.transform.position = basePos;
            rb.linearVelocity = Vector2.zero;
            player.currentEnergy = player.maxEnergy; // 베이스라인은 폭주가 아닌 상태에서 찍어야 한다
            nearEnemy.transform.position = basePos + new Vector3(4f, 0.4f, 0f);   // 컬링 반경 안
            farEnemy.transform.position = basePos + new Vector3(20f, 0.4f, 0f);   // 컬링 반경 밖
            yield return new WaitForSeconds(0.25f);

            // ── 베이스라인(폭주 전) ──
            Color baseBL, baseTR, basePL;
            CaptureScreen(cam, rt, tex);
            // ⚠️ 가시 영역 안의 표본으로 "플레이어 스프라이트"를 쓰면 안 된다 — Idle 애니메이션이 계속
            //    돌아서 폭주와 무관하게 픽셀이 바뀐다(실측: 0.291 → 0.108, 프레임이 Idle_6으로 넘어감).
            //    대신 발밑 지형 타일을 쓴다. 정적이고, 가시 중심(+0.6up)에서 1.0유닛이라 반경 1.5 안이다.
            Vector3 vp = cam.WorldToViewportPoint(player.transform.position + Vector3.down * 0.4f);
            baseBL = tex.GetPixel(6, 6);
            baseTR = tex.GetPixel(W - 7, H - 7);
            basePL = SamplePixel(tex, vp, W, H);
            TestLog.Step(channel, $"baseline corner={Lum(baseBL):F3} inside={Lum(basePL):F3}");

            // ── 폭주 진입: 광원이 0이 되면 자동으로 들어간다(토글 입력 없음) ──
            player.currentEnergy = 0;
            yield return null;
            yield return new WaitForSeconds(0.35f); // 페이드인 0.25s + 여유

            var fx = RampageVisionFx.Instance;
            if (fx == null)
            {
                TestLog.Assert(channel, false, "RampageVisionFx.Instance == null (폭주 진입 실패?)");
                yield break;
            }

            // ① 어둠: 모서리는 어두워지고 플레이어 중심은 유지된다
            CaptureScreen(cam, rt, tex);
            vp = cam.WorldToViewportPoint(player.transform.position + Vector3.down * 0.4f);
            Color darkBL = tex.GetPixel(6, 6);
            Color darkPL = SamplePixel(tex, vp, W, H);
            cornerRatio = Lum(baseBL) > 0.01f ? Lum(darkBL) / Lum(baseBL) : 1f;
            darkened = cornerRatio < 0.6f;
            centerKept = Mathf.Abs(Lum(darkPL) - Lum(basePL)) < 0.03f;
            TestLog.Step(channel, $"darkness cornerRatio={cornerRatio:F3} insideDelta={Mathf.Abs(Lum(darkPL) - Lum(basePL)):F4}");

            // ② 적 아웃라인: 가까운 적에만 붙고, 컬링 반경 밖에는 안 붙는다
            nearHasOutline = nearEnemy.GetComponentInChildren<RampageEnemyOutlineFx>(true) != null;
            farHasNoOutline = farEnemy.GetComponentInChildren<RampageEnemyOutlineFx>(true) == null;

            var esr = nearEnemy.GetComponent<SpriteRenderer>();
            Bounds eb = esr.bounds;
            Vector3 v0 = cam.WorldToViewportPoint(eb.min - new Vector3(0.3f, 0.3f, 0f));
            Vector3 v1 = cam.WorldToViewportPoint(eb.max + new Vector3(0.3f, 0.3f, 0f));
            int x0 = Mathf.Clamp(Mathf.RoundToInt(v0.x * W), 0, W - 1), x1 = Mathf.Clamp(Mathf.RoundToInt(v1.x * W), 0, W - 1);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(v0.y * H), 0, H - 1), y1 = Mathf.Clamp(Mathf.RoundToInt(v1.y * H), 0, H - 1);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                {
                    Color p = tex.GetPixel(x, y);
                    if (p.r > 0.25f && p.r > p.g * 2.5f && p.r > p.b * 2.5f) ringPx++;
                }
            ringVisible = ringPx > 20;
            Color corePx = SamplePixel(tex, cam.WorldToViewportPoint(eb.center), W, H);
            coreDark = Lum(corePx) < 0.06f; // 링(밝은 적색)과 달리 속은 어두워야 테두리로 읽힌다
            TestLog.Step(channel, $"enemy near={nearHasOutline} far={farHasNoOutline} ringPx={ringPx} core={Lum(corePx):F4}");

            // ③ 지형: 조각이 만들어지고, 결손 집합이 글리치 스텝마다 재추첨된다
            var terrain = fx.Terrain;
            terrainBuilt = terrain != null && terrain.SegmentCount > 0;
            if (terrain != null)
            {
                var sigs = new System.Collections.Generic.HashSet<int>();
                for (int i = 0; i < 12; i++)
                {
                    sigs.Add(terrain.AliveSignature);
                    yield return new WaitForSeconds(0.05f);
                }
                distinctSigs = sigs.Count;
                glitchChanges = distinctSigs >= 3; // 12Hz 스텝이 계속 다시 뽑히면 표본 12개에 값이 여럿 나온다
                TestLog.Step(channel, $"terrain segs={terrain.SegmentCount} alive={terrain.AliveCount} distinctSigs={distinctSigs}");

                // 부서지는 발판 대응: 콜라이더가 꺼지면 그 선도 사라진다(CrumblingPlatform과 같은 경로)
                probe = new GameObject("RampageVisionProbe");
                probe.layer = LayerMask.NameToLayer("Ground");
                probe.transform.position = player.transform.position + new Vector3(3f, 0f, 0f);
                var bc = probe.AddComponent<BoxCollider2D>();
                bc.size = new Vector2(4f, 1f);
                yield return new WaitForSeconds(0.6f); // 토폴로지 재수집 주기(0.5s)
                segsWithProbe = terrain.SegmentCount;
                bc.enabled = false;
                yield return new WaitForSeconds(0.6f);
                segsWithoutProbe = terrain.SegmentCount;
                // 조각 목록엔 남아도 그려지지 않으므로, 콜라이더를 아예 파괴해 목록에서 빠지는 것으로 판정한다
                UnityEngine.Object.Destroy(probe);
                probe = null;
                yield return new WaitForSeconds(0.6f);
                crumbleHides = segsWithProbe > terrain.SegmentCount;
                TestLog.Step(channel, $"probe segs withProbe={segsWithProbe} disabled={segsWithoutProbe} removed={terrain.SegmentCount}");
            }

            // ④ 해제: 빛을 되찾으면 자동 해제 — 화면이 베이스라인으로 완전히 돌아오고 오브젝트가 남지 않는다
            player.currentEnergy = player.maxEnergy;
            yield return null;
            yield return new WaitForSeconds(0.5f); // 페이드아웃 0.30s + 여유

            CaptureScreen(cam, rt, tex);
            vp = cam.WorldToViewportPoint(player.transform.position + Vector3.down * 0.4f);
            Color afterBL = tex.GetPixel(6, 6);
            Color afterTR = tex.GetPixel(W - 7, H - 7);
            Color afterPL = SamplePixel(tex, vp, W, H);
            restored = ColorClose(afterBL, baseBL) && ColorClose(afterTR, baseTR) && ColorClose(afterPL, basePL);

            noLeak = RampageVisionFx.Instance == null
                && FindObjectsByType<RampageTerrainOutlineFx>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0
                && FindObjectsByType<RampageEnemyOutlineFx>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0
                && (RampageVisionFeature.Instance == null || RampageVisionFeature.Instance.Intensity <= 0.001f);
            TestLog.Step(channel, $"restored={restored} noLeak={noLeak}");
        }
        finally
        {
            for (int i = 0; i < dummies.Length; i++)
            {
                dummies[i].moveSpeed = prevSpeeds[i];
                dummies[i].playerDamageCount = prevDamage[i];
                dummies[i].transform.position = prevPos[i];
            }
            if (probe != null) UnityEngine.Object.Destroy(probe);
            if (rt != null) { RenderTexture.active = null; rt.Release(); UnityEngine.Object.Destroy(rt); }
            if (tex != null) UnityEngine.Object.Destroy(tex);
            InputInjector.SetMoveX(0f);
            // ⚠️ 반드시 가상 장치를 떼고 끝낸다. 안 그러면 Keyboard.current가 가상 키보드를 계속 가리켜
            //    실제 키보드의 Q/R/E/F 직접 폴링이 죽는다(2026-08-01에 실제로 발생시킨 사고).
            InputInjector.Cleanup();
        }

        bool pass = darkened && centerKept && nearHasOutline && farHasNoOutline && ringVisible && coreDark
            && terrainBuilt && glitchChanges && crumbleHides && restored && noLeak;
        TestLog.Assert(channel, pass,
            $"dark={darkened}({cornerRatio:F2}) center={centerKept} nearOutline={nearHasOutline} farCulled={farHasNoOutline} "
            + $"ring={ringVisible}({ringPx}px) core={coreDark} terrain={terrainBuilt} glitch={glitchChanges}({distinctSigs}) "
            + $"crumble={crumbleHides} restored={restored} noLeak={noLeak}");

        yield return new WaitForSeconds(0.8f); // 테일

#if UNITY_EDITOR
        string recordingPath = TestRecorder.StopRecording();
        TestLog.Event(channel, $"recording_saved={recordingPath}");
#endif
    }

    // 카메라를 RenderTexture로 직접 렌더해 픽셀을 읽는다. 원격(비포커스) 에디터의 프레임 스로틀링과
    // 무관하게 "지금 이 순간의 화면"을 결정적으로 얻을 수 있어 영상 판정보다 훨씬 안정적이다.
    static void CaptureScreen(Camera cam, RenderTexture rt, Texture2D tex)
    {
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = prev;
    }

    static Color SamplePixel(Texture2D tex, Vector3 viewport, int w, int h)
        => tex.GetPixel(Mathf.Clamp(Mathf.RoundToInt(viewport.x * w), 0, w - 1),
                        Mathf.Clamp(Mathf.RoundToInt(viewport.y * h), 0, h - 1));

    static float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

    static bool ColorClose(Color a, Color b)
        => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

    // Q키 토글 1회. 눌린 프레임(wasPressedThisFrame)을 PlayerController.Update가 확실히 보도록
    // 누른 뒤 두 프레임을 준다(입력 이벤트는 다음 InputSystem 업데이트에서 처리된다).
    IEnumerator ToggleRampage()
    {
        InputInjector.PressRampage();
        yield return null;
        yield return null;
        InputInjector.ReleaseRampage();
        yield return null;
    }

    // 근접 1타 측정용 배치: 플레이어를 기준점에, 더미를 히트박스 안에 세우고 HP를 가득 채운다.
    IEnumerator PlaceForMelee(PlayerController player, Rigidbody2D rb, DummyEnemy dummy, Vector3 basePos, Vector3 nearPos)
    {
        player.transform.position = basePos;
        rb.linearVelocity = Vector2.zero;
        dummy.transform.position = nearPos;
        dummy.currentHp = dummy.maxHp;
        InputInjector.SetMoveX(1f); // 오른쪽을 보도록 한 프레임
        yield return null;
        InputInjector.SetMoveX(0f);
        rb.linearVelocity = Vector2.zero;
        yield return null;
    }

    // 좌클릭 1타. 판정은 Animation Event(AttackHitFrame)라 클립이 끝날 때까지 기다린다.
    IEnumerator AttackOnce(PlayerController player)
    {
        InputInjector.PressAttack();
        yield return null;
        InputInjector.ReleaseAttack();
        yield return new WaitForSeconds(player.attack1Duration + 0.15f);
    }

    // 지정 방향으로 한 번 대시하고, 대시가 끝나면 제자리에 서도록 입력을 중립화한다(걸어나가 낙하 방지).
    IEnumerator DashOnce(PlayerController player, int dir, string channel, string step)
    {
        InputInjector.SetMoveX(dir);
        yield return null;
        InputInjector.PressDash();
        yield return null;
        InputInjector.ReleaseDash();
        TestLog.Step(channel, step);
        yield return new WaitForSeconds(player.dashDuration + 0.02f); // 대시 종료까지
        InputInjector.SetMoveX(0f);                                   // 즉시 정지
    }
}
