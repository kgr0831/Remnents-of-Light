using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 검 획득 컷신. SwordUI가 떠 있는 동안 F를 누르면 아래 순서로 끝까지 진행한다.
//
//   암전 페이드 인 → [SwordUI · Sword 삭제] → 0.5초 대기 → 암전 페이드 아웃
//   → 레터박스 등장 → text-tr "카타나 코어 모듈을 획득했다." (F로 닫기)
//   → SwordPanel 활성화 + 애니메이션 정방향 1회
//   → text-action-1 타이핑 → text-action-2 타이핑 → 0.5초 대기 → 둘 동시에 역타이핑
//   → SwordPanel 애니메이션 역재생 → 글리치 → 암전 + BGM 페이드 아웃 → 0.5초 → TutorialScene
//
// ⚠️ Sword에 붙이면 안 된다 — 연출 도중 Sword를 삭제하므로 코루틴이 거기서 같이 죽는다.
//    삭제 대상이 아닌 Canvas에 얹는다(IntroTextSequence·IntroFallStartSequence와 같은 이유).
//
// 타이핑 간격은 IntroTextSequence.charInterval을 그대로 읽어 쓴다 — "기존 text-tr과 같은 타이핑
// 효과"라는 요구라, 값을 복제해 두면 한쪽만 고쳤을 때 티 나게 어긋난다.
public class SwordPickupSequence : MonoBehaviour
{
    [Header("발동 조건")]
    [Tooltip("이게 떠 있는 동안에만 F를 받는다 (Sword의 SwordUIProximity)")]
    public SwordUIProximity proximity;
    [Tooltip("타이핑 · F 대기를 재사용한다. 대사가 떠 있는 동안에는 F를 양보한다")]
    public IntroTextSequence textSequence;

    [Header("연출 부품")]
    public ScreenBlackout blackout;
    public CinematicLetterbox letterbox;
    [Tooltip("SwordPanel의 Animator — 켜고 끄는 용도. 재생은 이 스크립트가 직접 한다(Start에서 비활성화)")]
    public Animator swordPanel;
    [Tooltip("SwordPanel의 Image — 프레임 스프라이트를 여기에 꽂는다")]
    public Image panelImage;
    [Tooltip("패널 애니메이션 스프라이트. **열리는 순서**(0 → 마지막)로 넣는다. 역재생은 이 배열을 거꾸로 훑는다")]
    public Sprite[] panelFrames;
    [Tooltip("초당 프레임 수 (원본 클립 12fps)")]
    public float panelFrameRate = 12f;
    public Text actionText1;   // text-action-1
    public Text actionText2;   // text-action-2
    [Tooltip("마지막 암전과 같은 시간에 걸쳐 같이 잦아든다")]
    public AudioSource bgm;

    [Header("효과음")]
    [Tooltip("IntroTextSequence와 같은 소스를 물린다")]
    public AudioSource sfxSource;
    [Tooltip("SwordPanel 애니메이션 정방향 · 역재생 시작 때 각각 1회")]
    public AudioClip panelSfx;

    [Header("획득 시 삭제할 것들")]
    public GameObject[] destroyOnPickup;

    [Header("문구")]
    [TextArea] public string pickupMessage = "카타나 코어 모듈을 획득했다.";

    [Header("타이밍(초) — 획득 암전")]
    public float blackFadeIn = 0.4f;
    [Tooltip("완전히 검어진 뒤 유지하는 시간. 이 대기가 시작될 때 위 오브젝트들이 삭제된다")]
    public float blackHold = 0.5f;
    public float blackFadeOut = 0.4f;

    [Header("타이밍(초) — 패널")]
    [Tooltip("text-action 둘이 다 뜬 뒤 지워지기까지의 대기")]
    public float panelHold = 0.5f;

    [Header("타이밍(초) — 마무리")]
    [Tooltip("글리치만 보여주는 시간. 0이면 암전과 완전히 동시에 시작한다")]
    public float glitchLead = 0.6f;
    [Tooltip("마지막 암전과 BGM 페이드 아웃에 걸리는 시간")]
    public float endFadeDuration = 1f;
    [Tooltip("완전히 검어진 뒤 씬이 바뀌기까지의 대기")]
    public float endHold = 0.5f;

    [Header("씬 전환")]
    [Tooltip("Build Settings에 등록돼 있어야 한다")]
    public string nextScene = "TutorialScene";

    string line1, line2;      // text-action의 씬 저장 문구 — 타이핑으로 되살릴 원본
    bool done;

    void Start()
    {
        // SwordPanel이 켜지는 순간 완성된 문장이 한 프레임 보이면 안 된다 — 원본을 챙겨 두고 비운다.
        // (부모가 비활성이어도 컴포넌트 참조로 접근·수정은 정상 동작한다)
        if (actionText1 != null) { line1 = actionText1.text; actionText1.text = string.Empty; }
        if (actionText2 != null) { line2 = actionText2.text; actionText2.text = string.Empty; }

        // Animator를 꺼둔다 — 켜져 있으면 매 프레임 Image.sprite를 자기 값으로 덮어써서
        // 이 스크립트가 넘기는 프레임이 무시된다.
        if (swordPanel != null) swordPanel.enabled = false;
    }

