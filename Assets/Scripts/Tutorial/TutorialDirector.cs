using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>스텝의 성공 판정 방식.</summary>
public enum TutorialGoal
{
    None,               // 대사만 보여주고 넘어간다(인트로·마무리)
    ReachZone,          // successZone 안에 접지 상태로 들어옴
    KillEnemies,        // 이 스텝의 적이 전부 죽음
    Dash,               // 대시 1회
    DodgeCounter,       // 회피-카운터 성공(카운터 공격까지)
    Parry,              // 패링 성공
    TimeAccelKill,      // 시간 가속을 쓴 상태로 적 처치(안 쓰고 처치하면 실패)
    IlseomKill,         // 일섬을 쓴 상태로 적 처치(안 쓰고 처치하면 실패)
    LightSpendRelease,  // 광원 방출(E 홀드) 후 홀드 해제
    Execution,          // 처형 성공
    TranscendKill,      // 초월 상태를 겪으며 적 처치
    RampageKill,        // 폭주 상태를 겪으며 적 처치
}

/// <summary>한 동작에 대응하는 튜토리얼 구간 하나. 위치·상황·허용 동작·판정을 전부 여기에 적는다.</summary>
[System.Serializable]
public class TutorialStepData
{
    public string id = "step";
    [Tooltip("SwordPanel의 text-action-2에 타이핑될 문구")]
    [TextArea] public string message;
    [Tooltip("이 구간에서 플레이어가 서 있을 자리(발밑 기준)")]
    public Transform spawnPoint;

    [Header("이 구간에서 열리는 동작")]
    [Tooltip("여기 없는 동작은 연출·판정·입력 수신까지 전부 막힌다")]
    public TutorialAbility allowed = TutorialAbility.Move;
    [Tooltip("적의 공격을 받아도 체력이 닳지 않는다")]
    public bool invulnerable = true;
    [Tooltip("좌클릭 일반 공격의 크리티컬을 막는다(공격 스텝 전용)")]
    public bool noCrit;

    [Header("적")]
    public GameObject enemyPrefab;
    [Tooltip("적을 놓을 자리. 비우면 적 없음")]
    public Transform[] enemySpawns;
    public int enemyMaxHp = 1;
    [Tooltip("0이면 maxHp와 같다. 처형 스텝은 maxHp 5 · 시작 1(=20%)로 둬야 처형 조건이 성립한다")]
    public int enemyStartHp;

    [Header("플레이어 시작 상태")]
    [Tooltip("0이면 최대 체력")]
    public int startHealth;
    [Range(0, 100)] public int startEnergyPercent = 50;

    [Header("판정")]
    public TutorialGoal goal = TutorialGoal.None;
    [Tooltip("ReachZone 전용 — 이 트리거 안에 접지 상태로 들어오면 성공")]
    public Collider2D successZone;
    [Tooltip("플레이어 y가 이 값보다 낮아지면 실패(점프 스텝). 안 쓰면 아주 작은 값으로 둔다")]
    public float failBelowY = -9999f;
    [Tooltip(">0이면 자아 게이지가 0인 채로 이 시간(초)을 넘기면 실패(폭주 스텝)")]
    public float egoDepletedFailSeconds;
    [Tooltip(">=0이면 성공하는 순간 광원을 이 %로 되돌린다(초월·폭주 해제용). -1이면 안 건드림")]
    public int successEnergyPercent = -1;
}

/// <summary>전투 학습(튜토리얼) 진행자.
///
/// 한 스텝의 흐름(사용자 스펙 그대로):
///   노이즈 + 검정 페이드 인 → 0.5초 대기 → 해당 구역으로 순간이동 → 검정 페이드 아웃
///   → SwordPanel 열림 + 타이핑 설명 → 1초 대기 → 자동으로 닫힘
///   → **여기서부터 시간이 흐르고 그 동작만 입력이 열린다**
///   → 실패: 페이드 인 → 0.5초 → 상황 초기화 → 페이드 아웃 → 재개
///   → 성공: 입력 제한 + 시간 정지 → 0.5초 → 페이드 인 → 다음 스텝
///
/// ⚠️ 연출 구간은 전부 Time.timeScale = 0이다. 그래서
///   1) 이 클래스의 모든 대기는 unscaled(WaitForSecondsRealtime)다.
///   2) 카메라는 SectionCamera.SnapToTarget()으로 직접 정착시킨다 — 그쪽 슬라이드는 Time.deltaTime
///      기반이라 멈춘 시간에는 한 프레임도 안 움직인다.
///   3) LateUpdate가 매 프레임 timeScale=0을 다시 눌러 준다 — 회피-카운터·히트스톱 코루틴의
///      finally가 자기 기준값(1)으로 되돌려 정지가 풀리는 것을 막는다.</summary>
public class TutorialDirector : MonoBehaviour
{
    [Header("연출 부품")]
    public ScreenBlackout blackout;
    public TutorialPanelUI panel;
    public PlayerController player;
    public SectionCamera sectionCamera;

