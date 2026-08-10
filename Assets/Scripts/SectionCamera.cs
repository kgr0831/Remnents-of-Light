using UnityEngine;
using UnityEngine.Rendering.Universal;

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
    float sustainFocusMaxPanDown; // 0이면 Y는 안 움직인다, 0보다 크면 Y도 플레이어를 따라간다(게이트 전용, 크기는 안 씀)

    // 룸 트리거 기반 전환(2026-08-03, 사용자 지시로 부활) — RoomTrigger가 EnterRoom을 부르면 그 순간부터
    // 그리드 자동분할 대신 룸 경계에 맞춘 프레이밍을 쓴다(옛 RoomCamera와 같은 "방 전체를 화면에 맞춤"
    // 계산을 이 클래스로 이식 — RoomCamera는 FocusPulse 등 이 게임의 전투 카메라 연출을 하나도 몰라서
    // 그대로 되살릴 수 없었다). 룸 트리거가 없는 씬(VfxSandbox 등)은 hasRoom이 그대로 false라 기존
    // 그리드 동작 그대로 — 회귀 없음.
    bool hasRoom;
    Vector3 roomTargetPos;
    float roomTargetOrthoSize;
    // 룸의 화면비가 실제 화면비와 다를 때, 그냥 더 넓게 보여주는 대신(기존 Max 방식) 레터박스/
    // 필러박스(검은 바)로 룸의 비율을 그대로 지킬지 여부 — 룸별로 opt-in(사용자 지시 2026-08-08,
    // BossRoomTrigger 전용). 기존 RoomTrigger들은 이 값이 기본 false라 동작이 그대로 유지된다.
    bool roomLetterbox;
    float roomAspect = 1f;

    /// <summary>룸(월드 Bounds)에 맞춰 카메라를 고정 프레임한다 — RoomTrigger.OnTriggerEnter2D가 호출.
    /// 그리드 자동분할을 대체(이후 LateUpdate는 이 값을 targetPos/baseOrthoSize로 슬라이드).
    /// letterbox가 true면 룸의 화면비를 그대로 지키고 남는 부분은 검은 바로 채운다.</summary>
    public void EnterRoom(Bounds b, bool letterbox = false)
    {
        roomLetterbox = letterbox;
        if (letterbox)
        {
            // 화면비를 룸 자체 비율로 고정 — 높이를 기준으로 orthoSize를 정하고, 뷰포트(cam.rect)로
            // 레터박스/필러박스를 만든다(실제 화면비와 다른 만큼만 검은 바).
            roomAspect = b.size.x / Mathf.Max(0.001f, b.size.y);
            roomTargetOrthoSize = b.size.y * 0.5f;
        }
        else
        {
            float halfH = b.size.y * 0.5f;
            float halfW = (b.size.x * 0.5f) / cam.aspect;
            roomTargetOrthoSize = Mathf.Max(halfH, halfW);
        }
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
    // maxPanDown(선택, 기본값 0이면 Y는 전혀 안 움직임 — 사용자 지시 2026-08-02: "E 차징 중 y 좌표는
    // 그대로"가 기본): 0보다 크면 Y도 X와 완전히 같은 방식(dir.y*pan)으로 target을 따라간다(단
    // "위로는 안 올라간다" — target이 basePos보다 위일 때는 안 끌어옴). 값 자체의 크기는 더 이상
    // 상한으로 쓰이지 않는다 — 켤지 말지 게이트일 뿐(2026-08-10 후속, floorY 상한 제거 참고).
    public void SetSustainedFocus(Transform target, float pan, float zoomMultiplier, float rampIn,
        float maxPanDown = 0f)
    {
        sustainFocusTarget = target;
        sustainFocusPan = pan;
        sustainFocusMaxPanDown = maxPanDown;
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
                // ⚠️ 버그 수정(2026-08-10, 사용자 리포트 "줌인이 플레이어가 아니라 위쪽을 향함").
                //    1차 수정(dir.y*pan을 floorLimit/maxPanDown로 클램프)은 방향은 맞았지만
                //    Play Mode 실측 결과 보스룸처럼 세로로 긴 방에서는 바닥 안전장치(floorLimit)가
                //    방-플레이어 Y격차(실측 12.75유닛)보다 훨씬 타이트(실측 1.09유닛)하게 걸려
                //    사실상 거의 못 내려갔다 — 여전히 위쪽에 남아 보이는 원인이 이거였다.
                //    사용자 확정(2026-08-10): "바닥 아래가 보이더라도 플레이어 중앙 정렬 우선".
                //    그래서 바닥 클램프·maxPanDown 상한을 버리고 X와 완전히 같은 방식(dir.y*pan)으로
                //    풀어준다 — "위로는 안 올라간다"(플레이어가 basePos보다 위일 때 억지로 안 끌어옴)
                //    원래 규칙만 유지. maxPanDown>0은 여전히 "Y를 따라갈지 말지" 게이트로만 쓰인다
                //    (이 함수를 다른 용도로 호출할 때 0을 넘기면 기존처럼 Y 고정 유지).
                targetOffset.y = Mathf.Min(dir.y * sustainFocusPan, 0f);
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

        ApplyLetterbox();
    }

    // hasRoom && roomLetterbox일 때만 카메라 뷰포트(cam.rect)를 룸 비율에 맞춰 줄이고, 남는 공간은
    // 검은 바로 남긴다. 그 외의 모든 경우(레터박스 안 쓰는 룸, 그리드 모드)는 항상 풀스크린으로
    // 되돌려 기존 동작을 지킨다.
    //
    // ⚠️ 빌드 전용 버그(사용자 리포트 2026-08-09: "게임이 앞 뒤 프레임을 반복하면서 깨진다"):
    //    "뷰포트 밖은 아무 카메라도 안 그리니 검게 남는다"는 **에디터 게임 뷰에서만** 맞는 말이다.
    //    게임 뷰는 매 프레임 렌더 타깃을 지워 주지만, 빌드의 백버퍼는 아무도 안 지우면 이전 내용이
    //    그대로 남는다. 더블/트리플 버퍼링이라 서로 다른 옛 프레임 두세 장이 번갈아 보이면서
    //    "앞뒤 프레임이 반복되며 깨지는" 것처럼 된다. 그래서 레터박스가 걸린 동안에는 화면 전체를
    //    검게 지우기만 하는 카메라를 메인보다 먼저 한 번 돌린다.
    void ApplyLetterbox()
    {
        if (!hasRoom || !roomLetterbox)
        {
            if (cam.rect != new Rect(0f, 0f, 1f, 1f)) cam.rect = new Rect(0f, 0f, 1f, 1f);
            SetLetterboxClearEnabled(false);
            return;
        }

        float screenAspect = (float)Screen.width / Screen.height;
        Rect r;
        if (roomAspect > screenAspect)
        {
            // 룸이 화면보다 상대적으로 넓다 → 위아래 레터박스
            float h = screenAspect / roomAspect;
            r = new Rect(0f, (1f - h) * 0.5f, 1f, h);
        }
        else
        {
            // 룸이 화면보다 상대적으로 좁다 → 좌우 필러박스
            float w = roomAspect / screenAspect;
            r = new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
        cam.rect = r;
        SetLetterboxClearEnabled(true);
    }

    // 화면 전체를 검게 지우기만 하는 보조 카메라(그리는 것은 없다 — cullingMask=0).
    // 씬에 오브젝트를 새로 두지 않고 필요할 때 런타임에 한 번 만든다.
    Camera letterboxClearCam;

    void SetLetterboxClearEnabled(bool enabled)
    {
        if (!enabled)
        {
            if (letterboxClearCam != null) letterboxClearCam.enabled = false;
            return;
        }

        if (letterboxClearCam == null)
        {
            var go = new GameObject("LetterboxClearCamera");
            go.transform.SetParent(transform, false);

            letterboxClearCam = go.AddComponent<Camera>();
            letterboxClearCam.clearFlags = CameraClearFlags.SolidColor;
            letterboxClearCam.backgroundColor = Color.black;
            letterboxClearCam.cullingMask = 0;      // 아무것도 안 그린다 — 지우기 전용
            letterboxClearCam.orthographic = true;
            letterboxClearCam.orthographicSize = 1f;
            letterboxClearCam.nearClipPlane = 0.1f;
            letterboxClearCam.farClipPlane = 1f;
            letterboxClearCam.rect = new Rect(0f, 0f, 1f, 1f); // 항상 화면 전체
            letterboxClearCam.useOcclusionCulling = false;
            letterboxClearCam.allowHDR = false;
            letterboxClearCam.allowMSAA = false;

            var data = letterboxClearCam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderType = CameraRenderType.Base;
                data.renderPostProcessing = false;
                data.renderShadows = false;
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
            }
        }

        // 메인 카메라보다 확실히 먼저 그려져야 한다(이 카메라가 지운 뒤 그 위에 본 화면이 얹힌다).
        letterboxClearCam.depth = cam.depth - 100f;
        letterboxClearCam.enabled = true;
    }
}
