using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Map-test(보스 스테이지)의 시작 → 보스 등장까지를 통째로 몰고 가는 감독(사용자 지시 2026-08-13).
//
// 한 줄 요약: 레터박스가 걸려 있는 동안은 "연출 구간"이고, 그동안 입력·HUD·적 AI가 전부 멈춘다.
//
//   [1] 레터박스 즉시 등장 · 글로벌 라이트 1 · 카메라 3.78 · 입력/적 정지 (전부 Start에서 동기 적용)
//   [2] 검은 화면 페이드 아웃 (다른 씬과 같은 암전 연출)
//   [3] 입력 없이 자동 보행 → walkTrigger에 닿을 때까지
//   [4] 레터박스 퇴장 · 입력 허용 · 적 AI 해제  ← 여기서 플레이 시작
//   [5] bossTrigger에 닿음 → 플레이어보다 왼쪽에 있는 적 전부 처형 · 레터박스 등장 · 입력 정지
//   [6] 카메라 줌 아웃(씬 기본 프레이밍으로 복귀)
//   [7] 글로벌 라이트 1 → 원래 값으로 페이드 + 카메라 쉐이크(동시)
//   [8] BossSFX-2 · 보스가 왼쪽 위에서 홈 위치까지 걸어 들어옴(라이트 전부 꺼진 채)
//   [9] 도착 → 쉐이크 정지 · 눈 라이트 페이드 인
//  [10] 빔이 위를 향한 채 페이드 인 → 다 커지면 플레이어 쪽으로 회전
//  [11] BossSFX-1 · text-tr 대사
//  [12] F로 대사를 닫으면 레터박스 퇴장
//  [13] 입력 허용 · 보스 행동 시작 · HUD 복귀 · BGM 재생 시작
//
// ⚠️ 전부 Start()에서 시작한다(Awake 아님) — IntroStartSequence·IntroFallStartSequence와 같은 이유다.
//    ① PlayerController.Awake가 ScriptedMoveX를 지운다. ② SectionCamera.Awake가 씬에 저장된 카메라
//    크기를 기준값으로 캡처한다. ③ BossEyeTracker.Awake가 원래 자리를 홈 좌표 기준으로 잡는다.
//    모든 Awake가 끝난 뒤에 Start가 돌고, 화면에 그려지는 건 그다음이라 첫 프레임 깜빡임은 없다.
public class BossStageDirector : MonoBehaviour
{
    [Header("연출 부품")]
    public CinematicLetterbox letterbox;
    public ScreenBlackout blackout;
    public IntroTextSequence textSequence;
    public SectionCamera sectionCamera;
    public Transform player;
    [Tooltip("씬의 Global Light 2D")]
    public Light2D globalLight;
    public BossEyeTracker boss;
    [Tooltip("이 씬의 BGM(Main Camera의 AudioSource). 연출이 끝나는 순간에야 재생을 시작한다")]
    public AudioSource bgm;

    [Header("트리거")]
    [Tooltip("[3] 자동 보행이 멈추는 지점")]
    public IntroActionTrigger walkTrigger;
    [Tooltip("[5] 보스 등장이 시작되는 지점 (씬의 BossTrigger)")]
    public IntroActionTrigger bossTrigger;

    [Header("[1] 시작 상태")]
    // ⚠️ 오프닝 카메라에는 값이 하나도 없다 — 씬에 저장해 둔 Main Camera의 **자리와 크기 그대로**를
    //    구도로 쓴다(사용자 지시 2026-08-13). 구도를 바꾸고 싶으면 에디터에서 카메라를 옮기면 되고,
    //    구간 전환 폭·높이는 SectionCamera의 sectionSize · gridOrigin이 정한다.
    //    카메라가 플레이어를 따라다니지는 않는다 — 구간에 고정됐다가 경계를 넘을 때만 슬라이드한다.
    [Tooltip("연출 구간의 글로벌 라이트 밝기 — [7]에서 씬에 저장된 값으로 되돌아간다")]
    public float introLightIntensity = 1f;