    [Header("문구")]
    [Tooltip("text-action-1에 항상 타이핑되는 머리말")]
    public string systemLine = "SYSTEM MESSAGE";
    [TextArea] public string introMessage = "전투 학습에 오신것을 환영합니다.";
    [TextArea] public string outroMessage = "전투 학습이 종료되었습니다.";
    [Tooltip("마무리 패널이 닫히고 글리치가 걷힌 뒤 넘어갈 씬. 비워두면 전환하지 않고 그 자리에 머문다(예전 동작)")]
    public string nextScene = "IntroScene_3";
    [Tooltip("인트로 대사 동안 플레이어를 세워 둘 자리(비우면 첫 스텝 자리)")]
    public Transform introSpawn;

    [Header("구간")]
    public TutorialStepData[] steps;

    [Header("타이밍(초 · 전부 실시간)")]
    public float fadeInDuration = 0.5f;
    [Tooltip("완전히 검어진 뒤 순간이동까지의 대기")]
    public float blackHold = 0.5f;
    public float fadeOutDuration = 0.5f;
    [Tooltip("설명이 다 타이핑된 뒤 패널이 닫히기까지의 대기")]
    public float panelHold = 1f;
    [Tooltip("성공한 뒤 암전이 시작되기까지의 대기. **시간이 흐르는 채로** 기다린다 — " +
             "적 사망 연출·히트 이펙트가 끝나야 하기 때문(사용자 지시 2026-08-12)")]
    public float successHold = 1f;
    [Tooltip("실패 암전이 유지되는 시간")]
    public float failBlackHold = 0.5f;

    [Header("적 글로우(블룸용 · 사용자 지시 2026-08-12)")]
    [Tooltip("적 뒤에 같은 실루엣을 조금 크게 깔아 테두리만 빛나게 한다. HDR 색(임계값 1.15 초과)이라야 블룸이 걸린다")]
    [ColorUsage(true, true)] public Color enemyGlowColor = new Color(2.2f, 0.5f, 0.55f);
    [Tooltip("실루엣을 몇 배로 키울지. 1.06이면 테두리가 6% 두께로 삐져나온다")]
    public float enemyGlowScale = 1.06f;
    [Tooltip("적 본체가 빛나는 세기(HDR 배율). 1이면 블룸 없음. 적의 원래 색이 그대로 밝아진다(테두리 안 생김)")]
    public float enemyGlowBoost = 1.8f;

    [Header("연출 효과음")]
    [Tooltip("연출 전용 소스. 패널 타이핑 소스와 나눠 둔다 — 그쪽은 타이핑이 끝날 때 Stop()으로 " +
             "루프를 끊어서, 같이 쓰면 글리치 효과음까지 잘려 나간다")]
    public AudioSource sfxSource;
    [Tooltip("간헐 글리치가 뜰 때 (GlitchSFX3)")]
    public AudioClip ambientGlitchSfx;
    [Tooltip("전환·컷신 글리치(암전 페이드 인)에 (SwitchSFX)")]
    public AudioClip transitionGlitchSfx;

    [Header("간헐 글리치(사용자 지시 2026-08-12)")]
    [Tooltip("구간을 플레이하는 동안 이 간격(초, 실시간) 사이에서 무작위로 화면이 잠깐 깨진다. x=최소 y=최대")]
    public Vector2 glitchInterval = new Vector2(4f, 9f);
    [Tooltip("한 번에 깨져 있는 시간(초). 여기에 페이드 인 0.15 · 아웃 0.2가 따로 붙는다")]
    public Vector2 glitchFlash = new Vector2(0.08f, 0.2f);

    // ── 진행 상태 ────────────────────────────────────────────────────────────────────────────
    readonly List<DummyEnemy> enemies = new List<DummyEnemy>();
    Material enemyGlowMat;   // 적 글로우 공유 머티리얼(HDR을 유니폼으로 태운다) — AttachEnemyGlow가 만든다
    bool frozen;
    bool stepActive;

