using UnityEngine;
using System.Collections;

// 부패충 3종 공통 AI (기획안 "전체_몬스터_기획" 참고):
//   · 소형 부패충 — 빠르게 다가와 앞다리로 할퀸다(짧은 리치, 빠른 선딜)
//   · 중형 부패충 — 조금 느리지만 더 긴 리치의 강 베기 (외형·애니메이션만 다른 스킨 2종)
//   · 거대 군주 벌레 — 느리지만 기본 공격에 경직되지 않고(Super Armor) 리치가 긴 강한 일격
// 셋의 차이는 전부 인스펙터 수치(속도·HP·리치·슈퍼아머·클립 이름)로만 낸다 — 로직은 하나다.
//
// GehennaHound와 같은 이유로 DummyEnemy를 상속한다: PlayerController의 전투 판정(검 공격·패링·
// 닷지·처형·히트스턴·넉백)이 DummyEnemy 타입에 고정돼 있어, 상속하지 않으면 플레이어가 이
// 몬스터를 때리거나 패링·처형할 수 없다.
//
// 개(GehennaHound)와의 유일한 구조적 차이는 "떠다닌다"는 것이다(사용자 지시):
//   · Rigidbody2D.gravityScale = 0 (프리팹 설정). 수직 속도는 이 스크립트가 매 프레임 직접 잡는다.
//   · 평소엔 제자리에서 위아래로만 살짝 흔들린다(보빙) — y 이동이 사실상 없다.
//   · 플레이어가 y로 움직여 높이 차가 데드존을 넘었을 때만 그 높이를 따라간다.
//   · 수면/기상/배회 단계가 없다 — detectRange 안에 들어오면 바로 추격한다.
//
// 알려진 단순화(범위 밖): 기획서의 중형 "가드(방어) 시 밀려납니다"는 플레이어에 가드 기능
// 자체가 없어 적용 대상이 없다. 거대 군주의 "독 상태이상"은 사용자 결정(2026-08-13)으로 빼고
// 데미지가 큰 단일 일격으로 대체했다.
[RequireComponent(typeof(Animator))]
public class CorruptedBug : DummyEnemy
{
    enum BugPhase { Idle, Aggro }

    [Header("부패충 - 감지")]
    // 이 거리 안에 플레이어가 들어오면 추격을 시작한다(한 번 발각되면 풀리지 않는다 — 개와 동일).
    public float detectRange = 8f;

    [Header("부패충 - 부유(Floating)")]
    // 상시 위아래 흔들림. 진폭은 월드 유닛, 속도는 라디안/초.
    public float bobAmplitude = 0.12f;
    public float bobSpeed = 2.4f;
    // 플레이어 높이를 따라갈 때의 최대 수직 속도와 접근 계수.
    public float verticalSpeed = 3f;
    public float verticalTrackGain = 3f;
    // 목표 높이와의 차이가 이 값 안쪽이면 y를 아예 건드리지 않는다 — "플레이어가 y축 이동을
    // 하는 경우를 제외하고는 y축 이동을 거의 하지 않는다"는 스펙이 이 데드존에서 나온다.
    public float verticalDeadzone = 0.25f;
    // 플레이어 발밑 기준 몇 유닛 위를 노릴지(0이면 발높이).
    public float hoverYOffset = 0.4f;
    // 스폰 높이 기준 위아래 추적 한계 — 없으면 플레이어를 따라 맵 밖까지 떠올라 영구 소실된다
    // (DummyEnemy.leashRange가 x축만 막아주기 때문에 y축은 여기서 따로 막는다).
    public float verticalLeash = 6f;