    [Header("[6] 보스 등장 컷씬 구도 (방 중심 기준)")]
    // 사용자 지시(2026-08-13): "보스 등장 컷씬에서만 좀 더 아래로 내려주고 카메라 크기 키워줘".
    // 기준은 이 방의 평소 프레이밍(중심 y=36.8 · 크기 13.5)이다.
    // ⚠️ 크기 상한이 있다: 배경 스프라이트(background1_0/2_0)가 카메라 y를 따라오지만 높이가 ±17.88
    //    월드뿐이라, 이 값이 17.88을 넘으면 화면 위아래에 배경 없는 빈 띠가 생긴다.
    [Tooltip("보스 등장 동안의 카메라 크기 — 방 기본(13.5)보다 크게. 17.8을 넘기면 배경이 모자란다")]
    public float bossSceneOrthoSize = 17.5f;
    [Tooltip("방 중심 기준 오프셋 — y를 음수로 내릴수록 시점이 아래로 내려간다")]
    public Vector2 bossSceneCenterOffset = new Vector2(-8f, -9f);

    [Header("타이밍(초)")]
    [Tooltip("[2] 검은 화면이 걷히는 시간")]
    public float blackoutFadeDuration = 1.2f;
    [Tooltip("[3] 자동 보행 방향(+1=오른쪽)")]
    public float walkDirection = 1f;
    [Tooltip("[3] 이 시간 안에 walkTrigger에 못 닿으면 연출을 다음 단계로 넘긴다(연출 고착 방지)")]
    public float walkTimeout = 20f;
    [Tooltip("[6] 보스 등장 구도로 내려가며 커지는 시간 / [12] 평소 구도로 되돌아오는 시간")]
    public float zoomOutDuration = 1.8f;
    [Tooltip("[7] 밝기가 원래대로 떨어지는 시간")]
    public float lightFadeDuration = 2f;
    [Tooltip("[8] 보스가 걸어 들어오는 시간")]
    public float bossMoveInDuration = 3.5f;
    [Tooltip("[9] 눈 라이트가 켜지는 시간")]
    public float eyeLightUpDuration = 1.2f;
    [Tooltip("[10] 빔이 위를 향한 채 켜지는 시간")]
    public float beamUpDuration = 1f;
    [Tooltip("[10] 빔이 플레이어 쪽으로 도는 시간")]
    public float beamAimDuration = 1.2f;

    [Header("[7] 카메라 쉐이크 (보스가 다가오는 동안 유지)")]
    // 사용자 지시(2026-08-13): "카메라 쉐이킹 강하게". 보스 발걸음 쿵(0.08)·플레이어 공격(0.15)보다
    // 크게 잡는다 — ambient 채널은 Perlin 저주파라 같은 세기라도 백색 잡음보다 훨씬 묵직하게 읽힌다.
    [Tooltip("보스가 다가오는 동안 깔리는 지속 진동 — 발걸음 쿵(0.08)·공격(0.15)보다 강하게")]
    public float approachShakeMagnitude = 0.4f;

    [Header("대사 (둘 다 text-tr · 같은 연출)")]
    [Tooltip("[3] 자동 보행이 끝나는 순간 — 비우면 대사 없이 바로 [4]로 넘어간다")]
    [TextArea] public string walkMessage = "오른쪽으로 나아가자.";
    [Tooltip("[11] 보스가 빔을 겨눈 뒤")]
    [TextArea] public string message = "도망쳐야한다.";
    [Tooltip("[11] 대사만 이 색으로 — 경고 대사라 붉게(사용자 지시). [3b] 대사는 씬의 원래 색 그대로다")]
    public Color messageColor = new Color(1f, 0.2f, 0.2f, 1f);

    float sceneLightIntensity;
    float sceneBgmVolume;
    bool running;

    void Awake()
    {
        // BGM은 [13]에서야 시작한다. 씬의 playOnAwake를 꺼 두는 것이 정답이지만, 켜진 채로 저장돼도
        // 여기서 확실히 멈춘다 — AudioSource가 먼저 Awake를 돌았어도 아직 소리는 거의 안 나갔다.
        if (bgm != null)
        {
            sceneBgmVolume = bgm.volume;
            bgm.playOnAwake = false;
            bgm.Stop();
        }
    }

