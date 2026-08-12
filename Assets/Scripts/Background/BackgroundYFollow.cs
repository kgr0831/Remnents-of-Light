using UnityEngine;

public class BackgroundYFollow : MonoBehaviour
{
    [Header("카메라 설정")]
    [SerializeField] private Transform targetCamera; // 따라갈 메인 카메라

    [Header("이동 비율 (Parallax Rate)")]
    [Range(0f, 1f)]
    [SerializeField] private float parallaxX = 0.3f; // X축(가로): 천천히 움직여 입체감 연출 (0.3 = 카메라의 30% 속도)
    [SerializeField] private bool followY = true;     // Y축(세로): 카메라 고정 여부

    [Header("오프셋")]
    [SerializeField] private Vector3 offset = new Vector3(0, 0, 10); // 카메라와의 거리/위치 보정

    private Vector3 lastCamPos;

    void Start()
    {
        // 타겟 카메라이 지정되지 않았으면 메인 카메라를 자동으로 찾음
        if (targetCamera == null)
        {
            if (Camera.main != null)
                targetCamera = Camera.main.transform;
            else
                Debug.LogWarning("메인 카메라를 찾을 수 없습니다! Inspecter에서 Target Camera를 할당해주세요.");
        }

        if (targetCamera != null)
        {
            lastCamPos = targetCamera.position;
        }
    }

    void LateUpdate()
    {
        if (targetCamera == null) return;

        // 카메라의 이동량 계산
        Vector3 deltaCam = targetCamera.position - lastCamPos;

        // Y축은 카메라 Y 좌표를 그대로(100%) 추적하여 빈 공간이 보이는 것을 방지
        float newY = followY ? targetCamera.position.y + offset.y : transform.position.y;
        
        // X축은 지정한 Parallax 비율(0.3 등)로 천천히 이동
        float newX = transform.position.x + (deltaCam.x * parallaxX);

        transform.position = new Vector3(newX, newY, transform.position.z);

        lastCamPos = targetCamera.position;
    }
}