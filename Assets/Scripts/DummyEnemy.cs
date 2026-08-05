using UnityEngine;
using System.Collections;

// 테스트용 더미 몬스터: 플레이어를 추적하다 사거리 안이면 멈춰서 창 찌르기 공격.
// 몸/창은 Unity 기본 Square 스프라이트 2개(몸=정사각형, 창=늘린 직사각형)로 구성, 애니메이션은
// 코드로 창의 localPosition을 windup(뒤로 뺌)->thrust(앞으로 찌름)->recover(복귀)로 트윈해서 만든다.
// 공격 판정은 thrust 구간에서만 창 끝 위치 기준 OverlapCircle로 이루어진다.
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class DummyEnemy : MonoBehaviour
{
    protected enum AiState { Chase, Windup, Thrust, Recover, Hitstun }

    [Header("Health")]
    public int maxHp = 20;
    public int currentHp;

    [Header("Hit Flash")]
    public Color flashColor = Color.white;
    public float flashDuration = 0.08f;

    [Header("Death")]
    public float respawnDelay = 0f; // >0이면 이 시간 뒤 자동 부활(반복 테스트용), 0이면 죽은 채 유지(비활성)

    [Header("Chase AI")]
    public float moveSpeed = 3f;
    // 이 거리 이하로 플레이어가 들어오면 멈추고 공격.
    // 창의 실제 도달거리 = spearThrustLocalPos.x(1.9) × 몸 스케일(1.2) = 2.28. 예전 값 1.8은 이보다
    // 짧아서, 적이 멈춘 뒤 찌르면 창끝이 플레이어를 0.48만큼 "관통해" 뒤쪽에 꽂혔다. 몸통 콜라이더가
    // 넓어(1.3) 피격 판정 자체는 났지만, 플레이어의 1타 공격 범위(앞쪽 0.2~1.8)와는 반대편이라
    // 패링 판정(스펙 3: 적 공격 범위 ∩ 1타 히트박스)이 수학적으로 절대 성립할 수 없었다
    // — 겹치려면 거리 ≥ 1.98이 필요한데 공격 자체가 ≤1.8에서만 시작됐다(실측 2026-07-25).
    // 창끝이 플레이어 몸 앞쪽에 닿는 거리로 맞춰 둘 다 정상 동작하게 한다(패링 유효 거리 1.98~2.4).
    public float attackRange = 2.4f;
    public Transform player;         // 비워두면 "Player" 태그로 자동 탐색
    // 스폰 지점 기준 이 거리보다 더 쫓아가지 않음 — 리쉬 밖에서도 계속 쫓다가 맵 경계를 넘어가면
    // 바닥이 없는 곳으로 떨어져(중력 gravityScale=1) 무한히 낙하해 사실상 영구 소실되는 버그가 있었음
    // (플레이어를 아주 멀리 보낸 뒤 재현 확인: 맵 밖에 두자마자 y=-91까지 즉시 낙하, 회복 수단 없음).
    public float leashRange = 12f;

    [Header("Spear Thrust Attack")]
    public Transform spear;
    public Vector2 spearIdleLocalPos = new Vector2(0.9f, 0f);
    public Vector2 spearWindupLocalPos = new Vector2(0.3f, 0f);
    public Vector2 spearThrustLocalPos = new Vector2(1.9f, 0f);
    public float windupDuration = 0.25f;
    // 초월 중엔 Windup을 늘려 "공격 방향·범위를 더 일찍 확정짓고 보여준다"(사용자 지시 2026-08-02).
    // Windup 시작 시 이미 위치·방향이 고정되므로(AttackLogic의 정지 + StartAttack의 FaceDirection 1회
    // 호출, 그 뒤로는 안 바뀜) 늘어난 시간만큼 그대로 "더 일찍 확정된 진짜 판정원"이 보이는 시간이 된다 —
    // 새 예비 단계를 만들 필요 없이 기존 Windup 길이만 늘리면 된다.
    public float transcendWindupMultiplier = 2.5f;
    public float thrustDuration = 0.12f;
    public float recoverDuration = 0.2f;
    public float attackCooldown = 0.6f;
    // 플레이어 체력이 수치가 아니라 "갯수"(칸)로 바뀌면서 단위가 달라졌다 — 이 값은 한 번 찔렀을 때
    // 깎이는 칸 수다(옛 이름 attackDamage=8은 100 스케일 기준이라 이름과 함께 폐기).
    public int playerDamageCount = 1;
    public float hitRadius = 0.5f;
    // 공격 판정 시점 = Thrust(창을 앞으로 뻗는) 애니메이션의 이 지점(0~1). 스펙: "찌르기가 거의
    // 마무리되는 순간". 예전엔 Thrust 진입 첫 프레임(t=0, 창이 아직 몸 근처)부터 매 프레임 판정해서
    // 창이 뻗기도 전에 맞는 이상한 판정이 났다.
    [Range(0f, 1f)] public float thrustHitNormalized = 0.85f;
    // 회피(대시-카운터) 인정 창 — 위 판정 순간을 기준으로 앞뒤로 열린다(스펙: "판정 직후 및 약간 전").
    // 피해는 창이 닫히는 순간(hitTime + dodgeWindowPost)에 확정된다 — "직후"에 들어온 회피까지
    // 유효하게 인정하려면 그 시간만큼 피해 확정을 미루는 수밖에 없기 때문. 대신 post를 짧게(0.05s)
    // 잡아 피해가 확정되는 시점에도 창이 아직 거의 다 뻗은 상태로 보이게 한다
    // (thrust 0.12s 기준: 판정 0.102s, 피해 확정 0.152s = 창이 16%만 회수된 시점).
    public float dodgeWindowPre = 0.12f;
    public float dodgeWindowPost = 0.05f;
    public LayerMask playerLayer; // "Player"만 포함 — 대시 무적 중엔 gameObject.layer가 PlayerInvincible로 바뀌어 자동으로 빗나감

    // 히트 스파크(Hit 프리팹)는 플레이어가 때릴 때만 뜨므로(사용자 스펙) 여기엔 VFX 프리팹 필드가 없다 —
    // 적이 플레이어를 맞췄을 때는 데미지 텍스트만 띄운다.
    [Header("Hit 표시 (플레이어를 맞췄을 때)")]
    public GameObject damageTextPrefab;
    public Color damageTextColor = new Color(1f, 0.3f, 0.3f, 1f); // 플레이어 피격은 붉은 계열로 구분

    [Header("Hitstun / 넉백 (피격 시)")]
    public float hitstunDuration = 0.25f;
    // 플레이어 공격에 맞으면 밀려나는 시간. 이동 거리는 때린 쪽(PlayerController)이
    // "attackLungeDistance × enemyKnockbackMultiplier"로 계산해 넘겨준다.
    public float knockbackDuration = 0.12f;

    SpriteRenderer sr;
    Rigidbody2D rb;
    Collider2D bodyCol;
    Color baseColor;
    bool baseCaptured;
    float flashTimer;
    protected bool dead;
    int playerHitMask; // Player + PlayerInvincible — 대시 무적 중(레이어 스왑)에도 찌르기가 플레이어를 감지하게

    protected AiState state = AiState.Chase;
    protected float stateTimer;
    float attackCooldownCounter;
    bool attackHitDone;      // 이번 찌르기의 판정이 종결됐는지(회피로 소비됐거나 피해가 확정됨)
    float attackClock;       // Thrust 시작 기준 경과 시간 — 판정 창이 Recover까지 넘어갈 수 있어 상태와 별개로 셈
    // 초월 공격 예고(T-3)용 — 이번 공격이 플레이어에 의해 무효화됐는가(패링·회피·피격 리셋).
    // 피해 확정·빗나감은 false로 남는다(터짐과 흐지부지를 구분하는 유일한 신호, PLAN §6 T-3a 참고).
    bool lastAttackNeutralized;
    PlayerController playerController; // 초월 여부 조회용(player Transform과 함께 캐싱)
    // StartAttack() 시점에 한 번 확정되는 이번 공격의 실제 Windup 길이 — 초월 중이면 늘어난다.
    // 도중에 초월이 풀려도 이미 시작된 공격의 길이는 바뀌지 않는다(적이 "이미 확정"했으므로).
    float effectiveWindupDuration = 0.25f;
    float knockbackRemaining; // 남은 넉백 거리(부호=방향). 0이면 넉백 중 아님
    float knockbackSpeed;
    bool knockbackActive;
    float spawnX;
    Vector3 lastPlayerPos; // 터널링 방지 스윕 체크용(빠른 대시가 한 프레임 사이에 판정원을 통과하는 것 방지)

    protected virtual void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        bodyCol = GetComponent<Collider2D>();
        currentHp = maxHp;
        spawnX = transform.position.x;

        if (spear == null) spear = transform.Find("Spear");
        if (spear != null) spear.localPosition = spearIdleLocalPos;
        if (playerLayer.value == 0) playerLayer = LayerMask.GetMask("Player");
        int invLayer = LayerMask.NameToLayer("PlayerInvincible");
        playerHitMask = playerLayer.value | (invLayer >= 0 ? (1 << invLayer) : 0);

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player != null)
        {
            lastPlayerPos = player.position; // 첫 프레임부터 유효한 값 보장
            playerController = player.GetComponent<PlayerController>();
        }
    }

    protected virtual void Update()
    {
        if (dead) return;

        TickTimers();

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) { player = p.transform; playerController = player.GetComponent<PlayerController>(); }
            else return;
        }

        switch (state)
        {
            case AiState.Hitstun:
                // 넉백 중엔 FixedUpdate가 속도를 관리한다(거리 정확도 보장) — 여기선 건드리지 않음.
                if (!knockbackActive) SetHorizontalVelocity(0f);
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f) { state = AiState.Chase; knockbackRemaining = 0f; }
                break;
            case AiState.Chase:
                ChaseLogic();
                break;
            default: // Windup / Thrust / Recover
                AttackLogic();
                break;
        }

        lastPlayerPos = player.position; // 다음 프레임 스윕 체크용(터널링 방지) — 매 프레임 끝에 갱신
    }

    // 피격 플래시·공격 쿨다운 타이머 갱신. Update()에서 분리해 둔 이유: GehennaHound가 Sleep/Waking
    // 단계(전투 로직 비활성 — base.Update() 미호출)에서도 이 부분만은 매 프레임 돌려야 흰색 피격
    // 플래시가 그 단계 중에도 정상적으로 꺼진다.
    protected void TickTimers()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && baseCaptured) sr.color = baseColor;
        }
        if (attackCooldownCounter > 0f) attackCooldownCounter -= Time.deltaTime;
    }

    // 넉백은 물리 스텝 단위로 "남은 거리"를 깎아가며 밀어낸다. Update에서 속도만 세팅하고 타이머로
    // 끄는 방식은 프레임 길이가 들쭉날쭉하면 마지막 프레임이 통째로 초과 이동해 실제 거리가 요청값의
    // 2~3배까지 늘어났다(실측: 0.45 요청 → 1.21 이동). 스텝마다 남은 거리를 클램프하면 프레임레이트와
    // 무관하게 정확히 요청한 거리만큼만 이동한다.
    void FixedUpdate()
    {
        if (dead) return;
        if (knockbackRemaining == 0f)
        {
            if (knockbackActive) { knockbackActive = false; SetHorizontalVelocity(0f); }
            return;
        }
        knockbackActive = true;
        float maxStep = knockbackSpeed * Time.fixedDeltaTime;
        float step = Mathf.Clamp(knockbackRemaining, -maxStep, maxStep);
        knockbackRemaining -= step;
        if (Mathf.Abs(knockbackRemaining) < 0.0001f) knockbackRemaining = 0f;
        SetHorizontalVelocity(step / Time.fixedDeltaTime);
    }

    void ChaseLogic()
    {
        float dx = player.position.x - transform.position.x;
        float dist = Mathf.Abs(dx);

        if (dist <= attackRange)
        {
            SetHorizontalVelocity(0f);
            if (attackCooldownCounter <= 0f) StartAttack(dx);
            return;
        }

        float dir = Mathf.Sign(dx);

        // 리쉬 범위 밖으로는 더 쫓아가지 않음(맵 경계 밖 낙사 방지). 이미 리쉬 경계에 있고
        // 그 방향으로 더 가려는 중이면 정지 — 반대 방향(복귀)은 항상 허용.
        float distFromSpawn = transform.position.x - spawnX;
        bool wouldLeaveLeash = (distFromSpawn >= leashRange && dir > 0f) || (distFromSpawn <= -leashRange && dir < 0f);
        if (wouldLeaveLeash)
        {
            SetHorizontalVelocity(0f);
            FaceDirection(dir);
            return;
        }

        SetHorizontalVelocity(dir * moveSpeed);
        FaceDirection(dir);
    }

    void StartAttack(float dx)
    {
        state = AiState.Windup;
        stateTimer = 0f;
        attackHitDone = false;
        lastAttackNeutralized = false; // 새 공격 시작 — 지난 공격의 무효화 흔적을 지운다
        // 이번 공격의 Windup 길이를 지금 확정한다(사용자 지시 2026-08-02) — 초월 중이면 늘려서 "공격
        // 방향·범위를 더 일찍 확정짓고 보여준다". 위치·방향도 바로 아래에서 함께 고정되므로, 이 순간부터
        // AttackHitPoint는 Thrust가 끝날 때까지 그대로다.
        effectiveWindupDuration = (playerController != null && playerController.IsTranscending)
            ? windupDuration * transcendWindupMultiplier
            : windupDuration;
        SetHorizontalVelocity(0f);
        FaceDirection(Mathf.Sign(dx));
        TestLog.Event("dummy_attack", "windup_start");
    }

    void AttackLogic()
    {
        SetHorizontalVelocity(0f); // 공격 중엔 반드시 정지
        stateTimer += Time.deltaTime;

        if (state == AiState.Windup)
        {
            // effectiveWindupDuration 기준(초월 중엔 StartAttack에서 늘려 확정한 값) — 창의 실제 이동
            // 애니메이션도 같이 늘어나 늘어난 시간 내내 자연스럽게 "예비동작 중"으로 보인다.
            float t = effectiveWindupDuration > 0f ? Mathf.Clamp01(stateTimer / effectiveWindupDuration) : 1f;
            SetSpearLocalPos(Vector2.Lerp(spearIdleLocalPos, spearWindupLocalPos, t));
            // 판정은 Thrust(창을 앞으로 찌르는 순간)에서만 — Windup(예비동작) 중 체크는 되돌림(사용자
            // 피드백: 애니메이션이 "시작되는" 순간부터 판정돼버려 너무 이름. 스펙: 찌르는 순간에만 판정).
            if (stateTimer >= effectiveWindupDuration)
            {
                state = AiState.Thrust;
                stateTimer = 0f;
                attackClock = 0f;
                attackHitDone = false;
                TestLog.Event("dummy_attack", "thrust_start");
            }
        }
        else if (state == AiState.Thrust)
        {
            float t = thrustDuration > 0f ? Mathf.Clamp01(stateTimer / thrustDuration) : 1f;
            SetSpearLocalPos(Vector2.Lerp(spearWindupLocalPos, spearThrustLocalPos, t));
            attackClock = stateTimer;
            if (!attackHitDone) ResolveThrustWindow();
            if (stateTimer >= thrustDuration)
            {
                state = AiState.Recover;
                stateTimer = 0f;
                TestLog.Event("dummy_attack", "recover_start");
            }
        }
        else // Recover
        {
            float t = recoverDuration > 0f ? Mathf.Clamp01(stateTimer / recoverDuration) : 1f;
            SetSpearLocalPos(Vector2.Lerp(spearThrustLocalPos, spearIdleLocalPos, t));
            // 판정 창(hitTime + dodgeWindowPost)이 Thrust 길이를 넘길 수 있어 Recover에서도 계속 이어서 본다.
            attackClock = thrustDuration + stateTimer;
            if (!attackHitDone) ResolveThrustWindow();
            if (stateTimer >= recoverDuration)
            {
                state = AiState.Chase;
                attackCooldownCounter = attackCooldown;
                TestLog.Event("dummy_attack", "attack_done");
            }
        }
    }

    // 찌르기 판정 타임라인(attackClock = Thrust 시작 기준 경과 시간):
    //   [hitTime - dodgeWindowPre] ── 회피(대시-카운터) 인정 시작
    //   [hitTime = thrustDuration * thrustHitNormalized] ── 창이 거의 다 뻗은 "판정 순간"
    //   [hitTime + dodgeWindowPost] ── 회피 인정 종료 = 이 시점에 피해가 확정된다
    // 피해를 창이 닫히는 순간에 확정하기 때문에 "판정 직후"에 들어온 회피도 유효하다(스펙).
    void ResolveThrustWindow()
    {
        float hitTime = thrustDuration * Mathf.Clamp01(thrustHitNormalized);
        if (attackClock < hitTime - dodgeWindowPre) return; // 아직 창이 안 열림

        PlayerController pc = FindPlayerAtHitPoint();

        // 저스트 닷지: 창이 열려 있는 동안 대시 회피 창(dodgeCounterGraceTimer)이 겹치면 기회로 소비(피해 무효)
        if (pc != null && pc.TryConsumeDodge(this))
        {
            attackHitDone = true;
            lastAttackNeutralized = true; // 회피로 무효화 — 예고 원은 터지지 않고 흐지부지 사라진다
            TestLog.Event("dummy_attack", "dodged_by_player");
            return;
        }

        if (attackClock < hitTime + dodgeWindowPost) return; // 아직 피해 확정 시점 아님(회피 기회 유지)

        attackHitDone = true;
        if (pc == null) return; // 창이 닫히는 순간 사거리 밖 → 빗나감

        // 무적인데 닷지가 소비되지 않음(이미 이 대시에서 발동 등) → 여전히 무적이라 피해 없음
        if (pc.IsInvincible)
        {
            TestLog.Event("dummy_attack", "blocked_iframe");
            return;
        }

        // 패링 성공으로 생긴 구형 실드가 남아 있으면 이번 공격 1회를 대신 막고 깨진다(패링 스펙 6).
        // 무적 판정과 같은 위치에서 걸러야 데미지뿐 아니라 데미지 텍스트도 안 뜬다.
        if (pc.TryConsumeParryShield())
        {
            TestLog.Event("dummy_attack", "blocked_parry_shield");
            return;
        }

        pc.TakeDamage(playerDamageCount);
        CombatFx.SpawnDamageText(damageTextPrefab, pc.transform.position, playerDamageCount, damageTextColor);
        TestLog.Event("dummy_attack", $"hit_player dmg={playerDamageCount}");
    }

    // ── 처형(Execution) 연동 ──────────────────────────────────────────────────────────────
    // "처형 가능한가"의 판정(HP 비율 임계값)은 PlayerController.executionHpThreshold 한 곳에서만 한다.
    // 여기서 상태만 노출하고 임계값은 갖지 않는다 — 예전엔 0.2f가 여기에도 하드코딩돼 있어,
    // 인스펙터에서 임계값을 바꿔도 이쪽 필터가 20%로 먼저 잘라내는 이중 진실 상태였다.
    public bool IsAlive => !dead && currentHp > 0;
    public float HpRatio => maxHp > 0 ? (float)currentHp / maxHp : 0f;

    // ── 패링(PlayerController.TryParry) 연동 ────────────────────────────────────────────────
    // 스펙 3: 패링이 성립하려면 (공격 모션 중) + ((B) 아직 그 공격에 맞지 않았거나 | (A) 대시 회피
    // 인정 창이 열려 있음). 두 조건을 따로 물어볼 수 있게 상태를 두 개로 쪼개 노출한다 —
    // (A)만 만족하는 경우란 "대시 무적으로 이미 흘려낸 공격을 그 유예 중에 되받아치는" 상황이다.
    public bool IsAttacking =>
        !dead && (state == AiState.Windup || state == AiState.Thrust || state == AiState.Recover);
    public bool IsAttackUnresolved => !attackHitDone;

    // 적의 "공격 범위" = ResolveThrustWindow가 실제로 쓰는 판정 캡슐(밑동~창끝 선분, 반지름 hitRadius).
    // 사용자 지시(2026-08-02): "창 전체가 범위" — 창끝 한 점(원)이 아니라 창을 든 위치(밑동)부터
    // 창끝까지 훑는 캡슐로 확장. 이 파일 전체(피격·패링·회피 판정)가 이 두 값을 기준으로 삼는다.
    public Vector2 AttackHitPoint => HitPoint();
    public Vector2 AttackHitPointBase => BasePoint();
    public float AttackHitRadius => hitRadius;

    // 초월 공격 예고(T-3a) — 진행률 0(예비동작 시작)~1(피해가 확정되는 순간). 예고 중이 아니면 -1.
    // ⚠️ Recover는 -1이 아니다 — 피해가 거기서 확정되기 때문(hitTime + dodgeWindowPost가 thrustDuration을
    // 넘는다, PLAN §6 "정정된 타임라인" 참고). 동작 변경 0 — 기존 필드(state·stateTimer·attackClock)를
    // 읽어 계산만 한다.
    public float AttackTelegraphProgress
    {
        get
        {
            if (dead || attackHitDone) return -1f;
            if (state == AiState.Chase || state == AiState.Hitstun) return -1f;

            float hitTime = thrustDuration * Mathf.Clamp01(thrustHitNormalized);
            // effectiveWindupDuration(초월 중이면 늘어난 실제 값)을 쓴다 — windupDuration 원본을 쓰면
            // 초월 중 늘어난 실제 Windup 길이와 진행률이 어긋난다.
            float total = effectiveWindupDuration + hitTime + dodgeWindowPost;
            if (total <= 0f) return -1f;

            // Windup 중엔 attackClock이 아직 갱신 전(0 또는 지난 공격의 잔여값)이라 stateTimer를 직접 쓴다.
            // Thrust/Recover에선 attackClock이 Thrust 시작 기준 경과 시간이라 effectiveWindupDuration만
            // 더하면 된다.
            float elapsed = state == AiState.Windup ? stateTimer : effectiveWindupDuration + attackClock;
            return Mathf.Clamp01(elapsed / total);
        }
    }

    // 이번 공격이 플레이어에 의해 무효화됐는가(패링·회피·피격 리셋). 피해 확정·빗나감은 false로 남는다.
    public bool LastAttackNeutralized => lastAttackNeutralized;

    /// <summary>공격 시작(Windup 진입)부터 **피해가 확정되는 순간**까지의 시간(초).
    /// 초월 중엔 Windup이 늘어난 만큼 이 값도 같이 늘어난다(effectiveWindupDuration은 StartAttack에서 확정).
    /// 공격 애니메이션을 이 타임라인에 맞춰야 하는 쪽이 읽는다(GehennaHound의 Bite 재생속도) — 기존
    /// 필드로 값만 계산하므로 동작 변경 0. ResolveThrustWindow의 피해 확정 조건과 같은 식이다.</summary>
    public float AttackDamageTime =>
        effectiveWindupDuration + thrustDuration * Mathf.Clamp01(thrustHitNormalized) + dodgeWindowPost;

    // 패링 성공 — 이번 찌르기를 판정 종결 처리해 피해가 확정되지 않게 한다(스펙 5).
    // 창 모션은 그대로 마저 재생된다(넉백·히트스턴 없음 — 스펙에 없는 동작을 추가하지 않는다).
    public void ConsumeParry()
    {
        attackHitDone = true;
        lastAttackNeutralized = true; // 패링으로 무효화 — 예고 원은 터지지 않고 흐지부지 사라진다
        TestLog.Event("dummy_attack", "parried_by_player");
    }

    // 판정 기준점(캡슐의 창끝 쪽 끝) = 창이 최대로 뻗었을 때의 창 끝 위치(월드). 창의 "현재" 위치를
    // 쓰면 판정 창이 Recover까지 이어질 때 이미 회수된 창 위치로 검사하게 돼 빗나가므로, 뻗은
    // 지점으로 고정한다.
    protected virtual Vector2 HitPoint()
    {
        return transform.TransformPoint(spearThrustLocalPos);
    }

    // 판정 기준점(캡슐의 밑동 쪽 끝) = 창을 몸 쪽으로 당긴 위치(Windup 자세 — 창을 "들고 있는" 곳에
    // 가장 가깝다). 사용자 지시(2026-08-02)로 창끝 한 점 대신 이 지점부터 창끝까지 훑는 캡슐 전체가
    // 판정 범위가 됐다.
    protected virtual Vector2 BasePoint()
    {
        return transform.TransformPoint(spearWindupLocalPos);
    }

    // 선분 a-b 위에서 p에 가장 가까운 점.
    static Vector2 ClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        Vector2 ab = b - a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.0001f) return a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
        return a + ab * t;
    }

    // 두 선분 사이의 최단 거리 근사(양 끝점 4개를 반대 선분에 투영해 최소값을 취한다) — 수학적으로
    // 완벽한 최소값은 아니지만, 기존의 "점 vs 선분" 스윕 체크보다 항상 같거나 더 넓게 잡아 회귀가
    // 없다. 터널링 방지용 안전망이라 완벽한 최소값보다 "놓치지 않는" 쪽이 중요하다.
    static float SegmentSegmentDistance(Vector2 p1, Vector2 q1, Vector2 p2, Vector2 q2)
    {
        float d = Vector2.Distance(p2, ClosestPointOnSegment(p1, q1, p2));
        d = Mathf.Min(d, Vector2.Distance(q2, ClosestPointOnSegment(p1, q1, q2)));
        d = Mathf.Min(d, Vector2.Distance(p1, ClosestPointOnSegment(p2, q2, p1)));
        d = Mathf.Min(d, Vector2.Distance(q1, ClosestPointOnSegment(p2, q2, q1)));
        return d;
    }

    // 사용자 지시(2026-08-02): "창 전체가 범위" — 창끝 한 점(원) 판정을 밑동~창끝 캡슐(선분 + 반지름
    // hitRadius) 판정으로 확장했다. 브로드 페이즈(캡슐을 감싸는 원)로 후보를 좁힌 뒤, 각 후보에
    // 대해 "세그먼트 위 최근접점 ↔ 콜라이더 표면 최근접점" 거리로 정확히 캡슐-콜라이더 겹침을 본다.
    PlayerController FindPlayerAtHitPoint()
    {
        Vector2 basePt = BasePoint();
        Vector2 tipPt = HitPoint();
        Vector2 mid = (basePt + tipPt) * 0.5f;
        float boundRadius = Vector2.Distance(basePt, tipPt) * 0.5f + hitRadius;

        // 대시 무적 중엔 플레이어가 PlayerInvincible 레이어라 Player 마스크로는 안 잡힘 → 두 레이어 모두 감지.
        // (쿼리는 excludeLayers 영향 없음 — 대시로 적을 통과하는 중에도 감지됨)
        Collider2D[] candidates = Physics2D.OverlapCircleAll(mid, boundRadius, playerHitMask);
        for (int i = 0; i < candidates.Length; i++)
        {
            Collider2D col = candidates[i];
            Vector2 segClosest = ClosestPointOnSegment(basePt, tipPt, col.bounds.center);
            Vector2 colClosest = col.ClosestPoint(segClosest);
            if (Vector2.Distance(segClosest, colClosest) <= hitRadius)
            {
                PlayerController found = col.GetComponent<PlayerController>();
                if (found != null) return found;
            }
        }

        // 터널링 방지: 빠른 대시(연장 대시 등)는 판정 캡슐을 한 프레임 사이에 그냥 통과해버려 위
        // 단일 시점 검사가 아예 못 잡는 경우가 있었음("F키를 눌러도 씹힘" 사용자 리포트) → 지난
        // 프레임 위치부터 이번 프레임 위치까지 이은 선분(플레이어 이동 경로)이 창 캡슐 축과
        // hitRadius 이내로 스쳤는지 두 선분 사이 거리로 확인.
        if (player == null) return null;
        float dist = SegmentSegmentDistance(basePt, tipPt, lastPlayerPos, player.position);
        if (dist <= hitRadius) return player.GetComponent<PlayerController>();
        return null;
    }

    void SetSpearLocalPos(Vector2 pos)
    {
        if (spear != null) spear.localPosition = pos;
    }

    void SetHorizontalVelocity(float vx)
    {
        Vector2 v = rb.linearVelocity;
        v.x = vx;
        rb.linearVelocity = v;
    }

    // HP가 0이 되는 즉시(사망 포즈를 유지하느라 base.Die()/SetActive(false)가 늦게 실행되는 서브클래스가
    // 있어도) 몸 콜라이더와 리지드바디를 꺼서 시체가 계속 맞는 걸 막는다. PlayerController의 모든 타격
    // 판정(OverlapBoxAll/OverlapCircleAll/OverlapPoint, enemyLayer 대상)이 콜라이더 기반이라
    // enabled=false만으로 그 프레임부터 완전히 판정에서 빠진다.
    protected void DisableHitDetection()
    {
        if (bodyCol != null) bodyCol.enabled = false;
        if (rb != null) rb.simulated = false;
    }

    protected void EnableHitDetection()
    {
        if (bodyCol != null) bodyCol.enabled = true;
        if (rb != null) rb.simulated = true;
    }

    // 몸 전체를 좌우 반전 — 창은 자식이라 부모 스케일 반전에 따라 자동으로 반대쪽을 향하게 된다.
    protected virtual void FaceDirection(float dir)
    {
        if (Mathf.Approximately(dir, 0f)) return;
        Vector3 s = transform.localScale;
        float sign = Mathf.Sign(dir);
        s.x = sign * Mathf.Abs(s.x);
        transform.localScale = s;
    }

    public void TakeDamage(int damage) { TakeDamage(damage, 0f); }

    // 데미지 적용: HP 감소 + 흰색 피격 플래시 + 잠깐 정지(Hitstun) + 공격 중이었다면 공격 리셋 + HP 0 시 사망.
    // knockbackDistance: 부호가 방향(+오른쪽/-왼쪽), 크기가 밀려날 거리(유닛). 때린 쪽이 계산해서 넘긴다.
    // 반환값(bool): 이 타격으로 적이 죽었으면 true — C-1(광원 획득)의 "적 처치" 보너스를
    // 호출부(PlayerController.CheckAttackHit)가 Die() 별도 훅 없이 그 자리에서 바로 판단하게 해준다.
    public bool TakeDamage(int damage, float knockbackDistance)
    {
        if (dead || damage <= 0) return false;

        if (!baseCaptured) { baseColor = sr.color; baseCaptured = true; }

        currentHp -= damage;
        sr.color = flashColor;
        flashTimer = flashDuration;
        TestLog.Event("dummy_damage", $"hp={currentHp}/{maxHp} dmg={damage}");

        if (currentHp <= 0) { Die(); return true; }

        bool wasAttacking = state == AiState.Windup || state == AiState.Thrust || state == AiState.Recover;
        if (wasAttacking)
        {
            SetSpearLocalPos(spearIdleLocalPos);
            attackCooldownCounter = attackCooldown;
            // ⚠️ attackHitDone은 세우지 않는다(원래 동작 그대로) — 대신 state가 곧 Hitstun으로 바뀌어
            // AttackTelegraphProgress는 자동으로 -1이 된다. 무효화 플래그는 여기서 별도로 세워야
            // 한다 — 안 그러면 적을 때려 끊은 공격이 "터진 것"처럼 보인다(PLAN §6 T-3a 경고).
            lastAttackNeutralized = true;
            TestLog.Event("dummy_attack", "reset_by_hit");
        }

        state = AiState.Hitstun;
        stateTimer = hitstunDuration;

        // 넉백: 요청한 거리를 knockbackDuration 동안 등속으로 소진(실제 이동은 FixedUpdate가 처리).
        // 속도 기반이라 지형/벽 충돌은 물리가 그대로 막아준다.
        if (!Mathf.Approximately(knockbackDistance, 0f) && knockbackDuration > 0f)
        {
            knockbackRemaining = knockbackDistance;
            knockbackSpeed = Mathf.Abs(knockbackDistance) / knockbackDuration;
        }
        else knockbackRemaining = 0f;

        return false;
    }

    protected virtual void Die()
    {
        dead = true;
        DisableHitDetection();
        currentHp = 0;
        SetHorizontalVelocity(0f);
        TestLog.Event("dummy_damage", "died");
        if (respawnDelay > 0f) StartCoroutine(RespawnAfter(respawnDelay));
        else gameObject.SetActive(false);
    }

    IEnumerator RespawnAfter(float delay)
    {
        sr.enabled = false;
        yield return new WaitForSeconds(delay);
        currentHp = maxHp;
        dead = false;
        state = AiState.Chase;
        SetSpearLocalPos(spearIdleLocalPos);
        if (baseCaptured) sr.color = baseColor;
        sr.enabled = true;
        EnableHitDetection();
        TestLog.Event("dummy_damage", "respawned");
    }
}