    // 스킵(F · 좌클릭) — 한 번 누르면 **지금 재생 중인 효과 하나**만 건너뛴다(사용자 확정 2026-08-12
    // "스킵이라는게 해당 효과만 스킵인겁니다"). 페이드 → 암전 대기 → 패널 열림 → 타이핑 → 유지 →
    // 역타이핑 → 패널 닫힘이 줄줄이 이어지므로, 끝까지 넘기려면 그만큼 누르면 된다.
    // 대사 타이핑만 예외적으로 두 줄을 한 번에 다 띄운다 — 첫 줄만 완성되고 둘째 줄이 다시
    // 타이핑되기 시작하면 "대사를 다 띄웠다"로 안 보이기 때문(사용자 지시 원문 그대로).
    bool skipPending;

    // 이번 스텝에서 "그 동작을 썼는가" — 순간에 끝나는 동작은 카운터 기준선과 비교한다.
    bool usedDash, usedTimeAccel, usedIlseom, releasedLightSpend, wasSpendingLight;
    float egoZeroTimer;
    int baseParry, baseDodgeCounter, baseExecution;

    // [ASSERT] 판독구
    public int CurrentStepIndex { get; private set; } = -1;
    public string CurrentStepId { get; private set; } = "";

    IEnumerator Start()
    {
        if (player == null || blackout == null || panel == null)
        {
            Debug.LogError("[TutorialDirector] blackout/panel/player 참조가 비어 있어 진행할 수 없습니다.", this);
            yield break;
        }

        // 이전 씬(검 획득 컷신)이 검게 덮은 채로 넘겨준다 — 첫 프레임부터 검어야 이음매가 안 보인다.
        // ScreenBlackout이 자기 Awake에서 알파 0인 패널을 이미 만들어 뒀으므로 여기서 값만 채운다.
        blackout.Set(1f);
        Freeze();
        EnsureTimeIndependentAudio();
        panel.consumeSkip = ConsumeSkip;   // 패널의 프레임 훑기·타이핑·역타이핑도 같은 입력을 나눠 쓴다
        SetHudVisible(false);   // 첫 프레임부터 검은 화면이라 HUD가 그 위에 떠 있으면 안 된다
        StartCoroutine(AmbientGlitchRoutine());

        if (introSpawn != null) Teleport(introSpawn.position);
        else if (steps.Length > 0 && steps[0].spawnPoint != null) Teleport(steps[0].spawnPoint.position);

        // ── 인트로: 검은 화면 위에 환영 문구 ────────────────────────────────────────────────
        yield return panel.Open(systemLine, introMessage);
        yield return Wait(panelHold);
        yield return panel.Close();

        // ── 각 구간 ────────────────────────────────────────────────────────────────────────
        for (int i = 0; i < steps.Length; i++)
        {
            TutorialStepData s = steps[i];
            CurrentStepIndex = i;
            CurrentStepId = s.id;
            TestLog.Step("tutorial", $"enter {s.id}");

            // 이 시점의 화면은 항상 완전 암전이다(인트로 직후 or 직전 스텝의 성공 암전).
            yield return Wait(blackHold);
            SetupStep(s);
            yield return blackout.FadeTo(0f, fadeOutDuration);
            ScreenGlitchFx.End(ScreenGlitchFx.Source.Cutscene);

            yield return panel.Open(systemLine, s.message);
            yield return Wait(panelHold);
            yield return panel.Close();

            Resume(s);

            // 성공/실패 감시 — 실패하면 같은 구간을 초기화하고 계속 본다.
            while (true)
            {
                yield return null;
                if (!stepActive) continue;
                if (IsFailed(s)) { yield return FailRoutine(s); continue; }
                if (IsSucceeded(s)) break;
            }

            yield return SucceedRoutine(s);
            yield return FadeToBlack();
        }

        // ── 마무리: 패널 → 대기 → 패널 닫기 → 글리치 해제 → 다음 씬(사용자 지시 2026-08-12) ──
        //    예전에는 패널을 띄운 채 암전 상태로 멈춰 있었다. 이제 각 구간과 같은 리듬
        //    (Open → panelHold → Close)으로 마무리하고 넘어간다.
        CurrentStepIndex = steps.Length;
        CurrentStepId = "outro";
        yield return Wait(blackHold);
        yield return panel.Open(systemLine, outroMessage);
        yield return Wait(panelHold);
        yield return panel.Close();

        // 전투 학습이 끝났으니 화면 노이즈를 걷는다 — 두 원인 모두 끈다(하나만 끄면 나머지가 살아 있어
        // 화면이 계속 깨진 채로 다음 씬으로 넘어간다).
        ScreenGlitchFx.End(ScreenGlitchFx.Source.Cutscene);
        ScreenGlitchFx.End(ScreenGlitchFx.Source.Tutorial);

        TestLog.Step("tutorial", "finished");

        if (string.IsNullOrEmpty(nextScene)) yield break;
        // 검은 화면인 채로 멈추면 원인을 찾기 어렵다 — 미등록이면 무엇을 해야 하는지 남긴다
        // (IntroFallCutscene · SwordPickupSequence와 같은 가드).
        if (!SceneTransitionTrigger.IsInBuildSettings(nextScene))
        {
            Debug.LogError("[TutorialDirector] '" + nextScene + "'이 Build Settings에 없어 전환할 수 없습니다. " +
                           "File > Build Profiles > Scene List에 추가하세요.", this);
            yield break;
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
    }

    // ── 흐름 조각 ────────────────────────────────────────────────────────────────────────────

    IEnumerator FadeToBlack()
    {
        ScreenGlitchFx.Begin(ScreenGlitchFx.Source.Cutscene);   // "노이즈 + 검정 페이드 인"
        PlaySfx(transitionGlitchSfx);
        yield return Fade(1f, fadeInDuration);
        SetHudVisible(false);   // 완전히 검어진 뒤에 끈다 — 페이드 시작과 동시에 끄면 툭 사라져 보인다
    }

    /// <summary>스킵을 받을 수 있는 페이드. ScreenBlackout의 코루틴을 직접 굴리다가
    /// 스킵이 들어오면 목표 알파로 바로 찍고 끝낸다.</summary>
    IEnumerator Fade(float target, float duration)
    {
        IEnumerator inner = blackout.FadeTo(target, duration);
        while (inner.MoveNext())
        {
            if (ConsumeSkip()) { blackout.Set(target); yield break; }
            yield return inner.Current;
        }
    }

    /// <summary>스킵 입력을 **한 번 소비**한다 — 호출한 그 효과 하나만 건너뛰고, 뒤이어 오는 효과는
    /// 다시 정상 재생된다(사용자 확정: "해당 효과만 스킵").</summary>
    bool ConsumeSkip()
    {
        if (!skipPending) return false;
        skipPending = false;
        return true;
    }

    /// <summary>연출을 건너뛰라는 입력이 이번 프레임에 들어왔는가.
    /// 좌클릭은 공격 버튼이기도 하지만, 연출 구간에는 게이트가 공격을 막아 두므로 새어 나가지 않는다.</summary>
    static bool SkipInputThisFrame()
    {
        if (IntroTextSequence.KeyPressedThisFrame(Key.F)) return true;
        var m = Mouse.current;
        return m != null && m.leftButton.wasPressedThisFrame;
    }

    /// <summary>연출 효과음 1회 재생.
    ///
    /// Unity 오디오는 원래 <see cref="Time.timeScale"/>의 영향을 받지 않는다(DSP 클럭으로 돈다) —
    /// 그래서 시간 정지 구간에서도 정상 속도·정상 음정으로 들린다. 다만 그 독립성이 깨질 수 있는
    /// 경로가 둘 있어 <see cref="EnsureTimeIndependentAudio"/>에서 못을 박아 둔다
    /// (사용자 지시 2026-08-12 "사운드들이 시간의 영향을 안 받게").</summary>
    void PlaySfx(AudioClip clip)
    {
        if (sfxSource != null && clip != null) sfxSource.PlayOneShot(clip);
    }

    /// <summary>이 씬의 오디오 소스가 시간·일시정지에 끌려다니지 않게 고정한다.
    ///   · <c>pitch = 1</c> — 어디선가 timeScale을 음정에 곱하는 코드가 생겨도 여기서 되돌린다.
    ///   · <c>ignoreListenerPause = true</c> — 다른 시스템이 AudioListener.pause를 켜도 이 소리는 계속 난다.
    /// (Time.timeScale 자체는 원래 오디오에 영향을 주지 않는다 — Play 실측으로 확인.)</summary>
    void EnsureTimeIndependentAudio()
    {
        AudioListener.pause = false;
        var sources = new AudioSource[] { sfxSource, panel != null ? panel.sfxSource : null };
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null) continue;
            sources[i].pitch = 1f;
            sources[i].ignoreListenerPause = true;
        }
    }

    /// <summary>HUD는 Screen Space **Overlay**라 이 씬의 UI 캔버스(Camera 모드 — 블룸을 받으려고
    /// 그렇게 뒀다)보다 항상 위에 그려진다. 암전 중에는 검은 화면 위에 HUD만 떠 있게 되므로 직접 끈다.</summary>
    void SetHudVisible(bool visible)
    {
        if (PlayerHudUI.Instance != null) PlayerHudUI.Instance.SetVisible(visible);
    }

    IEnumerator SucceedRoutine(TutorialStepData s)
    {
        stepActive = false;
        TestLog.Assert($"tutorial_{s.id}", true, "success");

        // 초월·폭주는 "광원 수치가 곧 상태"라 성공 순간 광원을 되돌려야 상태가 풀린다(사용자 스펙).
        if (s.successEnergyPercent >= 0)
            player.currentEnergy = Mathf.Clamp(
                Mathf.RoundToInt(player.maxEnergy * s.successEnergyPercent / 100f), 0, player.maxEnergy);

        // 성공한 순간부터 입력 제한(사용자 스펙). 게이트는 "시작"만 막으므로 이미 돌고 있는
        // 연출(회피-카운터 돌진 · 처형 · 일섬)은 그대로 끝까지 재생된다.
        TutorialGate.Allowed = TutorialAbility.None;

        // 그 연출들은 실시간(unscaled)으로 도는 구간이라, 여기서 시간을 멈춰도 안 멈춘다 —
        // 자기 시퀀스가 끝날 때까지 기다렸다가 정지시킨다.
        while (player.IsDodgeCountering || player.IsExecuting || player.IsIlseomActive)
            yield return null;

        // ⚠️ 여기서 먼저 Freeze()하면 안 된다 — timeScale=0이라 적의 사망 연출·히트 이펙트가
        //    그 자리에서 얼어붙은 채 암전으로 넘어간다. **시간이 흐르는 채로** 기다려 연출이
        //    끝나게 둔 뒤에 정지시킨다(사용자 지시 2026-08-12 "달성 후 1초간 대기").
        yield return Wait(successHold);
        Freeze();
    }

    IEnumerator FailRoutine(TutorialStepData s)
    {
        stepActive = false;
        TestLog.Assert($"tutorial_{s.id}", false, "failed - reset");

        Freeze();
        yield return FadeToBlack();
        yield return Wait(failBlackHold);

        SetupStep(s);   // 완전 암전 상태에서 상황 초기화 — 순간이동이 화면에 보이지 않게

        yield return Fade(0f, fadeOutDuration);
        ScreenGlitchFx.End(ScreenGlitchFx.Source.Cutscene);

        Resume(s);
    }

    // ── 상태 전환 ────────────────────────────────────────────────────────────────────────────

    void Freeze()
    {
        frozen = true;
        stepActive = false;
        ClearLingeringVfx();
        Time.timeScale = 0f;
        TutorialGate.Allowed = TutorialAbility.None;
        TutorialGate.Invulnerable = true;   // 멈춘 동안 새어 들어오는 피해까지 확실히 막는다
        TutorialGate.NoCrit = false;
    }

    /// <summary>화면에 떠 있는 일회성 전투 VFX를 지운다 — 시간을 멈추기 **직전**에 부른다.
    ///
    /// 이 VFX들은 각자 스케일 시간으로 수명을 센다. Update는 timeScale = 0에서도 돌지만 deltaTime이
    /// 0이라 타이머가 아예 안 늘어, 연출 구간에 들어가는 순간 화면에 얼어붙은 채 영영 남는다
    /// (사용자 리포트 2026-08-12 "전투학습이 종료된 시점에 일부 이펙트 스프라이트가 남아있음").
    ///
    /// ⚠️ 각 VFX의 시간축을 unscaled로 바꾸는 방식으로는 고칠 수 없다. 특히 <see cref="DashAfterImage"/>는
    ///    회피-카운터의 슬로우모션(timeScale 0.15) 동안 궤적을 보여주는 게 목적이라, 실시간으로 세면
    ///    6배 빨리 사라져 연출이 통째로 깨진다. 그래서 시간축은 그대로 두고 구간이 바뀌는 이 한 자리에서 지운다.
    ///    (수명을 실시간으로 바꿔도 되는 것들 — HitVfxAutoReturn · DamageText — 은 이미 각자 고쳤고,
    ///     여기서는 그 사이에 갓 태어난 것까지 확실히 정리하는 안전망 역할을 한다.)</summary>
    void ClearLingeringVfx()
    {
        foreach (var v in FindObjectsByType<DashAfterImage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Destroy(v.gameObject);
        foreach (var v in FindObjectsByType<HitVfxAutoReturn>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Destroy(v.gameObject);
        foreach (var v in FindObjectsByType<DamageText>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Destroy(v.gameObject);
    }

    void Resume(TutorialStepData s)
    {
        TutorialGate.Allowed = s.allowed;
        TutorialGate.Invulnerable = s.invulnerable;
        TutorialGate.NoCrit = s.noCrit;

        frozen = false;
        Time.timeScale = 1f;
        SetHudVisible(true);
        skipPending = false;   // 연출이 끝난 뒤 남은 입력이 다음 구간 첫 효과를 집어삼키지 않게 한다

        CaptureBaselines();
        stepActive = true;
        TestLog.Event("tutorial", $"{s.id} playable allowed={s.allowed}");
    }

    void SetupStep(TutorialStepData s)
    {
        ClearEnemies();

        // 앞 구간의 잔재(패링 실드·차지·쿨타임·입력 버퍼·자아)를 먼저 지운다 —
        // 남아 있으면 다음 구간의 첫 공격을 실드가 대신 막거나, 누르지도 않은 동작이 시작된다.
        player.ResetTransientCombatState();

        if (s.spawnPoint != null) Teleport(s.spawnPoint.position);

        player.currentHealth = s.startHealth > 0
            ? Mathf.Clamp(s.startHealth, 1, player.maxHealth)
            : player.maxHealth;
        player.currentEnergy = Mathf.Clamp(
            Mathf.RoundToInt(player.maxEnergy * s.startEnergyPercent / 100f), 0, player.maxEnergy);

        SpawnEnemies(s);
        CaptureBaselines();
    }

    void CaptureBaselines()
    {
        usedDash = false;
        usedTimeAccel = false;
        usedIlseom = false;
        releasedLightSpend = false;
        wasSpendingLight = false;
        egoZeroTimer = 0f;
        baseParry = player.ParrySuccessCount;
        baseDodgeCounter = player.DodgeCounterSuccessCount;
        baseExecution = player.ExecutionCount;
    }

    void Teleport(Vector3 pos)
    {
        var rb = player.GetComponent<Rigidbody2D>();
        player.transform.position = pos;
        if (rb != null) { rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f; }
        // 시간이 멈춰 있으면 물리 스텝이 안 돌아 콜라이더 위치가 갱신되지 않는다 — 즉시 동기화.
        Physics2D.SyncTransforms();
        if (sectionCamera != null) sectionCamera.SnapToTarget();
    }

    // ── 적 ───────────────────────────────────────────────────────────────────────────────────
    // 죽은 DummyEnemy는 dead 플래그가 남아 다시 켜도 되살아나지 않는다 → 매번 새로 만들고 지운다.

    void SpawnEnemies(TutorialStepData s)
    {
        if (s.enemyPrefab == null || s.enemySpawns == null) return;

        for (int i = 0; i < s.enemySpawns.Length; i++)
        {
            Transform t = s.enemySpawns[i];
            if (t == null) continue;

            GameObject go = Instantiate(s.enemyPrefab, t.position, Quaternion.identity);
            go.name = s.id + "-Enemy" + i;
            DummyEnemy e = go.GetComponent<DummyEnemy>();
            if (e == null) { Destroy(go); continue; }

            // Awake가 currentHp = maxHp로 채운 뒤라 순서대로 덮어쓴다.
            e.maxHp = Mathf.Max(1, s.enemyMaxHp);
            e.currentHp = s.enemyStartHp > 0 ? Mathf.Clamp(s.enemyStartHp, 1, e.maxHp) : e.maxHp;
            AttachEnemyGlow(go);
            enemies.Add(e);
        }
    }

    /// <summary>적 뒤에 같은 실루엣을 조금 크게 깔아 테두리만 빛나게 한다(지형 글로우와 같은 기법).
    ///
    /// ⚠️ 전면 교체(2026-08-12). 예전에는 적 뒤에 붉은 실루엣을 조금 크게 깔아 **테두리**를 냈는데,
    ///    그 기법은 "빛남"과 "상시 붉은 테두리"를 분리할 수 없다 — 사용자가 테두리 상시 노출을 버그로
    ///    리포트했고, 빛남만 남기려면 기법 자체를 바꿔야 했다. 이제 본체를 직접 밝힌다.
    ///
    /// 스프라이트 색(sr.color)을 HDR로 올리는 방식은 여전히 못 쓴다 — <see cref="DummyEnemy"/>가 피격
    /// 점멸을 위해 원래 색을 캐시해 두고 점멸이 끝날 때마다 되돌려 놓아 첫 피격에 날아간다. 대신
    /// **머티리얼 유니폼(_Color)** 을 쓴다: sr.color와 곱해지는 별개 값이라 점멸이 건드리지 않고,
    /// 덤으로 빌드에서 per-renderer 색이 Color32 정점 색으로 구워지며 1.0에 잘리는 함정도 피한다
    /// (맵 글로우 40개에서 쓴 것과 같은 해법). 배율이므로 적의 원래 색이 그대로 밝아진다 — 붉게
    /// 물들지 않는다. 하나를 공유해 배칭도 유지된다.
    ///
    /// (<c>enemyGlowScale</c> · <c>enemyGlowColor</c>는 옛 테두리 기법 전용이라 이제 쓰이지 않는다.
    ///  씬에 직렬화돼 있어 남겨 뒀다 — 정리는 별도 승인 후.)</summary>
    void AttachEnemyGlow(GameObject enemy)
    {
        SpriteRenderer body = enemy.GetComponent<SpriteRenderer>();
        if (body == null || enemyGlowBoost <= 1f) return;

        // 교체 전에 원본 머티리얼을 잡아 둔다 — 아래에서 본체를 바꾸고 나면 비교 기준이 사라진다.
        Material source = body.sharedMaterial;

        if (enemyGlowMat == null)
        {
            enemyGlowMat = new Material(source) { hideFlags = HideFlags.DontSave };
            enemyGlowMat.SetColor("_Color", new Color(enemyGlowBoost, enemyGlowBoost, enemyGlowBoost, 1f));
        }

        // 본체뿐 아니라 창 등 자식 스프라이트도 같이 빛나게 한다(사용자 지시 2026-08-12).
        // 원본과 같은 머티리얼을 쓰는 것만 교체한다 — 나중에 전용 셰이더를 쓰는 자식이 생겨도 안 덮어쓴다.
        var renderers = enemy.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i].sharedMaterial == source) renderers[i].sharedMaterial = enemyGlowMat;
    }

    void ClearEnemies()
    {
        for (int i = 0; i < enemies.Count; i++)
            if (enemies[i] != null) Destroy(enemies[i].gameObject);
        enemies.Clear();
    }

    bool AllEnemiesDead()
    {
        if (enemies.Count == 0) return false;
        for (int i = 0; i < enemies.Count; i++)
            if (enemies[i] != null && enemies[i].IsAlive) return false;
        return true;
    }

    // ── 판정 ─────────────────────────────────────────────────────────────────────────────────

    void Update()
    {
        // 스킵 감지는 stepActive와 무관하게 매 프레임 돈다 — 건너뛸 대상이 바로 그 "연출 구간"이다.
        // wasPressedThisFrame은 한 프레임만 참이라, 지금 도는 효과가 집어갈 때까지 여기 담아 둔다.
        if (!stepActive && SkipInputThisFrame()) skipPending = true;

        if (!stepActive) return;

        // 순간에 끝나는 동작은 상승/하강 엣지를 여기서 잡아 둔다.
        if (player.IsDashing) usedDash = true;
        if (player.IsTimeAccelActive) usedTimeAccel = true;
        if (player.IsIlseomActive) usedIlseom = true;

        bool spending = player.IsSpendingLight;
        if (wasSpendingLight && !spending) releasedLightSpend = true;
        wasSpendingLight = spending;

        TutorialStepData s = CurrentStepIndex >= 0 && CurrentStepIndex < steps.Length ? steps[CurrentStepIndex] : null;
        if (s != null && s.egoDepletedFailSeconds > 0f)
            egoZeroTimer = player.currentEgo <= 0 ? egoZeroTimer + Time.unscaledDeltaTime : 0f;
    }

    void LateUpdate()
    {
        // 회피-카운터·히트스톱 코루틴의 finally가 자기 기준값(1)으로 timeScale을 되돌릴 수 있다 —
        // 멈춰 있어야 하는 구간에서는 매 프레임 다시 눌러 확실히 정지 상태를 지킨다.
        if (frozen && Time.timeScale != 0f) Time.timeScale = 0f;
    }

    bool IsSucceeded(TutorialStepData s)
    {
        switch (s.goal)
        {
            case TutorialGoal.None: return true;
            case TutorialGoal.ReachZone: return InSuccessZone(s);
            case TutorialGoal.KillEnemies: return AllEnemiesDead();
            case TutorialGoal.Dash: return usedDash;
            case TutorialGoal.DodgeCounter: return player.DodgeCounterSuccessCount > baseDodgeCounter;
            case TutorialGoal.Parry: return player.ParrySuccessCount > baseParry;
            case TutorialGoal.TimeAccelKill: return usedTimeAccel && AllEnemiesDead();
            case TutorialGoal.IlseomKill: return usedIlseom && AllEnemiesDead();
            case TutorialGoal.LightSpendRelease: return releasedLightSpend;
            case TutorialGoal.Execution: return player.ExecutionCount > baseExecution;
            case TutorialGoal.TranscendKill: return AllEnemiesDead();
            case TutorialGoal.RampageKill: return AllEnemiesDead();
        }
        return false;
    }

    bool IsFailed(TutorialStepData s)
    {
        if (player.transform.position.y < s.failBelowY) return true;
        if (s.egoDepletedFailSeconds > 0f && egoZeroTimer >= s.egoDepletedFailSeconds) return true;
        // "그 동작을 안 쓰고 적을 처치했다" — 배우라고 만든 상황을 우회한 것이므로 실패.
        if (s.goal == TutorialGoal.TimeAccelKill && !usedTimeAccel && AllEnemiesDead()) return true;
        if (s.goal == TutorialGoal.IlseomKill && !usedIlseom && AllEnemiesDead()) return true;
        return false;
    }

    bool InSuccessZone(TutorialStepData s)
    {
        if (s.successZone == null) return false;
        if (!player.IsGrounded) return false;   // "잘 착지" — 지나가는 중이 아니라 발을 디딘 상태여야 한다
        return s.successZone.OverlapPoint(player.transform.position);
    }

    /// <summary>스킵을 받을 수 있는 실시간 대기. WaitForSecondsRealtime은 중간에 끊을 수 없어
    /// 직접 센다(첫 프레임 델타 폭주 대비 클램프도 같이 — ScreenBlackout 선례).</summary>
    IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (ConsumeSkip()) yield break;
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            yield return null;
        }
    }

    /// <summary>구간을 플레이하는 동안 화면이 간헐적으로 잠깐 깨진다(사용자 지시 2026-08-12).
    ///
    /// 연출 구간(암전·설명 패널)에는 켜지 않는다 — 거기선 이미 <see cref="ScreenGlitchFx.Source.Cutscene"/>가
    /// 돌고 있어 겹치면 "간헐적"이 아니라 계속 깨져 보인다. 원인 플래그를 Tutorial로 따로 두어
    /// 컷신 글리치가 끝나면서 이쪽까지 같이 꺼지는 일이 없게 했다.</summary>
    IEnumerator AmbientGlitchRoutine()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(Random.Range(glitchInterval.x, glitchInterval.y));
            if (!stepActive) continue;

            ScreenGlitchFx.Begin(ScreenGlitchFx.Source.Tutorial);
            PlaySfx(ambientGlitchSfx);
            yield return new WaitForSecondsRealtime(Random.Range(glitchFlash.x, glitchFlash.y));
            ScreenGlitchFx.End(ScreenGlitchFx.Source.Tutorial);
        }
    }

    // 연출 도중 씬이 바뀌거나 Play가 멈춰도 전역 상태(시간·게이트·암전·노이즈)가 남지 않게 한다.
    void OnDisable()
    {
        TutorialGate.ResetAll();
        Time.timeScale = 1f;
        ScreenGlitchFx.End(ScreenGlitchFx.Source.Cutscene);
        ScreenGlitchFx.End(ScreenGlitchFx.Source.Tutorial);
        if (blackout != null) blackout.Set(0f);
        if (panel != null) panel.HideImmediate();
        SetHudVisible(true);

        // 런타임에 만든 공유 머티리얼은 씬을 나가도 자동으로 정리되지 않는다.
        if (enemyGlowMat != null)
        {
            if (Application.isPlaying) Destroy(enemyGlowMat); else DestroyImmediate(enemyGlowMat);
            enemyGlowMat = null;
        }
    }
}