    [Header("부패충 - 공격 판정 상자 (씬에 배치한 R/L 자식)")]
    // 공격 범위의 **유일한 기준**(사용자 지시 2026-08-13: "공격 범위는 각각 오른쪽 왼쪽일 때 나눠서
    // R,L로 해놨어"). 비워두면 Awake에서 이름이 "R"/"L"인 자식을 자동으로 찾는다.
    // ⚠️ 이 오브젝트들은 **비활성이어도 된다** — 물리에 참여시키지 않고 순수한 도형으로만 읽는다.
    //    그래서 Collider2D.bounds(비활성이면 갱신 안 됨) 대신 트랜스폼·size·offset으로 직접 계산한다.
    // 예전의 attackReachNear/Far·attackHeightOffset 수치 필드는 이 상자로 대체돼 제거했다 —
    // 같은 범위를 두 곳에서 정의하면 반드시 어긋난다(DummyEnemy 처형 임계값 이중 진실 선례).
    public Transform hitboxRight;
    public Transform hitboxLeft;
    // 플레이어와의 높이 차가 이보다 크면 공격을 시작하지 않는다. DummyEnemy의 공격 개시 조건이
    // |dx| 하나뿐이라, 이 게이트가 없으면 플레이어가 바로 위/아래에 있을 때 허공에 계속 휘두른다.
    public float attackYTolerance = 0.6f;

    [Header("부패충 - 공격 애니메이션 정렬")]
    // Attack 클립에서 "실제로 때리는" 프레임의 위치(0~1). ⚠️ Awake에서 클립에 찍힌 애니메이션
    // 이벤트 시각으로 자동 덮어쓴다 — 인스펙터 값은 이벤트가 없을 때의 폴백일 뿐이다.
    [Range(0.05f, 0.95f)] public float attackClipHitNormalized = 0.65f;

    [Header("부패충 - 슈퍼아머 (거대 군주 전용)")]
    // 켜면 피해·사망은 그대로 받되 경직(Hitstun)과 넉백을 무시한다 — 공격 도중에 맞아도 안 끊긴다.
    public bool superArmor = false;

    [Header("부패충 - 사망")]
    // 사망 애니메이션을 보여주는 시간. Death 클립 길이보다 짧으면 중간에 잘린다.
    public float deadPoseDuration = 1f;

    [Header("부패충 - 애니메이션 클립 이름")]
    // 실제 클립 이름은 "<prefix>-IdleMove" / "-Attack" / "-Death". 스킨(중형 2종)이 달라도
    // 로직은 같으므로 이 접두사만 바꿔 끼운다.
    public string clipPrefix = "SmallBug";

    Animator anim;
    Rigidbody2D rb2D;
    SpriteRenderer spriteRenderer;

    BugPhase phase = BugPhase.Idle;
    Vector3 anchorPos;
    float bobPhase;
    string currentClip = "";

    // ── 피격 흰색 플래시(셰이더 기반) ─────────────────────────────────────────────
    // DummyEnemy.TakeDamage()의 sr.color = flashColor는 SpriteRenderer.color가 텍스처에 곱연산되는
    // 특성상 flashColor=흰색(항등원)일 때 아무 효과가 없다. GehennaHound와 같이 Custom/SpriteHitFlash
    // 셰이더의 _FlashAmount를 MaterialPropertyBlock으로 직접 구동해 진짜 흰색 오버레이를 만든다.
    // ⚠️ 개는 state가 Hitstun으로 바뀌는 상승 엣지로 플래시를 켜지만, 여기선 TakeDamage에서 직접
    //    켠다 — 슈퍼아머가 Hitstun을 즉시 되돌려 버려서 엣지를 놓칠 수 있기 때문이다.
    static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    MaterialPropertyBlock flashBlock;
    float hitFlashTimer;
    bool flashApplied;

    protected override void Awake()
    {
        base.Awake();
        anim = GetComponent<Animator>();
        rb2D = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        flashBlock = new MaterialPropertyBlock();
        anchorPos = transform.position;
        // 같은 방에 여러 마리가 있을 때 전부 같은 박자로 흔들리면 기계처럼 보인다.
        bobPhase = Random.value * Mathf.PI * 2f;

        if (hitboxRight == null) hitboxRight = transform.Find("R");
        if (hitboxLeft == null) hitboxLeft = transform.Find("L");
        DeriveAttackGeometryFromHitbox();
        AlignAttackTimingToEvent();

        PlayClip(clipPrefix + "-IdleMove");
    }

