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
    enum AiState { Chase, Windup, Thrust, Recover, Hitstun }

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
    public float thrustDuration = 0.12f;
    public float recoverDuration = 0.2f;
    public float attackCooldown = 0.6f;
    public int attackDamage = 8;
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
    Color baseColor;
    bool baseCaptured;
    float flashTimer;
    bool dead;
    int playerHitMask; // Player + PlayerInvincible — 대시 무적 중(레이어 스왑)에도 찌르기가 플레이어를 감지하게

    AiState state = AiState.Chase;
    float stateTimer;
    float attackCooldownCounter;
    bool attackHitDone;      // 이번 찌르기의 판정이 종결됐는지(회피로 소비됐거나 피해가 확정됨)
    float attackClock;       // Thrust 시작 기준 경과 시간 — 판정 창이 Recover까지 넘어갈 수 있어 상태와 별개로 셈
    float knockbackRemaining; // 남은 넉백 거리(부호=방향). 0이면 넉백 중 아님
    float knockbackSpeed;
    bool knockbackActive;
    float spawnX;
    Vector3 lastPlayerPos; // 터널링 방지 스윕 체크용(빠른 대시가 한 프레임 사이에 판정원을 통과하는 것 방지)

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
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
        if (player != null) lastPlayerPos = player.position; // 첫 프레임부터 유효한 값 보장
    }

    void Update()
    {
        if (dead) return;

        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f && baseCaptured) sr.color = baseColor;
        }
        if (attackCooldownCounter > 0f) attackCooldownCounter -= Time.deltaTime;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
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
            float t = windupDuration > 0f ? Mathf.Clamp01(stateTimer / windupDuration) : 1f;
            SetSpearLocalPos(Vector2.Lerp(spearIdleLocalPos, spearWindupLocalPos, t));
            // 판정은 Thrust(창을 앞으로 찌르는 순간)에서만 — Windup(예비동작) 중 체크는 되돌림(사용자
            // 피드백: 애니메이션이 "시작되는" 순간부터 판정돼버려 너무 이름. 스펙: 찌르는 순간에만 판정).
            if (stateTimer >= windupDuration)
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

        pc.TakeDamage(attackDamage);
        CombatFx.SpawnDamageText(damageTextPrefab, pc.transform.position, attackDamage, damageTextColor);
        TestLog.Event("dummy_attack", $"hit_player dmg={attackDamage}");
    }

    // ── 처형(Execution) 연동 ──────────────────────────────────────────────────────────────
    // 체력이 maxHp의 20% 이하이면 커서 호버로 처형할 수 있다.
    // 임계값은 PlayerController.executionHpThreshold에서 제어하지만, 이 프로퍼티는 하드코딩된
    // 0.2f를 기본값으로 쓴다 — PlayerController가 외부에서 재검증하므로 여기서의 값은 빠른 필터링용.
    public bool IsExecutable => !dead && currentHp > 0 && (float)currentHp / maxHp <= 0.2f;

    // ── 패링(PlayerController.TryParry) 연동 ────────────────────────────────────────────────
    // 스펙 3: 패링이 성립하려면 (공격 모션 중) + ((B) 아직 그 공격에 맞지 않았거나 | (A) 대시 회피
    // 인정 창이 열려 있음). 두 조건을 따로 물어볼 수 있게 상태를 두 개로 쪼개 노출한다 —
    // (A)만 만족하는 경우란 "대시 무적으로 이미 흘려낸 공격을 그 유예 중에 되받아치는" 상황이다.
    public bool IsAttacking =>
        !dead && (state == AiState.Windup || state == AiState.Thrust || state == AiState.Recover);
    public bool IsAttackUnresolved => !attackHitDone;

    // 적의 "공격 범위" = ResolveThrustWindow가 실제로 쓰는 판정원(창끝 중심, 반지름 hitRadius).
    public Vector2 AttackHitPoint => HitPoint();
    public float AttackHitRadius => hitRadius;

    // 패링 성공 — 이번 찌르기를 판정 종결 처리해 피해가 확정되지 않게 한다(스펙 5).
    // 창 모션은 그대로 마저 재생된다(넉백·히트스턴 없음 — 스펙에 없는 동작을 추가하지 않는다).
    public void ConsumeParry()
    {
        attackHitDone = true;
        TestLog.Event("dummy_attack", "parried_by_player");
    }

    // 판정 기준점 = 창이 최대로 뻗었을 때의 창 끝 위치(월드). 창의 "현재" 위치를 쓰면 판정 창이
    // Recover까지 이어질 때 이미 회수된 창 위치로 검사하게 돼 빗나가므로, 뻗은 지점으로 고정한다.
    Vector2 HitPoint()
    {
        return transform.TransformPoint(spearThrustLocalPos);
    }

    PlayerController FindPlayerAtHitPoint()
    {
        Vector2 hitPoint = HitPoint();
        // 대시 무적 중엔 플레이어가 PlayerInvincible 레이어라 Player 마스크로는 안 잡힘 → 두 레이어 모두 감지.
        // (쿼리는 excludeLayers 영향 없음 — 대시로 적을 통과하는 중에도 감지됨)
        Collider2D hit = Physics2D.OverlapCircle(hitPoint, hitRadius, playerHitMask);
        if (hit != null)
        {
            PlayerController found = hit.GetComponent<PlayerController>();
            if (found != null) return found;
        }

        // 터널링 방지: 빠른 대시(연장 대시 등)는 판정원을 한 프레임 사이에 그냥 통과해버려 위 단일 시점
        // OverlapCircle이 아예 못 잡는 경우가 있었음("F키를 눌러도 씹힘" 사용자 리포트) → 지난 프레임
        // 위치부터 이번 프레임 위치까지 이은 선분이 판정원과 스쳤는지도 함께 확인(스윕 체크).
        if (player == null) return null;
        Vector2 segStart = lastPlayerPos;
        Vector2 segEnd = player.position;
        Vector2 segDir = segEnd - segStart;
        float segLenSq = segDir.sqrMagnitude;
        float tParam = segLenSq > 0.0001f ? Mathf.Clamp01(Vector2.Dot(hitPoint - segStart, segDir) / segLenSq) : 0f;
        Vector2 closest = segStart + segDir * tParam;
        if (Vector2.Distance(hitPoint, closest) <= hitRadius)
            return player.GetComponent<PlayerController>();
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

    // 몸 전체를 좌우 반전 — 창은 자식이라 부모 스케일 반전에 따라 자동으로 반대쪽을 향하게 된다.
    void FaceDirection(float dir)
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
    public void TakeDamage(int damage, float knockbackDistance)
    {
        if (dead || damage <= 0) return;

        if (!baseCaptured) { baseColor = sr.color; baseCaptured = true; }

        currentHp -= damage;
        sr.color = flashColor;
        flashTimer = flashDuration;
        TestLog.Event("dummy_damage", $"hp={currentHp}/{maxHp} dmg={damage}");

        if (currentHp <= 0) { Die(); return; }

        bool wasAttacking = state == AiState.Windup || state == AiState.Thrust || state == AiState.Recover;
        if (wasAttacking)
        {
            SetSpearLocalPos(spearIdleLocalPos);
            attackCooldownCounter = attackCooldown;
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
    }

    void Die()
    {
        dead = true;
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
        TestLog.Event("dummy_damage", "respawned");
    }
}
