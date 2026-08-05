using UnityEngine;
using System.Collections;

// 게헨나 포식견 (기획안 "전체_몬스터_기획" 참고): Sleep/Lay down Idle(대기) → Wake up ➔ Run(추적) →
// Bite Attack(공격) → Walk Sniff(경계) → Jump/Fall/Land(지형이동) → Out/Dead(사망).
//
// DummyEnemy를 상속해 검 공격 판정·패링·닷지·처형·히트스턴·넉백·HP 로직을 그대로 재사용한다
// (PlayerController의 전투 판정이 DummyEnemy 타입에 고정돼 있어, 상속하지 않으면 플레이어가
// 이 몬스터를 때리거나 패링·처형할 수 없다 — 사용자 승인 하에 DummyEnemy.cs를 상속 가능하도록
// 최소 수정(private→protected virtual)했다).
//
// Sleep/Waking 단계는 DummyEnemy.Update()를 아예 호출하지 않고 자체 로직만 돈다(추적/공격 비활성).
// Aggro 진입 후에는 매 프레임 base.Update()로 DummyEnemy의 Chase/Windup/Thrust/Recover/Hitstun을
// 그대로 실행시키고, 그 결과(state)를 읽어 애니메이터만 동기화한다 — 전투 로직은 손대지 않는다.
//
// 알려진 단순화(범위 밖): "발소리(걷기/달리기)로 발각을 피한다"는 기획 문구는 거리 기반 감지로만
// 근사했다. Jump 클립은 존재하지만 실제 협곡 점프 AI(지형 갭 탐지)는 구현하지 않았다 — Fall/Land는
// 순수 시각 동기화(중력으로 떨어질 때만)로만 동작한다.
[RequireComponent(typeof(Animator))]
public class GehennaHound : DummyEnemy
{
    enum HoundPhase { Sleep, Patrol, Waking, Aggro }

    [Header("Gehenna Hound - Detection")]
    // 이 거리 안에 플레이어가 들어오면 기상(Wake) 시작(Sleep·Patrol 공통).
    public float detectRange = 8f;

    [Header("Gehenna Hound - Wander/Patrol")]
    // 발각되지 않은 채로 이 시간(초, 랜덤 범위) 동안 자고 나면 잠깐 배회한다.
    public float sleepDurationMin = 4f;
    public float sleepDurationMax = 8f;
    // 배회 지속 시간 / 스폰 지점 기준 반경 / 배회 이동 속도(추적보다 느리게).
    public float patrolDuration = 3f;
    public float patrolRadius = 2f;
    public float patrolSpeed = 1f;

    [Header("Gehenna Hound - Move Animation Gate")]
    // 이 속도 이하는 "정지"로 간주해 이동 애니메이션(Run/Walk Sniff) 대신 Stand를 튼다.
    public float moveAnimThreshold = 0.05f;

    [Header("Gehenna Hound - Ground Check")]
    public float groundCheckDistance = 0.15f;
    public LayerMask groundLayer;

    [Header("Gehenna Hound - Death")]
    // 사망 애니메이션(Dead)을 보여주는 시간. 이후 DummyEnemy.Die()가 비활성화/리스폰을 처리한다.
    public float deadPoseDuration = 1.2f;

    [Header("Gehenna Hound - Bite Hitbox")]
    // 씬에 배치한 자식 오브젝트(고정 로컬 위치, 기본 상태=오른쪽을 볼 때 기준). DummyEnemy와 동일하게
    // transform.localScale.x 부호로만 좌우를 뒤집으므로(FaceDirection 오버라이드 없음), 이 자식의
    // 월드 바운드도 부모 스케일을 따라 자동으로 반대편에 미러링된다 — 별도의 좌우 히트박스가 필요 없다.
    public Transform hitboxRight;

    Animator anim;
    Rigidbody2D rb2D;
    Collider2D bodyCollider;
    SpriteRenderer spriteRenderer;
    BoxCollider2D hitboxCollider;

    HoundPhase phase = HoundPhase.Sleep;
    Vector3 anchorPos;
    float sleepTimer;
    float patrolTimer;
    float patrolTargetX;
    float wakeTimer;
    float wakeClipDuration;
    bool wasHoundGrounded = true;
    string currentClip = "";
    // Bite 클립에서 "실제로 물어뜯는" 프레임의 시각(초). BiteHitFrameMarker 이벤트가 찍힌 지점을
    // Awake에서 읽어 둔다 — 아래 재생속도 정렬의 기준점이다(값을 코드에 박아 두면 클립을 재편집할 때
    // 조용히 어긋난다).
    float biteEventTime = 0.5f;