    /// <summary>판정 캡슐의 굵기(hitRadius)와 멈춰서 공격을 시작하는 거리(attackRange)를 R 상자에서 뽑는다.
    /// 상자가 유일한 기준이 되도록 여기서 한 번에 덮어쓴다 — 인스펙터에 따로 적어 두면 상자를 옮겼을 때
    /// 조용히 어긋난다.</summary>
    void DeriveAttackGeometryFromHitbox()
    {
        if (!TryGetAttackBox(hitboxRight, out Vector2 center, out Vector2 half))
        {
            Debug.LogWarning($"[CorruptedBug] {name}: 공격 판정 상자(R/L 자식)를 못 찾았습니다 — DummyEnemy 기본 판정으로 폴백합니다.", this);
            return;
        }
        hitRadius = half.y;   // 캡슐 굵기 = 상자 높이의 절반
        // 상자 바깥쪽 끝이 닿는 거리에서 멈춰 공격한다. center는 **월드 좌표**라 몸 위치를 빼야
        // 거리가 된다(안 빼면 attackRange가 씬 좌표 크기, 실측 78.25가 나온다).
        attackRange = Mathf.Abs(center.x - transform.position.x) + half.x;
    }

    /// <summary>사용자가 Attack 클립의 특정 프레임에 찍어둔 이벤트가 "피해가 확정되는 순간"이다
    /// (지시 2026-08-13). DummyEnemy의 판정 파이프라인(패링·회피·무적·넉백)은 그대로 두고, 그
    /// 파이프라인의 피해 확정 시각(AttackDamageTime)이 이벤트 프레임과 정확히 겹치도록 선딜·후딜만
    /// 역산한다 — 이벤트가 직접 피해를 주게 만들면 저 판정 규칙들을 전부 우회하게 된다.
    ///
    /// 이벤트를 옮기면 다음 Play에서 자동으로 다시 맞춰지므로 인스펙터를 손댈 필요가 없다.</summary>
    void AlignAttackTimingToEvent()
    {
        float clipLength = GetClipLength(clipPrefix + "-Attack");
        float eventTime = GetAttackEventTime();
        if (clipLength <= 0.01f || eventTime <= 0f) return;

        attackClipHitNormalized = Mathf.Clamp(eventTime / clipLength, 0.05f, 0.95f);
        // AttackDamageTime = windup + thrust*thrustHitNormalized + dodgeWindowPost 이므로 역산한다.
        windupDuration = Mathf.Max(0.02f, eventTime - thrustDuration * Mathf.Clamp01(thrustHitNormalized) - dodgeWindowPost);
        // 공격 모션이 클립과 같이 끝나도록 후딜로 남은 길이를 채운다.
        recoverDuration = Mathf.Max(0.02f, clipLength - windupDuration - thrustDuration);
    }

    float GetAttackEventTime()
    {
        var controller = anim.runtimeAnimatorController;
        if (controller == null) return 0f;
        string wanted = clipPrefix + "-Attack";
        foreach (var c in controller.animationClips)
        {
            if (c == null || c.name != wanted) continue;
            var events = c.events;
            if (events != null && events.Length > 0) return events[0].time;
        }
        return 0f;
    }

    /// <summary>Attack 클립에 찍힌 이벤트가 호출하는 진단용 훅. 실제 피해는 DummyEnemy의 판정
    /// 파이프라인이 처리하고, 여기서는 그 타이밍이 이 프레임과 맞아떨어졌는지 로그로 확인만 한다
    /// (GehennaHound.BiteHitFrameMarker와 같은 역할).</summary>
    public void AttackHitFrame()
    {
        TestLog.Event("bug_attack", clipPrefix + " hit_frame resolved=" + !IsAttackUnresolved);
    }

    protected override void Update()
    {
        // ⚠️ 흰색 플래시 갱신은 모든 조기 return보다 **앞에** 있어야 한다. 아래 `!IsAlive` return 뒤에
        // 두면, 죽는 순간 플래시가 켜져 있던 경우(연타 콤보의 마지막 타격 — 흔하다) _FlashAmount가 1로
        // 박제돼 사망 연출(터지는 파편·떠오르는 해골)이 통째로 새하얀 실루엣이 된다(실측 2026-08-13:
        // 즉사 직후 _FlashAmount=1, 이후 Update가 안 돌아 영원히 안 내려감 → "죽는 연출이 안 나옴").
        UpdateHitFlash();

        if (!IsAlive) return; // Die()가 사망 연출/비활성화를 별도로 처리한다.
        if (AiFrozen) { HoldStill(); return; }

        if (phase == BugPhase.Idle)
        {
            // 전투 로직(base.Update)은 안 돌리지만 피격 플래시·공격 쿨다운 타이머는 계속 가야 한다.
            TickTimers();
            IdleLogic();
        }
        else
        {
            // 수직으로 크게 어긋나 있는 동안엔 base.Update()를 태우지 않는다 — 태우면 |dx|만 보고
            // 공격을 시작해 허공을 친다. 대신 수평 추격만 직접 굴리고 공격은 못 하게 막는다.
            if (state == AiState.Chase && Mathf.Abs(VerticalGap()) > attackYTolerance)
            {
                TickTimers();
                ChaseHorizontalOnly();
            }
            else
            {
                base.Update(); // DummyEnemy의 Chase/Windup/Thrust/Recover/Hitstun 전투 로직 그대로.
            }
            SyncAnimation();
        }

        UpdateFloat();
    }