    void Start()
    {
        StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        running = true;

        // ── [1] 첫 프레임부터 완성된 그림 ──────────────────────────────────────────────
        BeginCutsceneLock();
        blackout.Set(1f);
        letterbox.ShowInstant();

        if (globalLight != null)
        {
            sceneLightIntensity = globalLight.intensity;
            globalLight.intensity = introLightIntensity;
        }
        if (sectionCamera != null)
        {
            sectionCamera.BeginSceneCutsceneFraming();
            // 자동 보행 동안은 카메라를 아예 세워 둔다(사용자 지시) — 구간 경계를 넘어도 안 따라간다.
            // IntroScene_3의 퇴장 보행도 같은 규칙이라 두 씬이 "고정된 그림" 하나로 이어진다.
            sectionCamera.SetCutsceneFrozen(true);
        }

        // 보스는 이 시점엔 씬에 찍어둔 자리(플레이어 바로 옆)에 서 있다 — 화면 밖으로 치워 둔다.
        if (boss != null) boss.IntroPlaceAtSpawn();

        // ⚠️ 자동 보행은 암전이 걷히기를 기다리지 않고 **첫 프레임부터** 시작한다(사용자 지시
        //    2026-08-13 "걷는 것부터 시작 / IntroScene_3와 아예 이어지는 느낌"). 걸음을 [2] 뒤로
        //    미루면 암전이 걷히는 1.2초 동안 플레이어가 제자리에 서 있어 Idle(그리고 스폰 낙하 때문에
        //    Land)이 재생된다 — 앞 씬에서 걸어 나온 흐름이 거기서 한 번 끊긴다.
        SnapPlayerToGround();
        PlayerController.ScriptedMoveX = Mathf.Sign(walkDirection);

        TestLog.Event("boss_stage", "[ASSERT] step1 letterbox+lock ready");

        // ── [2] 암전 페이드 아웃 (걸어가는 중에 걷힌다) ───────────────────────────────
        yield return blackout.FadeTo(0f, blackoutFadeDuration);
        TestLog.Event("boss_stage", "[ASSERT] step2 blackout_cleared");

        // ── [3] walkTrigger에 닿을 때까지 계속 걷는다 ────────────────────────────────
        float walked = 0f;
        while (walkTrigger != null && !walkTrigger.Fired && walked < walkTimeout)
        {
            walked += Time.deltaTime;
            yield return null;
        }
        PlayerController.ScriptedMoveX = null;
        if (walkTrigger != null && !walkTrigger.Fired)
            Debug.LogWarning($"[BossStageDirector] walkTrigger에 {walkTimeout}초 안에 닿지 못해 자동 보행을 끊었습니다 — 트리거 위치를 확인하세요.", this);
        TestLog.Event("boss_stage", $"[ASSERT] step3 walk_done t={walked:F1}s");

        // 걸음이 멎은 그 자리에서 대사 한 번(사용자 지시 2026-08-13). 레터박스가 아직 떠 있는 동안
        // 띄워야 [11]의 대사와 같은 그림(검은 바 위 자막)이 된다 — 그래서 [4]보다 먼저 온다.
        if (!string.IsNullOrEmpty(walkMessage))
        {
            yield return textSequence.ShowMessage(walkMessage);
            TestLog.Event("boss_stage", "[ASSERT] step3b walk_message_closed");
        }

        // ── [4] 레터박스 퇴장 · 플레이 시작 ───────────────────────────────────────────
        if (sectionCamera != null) sectionCamera.SetCutsceneFrozen(false); // 여기서부터 구간 전환 재개
        yield return letterbox.Hide();
        EndCutsceneLock();
        TestLog.Event("boss_stage", "[ASSERT] step4 control_given");

        // ── [5] BossTrigger → 처형 + 연출 재진입 ──────────────────────────────────────
        while (bossTrigger != null && !bossTrigger.Fired) yield return null;

        int executed = ExecuteEnemiesBehindPlayer();
        BeginCutsceneLock();
        yield return letterbox.Show();
        TestLog.Event("boss_stage", $"[ASSERT] step5 boss_trigger executed={executed}");

        // ── [6] 보스 등장 구도로 (아래로 내려가며 커진다) ─────────────────────────────
        yield return ZoomToBossFraming(zoomOutDuration);
        TestLog.Event("boss_stage", "[ASSERT] step6 zoom_out_done");

        // ── [7] 밝기 페이드 + 쉐이크(동시에 시작, 쉐이크는 [9]까지 유지) ──────────────
        if (sectionCamera != null) sectionCamera.SetAmbientShake(approachShakeMagnitude);
        yield return FadeGlobalLight(introLightIntensity, sceneLightIntensity, lightFadeDuration);
        TestLog.Event("boss_stage", "[ASSERT] step7 light_faded");

        // ── [8] 보스 등장 ─────────────────────────────────────────────────────────────
        GameSfx.Play(Sfx.BossAppear);
        if (boss != null)
        {
            boss.IntroPlaceAtSpawn();   // 플레이어가 [3]~[5] 동안 이동했으므로 시작 위치를 다시 잡는다
            yield return boss.IntroMoveIn(bossMoveInDuration);
        }
        TestLog.Event("boss_stage", "[ASSERT] step8 boss_arrived");

        // ── [9] 쉐이크 정지 + 눈 라이트 ───────────────────────────────────────────────
        if (sectionCamera != null) sectionCamera.SetAmbientShake(0f);
        if (boss != null) yield return boss.IntroEyeLightUp(eyeLightUpDuration);
        TestLog.Event("boss_stage", "[ASSERT] step9 eye_on");

        // ── [10] 빔 점등 → 플레이어 조준 ──────────────────────────────────────────────
        if (boss != null)
        {
            yield return boss.IntroBeamUp(beamUpDuration);
            yield return boss.IntroBeamAimToPlayer(beamAimDuration);
        }
        TestLog.Event("boss_stage", "[ASSERT] step10 beam_aimed");

        // ── [11] 포효 + 대사 ─────────────────────────────────────────────────────────
        GameSfx.Play(Sfx.BossRoar);
        yield return textSequence.ShowMessage(message, null, messageColor);

        // ── [12] 레터박스 퇴장 + 카메라 원복 ─────────────────────────────────────────
        // 카메라는 기다리지 않는다 — 레터박스가 걷히는 순간 조작이 돌아와야 하고(스펙 13),
        // 남은 복귀 이동은 게임이 시작된 뒤에도 이어서 잦아들면 된다.
        StartCoroutine(ReleaseBossFraming(zoomOutDuration));
        yield return letterbox.Hide();

        // ── [13] 조작 · 보스 · HUD · BGM ─────────────────────────────────────────────
        EndCutsceneLock();
        if (boss != null) boss.IntroFinish();
        if (bgm != null)
        {
            bgm.volume = sceneBgmVolume;
            bgm.Play();
        }
        running = false;
        TestLog.Event("boss_stage", "[ASSERT] step13 stage_live");
    }

