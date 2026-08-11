using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// text-tr / text-tr-2를 쓰는 대사 표시기.
//   타이핑 → text-tr-2 점멸 → F 입력 → 페이드아웃
// 이 부분은 ShowMessage(문구)로 떼어 놨다 — 씬 시작 연출(IntroStartSequence)과 Trigger-Text 양쪽이
// 같은 UI를 서로 다른 문구로 재사용한다.
//
// Play()는 Trigger-Text 경로 전용으로, 위 흐름에 레터박스 등·퇴장과 이동 잠금을 더한 것이다.
//
// 트리거 오브젝트가 아니라 Canvas에 붙인다 — 트리거는 진입 즉시 자신을 삭제하므로, 시퀀스가 거기
// 얹혀 있으면 코루틴이 첫 프레임에 같이 죽는다.
//
// 페이드는 text-tr의 CanvasGroup 하나로 처리한다. text-tr-2가 text-tr의 자식이라 한 번에 묶이고,
// Text.color의 알파만 건드리는 방식과 달리 자식과 Outline까지 확실히 따라온다.
public class IntroTextSequence : MonoBehaviour
{
    public CinematicLetterbox letterbox;
    public Text mainText;     // text-tr
    public Text promptText;   // text-tr-2 (mainText의 자식)

    [Tooltip("Trigger-Text가 발동하는 순간 이 카메라를 정지시킨다(이후 구간 이동 없음). 비워두면 안 건드림")]
    public SectionCamera freezeCamera;

    [Header("타이핑")]
    [Tooltip("글자 하나당 간격(초)")]
    public float charInterval = 0.06f;

    [Header("프롬프트(text-tr-2) 맥동 — 페이드 인 → 유지 → 페이드 아웃 → 대기 반복")]
    public float promptFadeIn = 0.4f;
    public float promptHold = 0.9f;
    public float promptFadeOut = 0.4f;
    [Tooltip("완전히 사라진 뒤 다시 나타나기까지의 대기(초)")]
    public float promptGap = 0.35f;

    [Header("페이드")]
    public float textFadeOut = 0.35f;

    /// <summary>대사가 떠 있는 동안 true — 다른 연출이 같은 UI를 겹쳐 쓰는 걸 막는다.</summary>
    public bool Busy { get; private set; }

    CanvasGroup textGroup;    // text-tr — 대사 전체(자식 포함) 페이드
    CanvasGroup promptGroup;  // text-tr-2 — 맥동. 중첩 CanvasGroup은 알파가 곱해지므로 위와 안 밟는다
    string triggerMessage;
    bool triggerDone;
    Coroutine pulse;

    void Awake()
    {
        textGroup = mainText.GetComponent<CanvasGroup>();
        if (textGroup == null) textGroup = mainText.gameObject.AddComponent<CanvasGroup>();
        textGroup.interactable = false;
        textGroup.blocksRaycasts = false;

        promptGroup = promptText.GetComponent<CanvasGroup>();
        if (promptGroup == null) promptGroup = promptText.gameObject.AddComponent<CanvasGroup>();
        promptGroup.interactable = false;
        promptGroup.blocksRaycasts = false;

        triggerMessage = mainText.text;   // Trigger-Text용 문구는 씬에 적어둔 것을 그대로 쓴다
        mainText.gameObject.SetActive(false);
        promptText.gameObject.SetActive(false);
    }

    // ── Trigger-Text 경로 ────────────────────────────────────────────────────────────────────
    public void Play()
    {
        if (Busy || triggerDone) return;
        StartCoroutine(TriggerSequence());
    }

    IEnumerator TriggerSequence()
    {
        TutorialMover.InputLocked = true;

        // 이 시점부터 카메라를 고정한다(사용자 지시) — 이후 플레이어가 오른쪽 끝에서 떨어져도
        // 구간이 따라 내려가지 않고 화면 밖으로 사라진다. 다시 켜지 않는다.
        if (freezeCamera != null) freezeCamera.enabled = false;
        yield return letterbox.Show();
        yield return ShowMessage(triggerMessage);
        yield return letterbox.Hide();
        TutorialMover.InputLocked = false;
        triggerDone = true;
    }

    // ── 공용 대사 표시 ───────────────────────────────────────────────────────────────────────
    /// <summary>문구를 타이핑하고, 프롬프트를 점멸시키고, F를 기다렸다가 페이드아웃한다.</summary>
    public IEnumerator ShowMessage(string message)
    {
        Busy = true;

        textGroup.alpha = 1f;
        mainText.text = string.Empty;
        mainText.gameObject.SetActive(true);
        for (int i = 1; i <= message.Length; i++)
        {
            mainText.text = message.Substring(0, i);
            yield return new WaitForSecondsRealtime(charInterval);
        }

        promptGroup.alpha = 0f;      // 안 보이는 데서 페이드 인으로 시작
        promptText.gameObject.SetActive(true);
        pulse = StartCoroutine(PromptPulse());

        // 타이핑이 끝난 뒤부터 F를 받는다(도중에 누른 건 무시).
        while (!KeyPressedThisFrame(Key.F)) yield return null;

        // 맥동을 멈추되 알파는 그 순간 값을 그대로 둔다 — 1로 되돌리면 툭 밝아졌다 사라져 보인다.
        // 남은 알파는 부모 textGroup 페이드에 곱해져 자연스럽게 같이 사라진다.
        if (pulse != null) { StopCoroutine(pulse); pulse = null; }

        float t = 0f;
        while (t < textFadeOut)
        {
            t += Time.unscaledDeltaTime;
            textGroup.alpha = 1f - Mathf.Clamp01(t / textFadeOut);
            yield return null;
        }
        textGroup.alpha = 0f;
        mainText.gameObject.SetActive(false);
        promptText.gameObject.SetActive(false);

        Busy = false;
    }

    // 페이드 인 → 잠시 유지 → 페이드 아웃 → 잠시 대기를 반복한다.
    IEnumerator PromptPulse()
    {
        while (true)
        {
            yield return FadeGroup(promptGroup, 0f, 1f, promptFadeIn);
            if (promptHold > 0f) yield return new WaitForSecondsRealtime(promptHold);
            yield return FadeGroup(promptGroup, 1f, 0f, promptFadeOut);
            if (promptGap > 0f) yield return new WaitForSecondsRealtime(promptGap);
        }
    }

    static IEnumerator FadeGroup(CanvasGroup g, float from, float to, float duration)
    {
        if (duration <= 0f) { g.alpha = to; yield break; }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        g.alpha = to;
    }

    // ⚠️ Keyboard.current를 쓰지 않는다 — PlayTest가 남긴 가상 키보드가 current를 가로채면 실제 입력이
    //    무시된다(PlayerController.KeyPressedThisFrame과 같은 이유).
    public static bool KeyPressedThisFrame(Key key)
    {
        var devices = InputSystem.devices;
        for (int i = 0; i < devices.Count; i++)
            if (devices[i] is Keyboard kb && kb[key].wasPressedThisFrame) return true;
        return false;
    }

    // 연출 도중 씬이 바뀌거나 오브젝트가 꺼져도 이동 잠금이 남지 않게 한다.
    void OnDisable()
    {
        if (Busy) TutorialMover.InputLocked = false;
    }
}
