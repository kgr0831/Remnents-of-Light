using UnityEngine;

// 방 하나를 나타내는 트리거 볼륨. 플레이어가 들어오면 RoomCamera에 방 경계를 알림.
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
        if (RoomCamera.Instance == null) return;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && box.OverlapPoint(player.transform.position))
            RoomCamera.Instance.EnterRoom(box.bounds);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (RoomCamera.Instance != null)
            RoomCamera.Instance.EnterRoom(box.bounds);
    }
}