    // ── 잠금 ────────────────────────────────────────────────────────────────────────────
    // 레터박스가 떠 있는 동안의 상태를 한 덩어리로 묶는다 — "레터박스가 있을 때는 UI를 가린다"는
    // 규칙이 두 구간([1]~[4], [5]~[13]) 모두에 같은 방식으로 걸리게 하기 위해서다.
    void BeginCutsceneLock()
    {
        TutorialGate.Allowed = TutorialAbility.None;
        PlayerController.ScriptedMoveX = null;
        DummyEnemy.AiFrozen = true;
        PlayerHudUI.GetOrCreate().SetVisible(false);
    }

    void EndCutsceneLock()
    {
        PlayerController.ScriptedMoveX = null;
        TutorialGate.ResetAll();
        DummyEnemy.AiFrozen = false;
        PlayerHudUI.GetOrCreate().SetVisible(true);
    }

    // ── 개별 동작 ───────────────────────────────────────────────────────────────────────

    /// <summary>씬에 찍어둔 플레이어 위치가 지면보다 살짝 떠 있으면 그만큼 내려 붙인다.
    ///
    /// 이게 없으면 씬 시작 직후 짧은 낙하 → 착지가 생겨 Land 애니메이션(과 착지음)이 한 번 튄다.
    /// 앞 씬에서 걸어 들어온 흐름이 거기서 끊기므로, 첫 프레임부터 그냥 지면에 서 있게 만든다.
    /// 큰 간격(발판이 아예 없는 배치)은 건드리지 않는다 — 의도적인 낙하 연출까지 없애면 안 된다.</summary>
    void SnapPlayerToGround()
    {
        if (player == null) return;

        var col = player.GetComponent<Collider2D>();
        if (col == null) return;

        int groundMask = LayerMask.GetMask("Ground");
        if (groundMask == 0) return;

        // 콜라이더 바닥에서 아래로 쏜다. 바닥 자신에 맞지 않도록 살짝 위에서 출발한다.
        var hit = Physics2D.Raycast(new Vector2(col.bounds.center.x, col.bounds.min.y + 0.05f),
                                    Vector2.down, MaxGroundSnapDistance + 0.05f, groundMask);
        if (hit.collider == null) return;

        float gap = col.bounds.min.y - hit.point.y;
        if (gap <= 0.001f || gap > MaxGroundSnapDistance) return;

        player.position -= new Vector3(0f, gap, 0f);
        TestLog.Event("boss_stage", $"ground_snap gap={gap:F3}");
    }

