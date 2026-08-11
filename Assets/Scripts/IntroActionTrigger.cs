using UnityEngine;

// Trigger-Action 전용 1회성 감지기 — 플레이어가 닿았는지만 IntroFallStartSequence에 알린다.
//
// IntroTextTrigger와 달리 자신을 삭제하지 않는다: 저쪽은 시퀀스를 한 번 호출하고 끝이라 사라져도
// 되지만, 여기서는 시퀀스가 Fired를 폴링하며 기다리므로 삭제하면 참조가 죽어 대사가 영영 안 뜬다.
// 대신 콜라이더만 꺼서 재발동을 막는다.
[RequireComponent(typeof(BoxCollider2D))]
public class IntroActionTrigger : MonoBehaviour
{
    /// <summary>플레이어가 한 번이라도 닿았는가.</summary>
    public bool Fired { get; private set; }

    BoxCollider2D box;

    void Awake()
    {
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (Fired || !other.CompareTag("Player")) return;

        Fired = true;
        box.enabled = false;
    }
}
