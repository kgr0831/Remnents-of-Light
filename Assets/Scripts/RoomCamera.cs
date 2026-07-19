using UnityEngine;

// 방 단위 고정 카메라 (Sanabi 스타일). 현재 방을 프레임하도록 위치/오쏘 사이즈를 부드럽게 슬라이드.
[RequireComponent(typeof(Camera))]
public class RoomCamera : MonoBehaviour
{
    public static RoomCamera Instance;

    [Tooltip("방 전환 슬라이드 속도 (클수록 빠름)")]
    public float slideSpeed = 8f;

    private Camera cam;
    private Vector3 targetPos;
    private float targetSize;
    private bool hasRoom;

    void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        targetPos = transform.position;
        targetSize = cam.orthographicSize;
    }

    // 방 경계(월드 Bounds)를 받아 카메라가 그 방을 꽉 채우도록 목표값 설정
    public void EnterRoom(Bounds b)
    {
        float halfH = b.size.y * 0.5f;
        float halfW = (b.size.x * 0.5f) / cam.aspect;
        targetSize = Mathf.Max(halfH, halfW);
        targetPos = new Vector3(b.center.x, b.center.y, transform.position.z);
        hasRoom = true;
    }

    void LateUpdate()
    {
        if (!hasRoom) return;
        // 프레임 독립적인 지수 감쇠 보간
        float t = 1f - Mathf.Exp(-slideSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPos, t);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, t);
    }
}
