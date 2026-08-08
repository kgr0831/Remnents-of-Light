using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 플레이 모드 없이 에디터(씬 뷰)에서 이 오브젝트의 BoxCollider2D를 드래그해 크기/위치를 바꾸면
// 그 즉시 메인 카메라가 그 박스에 맞춰지고, 화면비가 안 맞는 만큼 레터박스(검은 바)가 생긴다 —
// SectionCamera.EnterRoom의 레터박스 계산(2026-08-08)과 같은 공식을 쓰되, RoomTrigger/SectionCamera
// 자체는 건드리지 않는다(플레이 모드 진입 시엔 이 스크립트가 손 떼고 기존 트리거 로직이 담당).
//
// 버그(2026-08-08): 프리뷰가 카메라의 orthoSize/rect를 바꿔놓은 채로 플레이 모드에 들어가면,
// SectionCamera.Awake가 그 "깨진" 값을 baseOrthoSize로 캡처해버려서 보스가 엉뚱한 위치로
// 튀었다. 플레이 모드에 진입하는 순간(ExitingEditMode) 정상 기본값으로 되돌린다.
[ExecuteAlways]
[RequireComponent(typeof(BoxCollider2D))]
public class BossCameraFramePreview : MonoBehaviour
{
    // 런타임에 "원래 카메라 상태"를 캡처해뒀다가 되돌리는 방식은 캡처 타이밍이 꼬이면(예: 이미
    // 프리뷰가 카메라를 바꿔놓은 뒤에 캡처됨) 깨진 값을 "원본"으로 저장해버리는 문제가 있었다
    // (실제로 한 번 발생 — 플레이 모드 진입 시 깨진 orthoSize/rect로 복구됨). 그래서 프로젝트의
    // 정상 기본값(SectionCamera 기준 orthoSize=9, 풀스크린 rect)을 직접 박아넣는다.
    static readonly float NormalOrthoSize = 9f;
    static readonly Rect NormalRect = new Rect(0f, 0f, 1f, 1f);

    BoxCollider2D box;
    Camera cam;

    void OnEnable()
    {
        box = GetComponent<BoxCollider2D>();
        cam = Camera.main;
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
        RestoreNormal();
    }

    void RestoreNormal()
    {
        if (cam == null || Application.isPlaying) return;
        cam.orthographicSize = NormalOrthoSize;
        cam.rect = NormalRect;
    }

#if UNITY_EDITOR
    void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // 플레이 모드로 넘어가기 직전 — SectionCamera.Awake가 값을 읽기 전에 원상복구해야 한다.
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            RestoreNormal();
        }
    }
#endif

    void Update()
    {
        if (Application.isPlaying) return; // 플레이 중엔 RoomTrigger → SectionCamera가 담당
        if (cam == null) cam = Camera.main;
        if (cam == null || box == null) return;

        Vector2 size = box.size;
        if (size.x < 0.01f || size.y < 0.01f) return;

        Vector3 center = transform.TransformPoint(box.offset);
        Vector3 camPos = cam.transform.position;
        cam.transform.position = new Vector3(center.x, center.y, camPos.z);
        cam.orthographicSize = size.y * 0.5f;

        float boxAspect = size.x / size.y;
        float screenAspect = (float)Screen.width / Mathf.Max(1, Screen.height);

        Rect r;
        if (boxAspect > screenAspect)
        {
            float h = screenAspect / boxAspect;
            r = new Rect(0f, (1f - h) * 0.5f, 1f, h);
        }
        else
        {
            float w = boxAspect / screenAspect;
            r = new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
        cam.rect = r;
    }
}
