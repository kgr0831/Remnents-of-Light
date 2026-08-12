using UnityEngine;
using UnityEngine.SceneManagement;

// 방 하나를 나타내는 트리거 볼륨. 플레이어가 들어오면 SectionCamera에 방 경계를 알리고, 그 지점을
// 체크포인트로 자동 저장한다(기능_구현_명세서 1장 — 세이브 포인트 오브젝트 폐지, 방 진입마다 자동 저장,
// 사망 시 그 방 입구에서 즉시 부활. 사용자 확정 2026-08-10).
// (2026-08-03, 사용자 지시로 부활 — 원래는 RoomCamera를 불렀지만 그 컴포넌트는 Main Camera에
// 붙어있지 않아 죽은 경로였다. SectionCamera.EnterRoom으로 로직을 이식해 실제로 동작하게 함.)
[RequireComponent(typeof(BoxCollider2D))]
public class RoomTrigger : MonoBehaviour
{
    // 이 방의 화면비를 그대로 지키고(카메라가 그냥 더 넓게 보여주는 대신) 화면비 안 맞는 만큼
    // 레터박스/필러박스(검은 바)로 채운다 — 기본 false라 기존 방들은 동작 그대로.
    public bool useLetterbox = false;

    private BoxCollider2D box;

    void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
    }

    void Start()
    {
        // 시작 시 플레이어가 이미 이 방 안에 있으면 즉시 프레임 + 체크포인트 저장
        if (SectionCamera.Instance == null) return;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && box.OverlapPoint(player.transform.position))
        {
            SectionCamera.Instance.EnterRoom(box.bounds, useLetterbox);
            SaveCheckpointFor(player);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (SectionCamera.Instance != null)
            SectionCamera.Instance.EnterRoom(box.bounds, useLetterbox);
        SaveCheckpointFor(other.gameObject);
    }

    /// <summary>worldPos를 품는 방을 찾아 카메라를 그 방으로 다시 프레이밍한다. 찾았으면 true.
    ///
    /// 순간이동(낙사 복귀 · 체크포인트 부활)은 <c>Time.timeScale = 0</c> 상태에서 일어나는데, 그때는
    /// 물리가 한 스텝도 안 돌아 <see cref="OnTriggerEnter2D"/>가 **발생하지 않는다**. 그래서 방을
    /// 넘어가는 복귀인데도 카메라는 옛 방의 중심·크기·레터박스를 그대로 붙들고 있었다
    /// (사용자 리포트 2026-08-13 "구간 전환이 틀어짐"). 트리거에 의존하지 않고 직접 찾는다.
    ///
    /// 방이 겹쳐 있으면 **더 작은 쪽**을 고른다 — OnTriggerEnter2D 경로는 "마지막에 들어간 것"이
    /// 이겨서 진입 순서에 따라 결과가 달라지지만, 이쪽은 항상 같은 답이 나와야 한다.</summary>
    public static bool ApplyRoomAt(Vector3 worldPos)
    {
        if (SectionCamera.Instance == null) return false;

        RoomTrigger best = null;
        Bounds bestBounds = default;

        var rooms = FindObjectsByType<RoomTrigger>(FindObjectsSortMode.None);
        for (int i = 0; i < rooms.Length; i++)
        {
            // box는 Awake에서 채워지는데 그게 아직 안 돌았을 수 있어 직접 가져온다.
            var b = rooms[i].GetComponent<BoxCollider2D>();
            if (b == null || !b.OverlapPoint(worldPos)) continue;
            if (best != null && b.bounds.size.sqrMagnitude >= bestBounds.size.sqrMagnitude) continue;

            best = rooms[i];
            bestBounds = b.bounds;
        }
        if (best == null) return false;

        SectionCamera.Instance.EnterRoom(bestBounds, best.useLetterbox);
        return true;
    }

    void SaveCheckpointFor(GameObject player)
    {
        GameDataManager.SaveCheckpoint(SceneManager.GetActiveScene().name, player.transform.position);
        player.GetComponentInParent<PlayerController>()?.NotifyRoomEntered();
    }
}
