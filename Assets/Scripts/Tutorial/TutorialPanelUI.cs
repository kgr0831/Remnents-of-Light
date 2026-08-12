using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 튜토리얼 설명 패널(SwordPanel 재사용).
//
//   패널 애니메이션 정방향 → text-action-1 타이핑 → text-action-2 타이핑
//   → (호출부가 원하는 만큼 대기) → 둘 동시에 역타이핑 → 패널 애니메이션 역재생 → 비활성
//
// SwordPickupSequence의 검증된 방식을 그대로 따른다.
//  ⚠️ Animator.speed=-1 역재생은 스프라이트(오브젝트 레퍼런스) 커브에서 제대로 안 걸린다
//     (2026-08-12 실측) — 그래서 Animator를 꺼두고 배열을 직접 훑는다. 역재생은 배열을 거꾸로 훑는다.
//  ⚠️ 모든 대기는 unscaled다 — 튜토리얼은 Time.timeScale=0으로 세계를 멈춘 채 이 패널을 띄운다.
public class TutorialPanelUI : MonoBehaviour
{
    [Tooltip("SwordPanel 루트(켜고 끈다). 씬에는 꺼진 채로 저장해 둔다")]
    public GameObject panelRoot;
    [Tooltip("SwordPanel의 Animator — 켜져 있으면 매 프레임 Image.sprite를 덮어써서 이 스크립트의 프레임 전환이 무시된다. Awake에서 끈다")]
    public Animator panelAnimator;
    [Tooltip("프레임 스프라이트를 꽂을 Image")]
    public Image panelImage;
    [Tooltip("**열리는 순서**(0 → 마지막). 역재생은 이 배열을 거꾸로 훑는다")]
    public Sprite[] panelFrames;
    [Tooltip("초당 프레임 수(원본 클립 12fps)")]
    public float panelFrameRate = 12f;

    public Text actionText1;   // text-action-1 ("SYSTEM MESSAGE")
    public Text actionText2;   // text-action-2 (동작 설명)

    [Header("타이핑")]
    [Tooltip("글자 하나당 간격(초). 기존 text-tr 타이핑과 같은 값")]
    public float charInterval = 0.06f;
    [Tooltip("사라질 때(역타이핑) 속도 배율. 3이면 타이핑의 3배 빠르게 지워진다(사용자 지시 2026-08-12)")]
    public float untypeSpeedMultiplier = 3f;

    /// <summary>"지금 재생 중인 효과 하나를 건너뛰라"는 입력이 들어왔는지 묻는다 —
    /// 들어왔으면 그 자리에서 **소비**된다(한 번 누르면 한 효과만 넘어간다).
    /// <see cref="TutorialDirector"/>가 연결한다. 비어 있으면 스킵 없이 전부 재생된다.</summary>
    [System.NonSerialized] public System.Func<bool> consumeSkip;

    bool skipTriggered;   // 직전 WaitOrSkip이 스킵으로 끝났는가
    bool typingSkipped;   // 이번 Open()의 대사 타이핑을 이미 건너뛰었는가(두 줄을 한 번에 다 띄운다)

    [Header("사운드")]
    public AudioSource sfxSource;
    [Tooltip("패널 정방향 · 역재생 시작 때 각각 1회")]
    public AudioClip panelSfx;
    [Tooltip("타이핑이 도는 동안 루프로 깔린다")]
    public AudioClip typingSfx;

    string line1, line2;   // 지금 떠 있는 문구 — 역타이핑이 되돌릴 원본

