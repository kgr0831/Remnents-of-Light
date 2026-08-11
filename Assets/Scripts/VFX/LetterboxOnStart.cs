using System.Collections;
using UnityEngine;

// 씬 시작 후 레터박스를 띄운다. CinematicLetterbox는 바를 Awake에서 높이 0으로 만들어 두기 때문에,
// 누군가 Show/ShowInstant를 불러주지 않으면 영영 안 보인다.
// IntroStartSequence처럼 긴 연출이 없는 컷신 씬용 — 그쪽은 자기 시퀀스 안에서 직접 부른다.
[RequireComponent(typeof(CinematicLetterbox))]
public class LetterboxOnStart : MonoBehaviour
{
    [Tooltip("이 암전이 완전히 걷힌 뒤부터 delay를 센다. 비워두면 씬 시작 기준")]
    public ScreenBlackoutOnStart waitFor;

    [Tooltip("암전이 걷힌 뒤 레터박스가 나타나기까지의 대기(초)")]
    public float delay = 2f;

    [Tooltip("켜면 바가 자라나며 등장한다(CinematicLetterbox.duration). 끄면 즉시 펼쳐진다")]
    public bool animate = true;

    IEnumerator Start()
    {
        var letterbox = GetComponent<CinematicLetterbox>();

        while (waitFor != null && !waitFor.Done) yield return null;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        if (animate) yield return letterbox.Show();
        else letterbox.ShowInstant();
    }
}
