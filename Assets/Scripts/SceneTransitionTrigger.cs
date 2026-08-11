using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 플레이어가 이 트리거에 닿으면 화면을 검게 덮은 뒤 지정한 씬으로 넘어간다.
// RoomTrigger와 달리 카메라·체크포인트에는 일절 관여하지 않는다 — 씬 전환 하나만 한다.
//
// ⚠️ 대상 씬은 Build Settings에 등록돼 있어야 한다. 안 그러면 런타임에
//    "Scene couldn't be loaded because it has not been added to the build settings"로 실패한다.
[RequireComponent(typeof(BoxCollider2D))]
public class SceneTransitionTrigger : MonoBehaviour
{
    [Tooltip("이동할 씬 이름 (Build Settings에 등록돼 있어야 함)")]
    public string sceneName = "Intro-cutScene";

    [Tooltip("비워두면 페이드 없이 즉시 전환한다")]
    public ScreenBlackout blackout;
    public float fadeDuration = 1f;

    [Tooltip("화면 암전과 같은 시간에 걸쳐 같이 잦아든다 (비워두면 무시)")]
    public AudioSource bgm;

    // 페이드가 끝나기 전에 트리거가 또 들어오면 전환이 두 번 걸린다.
    bool fired;

    void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    /// <summary>씬 이름이 Build Settings에 등록돼 있는가. 전환 직전 가드용.
    ///
    /// ⚠️ Application.CanStreamedLevelBeLoaded를 쓰면 안 된다 — 에디터에서는 **등록된 씬에도 항상
    ///    false**를 반환한다(2026-08-12 실측: TitleScene 포함 전부 false). 그걸 가드로 쓰면 에디터
    ///    플레이에서 모든 씬 전환이 통째로 막힌다. 빌드 인덱스를 직접 훑는 이 방식은 양쪽 다 맞다.</summary>
    public static bool IsInBuildSettings(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            if (System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)) == sceneName)
                return true;
        return false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (fired || !other.CompareTag("Player")) return;

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[SceneTransitionTrigger] sceneName이 비어 있어 전환을 건너뜁니다: " + name, this);
            return;
        }

        fired = true;
        StartCoroutine(FadeAndLoad());
    }

    IEnumerator FadeAndLoad()
    {
        // BGM은 화면 암전과 나란히 흘러야 하므로 기다리지 않고 따로 돌린다.
        if (bgm != null) StartCoroutine(FadeBgm());

        if (blackout != null) yield return blackout.FadeTo(1f, fadeDuration);
        SceneManager.LoadScene(sceneName);
    }

    IEnumerator FadeBgm()
    {
        float from = bgm.volume;
        float t = 0f;
        // 암전과 같은 unscaled 시계를 쓴다 — timeScale이 흔들려도 둘이 안 어긋난다.
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            bgm.volume = Mathf.Lerp(from, 0f, Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        bgm.volume = 0f;
    }
}
