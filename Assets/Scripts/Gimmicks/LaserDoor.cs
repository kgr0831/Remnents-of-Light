using UnityEngine;

// 문/스위치 기믹 - 레이저 문. laser.png를 9프레임(0~2 점화 예비동작, 3~8 완전 점등 루프)으로 슬라이스해
// Animator 없이 PressTrap과 같은 자체 타이머로 재생한다.
// requiredSwitches가 전부 On이어야 안전(레이저 Off, 통과 가능) — 하나라도 Off면 위험(레이저 On).
// 완전 점등(Loop) 상태에서만 접촉 시 피해 — 예비동작·소등 애니메이션 구간은 텔레그래프로 취급해 무해하다
// (2026-08-10 사용자 확정). ⚠️ 대시·일섬·처형의 무적 프레임도 무시하고 항상 피해가 들어간다
// (PressTrap 등 기존 함정과 다른 점 — 사용자가 명시적으로 반대 방향 지시).
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class LaserDoor : MonoBehaviour
{
    public const string Channel = "laser_door";

    const float KnockbackLock = 0.2f; // PressTrap과 동일 이유 — 넉백 직후 수평 입력이 속도를 덮어쓰지 않게 잠깐 잠금

    public enum Phase { SafeOff, Arming, Loop, Closing }

    [Header("연동 스위치 (여기 넣은 스위치가 전부 On이어야 안전 — AND)")]
    [Tooltip("이 문을 열어줄(레이저를 끌) 스위치들. 여기 넣은 스위치가 전부 켜져야(On) 레이저가 꺼져서 지나갈 수 있다. 하나라도 꺼져 있으면 레이저가 켜진 채로 유지된다. 비워두면 스위치와 무관하게 항상 위험 상태로 고정된다(장식용 고정 함정)")]
    public DoorSwitch[] requiredSwitches;

    [Header("애니메이션 (laser_0~laser_8을 슬라이스한 스프라이트)")]
    [Tooltip("레이저가 켜지기 시작할 때 순서대로 재생되는 3프레임 — laser_0, laser_1, laser_2 순서로 넣는다. 이 구간은 예고(텔레그래프)라 닿아도 피해가 없다")]
    public Sprite[] armingFrames = new Sprite[3];
    [Tooltip("완전히 켜진 상태에서 반복 재생되는 6프레임 — laser_3, laser_4, laser_5, laser_6, laser_7, laser_8 순서로 넣는다. 이 구간에서만 닿으면 피해+넉백이 들어간다")]
    public Sprite[] loopFrames = new Sprite[6];
    [Tooltip("애니메이션 재생 속도(초당 프레임 수). 켜짐 예고·완전 점등·꺼짐 예고 세 구간 전부 이 값을 공통으로 쓴다")]
    public float frameRate = 10f; // 초당 프레임 수, 예비동작·루프·소등 공통

    [Header("발광 (파란 빛 부분만 빛남 — 완전 점등 프레임에 자동으로만 나타남)")]
    [Tooltip("빛나야 할 부분(빔의 파란 픽셀)만 표시한 마스크 텍스처. Assets/Sprites/Gimmicks/LaserEmissionMask.png를 그대로 연결하면 됨 — 이미 만들어져 있으니 새로 만들 필요 없음. laser.png 전체와 동일한 크기라 9프레임 전부에 자동으로 맞는다")]
    public Texture2D emissionMask; // Assets/Sprites/Gimmicks/LaserEmissionMask.png
    [Tooltip("빛나는 색. 기본값은 원본 빔의 파란색을 그대로 사용")]
    public Color glowColor = new Color(0.66f, 0.79f, 1f, 1f); // laser.png 빔의 밝은 파란색 실측값
    [Tooltip("빛나는 강도 — 값이 클수록 더 쨍하게 빛난다")]
    public float bloomBoost = 2.2f;

    [Header("피해 (완전 점등 상태에서만 적용)")]
    [Tooltip("맞았을 때 깎이는 체력 칸 수")]
    public int damageCount = 1;
    [Tooltip("맞았을 때 옆으로 밀려나는 속도")]
    public float knockbackSpeed = 9f;
    [Tooltip("맞았을 때 위로 살짝 뜨는 속도 — 0이면 수평으로만 밀려난다")]
    public float knockbackUpSpeed = 4f;
    [Tooltip("한 번 맞은 뒤 다시 피해를 줄 수 있을 때까지의 최소 간격(초). 완전 점등 상태에 계속 닿아있어도 이 간격마다만 피해가 들어간다")]
    public float hitCooldown = 0.6f;

    public Phase CurrentPhase { get; private set; }
    public bool IsSafe => CurrentPhase == Phase.SafeOff;

    SpriteRenderer sr;
    SpriteRenderer glowSr;
    Material glowMat;
    Collider2D col;
    int frameIndex;
    float frameTimer;
    float hitCooldownRemaining;
    bool wasSafe;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        BuildGlowOverlay();
        wasSafe = AllSwitchesOn();
        SetPhase(wasSafe ? Phase.SafeOff : Phase.Loop); // 시작 시엔 전환 애니메이션 없이 바로 확정 상태로
    }

    // DoorSwitch.BuildGlowOverlay와 동일 패턴 — Custom/PlayerBloomOverlay 재사용, 마스크가
    // laser.png 전체와 같은 크기라 _MainTex와 UV를 그대로 공유해 9프레임 전부에 자동으로 맞는다.
    void BuildGlowOverlay()
    {
        Shader sh = Shader.Find("Custom/PlayerBloomOverlay");
        if (sh == null) return;

        var go = new GameObject("Glow");
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);

        glowMat = new Material(sh);
        glowMat.SetColor("_Color", glowColor);
        glowMat.SetFloat("_BloomBoost", bloomBoost);
        glowMat.SetFloat("_Flatten", 1f);
        glowMat.SetFloat("_MaskFloor", 0f);
        glowMat.SetFloat("_Intensity", 1f);
        if (emissionMask != null) glowMat.SetTexture("_EmissionMask", emissionMask);

        glowSr = go.AddComponent<SpriteRenderer>();
        glowSr.sharedMaterial = glowMat;
        glowSr.sortingLayerID = sr.sortingLayerID;
        glowSr.sortingOrder = sr.sortingOrder + 1;
    }

    // sr.sprite를 바꿀 땐 항상 이 함수를 거친다 — 글로우 오버레이가 매 프레임 같은 스프라이트를
    // 보여줘야 마스크 UV가 어긋나지 않는다(0번 프레임처럼 빔이 없는 프레임은 마스크도 전부 검정이라
    // 별도 분기 없이 자동으로 안 빛난다).
    void SetSprite(Sprite s)
    {
        sr.sprite = s;
        if (glowSr != null) glowSr.sprite = s;
    }

    void Update()
    {
        if (hitCooldownRemaining > 0f) hitCooldownRemaining -= Time.deltaTime;

        bool safeNow = AllSwitchesOn();
        if (safeNow != wasSafe)
        {
            wasSafe = safeNow;
            SetPhase(safeNow ? Phase.Closing : Phase.Arming);
        }

        StepAnimation();
    }

    // 연결된 스위치가 하나도 없으면 배선을 깜빡한 게 아니라 "항상 위험한 고정 해저드"로 취급한다.
    bool AllSwitchesOn()
    {
        if (requiredSwitches == null || requiredSwitches.Length == 0) return false;
        for (int i = 0; i < requiredSwitches.Length; i++)
            if (requiredSwitches[i] == null || !requiredSwitches[i].IsOn) return false;
        return true;
    }

    void SetPhase(Phase next)
    {
        CurrentPhase = next;
        frameIndex = 0;
        frameTimer = 0f;
        // 완전 점등(Loop)일 때만 물리적으로 막는다 — 트리거로만 두면 피해는 들어가도 그냥 걸어서건
        // 대시로건 통과가 돼버린다(사용자 리포트). 그 외 구간(예고·소등)은 안전하니 그대로 통과 가능.
        col.isTrigger = next != Phase.Loop;
        SetSprite(next == Phase.Loop ? loopFrames[0] : armingFrames[0]);
        TestLog.Event(Channel, $"{name} phase={next}");
    }

    void StepAnimation()
    {
        if (CurrentPhase == Phase.SafeOff) return; // 정지 프레임, 재생 없음

        float frameDuration = 1f / Mathf.Max(1f, frameRate);
        frameTimer += Time.deltaTime;
        if (frameTimer < frameDuration) return;
        frameTimer -= frameDuration;

        switch (CurrentPhase)
        {
            case Phase.Arming:
                frameIndex++;
                if (frameIndex >= armingFrames.Length) { SetPhase(Phase.Loop); return; }
                SetSprite(armingFrames[frameIndex]);
                break;

            case Phase.Loop:
                frameIndex = (frameIndex + 1) % loopFrames.Length;
                SetSprite(loopFrames[frameIndex]);
                break;

            case Phase.Closing:
                // 명세: "3 -> 0 프레임" — loopFrames[0](원본 3번)에서 시작해 armingFrames를 역순으로 밟고 꺼진다.
                frameIndex++;
                if (frameIndex > 3) { SetPhase(Phase.SafeOff); return; }
                SetSprite(frameIndex switch
                {
                    1 => armingFrames[2],
                    2 => armingFrames[1],
                    _ => armingFrames[0],
                });
                break;
        }
    }

    // Loop 중엔 col.isTrigger=false(막는 벽)라 Trigger가 아니라 Collision 콜백이 실제로 도는 쪽이다 —
    // 둘 다 걸어놓고 TryHitPlayer의 Phase.Loop 가드로 안전하게 구분한다.
    void OnTriggerEnter2D(Collider2D other) => TryHitPlayer(other);
    void OnTriggerStay2D(Collider2D other) => TryHitPlayer(other);
    void OnCollisionEnter2D(Collision2D collision) => TryHitPlayer(collision.collider);
    void OnCollisionStay2D(Collision2D collision) => TryHitPlayer(collision.collider);

    void TryHitPlayer(Collider2D other)
    {
        if (CurrentPhase != Phase.Loop) return; // 완전 점등 중에만 위험
        if (hitCooldownRemaining > 0f) return;

        var pc = other.GetComponentInParent<PlayerController>();
        if (pc == null) return;

        // ⚠️ pc.IsInvincible을 일부러 확인하지 않는다 — 대시/일섬/처형으로도 못 뚫는다는 사용자 지시.
        hitCooldownRemaining = hitCooldown;
        pc.TakeDamage(damageCount);

        float dirX = pc.transform.position.x >= transform.position.x ? 1f : -1f;
        pc.ApplyKnockback(new Vector2(dirX * knockbackSpeed, knockbackUpSpeed), KnockbackLock);

        TestLog.Event(Channel, $"hit dmg={damageCount} dir={dirX} hp={pc.currentHealth}/{pc.maxHealth}");
    }

    void OnDestroy()
    {
        if (glowMat != null) Destroy(glowMat);
    }
}