    void IdleLogic()
    {
        SetHorizontalVelocity(0f);
        PlayClip(clipPrefix + "-IdleMove");

        // 대기 중 기습당하면 거리와 무관하게 즉시 반응한다.
        if (state == AiState.Hitstun) { BeginAggro(); return; }

        EnsurePlayer();
        if (player != null && Vector2.Distance(transform.position, player.position) <= detectRange)
            BeginAggro();
    }

    void BeginAggro()
    {
        phase = BugPhase.Aggro;
        TestLog.Event("bug_state", clipPrefix + " aggro_start");
    }

    // DummyEnemy.ChaseLogic의 수평 부분만 떼어낸 것(공격 개시 없음). leashRange 기준은 base와
    // 같다 — base는 Awake 시점 x를, 여기선 같은 값인 anchorPos.x를 쓴다.
    void ChaseHorizontalOnly()
    {
        float dx = player.position.x - transform.position.x;
        float dir = Mathf.Sign(dx);
        float fromAnchor = transform.position.x - anchorPos.x;
        bool wouldLeaveLeash = (fromAnchor >= leashRange && dir > 0f) || (fromAnchor <= -leashRange && dir < 0f);

        SetHorizontalVelocity(wouldLeaveLeash || Mathf.Abs(dx) <= attackRange ? 0f : dir * moveSpeed);
        FaceDirection(dir);
    }

    void SyncAnimation()
    {
        if (IsAttacking)
        {
            ApplyAttackClipSpeed();
            PlayClip(clipPrefix + "-Attack");
            return;
        }
        anim.speed = 1f; // 공격이 끝났으면 정렬용 감속을 반드시 되돌린다.
        PlayClip(clipPrefix + "-IdleMove"); // 대기·이동·경직이 전부 같은 클립(시트가 idle+move 통합).
    }

    /// <summary>Attack 클립의 "때리는 프레임"이 실제 피해 확정 시각에 오도록 재생속도를 맞춘다.
    /// GehennaHound.ApplyBiteClipSpeed와 같은 계산이며, 1을 넘지 않게 클램프하는 게 핵심이다 —
    /// 클램프가 없으면 평상시 공격이 원래보다 빨라져 버린다(늦추는 것만이 목적).</summary>
    void ApplyAttackClipSpeed()
    {
        float damageTime = AttackDamageTime;
        float clipHitTime = GetClipLength(clipPrefix + "-Attack") * attackClipHitNormalized;
        if (damageTime <= 0.01f || clipHitTime <= 0f) { anim.speed = 1f; return; }
        anim.speed = Mathf.Min(1f, clipHitTime / damageTime);
    }