    void Awake()
    {
        if (panelAnimator != null) panelAnimator.enabled = false;
        if (actionText1 != null) actionText1.text = string.Empty;
        if (actionText2 != null) actionText2.text = string.Empty;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>패널을 열고 두 줄을 차례로 타이핑한다. 끝나면 문구가 떠 있는 상태로 반환.</summary>
    public IEnumerator Open(string message1, string message2)
    {
        typingSkipped = false;
        line1 = message1 ?? string.Empty;
        line2 = message2 ?? string.Empty;

        if (actionText1 != null) actionText1.text = string.Empty;
        if (actionText2 != null) actionText2.text = string.Empty;
        if (panelRoot != null)
        {
            // ScreenBlackout이 Awake에서 자기 검은 패널을 맨 마지막 형제로 밀어 넣는다 —
            // 인트로·마무리 문구는 **완전 암전 위에** 떠야 하므로 그때마다 다시 맨 위로 올린다.
            panelRoot.transform.SetAsLastSibling();
            panelRoot.SetActive(true);
        }

        yield return PlayPanel(true);
        yield return Type(actionText1, line1);
        yield return Type(actionText2, line2);
    }

    /// <summary>문구를 역타이핑으로 지우고 패널을 역재생으로 닫는다.</summary>
    public IEnumerator Close()
    {
        yield return UntypeBoth();
        yield return PlayPanel(false);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>연출이 중간에 끊겼을 때의 즉시 정리(코루틴 없이).</summary>
    public void HideImmediate()
    {
        EndTypingSfx();
        if (actionText1 != null) actionText1.text = string.Empty;
        if (actionText2 != null) actionText2.text = string.Empty;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    IEnumerator PlayPanel(bool forward)
    {
        if (panelImage == null || panelFrames == null || panelFrames.Length == 0) yield break;

        if (sfxSource != null && panelSfx != null) sfxSource.PlayOneShot(panelSfx);

        float step = panelFrameRate > 0f ? 1f / panelFrameRate : 1f / 12f;
        int n = panelFrames.Length;
        for (int i = 0; i < n; i++)
        {
            panelImage.sprite = forward ? panelFrames[i] : panelFrames[n - 1 - i];
            yield return WaitOrSkip(step);
            if (skipTriggered)
            {
                // 이 애니메이션만 건너뛴다 — 마지막 프레임으로 확정하고 끝.
                panelImage.sprite = forward ? panelFrames[n - 1] : panelFrames[0];
                yield break;
            }
        }
    }

    /// <summary>스킵을 받을 수 있는 실시간 대기. 스킵으로 끝났으면 <see cref="skipTriggered"/>가 true다.
    /// WaitForSecondsRealtime은 중간에 끊을 수 없어 직접 센다(첫 프레임 델타 폭주 대비 클램프 포함).</summary>
    IEnumerator WaitOrSkip(float seconds)
    {
        skipTriggered = false;
        float t = 0f;
        while (t < seconds)
        {
            if (consumeSkip != null && consumeSkip()) { skipTriggered = true; yield break; }
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            yield return null;
        }
    }

    IEnumerator Type(Text target, string message)
    {
        if (target == null || string.IsNullOrEmpty(message)) yield break;

        // 이번 대사는 이미 건너뛰기로 했다 — 둘째 줄도 기다리지 않고 통째로 띄운다.
        // ("대사 타이핑 중이면 대사 다 띄우는 걸로 스킵" — 한 줄만 완성하고 다음 줄을 다시
        //  타이핑하기 시작하면 스킵한 것처럼 안 보인다.)
        if (typingSkipped) { target.text = message; yield break; }

        target.text = string.Empty;
        BeginTypingSfx();
        for (int i = 1; i <= message.Length; i++)
        {
            target.text = message.Substring(0, i);
            yield return WaitOrSkip(charInterval);
            if (skipTriggered) { typingSkipped = true; target.text = message; break; }
        }
        EndTypingSfx();
    }

    // 둘을 같은 시계로 동시에 줄인다 — 길이가 달라도 같은 순간에 비워진다(짧은 쪽이 먼저 바닥에서 멈춤).
    IEnumerator UntypeBoth()
    {
        int len1 = line1 != null ? line1.Length : 0;
        int len2 = line2 != null ? line2.Length : 0;
        // 지워지는 건 읽을 필요가 없는 구간이라 타이핑보다 빠르게 넘긴다(사용자 지시).
        float step = charInterval / Mathf.Max(0.01f, untypeSpeedMultiplier);

        for (int i = Mathf.Max(len1, len2) - 1; i >= 0; i--)
        {
            if (actionText1 != null) actionText1.text = line1.Substring(0, Mathf.Min(i, len1));
            if (actionText2 != null) actionText2.text = line2.Substring(0, Mathf.Min(i, len2));
            yield return WaitOrSkip(step);
            if (skipTriggered) break;   // 스킵: 아래에서 두 줄을 한 번에 비운다
        }
        if (actionText1 != null) actionText1.text = string.Empty;
        if (actionText2 != null) actionText2.text = string.Empty;
    }

    void BeginTypingSfx()
    {
        if (sfxSource == null || typingSfx == null) return;
        sfxSource.clip = typingSfx;
        sfxSource.loop = true;
        sfxSource.Play();
    }

    void EndTypingSfx()
    {
        if (sfxSource == null) return;
        sfxSource.Stop();
        sfxSource.loop = false;
        sfxSource.clip = null;
    }

    void OnDisable()
    {
        EndTypingSfx();   // 타이핑 도중 꺼지면 루프가 계속 돈다
    }
}
