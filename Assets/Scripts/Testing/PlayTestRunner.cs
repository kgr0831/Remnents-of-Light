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

            int hpBefore = player.currentHp;
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
            bool noDamage = player.currentHp == hpBefore;
            bool phase1 = shieldUp && successLogged && noDamage && motionPlaying;
            TestLog.Step(channel, $"phase1_result hp={player.currentHp}/{hpBefore} noDamage={noDamage} pass={phase1}");

            // ── Phase 2: 실드가 다음 공격 1회를 막고 깨진다 ────────────────────────────
            int hpBeforeShield = player.currentHp;
            float t2 = 0f;
            while (t2 < 4f && player.HasParryShield) { t2 += Time.deltaTime; yield return null; }
            bool shieldConsumed = !player.HasParryShield;
            bool blockLogged = events.Exists(e => e.Contains("shield_blocked"));
            yield return null;
            bool stillNoDamage = player.currentHp == hpBeforeShield;
            bool phase2 = shieldConsumed && blockLogged && stillNoDamage;
            TestLog.Step(channel, $"phase2_result consumed={shieldConsumed} block_logged={blockLogged} " +
                $"hp={player.currentHp}/{hpBeforeShield} pass={phase2}");

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
        int startHp = player.currentHp;
        float waitBudget = 4f;
        float t = 0f;
        while (t < waitBudget && player.currentHp >= startHp)
        {
            t += Time.deltaTime;
            yield return null;
        }
        bool playerHit = player.currentHp < startHp;
        TestLog.Step(dummyChannel, $"player_hp={player.currentHp} playerHit={playerHit}");

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
