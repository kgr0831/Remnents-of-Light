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

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (target == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) target = p.transform;
        }
        targetPos = transform.position;
    }

    void LateUpdate()
    {
        if (target == null) return;

        float w = sectionSize.x > 0.01f ? sectionSize.x : cam.orthographicSize * 2f * cam.aspect;
        float h = sectionSize.y > 0.01f ? sectionSize.y : cam.orthographicSize * 2f;

        // 플레이어가 속한 구간의 인덱스 → 그 구간의 중심으로 카메라 목표 설정
        float sx = Mathf.Floor((target.position.x - gridOrigin.x) / w);
        float sy = Mathf.Floor((target.position.y - gridOrigin.y) / h);
        float cx = gridOrigin.x + (sx + 0.5f) * w;
        float cy = gridOrigin.y + (sy + 0.5f) * h;
        targetPos = new Vector3(cx, cy, transform.position.z);

        float t = 1f - Mathf.Exp(-slideSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPos, t);
    }
}