    // ── 피격 흰색 플래시(셰이더 기반) ─────────────────────────────────────────────
    // DummyEnemy.TakeDamage()의 sr.color = flashColor는 SpriteRenderer.color가 텍스처에 곱연산되는
    // 특성상 flashColor=흰색(항등원)일 때 실제 텍스처가 있는 스프라이트에는 아무 효과가 없다
    // (사용자 리포트: "절대 흰색이어야 한다, 스프라이트 렌더러 컬러 아니다"). Custom/SpriteHitFlash
    // 셰이더의 _FlashAmount를 MaterialPropertyBlock으로 직접 구동해 진짜 흰색 오버레이를 만든다.
    // DummyEnemy의 flashColor/flashDuration 필드를 그대로 재사용(새 필드 없이 타이밍·색 통일).
    static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    MaterialPropertyBlock flashBlock;
    AiState lastHitFlashState = AiState.Chase;
    float hitFlashTimer;
    bool flashApplied;

    protected override void Awake()
    {
        base.Awake();
        anim = GetComponent<Animator>();
        rb2D = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (hitboxRight != null) hitboxCollider = hitboxRight.GetComponent<BoxCollider2D>();
        if (groundLayer.value == 0) groundLayer = LayerMask.GetMask("Ground");

        flashBlock = new MaterialPropertyBlock();
        biteEventTime = GetClipEventTime("GehennaHound-Bite", "BiteHitFrameMarker", biteEventTime);
        anchorPos = transform.position;
        moveCheckStartX = transform.position.x;
        EnterSleep();
    }

    protected override void Update()
    {
        if (!IsAlive) return; // Die()가 사망 연출/비활성화를 별도로 처리한다.

        UpdateHitFlash(); // phase와 무관하게 항상 — Sleep/Patrol 중 기습당해도 흰색 플래시는 켜져야 한다.
        TickMovementCheck();

        if (phase == HoundPhase.Aggro)
        {
            base.Update(); // DummyEnemy의 Chase/Windup/Thrust/Recover/Hitstun 전투 로직 그대로(TickTimers 포함).
            SyncAggroAnimation();
        }
        else
        {
            // Sleep/Patrol/Waking 중엔 전투 로직(base.Update)을 안 돌리지만, 피격 흰색 플래시·공격
            // 쿨다운 타이머는 계속 진행돼야 한다(안 그러면 자다가 맞았을 때 색이 안 꺼짐).
            TickTimers();
            switch (phase)
            {
                case HoundPhase.Sleep: SleepLogic(); break;
                case HoundPhase.Patrol: PatrolLogic(); break;
                case HoundPhase.Waking: WakingLogic(); break;
            }
        }
    }

    // 창(프레임)마다 위치를 비교하면 렌더 프레임이 물리 스텝(FixedUpdate, 기본 50Hz)보다 빠를 때
    // 물리가 아직 안 돈 프레임엔 위치가 그대로라 "안 움직임"으로 잘못 읽혀, Run↔Stand가 매 프레임
    // 깜빡이며 anim.Play(...,0f)가 계속 0프레임으로 리셋돼 "두 애니메이션이 빠르게 전환/초기화되는
    // 것처럼 보이는" 버그가 났다(사용자 리포트). 짧은 시간 창(0.08초, 여러 물리 스텝을 포함) 동안의
    // 누적 변위로만 판단해 렌더/물리 프레임 어긋남에 흔들리지 않게 한다 — 벽에 막혔을 때(변위 0)
    // Stand로 바뀌는 원래 목적은 그대로 유지된다.
    const float MoveCheckWindow = 0.08f;
    float moveCheckTimer;
    float moveCheckStartX;
    bool cachedIsMoving;

    void TickMovementCheck()
    {
        moveCheckTimer += Time.deltaTime;
        if (moveCheckTimer < MoveCheckWindow) return;

        float delta = transform.position.x - moveCheckStartX;
        cachedIsMoving = Mathf.Abs(delta) > moveAnimThreshold * MoveCheckWindow;
        moveCheckStartX = transform.position.x;
        moveCheckTimer = 0f;
    }

    bool IsActuallyMoving() => cachedIsMoving;