    void Update()
    {
        if (done) return;
        if (proximity == null || !proximity.Shown) return;
        if (textSequence != null && textSequence.Busy) return;
        if (!IntroTextSequence.KeyPressedThisFrame(Key.F)) return;

        done = true;
        StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        TutorialMover.InputLocked = true;

        // ── 획득: 암전으로 덮고 그 안에서 치운다 ────────────────────────────────────────────
        yield return blackout.FadeTo(1f, blackFadeIn);

        for (int i = 0; i < destroyOnPickup.Length; i++)
            if (destroyOnPickup[i] != null) Destroy(destroyOnPickup[i]);

        if (blackHold > 0f) yield return new WaitForSecondsRealtime(blackHold);
        yield return blackout.FadeTo(0f, blackFadeOut);

        // ── 획득 대사 ────────────────────────────────────────────────────────────────────
        yield return letterbox.Show();
        yield return textSequence.ShowMessage(pickupMessage);
        yield return letterbox.Hide();

        // ── 시스템 패널 ──────────────────────────────────────────────────────────────────
        swordPanel.gameObject.SetActive(true);
        yield return PlayPanel(true);

        yield return Type(actionText1, line1);
        yield return Type(actionText2, line2);

        if (panelHold > 0f) yield return new WaitForSecondsRealtime(panelHold);
        yield return UntypeBoth();

        yield return PlayPanel(false);
        swordPanel.gameObject.SetActive(false);

        // ── 마무리: 글리치 → 암전 + BGM → 씬 전환 ─────────────────────────────────────────
        ScreenGlitchFx.Begin(ScreenGlitchFx.Source.Cutscene);
        if (glitchLead > 0f) yield return new WaitForSecondsRealtime(glitchLead);

        // BGM은 암전과 나란히 흘러야 하므로 기다리지 않고 따로 돌린다(SceneTransitionTrigger 선례).
        if (bgm != null) StartCoroutine(FadeBgm());
        yield return blackout.FadeTo(1f, endFadeDuration);

        if (endHold > 0f) yield return new WaitForSecondsRealtime(endHold);

        // 검은 화면인 채로 멈추면 원인을 찾기 어렵다 — 미등록이면 무엇을 해야 하는지 남긴다.
        if (!SceneTransitionTrigger.IsInBuildSettings(nextScene))
        {
            Debug.LogError("[SwordPickupSequence] '" + nextScene + "'이 Build Settings에 없어 전환할 수 없습니다. " +
                           "File > Build Profiles > Scene List에 추가하세요.", this);
            yield break;
        }
        SceneManager.LoadScene(nextScene);
    }

    // 스프라이트를 직접 순서대로 꽂는다.
    //
    // ⚠️ Animator.speed = -1 + Play(state, 0, 1f) 방식은 쓰지 않는다 — 스프라이트(오브젝트 레퍼런스)
    //    커브에서는 역재생이 제대로 걸리지 않는다(사용자 리포트 2026-08-12 "아예 스프라이트 재생
    //    순서 반대로"). 배열을 거꾸로 훑으면 순서 반전이 문자 그대로 보장되고, 타이밍도 이 컷신의
    //    다른 대기와 같은 unscaled 시계로 통일된다.
    IEnumerator PlayPanel(bool forward)
    {
        if (panelImage == null || panelFrames == null || panelFrames.Length == 0) yield break;

        // 효과음 클립(1.06초)이 애니메이션(0.5초)보다 길다 — 자르지 않고 끝까지 울리게 둔다.
        if (sfxSource != null && panelSfx != null) sfxSource.PlayOneShot(panelSfx);

        float step = panelFrameRate > 0f ? 1f / panelFrameRate : 1f / 12f;
        int n = panelFrames.Length;
        for (int i = 0; i < n; i++)
        {
            panelImage.sprite = forward ? panelFrames[i] : panelFrames[n - 1 - i];
            yield return new WaitForSecondsRealtime(step);
        }
    }

    IEnumerator Type(Text target, string message)
    {
        if (target == null || string.IsNullOrEmpty(message)) yield break;

        float interval = textSequence != null ? textSequence.charInterval : 0.06f;
        target.text = string.Empty;
        if (textSequence != null) textSequence.BeginTypingSfx();
        for (int i = 1; i <= message.Length; i++)
        {
            target.text = message.Substring(0, i);
            yield return new WaitForSecondsRealtime(interval);
        }
        if (textSequence != null) textSequence.EndTypingSfx();
    }

    // 둘을 같은 시계로 동시에 줄인다 — 길이가 달라도 같은 순간에 비워진다(짧은 쪽은 먼저 바닥에서 멈춤).
    IEnumerator UntypeBoth()
    {
        float interval = textSequence != null ? textSequence.charInterval : 0.06f;
        int len1 = line1 != null ? line1.Length : 0;
        int len2 = line2 != null ? line2.Length : 0;

        for (int i = Mathf.Max(len1, len2) - 1; i >= 0; i--)
        {
            if (actionText1 != null) actionText1.text = line1.Substring(0, Mathf.Min(i, len1));
            if (actionText2 != null) actionText2.text = line2.Substring(0, Mathf.Min(i, len2));
            yield return new WaitForSecondsRealtime(interval);
        }
    }

    IEnumerator FadeBgm()
    {
        float from = bgm.volume;
        float t = 0f;
        while (t < endFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            bgm.volume = Mathf.Lerp(from, 0f, Mathf.Clamp01(t / endFadeDuration));
            yield return null;
        }
        bgm.volume = 0f;
    }

    // 연출 도중 씬이 바뀌어도 이동 잠금·글리치가 남지 않게 한다(암전은 전환 연출이라 그대로 둔다).
    void OnDisable()
    {
        if (!done) return;
        TutorialMover.InputLocked = false;
        ScreenGlitchFx.End(ScreenGlitchFx.Source.Cutscene);
        if (textSequence != null) textSequence.EndTypingSfx();   // text-action 타이핑 중 꺼지면 루프가 남는다
    }
}
