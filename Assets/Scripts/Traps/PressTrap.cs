using UnityEngine;

// 압착기(프레스) — 2주차 함정 2 / 명세서 Map 1-7·1-9: "일정 주기로 상하 왕복하는 프레스기 기믹, 충돌 시 즉사/넉백".
// 위에서 대기 → 빠르게 내려찍음 → 바닥에서 잠깐 머무름 → 천천히 복귀를 무한 반복한다.
// 내려찍는 동안(과 바닥에 머무는 동안) 프레스 아래에 깔린 플레이어를 때리고 옆으로 밀어낸다.
// 경고 점멸·먼지·화면 흔들림 같은 연출은 이번 범위 밖 — 기능만 넣는다.
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class PressTrap : MonoBehaviour
{
    public const string Channel = "trap_press";

    public enum Phase { WaitTop, Slam, HoldBottom, Rise }

    // 밀려난 직후 수평 입력을 잠그는 시간. 이게 없으면 다음 FixedUpdate의 HandleMovement가
    // 넉백 속도를 그대로 덮어써서 아예 안 밀린 것처럼 보인다.
    const float KnockbackLock = 0.2f;

    [Header("Cycle")]
    [Tooltip("내려찍기 전 위에서 대기하는 시간")]
    public float waitTop = 1.2f;
    [Tooltip("내려찍는 거리(월드 유닛, 아래 방향). 시작 위치가 '위'다")]
    public float travelDistance = 4f;
    [Tooltip("내려찍는 속도 — 복귀보다 훨씬 빨라야 프레스처럼 보인다")]
    public float slamSpeed = 18f;
    [Tooltip("바닥에서 머무는 시간")]
    public float holdBottom = 0.4f;
    public float riseSpeed = 3f;

    [Header("Damage")]
    [Tooltip("깔렸을 때 깎이는 체력 칸 수")]
    public int damageCount = 1;
    [Tooltip("켜면 칸 수와 무관하게 체력을 전부 날린다(즉사 판정)")]
    public bool instantKill = false;
    [Tooltip("옆으로 밀어내는 속도")]
    public float knockbackSpeed = 9f;
    [Tooltip("밀려날 때 살짝 뜨는 속도(0이면 수평으로만 밀린다)")]
    public float knockbackUpSpeed = 4f;
    [Tooltip("같은 대상을 다시 때리기까지의 최소 간격")]
    public float hitCooldown = 0.6f;

    public Phase CurrentPhase { get; private set; }
    public int HitCount { get; private set; }
    public float TopY => topY;
    public float BottomY => bottomY;

    Rigidbody2D rb;
    float topY;
    float bottomY;
    float phaseTimer;
    float hitCooldownRemaining;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // transform을 직접 옮기면 위/아래의 물체를 밀어내지 못하고 뚫고 지나간다 —
        // 프레스는 "움직이는 지형"이라 Kinematic + MovePosition이어야 한다.
        rb.bodyType = RigidbodyType2D.Kinematic;

        topY = rb.position.y;
        bottomY = topY - travelDistance;
        SetPhase(Phase.WaitTop);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (hitCooldownRemaining > 0f) hitCooldownRemaining -= dt;

        switch (CurrentPhase)
        {
            case Phase.WaitTop:
                phaseTimer -= dt;
                if (phaseTimer <= 0f) SetPhase(Phase.Slam);
                break;

            case Phase.Slam:
                if (MoveToward(bottomY, slamSpeed * dt)) SetPhase(Phase.HoldBottom);
                break;

            case Phase.HoldBottom:
                phaseTimer -= dt;
                if (phaseTimer <= 0f) SetPhase(Phase.Rise);
                break;

            case Phase.Rise:
                if (MoveToward(topY, riseSpeed * dt)) SetPhase(Phase.WaitTop);
                break;
        }
    }

    // 목표 y로 한 스텝 이동하고, 도착했으면 true.
    bool MoveToward(float targetY, float maxStep)
    {
        float y = Mathf.MoveTowards(rb.position.y, targetY, maxStep);
        rb.MovePosition(new Vector2(rb.position.x, y));
        return y == targetY;
    }

    void SetPhase(Phase next)
    {
        CurrentPhase = next;
        if (next == Phase.WaitTop) phaseTimer = waitTop;
        else if (next == Phase.HoldBottom) phaseTimer = holdBottom;
        TestLog.Event(Channel, $"phase={next} y={rb.position.y:F2}");
    }

    void OnCollisionEnter2D(Collision2D collision) => TryCrush(collision);
    void OnCollisionStay2D(Collision2D collision) => TryCrush(collision);

    void TryCrush(Collision2D collision)
    {
        // 올라가는 중이거나 위에서 대기 중일 땐 무해하다 — 프레스 위에 올라타 이동하는 것도 정상 플레이.
        if (CurrentPhase != Phase.Slam && CurrentPhase != Phase.HoldBottom) return;
        if (hitCooldownRemaining > 0f) return;

        // 대시 중엔 플레이어 레이어가 PlayerInvincible로 스왑되므로 레이어가 아니라 컴포넌트로 판별한다.
        var pc = collision.collider.GetComponentInParent<PlayerController>();
        if (pc == null) return;

        // 대시·일섬·처형 무적은 프레스도 뚫는다(명세서 Map 1-7·1-9 "일섬의 무적 판정 테스트").
        if (pc.IsInvincible)
        {
            TestLog.Event(Channel, "blocked_by_iframe");
            return;
        }

        // 프레스 위에 올라탄 상태는 "깔렸다"가 아니다.
        if (pc.transform.position.y >= rb.position.y) return;

        hitCooldownRemaining = hitCooldown;
        HitCount++;

        int dmg = instantKill ? pc.maxHealth : damageCount;
        pc.TakeDamage(dmg);

        // 프레스 중심에서 먼 쪽으로 밀어낸다(정확히 중앙이면 오른쪽).
        float dirX = pc.transform.position.x >= rb.position.x ? 1f : -1f;
        pc.ApplyKnockback(new Vector2(dirX * knockbackSpeed, knockbackUpSpeed), KnockbackLock);

        TestLog.Event(Channel, $"crushed dmg={dmg} dir={dirX} hp={pc.currentHealth}/{pc.maxHealth}");
    }
}
