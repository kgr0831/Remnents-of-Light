using UnityEngine;

// 방 하나를 나타내는 트리거 볼륨. 플레이어가 들어오면 SectionCamera에 방 경계를 알린다.
// (2026-08-03, 사용자 지시로 부활 — 원래는 RoomCamera를 불렀지만 그 컴포넌트는 Main Camera에
// 붙어있지 않아 죽은 경로였다. SectionCamera.EnterRoom으로 로직을 이식해 실제로 동작하게 함.)
[RequireComponent(typeof(BoxCollider2D))]
public class RoomTrigger : MonoBehaviour
{
    private BoxCollider2D box;

    void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
    }

    void Start()
    {
        // 시작 시 플레이어가 이미 이 방 안에 있으면 즉시 프레임
        if (SectionCamera.Instance == null) return;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && box.OverlapPoint(player.transform.position))
            SectionCamera.Instance.EnterRoom(box.bounds);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (SectionCamera.Instance != null)
            SectionCamera.Instance.EnterRoom(box.bounds);
    }
}
