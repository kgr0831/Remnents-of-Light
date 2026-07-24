using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    
    [Header("Jump")]
    public float jumpForce = 9f;
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;
    public float coyoteTime = 0.1f;
    
    [Header("Wall Slide & Jump")]
    public float wallSlideSpeed = 2f;
    public float wallSlideAccel = 20f;
    public Vector2 wallJumpForce = new Vector2(5f, 9f);
    public LayerMask groundLayer;
    public LayerMask wallLayer;

    [Header("Dash")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.18f;
    public float dashCooldown = 0.5f;
    public string invincibleLayerName = "PlayerInvincible";

    [Header("Dash VFX (Sandevistan)")]
    public bool dashAfterImage = true;
    public float afterImageInterval = 0.01f;   // 대시 중 잔상 스폰 간격 (촘촘할수록 잔상↑)
    public float afterImageLifetime = 0.35f;   // 잔상 하나가 사라지기까지 시간
    [Range(0f, 1f)] public float afterImageAlpha = 0.7f;
    public int afterImageSortingOffset = -1;   // 원본 뒤에 깔리도록
    // 산데비스탄: 생성 순서에 따라 초록→파랑→보라→빨강→노랑으로 에코 색이 바뀐다
    public Color[] afterImageColors = new Color[] {
        new Color(0.35f, 1f, 0.6f),
        new Color(0.35f, 0.65f, 1f),
        new Color(0.7f, 0.4f, 1f),
        new Color(1f, 0.35f, 0.45f),
        new Color(1f, 0.9f, 0.4f),
    };

    [Header("Dash Anim Freeze")]
    public bool dashFreezeAnim = true;
    public string dashFreezeState = "Glitch Samurai-Run"; // 대시 중 고정할 상태
    public int dashFreezeFrame = 3;                        // 고정할 프레임 인덱스
    public int dashFreezeFrameCount = 12;                  // Run 클립 총 프레임 수

    [Header("Dash Hitstop")]
    public bool dashHitstop = true;                 // 대시 시작 순간 짧은 시간정지(임팩트)
    public float hitstopDuration = 0.05f;           // 정지 길이(실시간 초, 40~80ms 권장)
    [Range(0f, 1f)] public float hitstopScale = 0f; // 정지 중 timeScale(0=완전 정지)

    [Header("Health")]
    public int maxHp = 100;
    public int currentHp;

    [Header("Attack (1-2 Combo)")]
    public int attack1Damage = 1;
    public int attack2Damage = 1;
    public float attack1Duration = 0.4167f; // Glitch Samurai-Slash 1 클립 길이(사용자가 5프레임으로 재편집), 애니메이터 재생속도 1
    public float attack2Duration = 0.4167f; // Glitch Samurai-Slash 2 클립 길이(5프레임), 애니메이터 재생속도 1
    public float comboBufferDuration = 2f;                     // 마지막 공격이 끝난 뒤 이 시간 안에 다시 공격하면 콤보로 이어짐, 지나면 1타로 리셋
    public float attackInputBufferDuration = 0.3f;              // 공격 중/쿨다운 중에 눌러도 이 시간 안이면 버퍼링돼 자동 발동(UniTrio-Game-2026 PlayerWeaponController._attackQueued 참고)
    public Vector2 attackHitboxSize = new Vector2(1.6f, 1.2f);
    public float attackHitboxDistance = 1f;
    public float attackLungeDistance = 0.3f; // 공격 시작 시 바라보는 방향으로 전진하는 거리(UniTrio 참고)
    // 적이 플레이어 공격에 맞으면 "플레이어가 공격 시 전진하는 거리 × 이 배율"만큼 밀려난다(사용자 스펙).
    public float enemyKnockbackMultiplier = 1.5f;
    public LayerMask enemyLayer;

    [Header("Attack VFX")]
    public bool attackHitstop = true;
    public float attackHitstopDuration = 0.05f;
    [Range(0f, 1f)] public float attackHitstopScale = 0f;
    public bool attackScreenShake = true;
    public float attackShakeDuration = 0.12f;
    public float attackShakeMagnitude = 0.15f;
    // UniTrio-Game-2026(SwordHitbox.SpawnHitVFX/SpawnDamageText)에서 참고: 타격 지점에 스파크+데미지 숫자 스폰
    public GameObject[] hitVfxPrefabs;
    public float hitVfxOffsetTowardsEnemy = 0.3f;
    public GameObject damageTextPrefab;
    public Color damageTextColor = Color.white;

    // UniTrio-Game-2026(JustDodge) 참고 — 타이밍 기반 저스트 닷지. 대시 무적 윈도우 중 적 찌르기가
    // 실제로 닿는 순간(DummyEnemy.CheckThrustHit → TryConsumeDodge) 발동 → 슬로우모션 +
    // 확인키(F, 보조로 우클릭) 대기 → 성공 시 적 쪽으로 돌진해 배율 데미지 카운터(y좌표는 유지).
    [Header("Dodge Counter")]
    public bool dodgeCounterEnabled = true;
    // 대시 실제 지속시간(0.18s)만으로는 적 찌르기 활성 프레임과 겹치는 타이밍이 너무 빡빡함(사용자 피드백) —
    // UniTrio DashHandler._justDodgeWindow 참고: 무적(i-frame) 자체는 대시 길이 그대로 두고, "닷지로 잡아줄
    // 창"만 대시 시작 기준으로 더 길게 열어둠(대시가 끝난 직후 살짝의 유예 동안도 닷지 인정).
    public float dodgeCounterGraceWindow = 0.35f;    // 대시 시작 후 이 시간 동안 접촉하면 닷지 인정(대시 길이보다 길게)
    public float dodgeCounterInputWindow = 2f;       // 확인키 대기 시간(실시간 초, 사용자 요청으로 1→2초)
    [Range(0.01f, 1f)] public float dodgeCounterSlowScale = 0.15f; // 대기 중 timeScale
    public Color dodgeCounterGlowColor = new Color(0.6f, 0.95f, 1f, 1f); // 대기 중 플레이어 스프라이트 틴트
    // 회피 성공 순간 대시를 이 배율만큼 연장해서 슬로우모션과 함께 계속 미끄러지듯 나아가게 함
    // (UniTrio DashHandler.ExtendDash 참고). 연장 중엔 물리 무적(IsInvincible=isDashing)도 함께 유지됨.
    public float dodgeCounterDashExtendMultiplier = 2f;
    public float dodgeCounterRushDuration = 0.12f;   // 적 뒤로 돌진하는 시간(실시간 초)
    public float dodgeCounterRushPastDistance = 2.2f; // 적 중심 기준 얼마나 지나쳐서 멈출지(사용자 요청으로 거리 확대)
    public float dodgeCounterDamageMultiplier = 3f;  // attack1Damage 대비 배율
    public float dodgeCounterHold = 0.3f;            // 타격 후 여운(실시간 초)
    public float dodgeCounterActivationShakeDuration = 0.08f;
    public float dodgeCounterActivationShakeMagnitude = 0.04f;
    public float dodgeCounterHitShakeDuration = 0.1f;
    public float dodgeCounterHitShakeMagnitude = 0.07f;
    public float dodgeCounterHitstopDuration = 0.05f;
    [Range(0f, 1f)] public float dodgeCounterHitstopScale = 0.1f;
    // 카운터 러시(적 뒤로 돌진) 중 잔상을 대량 생성(사용자 요청) — 평소 대시(0.01s)보다도 촘촘하게.
    // 러시가 0.12s로 매우 짧아 프레임당 1회 스폰 방식은 프레임레이트에 막혀 실제로는 몇 개 안 나감 →
    // 경로를 미리 계산해 한 번에 까는 방식으로 변경(간격은 "몇 개를 깔지" 계산용으로만 사용).
    public float dodgeCounterAfterImageInterval = 0.008f;
    public float dodgeCounterAfterImageLifetime = 0.6f; // 평소 대시 잔상보다 오래 남아 궤적이 잘 보이게
    // 카운터 명중 임팩트 VFX(UniTrio-Game-2026 JustDodgeVFX 절차적 슬래시 — 외부 아트 의존 없음).
    // 회피 발동 순간의 Sparkle/Glow(흰빛 이펙트)는 사용자 피드백으로 제거 — 그레이스케일 확산만으로 신호.
    public Color dodgeImpactColor = new Color(0.85f, 0.97f, 1f, 1f);
    public float dodgeImpactScale = 3f;
    // 흑백 화면 확산(UniTrio JustDodgeController 참고, GrayscaleRendererFeature 필요 — URP 렌더러에 미등록
    // 상태면 Instance가 null이라 조용히 무시됨, 등록 시 자동 활성화)
    public float dodgeGrayscaleMaxRadius = 1.8f; // 화면 UV 기준 — 플레이어가 화면 어디에 있어도 전체를 덮도록 넉넉하게
    public float dodgeGrayscaleRampIn = 0.6f;    // 흑백이 플레이어 중심에서 퍼지는 시간(실시간 초)
    public float dodgeGrayscaleRampOut = 0.2f;   // 종료 시 원복 시간(실시간 초)
    // 카메라 포커스 이벤트(UniTrio JustDodgeController의 팬+줌 참고) — 회피 발동 시 적 쪽으로 살짝 다가가며 줌인.
    public float dodgeCamPanAmount = 1.2f;
    public float dodgeCamZoomAmount = 0.8f;

    // "가끔 이동이 막힌다"는 리포트의 원인을 현장에서 지목하기 위한 임시 진단(원인 확정 후 제거).
    [Header("Diagnostics (임시)")]
    public bool logMovementStall = true;
    public float stallLogThreshold = 0.3f; // 이 시간 이상 "입력은 있는데 안 움직임"이면 1회 로그

    Rigidbody2D rb;
    BoxCollider2D coll;
    Animator anim;
    SpriteRenderer sr;

    Vector2 moveInput;
    bool isJumping;
    bool isJumpHeld;
    float coyoteTimeCounter;

    bool isWallSliding;
    bool isTouchingWall;
    bool isGrounded;
    int wallDirX;

    float wallJumpLockCounter;
    bool wasGrounded;

    bool dashRequested;
    bool isDashing;
    float dashTimer;
    float dashCooldownCounter;
    int dashDirX = 1;
    int normalLayer;
    int invincibleLayer;

    float afterImageTimer;
    int afterImageIndex;

    bool attackQueued;
    float attackQueueTime;
    bool isAttacking;
    int attackStage = 1; // 현재(공격 중) 또는 다음(대기 중) 발동될 콤보 타수 — 공격 종료 시 다음 타수로 순환됨
    float attackTimer;
    bool attackHitDone;
    float lastAttackEndTime = -999f;
    SectionCamera sectionCamera;

    float stallTimer;
    bool stallLogged;

    bool isDodgeCountering;
    bool dodgeCounterTriggeredThisDash;
    bool parryPressed;
    float dodgeCounterGraceTimer; // 대시 시작 시 dodgeCounterGraceWindow로 세팅, 대시 지속시간과 무관하게 독립 카운트다운

    void Awake()
    {
        // 방어적 리셋: Time.timeScale은 에디터에서 Stop→Play를 반복해도 자동으로 1로
        // 초기화되지 않는 정적 값(도메인 리로드 전까지 유지)이라, 이전 Play 세션이 슬로우모션
        // 코루틴 도중 비정상 종료됐다면 다음 Play가 그 값을 그대로 물려받는다. 항상 정상 속도로 시작.
        Time.timeScale = 1f;

        rb = GetComponent<Rigidbody2D>();
        coll = GetComponent<BoxCollider2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();

        // Rigidbody2D 기본 셋팅
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        normalLayer = gameObject.layer;
        invincibleLayer = LayerMask.NameToLayer(invincibleLayerName);

        currentHp = maxHp;
        if (enemyLayer.value == 0) enemyLayer = LayerMask.GetMask("Enemy");
        // 적 몸체와의 물리 충돌을 항상 제외한다. 예전엔 대시 중에만 제외했는데, 평상시 이동에서 적에게
        // 밀착하면 서로 밀어내느라 수평 속도가 죽어(실측: 5u/s → 0.96u/s, 약 80% 감소) 사용자가 본
        // "이동 중 갑자기 특정 방향으로 못 감(애니·flipX는 정상)" 증상이 발생했다 — moveInput은 정상
        // 수신되니 애니/flip은 그대로 돌고 좌표만 거의 안 변하는 정확한 시그니처. 전투 판정은 전부
        // Overlap 쿼리(excludeLayers 영향 없음)라 이 제외로 잃는 기능이 없다.
        rb.excludeLayers = rb.excludeLayers.value | enemyLayer.value;
        if (Camera.main != null) sectionCamera = Camera.main.GetComponent<SectionCamera>();
    }

    void Update()
    {
        if (wallJumpLockCounter > 0f) wallJumpLockCounter -= Time.deltaTime;
        if (dashCooldownCounter > 0f) dashCooldownCounter -= Time.deltaTime;
        if (dodgeCounterGraceTimer > 0f) dodgeCounterGraceTimer -= Time.deltaTime;

        CheckEnvironment();
        HandleJump();
        HandleWallSlide();
        HandleDash();
        HandleAttack();
        UpdateAnimations();
        CheckMovementStall();
    }

    // ── 임시 진단: "이동 입력은 있는데 실제로 안 움직임"을 잡아 원인을 콘솔에 지목한다 ──────────
    // 이동을 막을 수 있는 경로는 (a) 코드 상태 잠금(공격/회피카운터/대시/벽점프 잠금)과
    // (b) 물리 충돌 둘뿐이다. 어느 쪽인지 매번 추측하지 않으려고 스톨이 감지되면 그 순간의
    // 상태와 수평 접촉 콜라이더를 한 번만 로그로 남긴다. 원인 확정 후 제거할 코드.
    void CheckMovementStall()
    {
        if (!logMovementStall) return;

        bool wantsMove = Mathf.Abs(moveInput.x) > 0.01f;
        bool moving = Mathf.Abs(rb.linearVelocity.x) > 0.5f;
        if (!wantsMove || moving) { stallTimer = 0f; stallLogged = false; return; }

        stallTimer += Time.unscaledDeltaTime;
        if (stallTimer < stallLogThreshold || stallLogged) return;
        stallLogged = true;

        string cause;
        if (isDodgeCountering) cause = "회피-카운터 시퀀스 중(isDodgeCountering)";
        else if (isAttacking) cause = "공격 중(isAttacking, stage=" + attackStage + " timer=" + attackTimer.ToString("F2") + "/" + (attackStage == 1 ? attack1Duration : attack2Duration).ToString("F2") + ")";
        else if (isDashing) cause = "대시 중(isDashing, dir=" + dashDirX + ")";
        else if (wallJumpLockCounter > 0f) cause = "벽점프 수평잠금(wallJumpLockCounter=" + wallJumpLockCounter.ToString("F2") + ")";
        else
        {
            // 코드 잠금이 아니면 물리 — 무엇이 수평으로 막고 있는지 접촉점에서 직접 조회
            string blockers = "";
            ContactPoint2D[] contacts = new ContactPoint2D[16];
            int n = rb.GetContacts(contacts);
            for (int i = 0; i < n; i++)
            {
                if (Mathf.Abs(contacts[i].normal.x) < 0.5f) continue; // 수평으로 밀어내는 접촉만
                blockers += contacts[i].collider.gameObject.name
                    + "(layer=" + LayerMask.LayerToName(contacts[i].collider.gameObject.layer)
                    + " n=" + contacts[i].normal.ToString("F1") + ") ";
            }
            cause = "물리 충돌 — 수평 접촉=[" + (blockers.Length > 0 ? blockers : "없음(원인 불명)") + "]";
        }

        Debug.LogWarning("[STALL] 이동 막힘 " + stallTimer.ToString("F2") + "s | 원인: " + cause
            + " | moveInput.x=" + moveInput.x.ToString("F2") + " vel=" + rb.linearVelocity.ToString("F2")
            + " pos=" + transform.position.ToString("F2")
            + " grounded=" + isGrounded + " touchWall=" + isTouchingWall + " wallDirX=" + wallDirX
            + " timeScale=" + Time.timeScale.ToString("F2"));
    }

    void FixedUpdate()
    {
        if (isDodgeCountering)
        {
            // 회피 성공 직후엔 연장된 대시가 계속 진행 중이어야 함(끊기지 않고 슬로우모션과 함께
            // 미끄러지듯 나아감, UniTrio ExtendDash 참고) — DodgeCounterRoutine이 연장 시간을 다 쓰면
            // isDashing=false로 내려주므로, 그 이후(CounterRush의 Lerp 이동/명중 후 대기)엔 물리 속도를
            // 0으로 고정해 CounterRush의 transform.position 직접 제어와 충돌하지 않게 한다.
            rb.linearVelocity = isDashing ? new Vector2(dashDirX * dashSpeed, 0f) : Vector2.zero;
        }
        else if (isDashing)
        {
            rb.linearVelocity = new Vector2(dashDirX * dashSpeed, 0f);
        }
        else
        {
            HandleMovement();
        }
        ApplyBetterJumpPhysics();
    }

    void CheckEnvironment()
    {
        Bounds bounds = coll.bounds;
        isGrounded = Physics2D.BoxCast(bounds.center, bounds.size, 0f, Vector2.down, 0.1f, groundLayer);
        
        bool rightWall = Physics2D.BoxCast(bounds.center, bounds.size, 0f, Vector2.right, 0.1f, wallLayer);
        bool leftWall = Physics2D.BoxCast(bounds.center, bounds.size, 0f, Vector2.left, 0.1f, wallLayer);
        isTouchingWall = rightWall || leftWall;
        wallDirX = rightWall ? 1 : (leftWall ? -1 : 0);

        if (isGrounded) 
        {
            coyoteTimeCounter = coyoteTime;
        } 
        else 
        {
            coyoteTimeCounter -= Time.deltaTime;
        }
    }

    void HandleMovement()
    {
        // 벽 점프 직후에는 수평 입력을 잠시 잠가 벽 반대 방향으로 확실히 밀어냄
        if (wallJumpLockCounter > 0f) return;

        // 공격 중 또는 회피-카운터 시퀀스 중엔 제자리에 멈춤 (이동 입력 무시, 수평 속도 고정)
        if (isAttacking || isDodgeCountering)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // 가속 없이 즉시 목표 속도로 (뚝뚝 끊기는 조작감)
        rb.linearVelocity = new Vector2(moveInput.x * moveSpeed, rb.linearVelocity.y);
    }

    void HandleWallSlide()
    {
        if (isDashing) { isWallSliding = false; return; }

        // 벽 방향 키를 누르고 있는 동안만 벽에 붙어 슬라이드 (즉시 이동과 궁합: 접촉 유지 안정화)
        bool pushingIntoWall = isTouchingWall && wallDirX != 0
            && Mathf.Abs(moveInput.x) > 0.01f && Mathf.Sign(moveInput.x) == wallDirX;

        if (pushingIntoWall && !isGrounded)
        {
            isWallSliding = true; // 벽 접촉 + 키 감지 순간 즉시 슬라이드
            // 하강 시 가속하며 최대 슬라이드 속도로 수렴 (상승 중이면 점프 유지)
            if (rb.linearVelocity.y < 0f)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x,
                    Mathf.MoveTowards(rb.linearVelocity.y, -wallSlideSpeed, wallSlideAccel * Time.deltaTime));
        }
        else
        {
            isWallSliding = false;
        }
    }

    void HandleJump()
    {
        if (isJumping) {
            // 공격/회피-카운터 중엔 점프로 캔슬할 수 없음 — 입력은 버림
            if (isAttacking || isDodgeCountering) { isJumping = false; return; }
            if (coyoteTimeCounter > 0f) {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                coyoteTimeCounter = 0f;
            }
            else if (isTouchingWall && Mathf.Abs(moveInput.x) > 0.01f && Mathf.Sign(moveInput.x) == wallDirX) {
                // 벽 방향 키를 누르고 있을 때만 벽 점프 (벽 반대 방향 + 약간 위)
                rb.linearVelocity = new Vector2(-wallDirX * wallJumpForce.x, wallJumpForce.y);
                wallJumpLockCounter = 0.15f;
            }
            isJumping = false;
        }
    }

    void HandleDash()
    {
        // 회피-카운터 시퀀스 동안은 대시 타이머를 동결(코루틴이 직접 관리, EndDash로 종료)
        if (isDodgeCountering) return;

        if (dashRequested)
        {
            dashRequested = false;
            // 공격 중엔 대시로 캔슬할 수 없음
            if (!isDashing && !isAttacking && dashCooldownCounter <= 0f)
            {
                isDashing = true;
                dashTimer = dashDuration;
                dashCooldownCounter = dashCooldown;
                dashDirX = Mathf.Abs(moveInput.x) > 0.01f
                    ? (int)Mathf.Sign(moveInput.x)
                    : ((sr != null && sr.flipX) ? -1 : 1);

                if (invincibleLayer != -1) gameObject.layer = invincibleLayer;
                // (적 통과는 이제 Awake에서 상시 적용 — 대시에서만 켜고 끄지 않는다)
                afterImageTimer = 0f;
                afterImageIndex = 0;
                dodgeCounterGraceTimer = dodgeCounterGraceWindow; // 닷지 트리거 창(대시 길이보다 길게 유예)
                if (dashFreezeAnim) FreezeDashAnim();

                // 히트스톱: 대시 시작 순간 짧게 시간정지 → 무게감
                if (dashHitstop) StartCoroutine(DashHitstop());

                TestLog.Event("dash_iframe", $"dash_started dir={dashDirX}");
            }
        }

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;

            if (dashAfterImage)
            {
                afterImageTimer -= Time.deltaTime;
                if (afterImageTimer <= 0f)
                {
                    SpawnAfterImage();
                    afterImageTimer = afterImageInterval;
                }
            }

            // 진행 방향 벽에 부딪히면 남은 시간 무시하고 즉시 종료.
            // (벽에 0.15s 내내 처박는 낭비 제거 → 벽 붙은 뒤 반대/낙하로 즉시 복귀)
            bool intoWall = isTouchingWall && wallDirX != 0 && wallDirX == dashDirX;

            if (dashTimer <= 0f || intoWall)
            {
                EndDash(intoWall ? "dash_cancelled_wall" : "dash_ended");
            }
        }
    }

    // 대시 종료 공통 처리(정상 타임아웃/벽 취소/회피-카운터 종료 모두 여기로 모음).
    void EndDash(string reason)
    {
        isDashing = false;
        if (invincibleLayer != -1) gameObject.layer = normalLayer;
        if (dashFreezeAnim && anim != null) anim.enabled = true; // 애니메이터 재가동
        dodgeCounterTriggeredThisDash = false;
        TestLog.Event("dash_iframe", reason);
    }

    // 물리 무적(i-frame) 자체는 대시 실제 지속시간 그대로(1주차 스펙 불변).
    public bool IsInvincible => isDashing;

    // DummyEnemy.CheckThrustHit가 찌르기가 실제로 닿는 순간 호출한다. 닷지 트리거는 dodgeCounterGraceTimer로
    // 판정 — 대시가 물리적으로 끝난 뒤에도 유예 시간 동안은 여전히 닷지로 잡아준다(타이밍 완화, 사용자 피드백).
    // 유예 중에는 대시가 끝나 무적이 풀린 상태일 수 있으므로, 닷지가 발동 못 하면 정상 피해가 그대로 들어간다
    // (공짜 무적 연장이 아니라 "잡아줄 기회의 창"만 넓어짐).
    public bool TryConsumeDodge(DummyEnemy attacker)
    {
        if (!dodgeCounterEnabled) return false;
        if (dodgeCounterGraceTimer <= 0f || dodgeCounterTriggeredThisDash || isDodgeCountering) return false;
        dodgeCounterTriggeredThisDash = true;
        StartCoroutine(DodgeCounterRoutine(attacker));
        return true;
    }

    // Phase 1: 슬로우모션 + 확인키(F, 보조 우클릭) 대기. Phase 2: 성공 시 CounterRush로 이어짐.
    // 실패(윈도우 경과)해도 페널티 없이 대시가 그대로 이어져 끝난다.
    // ★ try/finally로 timeScale·상태 복원을 항상 보장 — 중단/예외로 슬로우모션·입력잠금이 stuck되던 버그의 구조적 수정.
    System.Collections.IEnumerator DodgeCounterRoutine(DummyEnemy target)
    {
        isDodgeCountering = true;
        parryPressed = false;
        // 윈도우가 열리기 "전부터" F/우클릭을 이미 누르고 있던 경우(선입력) 구제: OnParry는 press 엣지
        // 이벤트라서 이미 눌려있는 버튼은 새 이벤트를 발생시키지 않아 위 리셋 이후 감지가 안 됨 — 그 결과
        // 윈도우가 조용히 만료될 때까지 반응이 없다가, 사용자가 떼었다 다시 눌러야 그제서야 잡히는 것처럼
        // 보였음("판정이 늦게 되는 것 같다" 버그의 실제 원인, 홀드 재현으로 확인). 윈도우가 열리는 시점의
        // 현재 홀드 상태를 한 번 직접 확인해 즉시 확인 처리한다.
        bool heldAtWindowOpen = (Keyboard.current != null && Keyboard.current.fKey.isPressed) ||
            (Mouse.current != null && Mouse.current.rightButton.isPressed);
        if (heldAtWindowOpen) parryPressed = true;
        // 회피 성공 순간 대시를 연장(기본 2배) — 즉시 멈추지 않고 슬로우모션과 함께 계속 미끄러지듯
        // 나아감(UniTrio ExtendDash 참고). isDashing은 이 연장 구간 동안만 true로 유지되고, 아래
        // while 루프에서 시간이 다 되면 자동으로 false로 내려간다(FixedUpdate가 그 시점부터 속도를 0으로).
        dashTimer = dashDuration * dodgeCounterDashExtendMultiplier;
        Color baseColor = sr != null ? sr.color : Color.white;

        try
        {
            if (sr != null) sr.color = dodgeCounterGlowColor;
            if (sectionCamera != null)
            {
                sectionCamera.Shake(dodgeCounterActivationShakeDuration, dodgeCounterActivationShakeMagnitude);
                if (target != null)
                    sectionCamera.FocusPulse(target.transform.position, dodgeCamPanAmount, dodgeCamZoomAmount,
                        dodgeGrayscaleRampIn, dodgeCounterInputWindow, dodgeGrayscaleRampOut);
            }
            DodgeUI.GetOrCreate().ShowPrompt();

            // 확인 대기창(실시간)과 연장된 대시(슬로우모션 스케일 시간)는 서로 다른 시계라 별개로 다뤄야 함.
            // 예: dashDuration*2=0.36s를 timeScale 0.15로 나누면 실시간 약 2.4초가 걸리는데, 확인창이
            // 2초(실시간)면 대시가 자기 타이머를 다 못 쓰고 창이 먼저 닫혀버림 — 예전엔 이 시점에 무조건
            // isDashing=false로 끊어버려서 "대시가 뚝뚝 끊기며 짧게 이동" 버그가 났었음. 이제 창이 닫혀도
            // 대시가 자기 타이머로 자연스럽게 끝날 때까지는 루프를 계속 돌려(입력만 더 안 받음) 항상
            // 의도한 전체 거리(2배)를 다 이동하도록 보장한다.
            bool confirmed = false;
            bool windowOpen = true;
            float elapsed = 0f;
            while (windowOpen || isDashing)
            {
                Time.timeScale = dodgeCounterSlowScale;

                if (windowOpen) elapsed += Time.unscaledDeltaTime;

                // 연장된 대시 진행(스케일된 시간이라 슬로우모션이 깊어질수록 더 천천히 줄어듦 —
                // "슬로우모션 속에서 계속 미끄러지는" 느낌). 다 되면 그 자리에서 자연스럽게 정지.
                // HandleDash()는 최상단에서 isDodgeCountering이면 통째로 return하므로(이 구간엔 안 들어옴)
                // 잔상 스폰 로직을 여기서 동일하게 반복해줘야 함 — 이게 빠져서 "회피 성공 후 잔상이
                // 안 생김" 버그였음(연장된 대시는 움직이는데 잔상 스폰 코드만 실행될 기회가 없었음).
                if (isDashing)
                {
                    dashTimer -= Time.deltaTime;
                    if (dashTimer <= 0f) { dashTimer = 0f; isDashing = false; }

                    if (dashAfterImage)
                    {
                        afterImageTimer -= Time.deltaTime;
                        if (afterImageTimer <= 0f)
                        {
                            SpawnAfterImage();
                            afterImageTimer = afterImageInterval;
                        }
                    }
                }

                SetDodgeGrayscale(Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, dodgeGrayscaleRampIn)));

                if (windowOpen)
                {
                    // 확인키: PlayerActions의 "Parry" 액션(F키+보조로 우클릭 둘 다 바인딩됨, OnParry 콜백이
                    // parryPressed를 세팅) — Unity Input System 이벤트 큐 기반이라 프레임 타이밍과 무관하게
                    // 정확히 한 번 전달됨. 예전엔 Keyboard.current.fKey를 직접 폴링했는데
                    // wasPressedThisFrame이 이 코루틴의 프레임 재개 시점과 어긋나 그 한 프레임을 놓쳐
                    // "F를 눌러도 반응 없음/여러 번 눌러야 늦게 반응" 버그가 있었음 — 이벤트 콜백 경로로
                    // 통일해 해결.
                    if (parryPressed)
                    {
                        // ★ 확인 즉시 카운터로 넘어간다(루프 탈출). 예전엔 창만 닫고 `isDashing`이 false가
                        // 될 때까지 루프를 계속 돌렸는데, 연장된 대시(dashDuration*2 = 0.36 게임초)는
                        // 슬로우모션(timeScale 0.15)에서 실시간 약 2.4초가 걸린다 → F를 눌러도 그 시간이
                        // 다 지나갈 때까지 카운터가 안 터지고, 사용자에겐 "F가 씹혔다가 1~2초 뒤에 다시
                        // 누르니 발동"으로 보였다(그 사이 첫 입력이 이미 확정돼 있었을 뿐). 확인이 들어온
                        // 순간엔 연장 대시를 더 끌 이유가 없으므로 대시를 종료하고 즉시 CounterRush로 간다.
                        confirmed = true; windowOpen = false;
                        isDashing = false;
                        break;
                    }
                    if (elapsed >= dodgeCounterInputWindow) { windowOpen = false; }
                }

                yield return null;
            }

            bool countered = confirmed && target != null;
            TestLog.Event("dodge_counter", countered ? "confirmed" : "window_expired");
            if (confirmed) DodgeUI.GetOrCreate().FlashHidePrompt();
            else DodgeUI.GetOrCreate().HidePromptImmediate();
            if (countered)
                yield return CounterRush(target);

            yield return GrayscaleRampOut();
        }
        finally
        {
            // 안전망: 램프아웃이 중단/예외로 못 끝나도 흑백이 화면에 stuck되지 않도록 즉시 하드 리셋
            // (Time.timeScale stuck 버그와 같은 종류의 문제를 사전 차단).
            if (GrayscaleRendererFeature.Instance != null) GrayscaleRendererFeature.Instance.Intensity = 0f;
            // 어떤 경로(성공/만료/예외/중단)로 끝나도 항상 복원 — 슬로우모션/입력잠금 stuck 방지
            Time.timeScale = 1f;
            if (sr != null) sr.color = baseColor;
            DodgeUI.GetOrCreate().HidePromptImmediate();
            EndDash("dodge_counter_end");
            isDodgeCountering = false;
        }
    }

    // 화면 흑백을 플레이어 위치 기준으로 gk(0~1)만큼 퍼뜨린다. GrayscaleRendererFeature가 URP 렌더러에
    // 등록 안 돼 있으면 Instance가 null이라 조용히 무시(에러 없음) — 등록 시 자동으로 살아남.
    void SetDodgeGrayscale(float gk)
    {
        var f = GrayscaleRendererFeature.Instance;
        if (f == null) return;
        f.Intensity = gk;
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 vp = cam.WorldToViewportPoint(transform.position + Vector3.up * 0.6f);
            f.Center = new Vector2(vp.x, vp.y);
        }
        f.Radius = Mathf.Lerp(0f, dodgeGrayscaleMaxRadius, gk);
    }

    System.Collections.IEnumerator GrayscaleRampOut()
    {
        var f = GrayscaleRendererFeature.Instance;
        if (f == null) yield break;
        float from = f.Intensity;
        float dur = Mathf.Max(0.0001f, dodgeGrayscaleRampOut);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            f.Intensity = Mathf.Lerp(from, 0f, Mathf.Clamp01(t / dur));
            yield return null;
        }
        f.Intensity = 0f;
    }

    // 적을 지나쳐 뒤로 돌진한 뒤 돌아서서 배율 데미지를 직접 적용한다(정상 히트박스를 거치지 않음 —
    // 돌진 거리 특성상 일반 판정 범위와 안 맞을 수 있어 UniTrio JustDodgeController와 동일하게 직접 적용).
    System.Collections.IEnumerator CounterRush(DummyEnemy target)
    {
        TestLog.Event("dodge_counter", "rush_start");
        Time.timeScale = 1f; // 카운터는 정상 속도로(슬로우모 해제) — 스냅있게

        Vector3 start = transform.position;

        // 더미는 창을 든 쪽(=transform.localScale.x 부호)이 바라보는 방향. "적 뒤"는 적이 바라보는
        // 반대편이므로 behindSide = -enemyFacing. 도착 후엔 그 자리에서 적을 바라봐야 하므로
        // 플레이어의 최종 방향은 enemyFacing과 같아진다(반대편에 서서 다시 적 쪽을 보게 되므로).
        float enemyFacing = Mathf.Sign(target.transform.localScale.x);
        if (Mathf.Approximately(enemyFacing, 0f)) enemyFacing = 1f;
        float behindSide = -enemyFacing;

        Vector3 behind = target.transform.position + new Vector3(behindSide * dodgeCounterRushPastDistance, 0f, 0f);
        behind.y = start.y; // y좌표 유지(수직 위치 안 바꿈) — 사용자 요청
        behind.z = start.z;

        if (anim != null) anim.enabled = true; // 대시 프리즈 해제(돌진 모션이 보이도록)
        rb.linearVelocity = Vector2.zero;

        // 카운터 러시 중 잔상 다량 생성(사용자 요청) — 러시가 0.12s로 워낙 짧아 "프레임당 1회 스폰"
        // 방식은 프레임레이트에 막혀 실제로는 몇 개 안 나가는 문제가 있었음. 경로를 미리 계산해
        // start→behind 사이를 균등 분할한 지점에 한 번에(같은 프레임 안, yield 없이) 깔아둔다 —
        // 프레임레이트/타임스케일과 무관하게 항상 동일한 밀도가 보장됨. 실제 이동은 텔레포트 후 즉시
        // 원위치 복구라 화면에는 순간이동이 안 보이고(같은 프레임 안이라 렌더 안 됨) 잔상만 경로에 남는다.
        int burstCount = Mathf.Max(4, Mathf.RoundToInt(dodgeCounterRushDuration / Mathf.Max(0.001f, dodgeCounterAfterImageInterval)));
        burstCount = Mathf.Min(burstCount, 40); // 과도한 스폰 방지
        for (int i = 0; i < burstCount; i++)
        {
            float bt = (float)i / (burstCount - 1);
            transform.position = Vector3.Lerp(start, behind, bt);
            SpawnAfterImage(dodgeCounterAfterImageLifetime);
        }
        transform.position = start; // 실제 이동은 아래 Lerp 루프가 다시 처음부터 담당

        float t = 0f;
        while (t < dodgeCounterRushDuration)
        {
            t += Time.unscaledDeltaTime;
            transform.position = Vector3.Lerp(start, behind, Mathf.Clamp01(t / dodgeCounterRushDuration));
            yield return null;
        }
        transform.position = behind;

        // 적을 바라봄 (적이 바라보는 방향과 같은 쪽)
        if (sr != null) sr.flipX = (enemyFacing < 0f);
        Vector2 facingBack = (sr != null && sr.flipX) ? Vector2.left : Vector2.right;

        if (anim != null) anim.SetTrigger("Attack1"); // 시각적 스윙만(isAttacking=false라 AttackHitFrame 판정은 무시됨)

        int dmg = Mathf.RoundToInt(attack1Damage * dodgeCounterDamageMultiplier);
        target.TakeDamage(dmg, facingBack.x * attackLungeDistance * enemyKnockbackMultiplier);
        CombatFx.SpawnHitVfx(hitVfxPrefabs, target.transform.position, facingBack, hitVfxOffsetTowardsEnemy);
        CombatFx.SpawnDamageText(damageTextPrefab, target.transform.position, dmg, damageTextColor);
        float impactAngle = Mathf.Atan2(facingBack.y, facingBack.x) * Mathf.Rad2Deg;
        JustDodgeVFX.SpawnImpact(target.transform.position, impactAngle, dodgeImpactColor, dodgeImpactScale);

        if (sectionCamera != null) sectionCamera.Shake(dodgeCounterHitShakeDuration, dodgeCounterHitShakeMagnitude);
        StartCoroutine(DodgeCounterHitstopCo());
        TestLog.Event("dodge_counter", $"hit dmg={dmg}");

        yield return new WaitForSecondsRealtime(dodgeCounterHold);
    }

    System.Collections.IEnumerator DodgeCounterHitstopCo()
    {
        float prev = Time.timeScale;
        Time.timeScale = Mathf.Clamp01(dodgeCounterHitstopScale);
        yield return new WaitForSecondsRealtime(dodgeCounterHitstopDuration);
        Time.timeScale = prev;
    }

    // 대시 중 Run 애니를 지정 프레임에 고정한다 (산데비스탄 잔상이 같은 실루엣을 남기도록).
    // speed=0은 AnyState→Fall 전이(공중)가 조건 평가로 프리즈를 덮으므로,
    // Run 프레임을 sr.sprite에 기록한 뒤 애니메이터 자체를 꺼서 지상/공중 모두 고정한다.
    void FreezeDashAnim()
    {
        if (anim == null) return;
        int count = Mathf.Max(1, dashFreezeFrameCount);
        float nt = (float)dashFreezeFrame / count;
        anim.enabled = true;                // 평가되도록 보장
        anim.Play(dashFreezeState, 0, nt);
        anim.Update(0f);                    // Run 프레임을 sr.sprite에 즉시 기록
        anim.enabled = false;               // 애니메이터 정지 → sr.sprite 고정
    }

    void SpawnAfterImage(float? lifetimeOverride = null)
    {
        if (sr == null || sr.sprite == null) return;
        Color tint = (afterImageColors != null && afterImageColors.Length > 0)
            ? afterImageColors[afterImageIndex % afterImageColors.Length]
            : Color.cyan;
        tint.a = afterImageAlpha;
        afterImageIndex++;
        DashAfterImage.Spawn(sr, tint, lifetimeOverride ?? afterImageLifetime, afterImageSortingOffset);
    }

    // 대시 시작 순간의 히트스톱: 짧게 timeScale을 떨궈 임팩트를 준 뒤 원래 값으로 복원.
    // 실시간 대기라 timeScale=0 중에도 정상 종료하고, 진입 시점의 timeScale을 되돌려
    // 슬로모션 showcase(0.08 등)와도 충돌하지 않는다.
    System.Collections.IEnumerator DashHitstop()
    {
        float prev = Time.timeScale;
        Time.timeScale = Mathf.Clamp01(hitstopScale);
        yield return new WaitForSecondsRealtime(hitstopDuration);
        Time.timeScale = prev;
    }

    // UniTrio-Game-2026(PlayerWeaponController.HandleAttackInput) 참고 재설계:
    // 스윙 도중엔 새 공격을 끼워넣지 않고(항상 끝까지 재생), 대신 입력을 attackInputBufferDuration만큼
    // 버퍼링해 공격이 끝나는 즉시 자동 발동시킨다. 콤보 타수는 attackStage 하나만 순환시켜 관리
    // (별도의 "마지막 타수 기억" 변수 없이 종료 시 다음 타수로 미리 넘겨둠).
    void HandleAttack()
    {
        if (attackQueued && Time.time - attackQueueTime > attackInputBufferDuration)
            attackQueued = false;

        // 공중 공격 금지(사용자 스펙): 지상에서만 스윙이 시작된다. 지상에서 눌러 버퍼링된 입력도
        // 그 사이에 공중으로 나가면 발동하지 않는다(아래 isGrounded 조건). 회피-카운터(F)는 이
        // 공격 시스템을 거치지 않는 별도 경로라 공중에서도 그대로 동작한다.
        if (!isAttacking && !isDashing && !isDodgeCountering && isGrounded && attackQueued)
        {
            attackQueued = false;

            // 마지막 공격이 끝난 뒤 콤보 유효시간이 지났으면 1타로 리셋
            if (Time.time - lastAttackEndTime > comboBufferDuration)
                attackStage = 1;

            StartAttackStage(attackStage);
        }

        if (!isAttacking) return;

        attackTimer += Time.deltaTime;
        float duration = attackStage == 1 ? attack1Duration : attack2Duration;

        if (attackTimer >= duration)
        {
            lastAttackEndTime = Time.time;
            isAttacking = false;
            attackStage = (attackStage % 2) + 1; // 다음 공격을 위해 미리 순환(1->2, 2->1)
        }
    }

    void StartAttackStage(int stage)
    {
        isAttacking = true;
        attackStage = stage;
        attackTimer = 0f;
        attackHitDone = false;
        if (anim != null) anim.SetTrigger(stage == 1 ? "Attack1" : "Attack2");

        // UniTrio-Game-2026(PlayerWeaponController.HandleAttackInput) 참고: 검 공격 시작 시
        // 바라보는 방향으로 살짝 전진해 타격감 연출(flipX: true=왼쪽, false=오른쪽).
        float lungeDirX = (sr != null && sr.flipX) ? -1f : 1f;
        transform.position += new Vector3(lungeDirX * attackLungeDistance, 0f, 0f);

        TestLog.Event("player_attack", $"stage={stage}_start");
    }

    // Slash 1/2 애니메이션 클립에 찍힌 Animation Event(사용자가 직접 표시한 판정 프레임)에서 호출됨.
    // 정규화시간 윈도우 폴링 대신 애니메이션이 그 프레임에 도달하는 정확한 순간에 판정 — 프레임 스킵에도 안전.
    public void AttackHitFrame()
    {
        if (!isAttacking || attackHitDone) return;
        CheckAttackHit(attackStage == 1 ? attack1Damage : attack2Damage);
    }

    void CheckAttackHit(int damage)
    {
        attackHitDone = true;
        Vector2 facing = (sr != null && sr.flipX) ? Vector2.left : Vector2.right;
        Vector2 center = (Vector2)transform.position + facing * attackHitboxDistance;
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, attackHitboxSize, 0f, enemyLayer);
        int hitCount = 0;
        for (int i = 0; i < hits.Length; i++)
        {
            DummyEnemy enemy = hits[i].GetComponent<DummyEnemy>();
            if (enemy != null)
            {
                // 넉백: 플레이어가 바라보는 방향으로 attackLungeDistance × 배율(기본 1.5)만큼 밀어냄
                enemy.TakeDamage(damage, facing.x * attackLungeDistance * enemyKnockbackMultiplier);
                hitCount++;
                CombatFx.SpawnHitVfx(hitVfxPrefabs, hits[i].transform.position, facing, hitVfxOffsetTowardsEnemy);
                CombatFx.SpawnDamageText(damageTextPrefab, hits[i].transform.position, damage, damageTextColor);
            }
        }

        if (hitCount > 0)
        {
            TestLog.Event("player_attack", $"stage={attackStage} dmg={damage} hits={hitCount}");
            if (attackHitstop) StartCoroutine(AttackHitstopCo());
            if (attackScreenShake && sectionCamera != null) sectionCamera.Shake(attackShakeDuration, attackShakeMagnitude);
        }
    }

    // 대시 히트스톱(DashHitstop)과 동일 패턴: 타격 확정 순간 짧게 시간정지.
    System.Collections.IEnumerator AttackHitstopCo()
    {
        float prev = Time.timeScale;
        Time.timeScale = Mathf.Clamp01(attackHitstopScale);
        yield return new WaitForSecondsRealtime(attackHitstopDuration);
        Time.timeScale = prev;
    }

    // 적 공격에 맞았을 때 호출됨(예: DummyEnemy 창 찌르기). HP UI는 별도 과제라 아직 없음 — 수치만 관리.
    public void TakeDamage(int damage)
    {
        if (damage <= 0) return;
        currentHp -= damage;
        TestLog.Event("player_damage", $"hp={currentHp}/{maxHp} dmg={damage}");
    }

    void ApplyBetterJumpPhysics()
    {
        if (isWallSliding || isDashing) return; // 벽 슬라이드/대시 중엔 각자 y를 제어
        if (rb.linearVelocity.y < 0) {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        } else if (rb.linearVelocity.y > 0 && !isJumpHeld) {
            rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    void UpdateAnimations()
    {
        if (anim != null) {
            anim.SetFloat("Speed", Mathf.Abs(moveInput.x));
            anim.SetFloat("yVelocity", rb.linearVelocity.y);
            anim.SetBool("isGrounded", isGrounded);
            anim.SetBool("isWallSliding", isWallSliding);

            if (isGrounded && !wasGrounded) anim.SetTrigger("Land");
            wasGrounded = isGrounded;
        }

        if (sr != null) {
            if (isWallSliding) {
                sr.flipX = (wallDirX == 1);
            } else {
                Vector3 currentScale = transform.localScale;
                if (moveInput.x > 0.01f){
                    sr.flipX = false;
                }
                else if (moveInput.x < -0.01f)
                {
                    sr.flipX = true;
                }
            }
        }
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed) {
            isJumping = true;
            isJumpHeld = true;
        } else {
            isJumpHeld = false;
        }
    }

    public void OnDash(InputValue value)
    {
        if (value.isPressed) dashRequested = true;
    }

    public void OnAttack(InputValue value)
    {
        if (value.isPressed)
        {
            // 공중에서는 공격 "입력" 자체를 받지 않는다(사용자 스펙) — 버퍼에도 안 쌓이므로
            // 착지하는 순간 밀린 입력이 자동으로 터지는 일도 없다.
            if (!isGrounded) return;

            attackQueued = true;
            attackQueueTime = Time.time;
        }
    }

    // 회피-카운터 확인키. PlayerActions "Parry" 액션에 F키+우클릭 둘 다 바인딩됨(둘 중 아무거나로 확인
    // 가능). 다른 상황에선 아직 쓰임새 없음 — DodgeCounterRoutine이 대기 중일 때만 의미 있음.
    public void OnParry(InputValue value)
    {
        if (value.isPressed)
        {
            parryPressed = true;
        }
    }
}
