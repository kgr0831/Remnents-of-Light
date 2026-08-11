using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 씬 시작 연출.
//   검은 화면 + 레터박스가 걸린 채로 시작 → 검은 화면 페이드 아웃 → (페이드가 끝난 뒤) 플레이어
//   클로즈업(ortho 2)에서 born 재생 → 줌 아웃 → "A,D키로 이동해보자." → F → 레터박스 퇴장 → 이동 가능
//
// 암전은 ScreenFadeUI를 안 쓰고 Canvas 위에 검은 Image를 직접 깐다. 그쪽은 알파 반영이 자기 Update에
// 달려 있어서, 오브젝트가 Start에서 막 생성되는 첫 프레임에는 안 검게 새어 나올 수 있었다.
// 여기서는 생성하면서 알파 1을 바로 넣고, 스트레치 앵커라 캔버스 rect 값과 무관하게 전체를 덮는다.
//
// ⚠️ 전부 Start()에서 한다(Awake 아님). 이유가 둘이다.
//   1) SectionCamera.Awake가 cam.orthographicSize를 baseOrthoSize로 캡처한다. Awake에서 미리 2로
//      줄여 버리면 실행 순서에 따라 2가 기준값으로 굳어 줌 아웃할 곳이 사라진다.
//   2) TutorialMover.Awake가 InputLocked를 false로 초기화한다. Awake에서 잠그면 그게 풀려 버린다.
//   Awake가 전부 끝난 뒤 Start가 돌고, 화면에 그려지는 건 그다음이라 깜빡임은 없다.
public class IntroStartSequence : MonoBehaviour
{
    public CinematicLetterbox letterbox;
    public IntroTextSequence textSequence;
    public ScreenBlackout blackout;
    public SectionCamera sectionCamera;
    public Transform player;
    public Animator playerAnimator;

    [Header("문구")]
    [TextArea] public string message = "A,D키로 이동해보자.";

    [Header("암전")]
    [Tooltip("검은 화면이 걷히는 시간(초)")]
    public float fadeOutDuration = 1.2f;
    [Tooltip("검은 화면이 완전히 걷힌 뒤 born이 시작되기까지의 뜸(초)")]
    public float beatBeforeBorn = 0.2f;

    [Header("카메라")]
    public float zoomInSize = 2f;
    public float zoomOutDuration = 1.0f;
    [Tooltip("클로즈업이 발밑이 아니라 몸통을 보게 올려주는 값(유닛)")]
    public float zoomInYOffset = 0.6f;

    Camera cam;
    Vector3 restPos;      // 씬에 저장된 카메라 위치 = SectionCamera가 잡고 있는 구간(0,0) 중심
    float restOrtho;

    void Start()
    {
        StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        TutorialMover.InputLocked = true;

        // 첫 프레임부터 완성된 그림이어야 하므로 둘 다 이 자리에서 동기 적용한다.
        blackout.Set(1f);
        letterbox.ShowInstant();

        cam = sectionCamera.GetComponent<Camera>();
        restPos = cam.transform.position;
        restOrtho = cam.orthographicSize;

        // SectionCamera가 매 LateUpdate에 위치·크기를 덮어쓰므로, 연출 동안은 꺼두고 직접 몬다.
        // 되돌릴 목표를 씬 저장값 그대로 쓰기 때문에 다시 켤 때 튀지 않는다(basePos가 그 값이다).
        sectionCamera.enabled = false;
        playerAnimator.speed = 0f;   // born이 암전 중에 흘러가 버리지 않게 잡아 둔다
        ApplyCloseUp();

        yield return blackout.FadeTo(0f, fadeOutDuration);

        // 여기서부터가 "페이드 아웃이 끝난 뒤" — born은 반드시 이 뒤에 재생된다.
        if (beatBeforeBorn > 0f) yield return new WaitForSecondsRealtime(beatBeforeBorn);

        playerAnimator.speed = 1f;
        yield return WaitForBorn();

        yield return ZoomOut();
        sectionCamera.enabled = true;

        yield return textSequence.ShowMessage(message);

        yield return letterbox.Hide();

        TutorialMover.InputLocked = false;
    }

    void ApplyCloseUp()
    {
        cam.orthographicSize = zoomInSize;
        cam.transform.position = new Vector3(player.position.x, player.position.y + zoomInYOffset, restPos.z);
    }

    // 기본 상태(Born-reverse)가 끝나 Idle로 넘어갈 때까지 기다린다. 클립 길이를 상수로 박지 않아
    // 프레임 수나 fps를 나중에 바꿔도 따라간다.
    IEnumerator WaitForBorn()
    {
        yield return null;   // Animator가 첫 평가를 하도록 한 프레임 넘긴다
        while (true)
        {
            var st = playerAnimator.GetCurrentAnimatorStateInfo(0);
            if (!st.IsName("Born-reverse") || st.normalizedTime >= 1f) yield break;
            yield return null;
        }
    }

    IEnumerator ZoomOut()
    {
        Vector3 fromPos = cam.transform.position;
        float fromOrtho = cam.orthographicSize;
        float t = 0f;
        while (t < zoomOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / zoomOutDuration));
            cam.transform.position = Vector3.Lerp(fromPos, restPos, k);
            cam.orthographicSize = Mathf.Lerp(fromOrtho, restOrtho, k);
            yield return null;
        }
        cam.transform.position = restPos;
        cam.orthographicSize = restOrtho;
    }

    // 연출이 중간에 끊겨도 검은 화면·이동 잠금·꺼진 카메라가 남지 않게 한다.
    void OnDisable()
    {
        TutorialMover.InputLocked = false;
        if (sectionCamera != null) sectionCamera.enabled = true;
        if (playerAnimator != null) playerAnimator.speed = 1f;
        if (blackout != null) blackout.Set(0f);
    }
}
