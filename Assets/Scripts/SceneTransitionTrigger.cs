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

    // 페이드가 끝나기 전에 트리거가 또 들어오면 전환이 두 번 걸린다.
    bool fired;

    void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
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
        if (blackout != null) yield return blackout.FadeTo(1f, fadeDuration);
        SceneManager.LoadScene(sceneName);
    }
}