    const float MaxGroundSnapDistance = 1.5f;

    /// <summary>플레이어보다 X가 작은(=지나온 쪽) 살아 있는 적을 전부 처형한다. 죽인 수를 돌려준다.</summary>
    int ExecuteEnemiesBehindPlayer()
    {
        if (player == null) return 0;

        int killed = 0;
        var enemies = FindObjectsByType<DummyEnemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            DummyEnemy e = enemies[i];
            if (e == null || !e.IsAlive) continue;
            if (e.transform.position.x >= player.position.x) continue;
            // 남은 HP만큼 때린다 — 사망 애니메이션·시체 처리 경로를 평소와 똑같이 태우기 위해서다.
            e.TakeDamage(e.currentHp);
            killed++;
        }
        // ⚠️ 처형 효과음은 내지 않는다(사용자 지시 2026-08-13 "보스로 인한 처형은 sfx 재생 안합니다").
        //    플레이어가 낸 처형이 아니라 보스가 쓸어버리는 연출이라 같은 소리를 쓰면 주체가 헷갈린다.
        return killed;
    }

    /// <summary>오프닝 구도(씬 저장값)에서 보스 등장 구도(방 중심 + 오프셋, 더 크게)로 내려간다.
    ///
    /// 시작 오프셋을 "지금 화면 중심 − 방 중심"으로 잡기 때문에 전환 첫 프레임에 위치가 튀지 않는다.
    /// 크기와 오프셋을 같은 k로 함께 몰아 팬과 줌이 하나의 동작으로 읽힌다.</summary>
    IEnumerator ZoomToBossFraming(float duration)
    {
        if (sectionCamera == null) yield break;

        Vector3 anchor = sectionCamera.RoomAnchorPosition;
        Vector2 fromOffset = new Vector2(sectionCamera.BasePosition.x - anchor.x,
                                         sectionCamera.BasePosition.y - anchor.y);
        float fromOrtho = sectionCamera.CutsceneOrthoSize;

        float d = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < d)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / d));
            sectionCamera.SetRoomCutsceneFraming(Mathf.Lerp(fromOrtho, bossSceneOrthoSize, k),
                                                 Vector2.Lerp(fromOffset, bossSceneCenterOffset, k), 1f);
            yield return null;
        }
        sectionCamera.SetRoomCutsceneFraming(bossSceneOrthoSize, bossSceneCenterOffset, 1f);
    }

    /// <summary>보스 등장 구도를 놓아 준다 — 블렌드만 1→0으로 내리면 위치·크기가 같이 평소 룸 프레이밍으로 풀린다.</summary>
    IEnumerator ReleaseBossFraming(float duration)
    {
        if (sectionCamera == null) yield break;

        float d = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < d)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / d));
            sectionCamera.SetRoomCutsceneFraming(bossSceneOrthoSize, bossSceneCenterOffset, 1f - k);
            yield return null;
        }
        sectionCamera.EndCutsceneFraming();
    }

    IEnumerator FadeGlobalLight(float from, float to, float duration)
    {
        if (globalLight == null) yield break;

        float d = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < d)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            globalLight.intensity = Mathf.Lerp(from, to, Mathf.Clamp01(t / d));
            yield return null;
        }
        globalLight.intensity = to;
    }

    // 씬 로드 직후 첫 프레임의 unscaledDeltaTime은 1초를 넘길 수 있다(ScreenBlackout 주석 참고).
    const float MaxStep = 0.05f;

    // 연출 도중 씬이 바뀌거나 오브젝트가 꺼져도 잠금·암전·쉐이크·자동 보행이 남지 않게 한다.
    void OnDisable()
    {
        if (!running) return;
        running = false;

        PlayerController.ScriptedMoveX = null;
        TutorialGate.ResetAll();
        DummyEnemy.AiFrozen = false;
        if (blackout != null) blackout.Set(0f);
        if (sectionCamera != null)
        {
            sectionCamera.SetAmbientShake(0f);
            sectionCamera.EndCutsceneFraming();
        }
        if (globalLight != null && sceneLightIntensity > 0f) globalLight.intensity = sceneLightIntensity;
    }
}
