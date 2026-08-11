using System.Collections;
using UnityEngine;

// 검은 화면으로 시작해 걷히게 한다 — IntroScene(IntroStartSequence)의 씬 시작 암전과 같은 연출을,
// 그쪽처럼 긴 시퀀스가 없는 컷신 씬에서 쓰려고 떼어낸 최소 버전.
//
// SceneTransitionTrigger가 검게 덮은 뒤 씬을 넘기므로, 새 씬도 검은 채로 시작해야 이음매가 안 보인다.
//
// Awake가 아니라 Start에서 한다: ScreenBlackout이 자기 Awake에서 패널을 만들기 때문에 그전에
// Set을 부르면 패널이 없다. 모든 Awake가 끝난 뒤 Start가 돌고 화면에 그려지는 건 그다음이라
// 첫 프레임이 새하얗게 새는 일은 없다(IntroStartSequence와 같은 근거).
[RequireComponent(typeof(ScreenBlackout))]
public class ScreenBlackoutOnStart : MonoBehaviour
{
    [Tooltip("검은 화면이 걷히는 시간(초). IntroScene과 같은 값이 기본")]
    public float fadeOutDuration = 1.2f;

    [Tooltip("걷히기 전에 검은 채로 머무는 시간(초)")]
    public float holdBefore;

    [Header("BGM")]
    [Tooltip("암전이 걷히는 동안 0에서 이 소스의 원래 볼륨까지 같이 차오른다(비워두면 무시)")]
    public AudioSource bgm;

    /// <summary>암전이 완전히 걷혔는지. 뒤따르는 연출(레터박스·나레이션)이 이 값을 기다린다.</summary>
    public bool Done { get; private set; }

    IEnumerator Start()
    {
        var blackout = GetComponent<ScreenBlackout>();
        blackout.Set(1f);

        // 목표 볼륨은 씬에 저장된 값을 그대로 쓴다 — 별도 필드를 두면 인스펙터에서 두 값이 어긋난다.
        float bgmTarget = bgm != null ? bgm.volume : 0f;
        if (bgm != null) bgm.volume = 0f;

        if (holdBefore > 0f) yield return new WaitForSecondsRealtime(holdBefore);

        // 화면과 소리를 한 루프에서 같이 올린다(ScreenBlackout.FadeTo를 대신 도는 이유).
        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeOutDuration);
            blackout.Set(1f - k);
            if (bgm != null) bgm.volume = bgmTarget * k;
            yield return null;
        }
        blackout.Set(0f);
        if (bgm != null) bgm.volume = bgmTarget;

        Done = true;
    }
}
