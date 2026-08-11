using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 컷신 나레이션 — 문구를 한 덩어리씩 페이드 인 → 유지 → 페이드 아웃으로 흘린다.
// 입력을 기다리지 않는다: 오프닝은 플레이어에게 F 연타를 시키면 몰입이 끊긴다.
// (입력을 받는 대사 UI는 IntroTextSequence 쪽이다 — 타이핑 + F 대기 + 레터박스 개폐까지 묶여 있다)
//
// 페이드는 Text의 CanvasGroup 하나로 처리한다 — Text.color 알파만 건드리면 Outline·자식이 안 따라온다
// (IntroTextSequence와 같은 이유).
public class CutsceneNarration : MonoBehaviour
{
    [Tooltip("한 항목 = 한 화면. 줄바꿈으로 여러 줄을 넣을 수 있다")]
    [TextArea(2, 5)] public string[] lines;

    public Text target;

    [Tooltip("이 암전이 완전히 걷힌 뒤부터 startDelay를 센다. 비워두면 씬 시작 기준")]
    public ScreenBlackoutOnStart waitFor;

    [Header("타이밍(초)")]
    [Tooltip("암전이 걷힌 뒤 첫 문구가 뜨기까지의 대기")]
    public float startDelay = 2f;
    public float fadeIn = 1f;
    public float hold = 2.6f;
    public float fadeOut = 0.8f;
    [Tooltip("문구와 문구 사이의 빈 화면 시간")]
    public float gap = 0.7f;

    /// <summary>마지막 문구까지 다 흘렀는지. 뒤따르는 연출(IntroFallCutscene)이 이 값을 기다린다.</summary>
    public bool Done { get; private set; }

    CanvasGroup group;

    IEnumerator Start()
    {
        group = target.GetComponent<CanvasGroup>();
        if (group == null) group = target.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = 0f;
        target.gameObject.SetActive(true);

        while (waitFor != null && !waitFor.Done) yield return null;
        if (startDelay > 0f) yield return new WaitForSecondsRealtime(startDelay);

        for (int i = 0; i < lines.Length; i++)
        {
            target.text = lines[i];
            yield return Fade(0f, 1f, fadeIn);
            if (hold > 0f) yield return new WaitForSecondsRealtime(hold);
            yield return Fade(1f, 0f, fadeOut);
            if (gap > 0f) yield return new WaitForSecondsRealtime(gap);
        }

        target.gameObject.SetActive(false);
        Done = true;
    }

    // 컷신 연출은 timeScale에 영향받지 않아야 한다(프로젝트 컨벤션).
    IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f) { group.alpha = to; yield break; }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        group.alpha = to;
    }
}