    // state가 Hitstun으로 막 전환된 순간(상승 엣지)을 잡아 flashDuration 동안 셰이더 플래시를 켠다.
    void UpdateHitFlash()
    {
        if (state == AiState.Hitstun && lastHitFlashState != AiState.Hitstun) hitFlashTimer = flashDuration;
        lastHitFlashState = state;

        if (hitFlashTimer > 0f)
        {
            hitFlashTimer -= Time.deltaTime;
            flashBlock.SetColor(FlashColorId, flashColor);
            flashBlock.SetFloat(FlashAmountId, hitFlashTimer > 0f ? 1f : 0f);
            spriteRenderer.SetPropertyBlock(flashBlock);
            flashApplied = true;
        }
        else if (flashApplied)
        {
            flashBlock.SetFloat(FlashAmountId, 0f);
            spriteRenderer.SetPropertyBlock(flashBlock);
            flashApplied = false;
        }
    }

    void EnterSleep()
    {
        phase = HoundPhase.Sleep;
        sleepTimer = Random.Range(sleepDurationMin, sleepDurationMax);
        SetHorizontalVelocity(0f);
        PlayClip("GehennaHound-Sleep");
    }

    void SleepLogic()
    {
        // 자는 동안 공격당하면(기습) 거리와 무관하게 즉시 깬다.
        if (state == AiState.Hitstun) { BeginWaking(); return; }

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player != null && Vector2.Distance(transform.position, player.position) <= detectRange)
        {
            BeginWaking();
            return;
        }

