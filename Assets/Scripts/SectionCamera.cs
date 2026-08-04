using UnityEngine;

// 화면(구간) 단위 카메라. 플레이어가 속한 구간을 프레임하고, 다른 구간으로 넘어가면 부드럽게 슬라이드.
// 구간 안에서는 카메라가 고정되므로 걷는 동안 타일 이음새 흔들림이 없다.
[RequireComponent(typeof(Camera))]
public class SectionCamera : MonoBehaviour
{
    public static SectionCamera Instance;

    public Transform target;

    [Tooltip("구간 전환 슬라이드 속도 (클수록 빠름)")]
    public float slideSpeed = 8f;

    [Tooltip("구간 크기. 0이면 카메라 화면 크기로 자동 설정")]
    public Vector2 sectionSize = Vector2.zero;

    public Vector2 gridOrigin = Vector2.zero;

    Camera cam;
    Vector3 targetPos;
    Vector3 basePos;   // 구간 추적 정착 위치
    Vector3 shakeOffset;
    Vector3 sustainOffset; // SetSustainedShake가 매 프레임 새로 뽑는 오프셋(Shake와 독립적으로 합산)
    float sustainMagnitude;
    Vector3 focusOffset;   // FocusPulse가 파고들 때 basePos에 더해지는 오프셋
    float focusZoomDelta;  // orthographicSize에 더해지는 값(음수=줌인)
    int focusToken;        // 중복 FocusPulse 호출 시 이전 코루틴을 무력화(값만 덮어씀, 안전한 종료 보장)

    Vector3 sustainFocusOffset;  // SetSustainedFocus가 유지하는 팬 오프셋 — focusOffset과 별도로 합산(동시 FocusPulse와 안 밟음)
    float sustainFocusZoomDelta;
    Transform sustainFocusTarget;
    float sustainFocusPan;
    int sustainFocusToken;
    float sustainFocusMaxPanDown; // Y로 내려갈 수 있는 최대 거리(월드 유닛) — 0이면 Y는 안 움직인다
    float sustainFocusFloorY = float.NegativeInfinity; // 이 아래로는 카메라 하단이 못 내려간다

    // 룸 트리거 기반 전환(2026-08-03, 사용자 지시로 부활) — RoomTrigger가 EnterRoom을 부르면 그 순간부터
    // 그리드 자동분할 대신 룸 경계에 맞춘 프레이밍을 쓴다(옛 RoomCamera와 같은 "방 전체를 화면에 맞춤"
    // 계산을 이 클래스로 이식 — RoomCamera는 FocusPulse 등 이 게임의 전투 카메라 연출을 하나도 몰라서
    // 그대로 되살릴 수 없었다). 룸 트리거가 없는 씬(VfxSandbox 등)은 hasRoom이 그대로 false라 기존
    // 그리드 동작 그대로 — 회귀 없음.
    bool hasRoom;
    Vector3 roomTargetPos;
    float roomTargetOrthoSize;

    /// <summary>룸(월드 Bounds)에 맞춰 카메라를 고정 프레임한다 — RoomTrigger.OnTriggerEnter2D가 호출.
    /// 그리드 자동분할을 대체(이후 LateUpdate는 이 값을 targetPos/baseOrthoSize로 슬라이드).</summary>
    public void EnterRoom(Bounds b)
    {
        float halfH = b.size.y * 0.5f;
        float halfW = (b.size.x * 0.5f) / cam.aspect;
        roomTargetOrthoSize = Mathf.Max(halfH, halfW);
        roomTargetPos = new Vector3(b.center.x, b.center.y, basePos.z);
        hasRoom = true;
    }

    public void Shake(float duration, float magnitude)
    {
        StartCoroutine(ShakeCo(duration, magnitude));
    }

