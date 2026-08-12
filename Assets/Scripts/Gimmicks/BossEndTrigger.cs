using UnityEngine;

// 보스전 종료 지점 - 플레이어가 닿으면 보스가 빔을 멈추고(페이드아웃 + 현재 걸린 빔 효과 전부 해제)
// 오른쪽 상단으로 물러나며 사라진다(사용자 지시 2026-08-12).
// 연출 본체는 BossEyeTracker.BeginRetreat()에 있고 여기선 1회성 감지만 한다.
// ⚠️ 씬의 콜라이더가 IsTrigger가 꺼진 채로 배치돼 있어(맵 디자이너 배치) 그대로 두면 벽이 되어
// 플레이어가 닿지도 못한다 — IntroTextTrigger·FanActiv와 같은 방식으로 Awake에서 강제한다.
[RequireComponent(typeof(Collider2D))]
public class BossEndTrigger : MonoBehaviour
{
    [Tooltip("비워두면 씬에서 BossEyeTracker를 자동으로 찾는다")]
    public BossEyeTracker boss;

    bool fired;

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (fired) return;
        if (!other.CompareTag("Player")) return;

        if (boss == null) boss = FindFirstObjectByType<BossEyeTracker>();
        if (boss == null)
        {
            Debug.LogWarning("[BossEndTrigger] 씬에서 BossEyeTracker를 찾지 못해 퇴장 연출을 건너뜁니다.", this);
            return;
        }

        fired = true;
        boss.BeginRetreat();
        TestLog.Event("boss_beam", "end_trigger_hit -> retreat");
    }
}
