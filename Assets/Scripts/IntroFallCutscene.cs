using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 오프닝 낙하 컷신의 후반부 진행자. 앞부분(암전 걷힘 → 레터박스 등장 → 나레이션 9덩어리)은
// ScreenBlackoutOnStart · LetterboxOnStart · CutsceneNarration이 각자 처리하고,
// 이 스크립트는 나레이션이 끝난 뒤부터를 순서대로 몬다.
//
//   나레이션 끝 → TeamUI(이미지+텍스트 동시) → 로고 → 레터박스 퇴장
//   → 플레이어가 실제로 아래로 가속 낙하 → 화면 밖으로 나가면 암전 + BGM 페이드 아웃
//
// 페이드는 전부 CanvasGroup 알파 하나로 처리한다 — TeamUI는 자식이 둘(이미지·텍스트)이라
// 각각 색 알파를 만지면 둘이 미묘하게 어긋난다. 그룹 하나면 무조건 같이 움직인다.
public class IntroFallCutscene : MonoBehaviour
{
    [Header("연출 부품")]
    [Tooltip("이게 끝나야 TeamUI가 시작된다")]
    public CutsceneNarration narration;
    public CanvasGroup teamUi;
    public CanvasGroup logo;
    [Tooltip("본체가 다 뜬 뒤에 뒤늦게 차오르는 발광 이미지(Image-AuraGlow · Logo-AuraGlow)")]
    public Graphic teamGlow;
    public Graphic logoGlow;
    public CinematicLetterbox letterbox;
    public ScreenBlackout blackout;

    [Header("낙하")]
    public Transform player;
    [Tooltip("낙하 시작 시 끈다 — 켜져 있으면 제자리로 계속 되돌려서 안 떨어진다")]
    public CutsceneSwayFx playerSway;
    public float fallAcceleration = 20f;
    public float fallMaxSpeed = 34f;
    [Tooltip("화면 아래 경계에서 이만큼 더 내려가야 '나갔다'고 본다(유닛)")]
    public float offscreenMargin = 2f;

    [Header("BGM")]
    [Tooltip("마지막 암전과 같은 시간에 걸쳐 같이 잦아든다(비워두면 무시)")]
    public AudioSource bgm;

    [Header("타이밍(초) — TeamUI·로고 공용")]
    public float fadeIn = 1f;
    [Tooltip("본체가 다 뜬 뒤 발광이 차오르는 시간")]
    public float glowFadeIn = 0.9f;
    public float hold = 2.6f;
    public float fadeOut = 0.8f;
    [Tooltip("항목과 항목 사이의 빈 화면 시간")]
    public float gap = 0.7f;

    [Header("타이밍(초) — 후반")]
    [Tooltip("로고가 사라지고 레터박스가 걷힌 뒤 낙하가 시작되기까지")]
    public float beforeFall = 0.6f;
    [Tooltip("플레이어가 화면 밖으로 나간 뒤 화면이 검어지는 시간")]
    public float endFadeDuration = 1.6f;

    [Header("씬 전환")]
    [Tooltip("암전이 끝난 뒤 넘어갈 씬. 비워두면 전환하지 않고 검은 화면으로 남는다(예전 동작)")]
    public string nextScene = "IntroScene_2";
    [Tooltip("완전히 검어진 뒤 씬이 바뀌기까지의 뜸")]
    public float endHold = 0.5f;

    IEnumerator Start()
    {
        if (teamUi != null) teamUi.alpha = 0f;
        if (logo != null) logo.alpha = 0f;
        SetAlpha(teamGlow, 0f);
        SetAlpha(logoGlow, 0f);

        while (narration != null && !narration.Done) yield return null;
        if (gap > 0f) yield return new WaitForSecondsRealtime(gap);

        yield return ShowGroup(teamUi, teamGlow);
        if (gap > 0f) yield return new WaitForSecondsRealtime(gap);

        yield return ShowGroup(logo, logoGlow);

        if (letterbox != null) yield return letterbox.Hide();
        if (beforeFall > 0f) yield return new WaitForSecondsRealtime(beforeFall);

        yield return Fall();

        yield return EndBlackout();

        if (string.IsNullOrEmpty(nextScene)) yield break;
        if (endHold > 0f) yield return new WaitForSecondsRealtime(endHold);

        // 검은 화면인 채로 멈추면 원인을 찾기 어렵다 — 미등록이면 무엇을 해야 하는지 남긴다.
        if (!SceneTransitionTrigger.IsInBuildSettings(nextScene))
        {
            Debug.LogError("[IntroFallCutscene] '" + nextScene + "'이 Build Settings에 없어 전환할 수 없습니다. " +
                           "File > Build Profiles > Scene List에 추가하세요.", this);
            yield break;
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
    }

    // 페이드 인 → (본체가 다 뜬 뒤) 발광 차오름 → 유지 → 페이드 아웃.
    //
    // 발광을 그룹 알파와 같이 올리면 처음부터 뿌옇게 시작해 로고 획이 뭉개진다 — 본체가 완전히
    // 자리잡은 뒤에 빛만 따로 차오르게 한다. 나갈 때는 그룹 알파가 둘을 같이 데려가므로 따로 안 내린다.
    IEnumerator ShowGroup(CanvasGroup group, Graphic glow)
    {
        if (group == null) yield break;
        SetAlpha(glow, 0f);
        yield return FadeGroup(group, 0f, 1f, fadeIn);
        yield return FadeGraphic(glow, 0f, 1f, glowFadeIn);
        if (hold > 0f) yield return new WaitForSecondsRealtime(hold);
        yield return FadeGroup(group, 1f, 0f, fadeOut);
        group.gameObject.SetActive(false);
    }

    // 여기서만 플레이어가 실제로 움직인다 — 그 전까지는 픽셀만 흘러 낙하처럼 보이게 했다.
    IEnumerator Fall()
    {
        if (player == null) yield break;
        if (playerSway != null) playerSway.enabled = false;   // 제자리로 되돌리는 걸 멈춘다

        Camera cam = Camera.main;
        float speed = 0f;
        while (true)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            speed = Mathf.Min(speed + fallAcceleration * dt, fallMaxSpeed);
            player.position += Vector3.down * speed * dt;

            if (cam == null) yield break;
            float bottom = cam.transform.position.y - cam.orthographicSize;
            if (player.position.y < bottom - offscreenMargin) yield break;

            yield return null;
        }
    }

    // 암전과 BGM을 한 루프에서 같이 내린다 — 따로 돌리면 미묘하게 어긋난다.
    IEnumerator EndBlackout()
    {
        float fromVolume = bgm != null ? bgm.volume : 0f;
        float t = 0f;
        while (t < endFadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / endFadeDuration);
            if (blackout != null) blackout.Set(k);
            if (bgm != null) bgm.volume = Mathf.Lerp(fromVolume, 0f, k);
            yield return null;
        }
        if (blackout != null) blackout.Set(1f);
        if (bgm != null) bgm.volume = 0f;
    }

    static void SetAlpha(Graphic g, float a)
    {
        if (g == null) return;
        var c = g.color;
        c.a = a;
        g.color = c;
    }

    // 발광은 CanvasGroup이 아니라 이미지 색 알파로 따로 움직인다 — 그룹 알파는 본체와 공유라
    // 여기서 만지면 본체까지 같이 흐려진다.
    static IEnumerator FadeGraphic(Graphic g, float from, float to, float duration)
    {
        if (g == null) yield break;
        if (duration <= 0f) { SetAlpha(g, to); yield break; }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(g, Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        SetAlpha(g, to);
    }

    static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
    {
        group.gameObject.SetActive(true);
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
