using System.Collections;
using UnityEngine;

// 부서지는 바닥 (2주차 함정 1 / 명세서 Map 1-3: "플레이어 충돌 감지 후 0.5초 딜레이 뒤 파괴되는 바닥").
// 밟히면 crumbleDelay 동안 경고(붉은 점멸 + 흔들림)를 낸 뒤 무너진다 —
// 콜라이더를 꺼서 플레이어가 그대로 통과해 떨어지고, 스프라이트는 파편으로 흩어진다.
// respawnDelay가 지나면 원상복구된다(0 이하면 영구 파괴).
[RequireComponent(typeof(Collider2D))]
public class CrumblingPlatform : MonoBehaviour
{
    public const string Channel = "trap_crumble";

    [Header("Crumble")]
    [Tooltip("밟은 뒤 무너지기까지의 시간")]
    public float crumbleDelay = 0.5f;
    [Tooltip("무너진 뒤 복구까지의 시간. 0 이하면 복구하지 않는다")]
    public float respawnDelay = 3f;

    [Header("Warning")]
    public Color warningColor = new Color(1f, 0.35f, 0.25f, 1f);
    [Tooltip("초당 점멸 왕복 횟수")]
    public float warningBlinkRate = 6f;
    [Tooltip("경고 흔들림 폭(월드 유닛). 스프라이트가 콜라이더와 다른 오브젝트일 때만 흔든다 — " +
             "콜라이더째로 흔들면 위에 선 플레이어를 물리적으로 밀어낸다")]
    public float warningShake = 0.04f;

    [Header("Debris (파괴 연출)")]
    public bool spawnDebris = true;
    public int debrisCount = 8;
    public float debrisSpeed = 3.5f;
    public float debrisLifetime = 0.8f;

    public bool IsCrumbling { get; private set; }
    public bool IsBroken { get; private set; }

    Collider2D col;
    SpriteRenderer sr;
    Color baseColor;

    void Awake()
    {
        col = GetComponent<Collider2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsCrumbling || IsBroken) return;
        // 대시 중에는 플레이어 레이어가 PlayerInvincible로 스왑되므로 레이어가 아니라 컴포넌트로 판별한다.
        if (collision.collider.GetComponentInParent<PlayerController>() == null) return;
        // 아래에서 머리로 들이받은 건 "밟았다"고 보지 않는다.
        if (collision.transform.position.y < transform.position.y) return;

        TestLog.Event(Channel, "stepped_on");
        StartCoroutine(CrumbleRoutine());
    }

    // 스위치·다른 기믹에서 강제로 무너뜨릴 때(테스트 포함) 쓰는 수동 트리거.
    public void Trigger()
    {
        if (IsCrumbling || IsBroken) return;
        TestLog.Event(Channel, "triggered");
        StartCoroutine(CrumbleRoutine());
    }

    IEnumerator CrumbleRoutine()
    {
        IsCrumbling = true;

        Transform visual = sr != null ? sr.transform : null;
        bool canShake = visual != null && visual != transform && warningShake > 0f;
        Vector3 visualOrigin = canShake ? visual.localPosition : Vector3.zero;

        float t = 0f;
        while (t < crumbleDelay)
        {
            t += Time.deltaTime;
            if (sr != null)
                sr.color = Color.Lerp(baseColor, warningColor, Mathf.PingPong(t * warningBlinkRate, 1f));
            if (canShake)
                visual.localPosition = visualOrigin + (Vector3)(Random.insideUnitCircle * warningShake);
            yield return null;
        }
        if (canShake) visual.localPosition = visualOrigin;

        Break();

        if (respawnDelay > 0f)
        {
            yield return new WaitForSeconds(respawnDelay);
            Restore();
        }
        IsCrumbling = false;
    }

    void Break()
    {
        IsBroken = true;
        col.enabled = false;
        if (sr != null)
        {
            sr.color = baseColor;
            if (spawnDebris) CrumbleDebris.Burst(sr, debrisCount, debrisSpeed, debrisLifetime);
            sr.enabled = false;
        }
        TestLog.Event(Channel, "broken");
    }

    void Restore()
    {
        col.enabled = true;
        if (sr != null)
        {
            sr.enabled = true;
            sr.color = baseColor;
        }
        IsBroken = false;
        TestLog.Event(Channel, "respawned");
    }
}