    System.Collections.IEnumerator ShakeCo(float duration, float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // 남은 시간 비율만큼 세기를 선형 감쇠시켜 뚝 끊기지 않고 부드럽게 잦아들게 한다.
            float falloff = 1f - (elapsed / duration);
            shakeOffset = Random.insideUnitCircle * magnitude * falloff;
            elapsed += Time.deltaTime;
            yield return null;
        }
        shakeOffset = Vector3.zero;
    }

    // 끝나는 시점이 정해져 있지 않은(입력을 떼야 끝나는) 쉐이크. 일섬 차지처럼 세기가 시간에 따라
    // 점점 커지는 연출용 — 매 프레임 원하는 세기를 넣고, 끝낼 때 0을 넣는다.
    // Shake()를 매 프레임 호출하는 방식은 코루틴이 프레임마다 쌓여 서로 shakeOffset을 덮어쓰므로 못 쓴다.
    public void SetSustainedShake(float magnitude)
    {
        sustainMagnitude = Mathf.Max(0f, magnitude);
    }

    // UniTrio JustDodgeController의 카메라 팬+줌 참고 — worldPos 쪽으로 살짝 다가가며 줌인했다가 원복.
    // Cinemachine 없이 SectionCamera 자체 basePos/orthographicSize를 직접 보간.
    public void FocusPulse(Vector3 worldPos, float panAmount, float zoomAmount, float rampIn, float hold, float rampOut)
    {
        StartCoroutine(FocusPulseCo(worldPos, panAmount, zoomAmount, rampIn, hold, rampOut));
    }

    System.Collections.IEnumerator FocusPulseCo(Vector3 worldPos, float panAmount, float zoomAmount, float rampIn, float hold, float rampOut)
    {
        int myToken = ++focusToken; // 이후 새 FocusPulse가 들어오면 이 코루틴은 자기 몫만 조용히 포기
        Vector3 dir = worldPos - basePos; dir.z = 0f;
        Vector3 targetOffset = (dir.sqrMagnitude > 0.0001f) ? dir.normalized * panAmount : Vector3.zero;

        float t = 0f;
        while (t < rampIn)
        {
            if (myToken != focusToken) yield break; // 새 펄스가 시작됨 — 이 코루틴은 상태를 안 건드리고 종료
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / rampIn);
            focusOffset = Vector3.Lerp(Vector3.zero, targetOffset, k);
            focusZoomDelta = Mathf.Lerp(0f, -zoomAmount, k);
            yield return null;
        }

        float held = 0f;
        while (held < hold)
        {
            if (myToken != focusToken) yield break;
            held += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        while (t < rampOut)
        {
            if (myToken != focusToken) yield break;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / rampOut);
            focusOffset = Vector3.Lerp(targetOffset, Vector3.zero, k);
            focusZoomDelta = Mathf.Lerp(-zoomAmount, 0f, k);
            yield return null;
        }

        if (myToken == focusToken) { focusOffset = Vector3.zero; focusZoomDelta = 0f; }
    }

    // FocusPulse의 "지속형" 버전 — 끝나는 시점이 정해져 있지 않은(E를 뗄 때까지) 줌인/팬. 목표값까지
    // 점점 다가간 뒤 그대로 유지하다가 ClearSustainedFocus를 불러야 풀린다(SetSustainedShake와 같은 관계).
    // zoomMultiplier: "몇 배 더 가깝게"(예: 1.30 = 30% 확대). FocusPulse의 zoomAmount(절대 감소량)와
    // 달리 배율로 받는다 — 구간(room)마다 baseOrthoSize가 달라도 항상 같은 비율로 줌인되게 하기 위해서다
    // (절대 감소량이면 큰 구간에서는 거의 안 보일 수 있다).
    // pan: 0~1 블렌드 계수(1이면 target이 화면 정중앙에 오도록 완전히 센터링). FocusPulse의 pan은
    // "살짝 다가감"용 고정 월드 거리였지만, 광원 소모는 정지 상태로 오래 유지되므로 완전 센터링이 맞다
    // (사용자 확정: "줌인이 플레이어 중심에 되어야") — 이건 X에만 적용된다.
    // maxPanDown/floorY(둘 다 선택, 기본값이면 Y는 전혀 안 움직임— 사용자 지시 2026-08-02: "E 차징
    // 중 y 좌표는 그대로"가 기본, 이후 "바닥은 보여도 되니 조금 더 아래로"로 완화): Y는 X와 별개로
    // maxPanDown만큼만 아래로 내려가되, 카메라 하단이 floorY 아래로 넘어가지 않게 매 프레임 클램프한다.
    public void SetSustainedFocus(Transform target, float pan, float zoomMultiplier, float rampIn,
        float maxPanDown = 0f, float floorY = float.NegativeInfinity)
    {
        sustainFocusTarget = target;
        sustainFocusPan = pan;
        sustainFocusMaxPanDown = maxPanDown;
        sustainFocusFloorY = floorY;
        StartCoroutine(SustainedFocusRampCo(++sustainFocusToken, zoomMultiplier, rampIn));
    }

    System.Collections.IEnumerator SustainedFocusRampCo(int myToken, float zoomMultiplier, float rampIn)
    {
        Vector3 fromOffset = sustainFocusOffset;
        float fromZoom = sustainFocusZoomDelta;
        float targetZoomDelta = -(baseOrthoSize - baseOrthoSize / Mathf.Max(0.01f, zoomMultiplier));
        float targetOrthoSize = baseOrthoSize / Mathf.Max(0.01f, zoomMultiplier);
        float t = 0f;
        while (t < rampIn)
        {
            if (myToken != sustainFocusToken) yield break; // ClearSustainedFocus가 먼저 불렸으면 조용히 포기
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / rampIn);
            // target - basePos(구간 중심)까지의 변위에 pan(0~1)을 곱한다 — pan=1이면 target이
            // 화면 중앙에 오도록 basePos를 그만큼 이동시키는 오프셋이 된다(방향만 쓰던 FocusPulse와 다름).
            Vector3 dir = sustainFocusTarget != null ? (sustainFocusTarget.position - basePos) : Vector3.zero;
            dir.z = 0f;
            Vector3 targetOffset = new Vector3(dir.x * sustainFocusPan, 0f, 0f);

            if (sustainFocusMaxPanDown > 0f)
            {
                // 카메라 하단(basePos.y + offsetY - targetOrthoSize)이 floorY 아래로 내려가지 않는
                // 한도 안에서만 내려간다. maxPanDown이 더 커도 바닥이 먼저 걸리면 거기서 멈춘다.
                float floorLimit = sustainFocusFloorY - basePos.y + targetOrthoSize;
                float offsetY = Mathf.Max(-sustainFocusMaxPanDown, floorLimit);
                targetOffset.y = Mathf.Min(offsetY, 0f); // 위로는 안 올라간다 — 내려가는 쪽만 허용
            }

            sustainFocusOffset = Vector3.Lerp(fromOffset, targetOffset, k);
            sustainFocusZoomDelta = Mathf.Lerp(fromZoom, targetZoomDelta, k);
            yield return null;
        }
        if (myToken == sustainFocusToken) sustainFocusZoomDelta = targetZoomDelta;
    }

    public void ClearSustainedFocus(float rampOut)
    {
        StartCoroutine(ClearSustainedFocusCo(++sustainFocusToken, rampOut));
    }

    System.Collections.IEnumerator ClearSustainedFocusCo(int myToken, float rampOut)
    {
        Vector3 fromOffset = sustainFocusOffset;
        float fromZoom = sustainFocusZoomDelta;
        float t = 0f;
        while (t < rampOut)
        {
            if (myToken != sustainFocusToken) yield break;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / rampOut);
            sustainFocusOffset = Vector3.Lerp(fromOffset, Vector3.zero, k);
            sustainFocusZoomDelta = Mathf.Lerp(fromZoom, 0f, k);
            yield return null;
        }
        if (myToken == sustainFocusToken) { sustainFocusOffset = Vector3.zero; sustainFocusZoomDelta = 0f; }
        sustainFocusTarget = null;
    }

    float baseOrthoSize; // FocusPulse가 줌을 되돌릴 기준값(구간 크기 auto-계산도 이 값으로 고정해 줌 중 흔들림 방지)

    void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        if (target == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }
        targetPos = transform.position;
        basePos = transform.position;
        baseOrthoSize = cam.orthographicSize;
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (hasRoom)
        {
            // 룸 트리거가 한 번이라도 불렸으면 그 방식이 그리드 자동분할을 대체한다(사용자 지시 2026-08-03).
            targetPos = roomTargetPos;
        }
        else
        {
            float w = sectionSize.x > 0.01f ? sectionSize.x : baseOrthoSize * 2f * cam.aspect;
            float h = sectionSize.y > 0.01f ? sectionSize.y : baseOrthoSize * 2f;

            // 플레이어가 속한 구간의 인덱스 → 그 구간의 중심으로 카메라 목표 설정
            float sx = Mathf.Floor((target.position.x - gridOrigin.x) / w);
            float sy = Mathf.Floor((target.position.y - gridOrigin.y) / h);
            float cx = gridOrigin.x + (sx + 0.5f) * w;
            float cy = gridOrigin.y + (sy + 0.5f) * h;
            targetPos = new Vector3(cx, cy, basePos.z);
        }

        // 시간 가속(PlayerController) 중엔 세계가 느려져도 플레이어는 평소 속도로 움직인다 — 추적까지
        // 같이 느려지면 카메라가 계속 뒤처져 화면 밖으로 밀려난다. 플레이어와 같은 실시간 배율을 곱해
        // 추적만 평소 속도로 유지한다(가속이 아닐 땐 이 값이 정확히 1이라 기존 동작 그대로).
        float t = 1f - Mathf.Exp(-slideSpeed * Time.deltaTime * PlayerController.PlayerTimeMultiplier);
        basePos = Vector3.Lerp(basePos, targetPos, t);
        if (hasRoom) baseOrthoSize = Mathf.Lerp(baseOrthoSize, roomTargetOrthoSize, t);

        // 지속 쉐이크는 unscaled 기준 난수라 히트스톱(timeScale=0) 중에도 계속 떨린다.
        sustainOffset = sustainMagnitude > 0f
            ? (Vector3)(Random.insideUnitCircle * sustainMagnitude)
            : Vector3.zero;

        transform.position = basePos + shakeOffset + sustainOffset + focusOffset + sustainFocusOffset;
        cam.orthographicSize = baseOrthoSize + focusZoomDelta + sustainFocusZoomDelta;
    }
}
