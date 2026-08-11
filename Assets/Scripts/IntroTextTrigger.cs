using UnityEngine;

// Trigger-Text 전용 1회성 트리거 — 플레이어가 닿으면 시퀀스를 시작하고 자신을 삭제한다.
// 시퀀스 본체는 Canvas에 있으므로 이 오브젝트가 사라져도 연출은 계속 돈다.
[RequireComponent(typeof(BoxCollider2D))]
public class IntroTextTrigger : MonoBehaviour
{
    public IntroTextSequence sequence;

    void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (sequence != null) sequence.Play();
        else Debug.LogWarning("[IntroTextTrigger] sequence가 비어 있어 연출을 건너뜁니다.", this);

        Destroy(gameObject);
    }
}