    // ── 부유 ──────────────────────────────────────────────────────────────────────
    void UpdateFloat()
    {
        if (rb2D == null) return;

        // 공격 중엔 수직으로도 완전히 멈춘다 — 판정 캡슐이 몸 기준이라, 흔들리면 예고했던 위치와
        // 실제로 맞는 위치가 달라진다(DummyEnemy가 공격 중 수평 속도를 0으로 눌러두는 것과 같은 이유).
        if (IsAttacking) { SetVerticalVelocity(0f); return; }

        bobPhase += Time.deltaTime * bobSpeed;
        // 위치 오프셋 sin(t)*amp를 시간으로 미분한 값 = 속도. Transform을 직접 옮기지 않고 속도로
        // 넣어야 벽·바닥 충돌을 물리가 그대로 막아준다.
        float bobVelocity = Mathf.Cos(bobPhase) * bobAmplitude * bobSpeed;

        // 사거리 안에 들어왔으면 추격을 멈춘다(사용자 지시 2026-08-13: "공격 범위 이내에 들어오면
        // 그때는 이동을 중지하고 공격"). 수평은 DummyEnemy.ChaseLogic이 이미 0으로 눌러 두므로
        // 여기선 수직 추적만 끄면 된다 — 보빙은 그대로 남겨 떠 있는 느낌을 유지한다.
        float trackVelocity = 0f;
        if (!IsInAttackRange())
        {
            float gap = VerticalGap();
            if (Mathf.Abs(gap) > verticalDeadzone)
                trackVelocity = Mathf.Clamp(gap * verticalTrackGain, -verticalSpeed, verticalSpeed);
        }

        SetVerticalVelocity(trackVelocity + bobVelocity);
    }

    /// <summary>추격 중이고 플레이어가 공격 사거리(=R/L 상자 바깥 끝) 안에 들어와 있는가.
    /// DummyEnemy.ChaseLogic의 정지·공격 개시 조건과 같은 식(|dx|)을 쓴다.</summary>
    bool IsInAttackRange()
    {
        if (phase != BugPhase.Aggro || player == null) return false;
        return Mathf.Abs(player.position.x - transform.position.x) <= attackRange;
    }

    /// <summary>목표 높이 − 현재 높이. 추격 중이면 플레이어 높이(스폰 높이 ± verticalLeash로 클램프),
    /// 아니면 스폰 높이. 플레이어가 같은 높이에 머무는 동안엔 이 값이 데드존 안이라 y가 멈춘다.</summary>
    float VerticalGap()
    {
        float targetY = anchorPos.y;
        if (phase == BugPhase.Aggro && player != null)
            targetY = Mathf.Clamp(player.position.y + hoverYOffset,
                                  anchorPos.y - verticalLeash, anchorPos.y + verticalLeash);
        return targetY - transform.position.y;
    }

    // ── 피격 ──────────────────────────────────────────────────────────────────────
    public override bool TakeDamage(int damage, float knockbackDistance)
    {
        if (dead || damage <= 0) return false;
        hitFlashTimer = flashDuration; // 슈퍼아머 여부와 무관하게 맞은 티는 항상 낸다.

        if (!superArmor) return base.TakeDamage(damage, knockbackDistance);

        // 슈퍼아머(기획: "기본 공격에 경직되지 않습니다"): 넉백 거리를 0으로 넘겨 밀리지 않게 하고,
        // base가 세운 Hitstun을 맞기 직전 상태로 되돌려 진행 중이던 공격이 끊기지 않게 한다.
        // ⚠️ base가 함께 세우는 lastAttackNeutralized(초월 예고 원이 "터지지 않고 사라짐"으로 보이는
        //    플래그)까지는 되돌리지 못한다 — 연출만의 문제라 그대로 둔다.
        AiState stateBeforeHit = state;
        float timerBeforeHit = stateTimer;
        bool died = base.TakeDamage(damage, 0f);
        if (!died && state == AiState.Hitstun)
        {
            state = stateBeforeHit;
            stateTimer = timerBeforeHit;
        }
        return died;
    }

