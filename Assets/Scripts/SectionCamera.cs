using UnityEngine;

// 화면(구간) 단위 카메라. 플레이어가 속한 구간을 프레임하고, 다른 구간으로 넘어가면 부드럽게 슬라이드.
// 구간 안에서는 카메라가 고정되므로 걷는 동안 타일 이음새 흔들림이 없다.
[RequireComponent(typeof(Camera))]
public class SectionCamera : MonoBehaviour
{
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
    Vector3 focusOffset;   // FocusPulse가 파고들 때 basePos에 더해지는 오프셋
    float focusZoomDelta;  // orthographicSize에 더해지는 값(음수=줌인)
    int focusToken;        // 중복 FocusPulse 호출 시 이전 코루틴을 무력화(값만 덮어씀, 안전한 종료 보장)

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

    float baseOrthoSize; // FocusPulse가 줌을 되돌릴 기준값(구간 크기 auto-계산도 이 값으로 고정해 줌 중 흔들림 방지)

    void Awake()
    {
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

        float w = sectionSize.x > 0.01f ? sectionSize.x : baseOrthoSize * 2f * cam.aspect;
        float h = sectionSize.y > 0.01f ? sectionSize.y : baseOrthoSize * 2f;

        // 플레이어가 속한 구간의 인덱스 → 그 구간의 중심으로 카메라 목표 설정
        float sx = Mathf.Floor((target.position.x - gridOrigin.x) / w);
        float sy = Mathf.Floor((target.position.y - gridOrigin.y) / h);
        float cx = gridOrigin.x + (sx + 0.5f) * w;
        float cy = gridOrigin.y + (sy + 0.5f) * h;
        targetPos = new Vector3(cx, cy, basePos.z);

        float t = 1f - Mathf.Exp(-slideSpeed * Time.deltaTime);
        basePos = Vector3.Lerp(basePos, targetPos, t);

        transform.position = basePos + shakeOffset + focusOffset;
        cam.orthographicSize = baseOrthoSize + focusZoomDelta;
    }
}