        sleepTimer -= Time.deltaTime;
        if (sleepTimer <= 0f) BeginPatrol();
    }

    void BeginPatrol()
    {
        phase = HoundPhase.Patrol;
        patrolTimer = patrolDuration;
        PickNewPatrolTarget();
        TestLog.Event("hound_state", "patrol_start");
    }

    void PickNewPatrolTarget()
    {
        patrolTargetX = anchorPos.x + Random.Range(-patrolRadius, patrolRadius);
    }

    // 기획안 "Walk Sniff → 경계(Patrol)"에 대응하는 배회 행동. 스폰 지점 반경 안에서 좌우로
    // 왔다 갔다 하다가 patrolDuration이 끝나면 다시 눕는다. 발각(거리 또는 피격)되면 즉시 Waking으로.
    void PatrolLogic()
    {
        if (state == AiState.Hitstun) { BeginWaking(); return; }

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (player != null && Vector2.Distance(transform.position, player.position) <= detectRange)
        {
            BeginWaking();
            return;
        }

        patrolTimer -= Time.deltaTime;
        if (patrolTimer <= 0f) { EnterSleep(); return; }

        float dx = patrolTargetX - transform.position.x;
        if (Mathf.Abs(dx) < 0.1f) PickNewPatrolTarget();

        float dir = Mathf.Sign(dx);
        SetHorizontalVelocity(dir * patrolSpeed);
        FaceDirection(dir);

        PlayClip(IsActuallyMoving() ? "GehennaHound-WalkSniff" : "GehennaHound-Stand");
    }

    void BeginWaking()
    {
        phase = HoundPhase.Waking;
        wakeTimer = 0f;
        SetHorizontalVelocity(0f);
        PlayClip("GehennaHound-WakeUp");
        wakeClipDuration = GetClipLength("GehennaHound-WakeUp");
        TestLog.Event("hound_state", "wake_start");
    }

    void WakingLogic()
    {
        wakeTimer += Time.deltaTime;
        if (wakeTimer >= wakeClipDuration)
        {
            phase = HoundPhase.Aggro;
            TestLog.Event("hound_state", "aggro_start");
        }
    }

    void SyncAggroAnimation()
    {
        switch (state)
        {
            case AiState.Windup:
            case AiState.Thrust:
            case AiState.Recover:
                ApplyBiteClipSpeed();
                PlayClip("GehennaHound-Bite");
                return;
            case AiState.Hitstun:
                anim.speed = 1f; // 물기 정렬용 감속 해제(아래 ApplyBiteClipSpeed 주석 참고)
                // 피격 시 물기 애니메이션을 끊고 정지 자세로(흰색 플래시·히트스톱은
                // DummyEnemy/PlayerController 쪽에서 이미 공통으로 처리됨).
                // 넉백(맞은 직후 잠깐 밀려나는 것)은 살려두되, 그 이후엔 확실히 멈춰야 한다 —
                // knockbackDuration이 지난 뒤에도 매 프레임 다시 속도를 0으로 눌러 보장한다
                // (사용자 리포트: 실제로는 이동이 멈추지 않는 것처럼 보임).
                if (hitstunDuration - stateTimer >= knockbackDuration) SetHorizontalVelocity(0f);
                PlayClip("GehennaHound-Stand");
                return;
        }

        anim.speed = 1f; // 공격이 끝났으면 정렬용 감속을 반드시 되돌린다(이동·정지 클립은 평소 속도)

        bool grounded = IsHoundGrounded();
        if (!grounded && rb2D.linearVelocity.y < -0.1f)
        {
            PlayClip("GehennaHound-Fall");
            wasHoundGrounded = false;
            return;
        }
        if (!wasHoundGrounded && grounded)
        {
            PlayClip("GehennaHound-Land");
            wasHoundGrounded = true;
            return;
        }
        wasHoundGrounded = grounded;

        // Walk Sniff는 배회(Patrol) 전용 — 추적 중엔 실제로 움직이고 있을 때만 Run, 아니면(예:
        // 공격 쿨다운 대기로 attackRange 안에서 멈춰 있을 때, 또는 벽에 막혀 속도는 있어도 실제로는
        // 못 움직일 때) Stand로 "걷는 중이 아님"을 반영한다.
        PlayClip(IsActuallyMoving() ? "GehennaHound-Run" : "GehennaHound-Stand");
    }

    bool IsHoundGrounded()
    {
        if (bodyCollider == null) return true;
        Bounds b = bodyCollider.bounds;
        Vector2 origin = new Vector2(b.center.x, b.min.y);
        return Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundLayer).collider != null;
    }

    // ⚠️ FaceDirection은 일부러 오버라이드하지 않는다 — DummyEnemy 기본(transform.localScale.x 부호)을
    // 그대로 쓴다. PlayerController.CounterRush 등 여러 곳이 "적의 방향 = localScale.x 부호"라는
    // 컨벤션을 이미 전제하고 있어(target.transform.localScale.x로 직접 읽음), 이 몬스터만 SpriteRenderer.
    // flipX로 갈아타면 그 코드들이 항상 "오른쪽"으로만 읽어 대시 카운터가 엉뚱한 방향으로 나가고
    // (사용자 리포트), 왼쪽을 볼 때 판정 관련 로직이 어긋나는 등 하위 호환이 깨진다(실측 확인).
    // localScale를 그대로 쓰면 아래 히트박스도 부모 스케일을 따라 자동으로 미러링되므로, 창(스펙:
    // transform.TransformPoint)과 완전히 같은 방식으로 좌우 문제가 저절로 해결된다.

    // 창 캡슐(HitPoint~BasePoint, hitRadius) 판정 파이프라인은 그대로 두고, 그 캡슐의 양 끝점만
    // Hitbox_R의 실제 월드 바운드(가로 양 끝, 세로 중앙)로 근사해 재사용한다 — 회피·무적·패링 실드·
    // 데미지 확정 로직(ResolveThrustWindow)은 DummyEnemy 그대로. 어느 쪽 끝이 "몸에서 먼 쪽(HitPoint)"
    // 인지는 hound 위치 기준 절대거리로 판단하므로, localScale 미러링으로 좌우가 뒤집혀도 항상 옳다.
    protected override Vector2 HitPoint()
    {
        GetHitboxEdges(out _, out Vector2 far);
        return far;
    }

    protected override Vector2 BasePoint()
    {
        GetHitboxEdges(out Vector2 near, out _);
        return near;
    }

    void GetHitboxEdges(out Vector2 near, out Vector2 far)
    {
        // 캐시가 비어 있으면(예: Awake 시점에 hitboxRight가 아직 안 붙어 있었던 경우) 매번 다시
        // 시도한다 — 그냥 넘어가면 DummyEnemy 기본(spear 좌표, 몸 스케일 2.5배라 캡슐 길이가 4유닛
        // 가까이 나옴)으로 조용히 새 버렸다(사용자 리포트: "이상하게 길고 위치도 이상함" — 실제로는
        // 사용자가 직접 배치한 Hitbox_R 대신 이 폴백이 쓰이고 있었다).
        if (hitboxCollider == null && hitboxRight != null) hitboxCollider = hitboxRight.GetComponent<BoxCollider2D>();
        if (hitboxCollider == null) { near = base.BasePoint(); far = base.HitPoint(); return; }
        Bounds b = hitboxCollider.bounds;
        Vector2 a = new Vector2(b.min.x, b.center.y);
        Vector2 c = new Vector2(b.max.x, b.center.y);
        float originX = transform.position.x;
        if (Mathf.Abs(a.x - originX) > Mathf.Abs(c.x - originX)) { far = a; near = c; }
        else { far = c; near = a; }
    }

    // Bite 클립의 "물어뜯는" 프레임에 찍어둔 Animation Event에 연결하는 진단용 훅.
    // 실제 판정 타이밍은 windupDuration/thrustDuration/thrustHitNormalized/dodgeWindow* 값을
    // 이 프레임(클립 기준 경과시간)과 맞춰 튜닝해 뒀다 — 여기서는 그 튜닝이 실제로 맞아떨어지는지
    // (이 프레임에 도달했을 때 판정이 이미 끝나 있는지) 콘솔 로그로 확인만 한다.
    public void BiteHitFrameMarker()
    {
        TestLog.Event("hound_attack", "bite_frame_reached resolved=" + !IsAttackUnresolved);
    }

    /// <summary>Bite 클립의 "물어뜯는 프레임"이 **실제 피해가 확정되는 순간**에 도달하도록 재생속도를 맞춘다.
    ///
    /// 문제(사용자 리포트 2026-08-05 "초월에서 개 공격 범위 예측 타이밍이 이상함"): 초월 중엔 Windup이
    /// transcendWindupMultiplier(2.5)배로 늘어나는데 Bite 클립은 정상 속도로 그대로 재생돼서, 개가 이미
    /// 다 물어버린 뒤에도 예고 캡슐은 계속 차오르고 한참 뒤에 피해가 들어왔다(실측: 시각적 물기 0.500s
    /// vs 피해 확정 0.777s = 277ms 어긋남).
    ///
    /// 정렬은 "물기 프레임 시각 ÷ 피해 확정 시각"으로 구한다. ⚠️ 1을 넘지 않게 클램프하는 게 중요하다 —
    /// 평상시엔 이 비율이 0.500/0.402 = 1.24로 1보다 커서, 클램프가 없으면 여태 잘 돌던 평상시 물기가
    /// 빨라져 버린다(사용자가 요청한 건 "늘어난 윈드업에 맞춰 **늦추는** 것"뿐이다). 결과적으로 평상시엔
    /// 정확히 1.0으로 기존 동작 그대로, 초월 중에만 0.64 정도로 늦춰진다.</summary>
    void ApplyBiteClipSpeed()
    {
        float damageTime = AttackDamageTime;
        if (damageTime <= 0.01f || biteEventTime <= 0f) { anim.speed = 1f; return; }
        anim.speed = Mathf.Min(1f, biteEventTime / damageTime);
    }

    void PlayClip(string clipName)
    {
        if (currentClip == clipName) return;
        currentClip = clipName;
        anim.Play(clipName, 0, 0f);
    }

    /// <summary>클립에 찍힌 Animation Event의 시각(초). 못 찾으면 fallback을 그대로 돌려준다.
    /// GetClipLength와 같은 방식으로 컨트롤러의 클립 목록을 이름으로 훑는다.</summary>
    float GetClipEventTime(string clipName, string functionName, float fallback)
    {
        var controller = anim.runtimeAnimatorController;
        if (controller == null) return fallback;
        foreach (var c in controller.animationClips)
        {
            if (c == null || c.name != clipName) continue;
            foreach (var ev in c.events)
                if (ev.functionName == functionName) return ev.time;
        }
        return fallback;
    }

    float GetClipLength(string clipName)
    {
        var controller = anim.runtimeAnimatorController;
        if (controller != null)
            foreach (var c in controller.animationClips)
                if (c != null && c.name == clipName) return c.length;
        return 0.5f;
    }

    protected override void Die()
    {
        // base.Die()가 사망 포즈 유지 시간(deadPoseDuration) 뒤로 미뤄지는 동안 dead가 false로
        // 남아 있으면, DummyEnemy.TakeDamage()의 `if (dead || damage <= 0) return false;` 가드가
        // 안 걸려 죽은 채 누워있는 동안에도 계속 맞을 수 있었다(사용자 리포트). 즉시 죽음 처리해
        // 이후의 모든 TakeDamage 호출을 막는다 — 아래 base.Die()가 나중에 다시 true로 세팅해도 무해.
        dead = true;
        DisableHitDetection(); // 사망 포즈(deadPoseDuration) 유지 중에도 시체가 즉시 맞지 않게
        PlayClip("GehennaHound-Dead");
        SetHorizontalVelocity(0f);
        StartCoroutine(DieAfterPose());
    }

    IEnumerator DieAfterPose()
    {
        yield return new WaitForSeconds(deadPoseDuration);
        base.Die();
    }

    void SetHorizontalVelocity(float vx)
    {
        if (rb2D == null) return;
        Vector2 v = rb2D.linearVelocity;
        v.x = vx;
        rb2D.linearVelocity = v;
    }
}