    void UpdateHitFlash()
    {
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

    // ── 공격 판정 캡슐 ────────────────────────────────────────────────────────────
    // 판정 파이프라인(회피·무적·패링 실드·피해 확정)은 DummyEnemy 그대로 두고, 캡슐의 두 끝점만
    // 씬에 배치된 R/L 상자의 가로 양 끝(세로는 중앙)으로 바꿔 끼운다. 굵기(hitRadius)는 상자 높이의
    // 절반이라 캡슐이 상자를 거의 그대로 덮는다.
    protected override Vector2 HitPoint()
    {
        if (!TryGetFacingBox(out Vector2 c, out Vector2 h)) return base.HitPoint();
        return new Vector2(c.x + Mathf.Sign(transform.localScale.x) * h.x, c.y); // 몸에서 먼 쪽 끝
    }

    protected override Vector2 BasePoint()
    {
        if (!TryGetFacingBox(out Vector2 c, out Vector2 h)) return base.BasePoint();
        return new Vector2(c.x - Mathf.Sign(transform.localScale.x) * h.x, c.y); // 몸에 가까운 쪽 끝
    }

    /// <summary>지금 바라보는 쪽 상자(오른쪽이면 R, 왼쪽이면 L)의 월드 중심·반크기.</summary>
    bool TryGetFacingBox(out Vector2 center, out Vector2 half)
    {
        return TryGetAttackBox(transform.localScale.x >= 0f ? hitboxRight : hitboxLeft, out center, out half);
    }

    /// <summary>R/L 자식이 "작성된 그대로"의 월드 상자를 돌려준다.
    ///
    /// ⚠️ TransformPoint·bounds를 쓰면 안 된다. 좌우 반전은 부모의 localScale.x 부호로 하는데,
    ///    그러면 왼쪽을 볼 때 L 상자까지 같이 미러링돼 오른쪽으로 넘어가 버린다(R/L을 따로 그려둔
    ///    의미가 사라진다). 그래서 부모 스케일은 **절대값만** 곱해서, 사용자가 에디터에서 그린
    ///    위치를 그대로 쓴다. bounds를 안 쓰는 이유는 또 있다 — 이 상자들은 비활성이라 물리가
    ///    bounds를 갱신해 주지 않는다.
    /// (부모 회전은 고려하지 않는다 — 이 몬스터들은 Rigidbody2D가 회전을 잠그고 있다.)</summary>
    bool TryGetAttackBox(Transform box, out Vector2 center, out Vector2 half)
    {
        center = Vector2.zero;
        half = Vector2.zero;
        if (box == null) return false;
        var bc = box.GetComponent<BoxCollider2D>();
        if (bc == null) return false;

        Vector2 parentScale = new Vector2(Mathf.Abs(transform.localScale.x), Mathf.Abs(transform.localScale.y));
        Vector2 localCenter = (Vector2)box.localPosition + Vector2.Scale(box.localScale, bc.offset);
        center = (Vector2)transform.position + Vector2.Scale(localCenter, parentScale);
        half = 0.5f * Vector2.Scale(
            new Vector2(Mathf.Abs(box.localScale.x), Mathf.Abs(box.localScale.y)),
            Vector2.Scale(bc.size, parentScale));
        return true;
    }

    // ── 사망 ──────────────────────────────────────────────────────────────────────
    protected override void Die()
    {
        // GehennaHound와 같은 이유로 즉시 dead 처리한다 — 안 그러면 사망 포즈를 유지하는 동안
        // DummyEnemy.TakeDamage의 dead 가드가 안 걸려 시체가 계속 맞는다.
        dead = true;
        anim.speed = 1f;
        PlayClip(clipPrefix + "-Death");
        SetHorizontalVelocity(0f);
        SetVerticalVelocity(0f);
        DisableHitDetection(); // 사망 포즈 유지 중에도 시체가 즉시 맞지 않게
        StartCoroutine(DieAfterPose());
    }

    IEnumerator DieAfterPose()
    {
        yield return new WaitForSeconds(deadPoseDuration);
        base.Die();
    }

    // ── 잡동사니 ──────────────────────────────────────────────────────────────────
    void EnsurePlayer()
    {
        if (player != null) return;
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;
    }

    void HoldStill()
    {
        SetHorizontalVelocity(0f);
        SetVerticalVelocity(0f);
    }

    void SetHorizontalVelocity(float vx)
    {
        if (rb2D == null) return;
        Vector2 v = rb2D.linearVelocity;
        v.x = vx;
        rb2D.linearVelocity = v;
    }

    void SetVerticalVelocity(float vy)
    {
        if (rb2D == null) return;
        Vector2 v = rb2D.linearVelocity;
        v.y = vy;
        rb2D.linearVelocity = v;
    }

    void PlayClip(string clipName)
    {
        if (currentClip == clipName) return;
        currentClip = clipName;
        anim.Play(clipName, 0, 0f);
    }

    float GetClipLength(string clipName)
    {
        var controller = anim.runtimeAnimatorController;
        if (controller != null)
            foreach (var c in controller.animationClips)
                if (c != null && c.name == clipName) return c.length;
        return 0.5f;
    }
}
