using System.Collections;
using UnityEngine;

// Sword 트리거에 플레이어가 겹치는 동안 SwordUI를 띄운다. 벗어나면 사라진다.
//
// CanvasGroup 하나로 하위 전체(Image · Text · Outline)의 알파를 다룬다 — Outline은 Graphic과 별개인
// effectColor 알파를 갖고 있어서, Graphic.color만 건드리면 글자는 사라지는데 테두리가 남는다.
// (SwordUI가 RectTransform이 아니라 일반 Transform인데도 CanvasGroup 상속은 정상 동작하는 것을
//  에디터에서 실측 확인함 — 하위 3개 그래픽 모두 inheritedAlpha가 따라옴)
[RequireComponent(typeof(BoxCollider2D))]
public class SwordUIProximity : MonoBehaviour
{
    public GameObject swordUI;
    public float fadeDuration = 0.25f;

    /// <summary>UI가 떠 있는가(페이드 인 시작 ~ 페이드 아웃 시작). SwordPickupSequence가 F 입력을
    /// 받아도 되는지 판단하는 데 쓴다 — 사라지는 중에 누른 F는 안 먹어야 한다.</summary>
    public bool Shown { get; private set; }

    CanvasGroup group;
    Coroutine fade;

    void Awake()
    {
        if (swordUI == null) { enabled = false; return; }

        group = swordUI.GetComponent<CanvasGroup>();
        if (group == null) group = swordUI.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        swordUI.SetActive(false);
    }

    void OnTriggerEnter2D(Collider2D other) { if (other.CompareTag("Player")) FadeTo(1f); }
    void OnTriggerExit2D(Collider2D other)  { if (other.CompareTag("Player")) FadeTo(0f); }

    void FadeTo(float target)
    {
        Shown = target > 0f;
        if (fade != null) StopCoroutine(fade);
        // 알파만 올려서는 안 된다 — 오브젝트가 꺼져 있으면 CanvasGroup을 아무리 건드려도 안 보인다.
        // 켤 때는 먼저 활성화하고, 끌 때는 페이드가 끝난 뒤에 비활성화한다.
        if (target > 0f) swordUI.SetActive(true);
        fade = StartCoroutine(Fade(target));
    }

    IEnumerator Fade(float target)
    {
        float from = group.alpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        group.alpha = target;
        if (target <= 0f) swordUI.SetActive(false);
        fade = null;
    }
}
