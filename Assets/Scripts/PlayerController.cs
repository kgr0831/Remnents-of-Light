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
    public float dashSpeed = 14f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.5f;
    public string invincibleLayerName = "PlayerInvincible";

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

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        coll = GetComponent<BoxCollider2D>();
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();

        // Rigidbody2D 기본 셋팅
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        normalLayer = gameObject.layer;
        invincibleLayer = LayerMask.NameToLayer(invincibleLayerName);
    }

    void Update()
    {
        if (wallJumpLockCounter > 0f) wallJumpLockCounter -= Time.deltaTime;
        if (dashCooldownCounter > 0f) dashCooldownCounter -= Time.deltaTime;

        CheckEnvironment();
        HandleJump();
        HandleWallSlide();
        HandleDash();
        UpdateAnimations();
    }

    void FixedUpdate()
    {
        if (isDashing)
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
        if (dashRequested)
        {
            dashRequested = false;
            if (!isDashing && dashCooldownCounter <= 0f)
            {
                isDashing = true;
                dashTimer = dashDuration;
                dashCooldownCounter = dashCooldown;
                dashDirX = Mathf.Abs(moveInput.x) > 0.01f
                    ? (int)Mathf.Sign(moveInput.x)
                    : ((sr != null && sr.flipX) ? -1 : 1);

                if (invincibleLayer != -1) gameObject.layer = invincibleLayer;
                TestLog.Event("dash_iframe", $"dash_started dir={dashDirX}");
            }
        }

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
            {
                isDashing = false;
                if (invincibleLayer != -1) gameObject.layer = normalLayer;
                TestLog.Event("dash_iframe", "dash_ended");
            }
        }
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
}
