using System.Collections;
using UnityEngine;

// IntroScene_2 시작 연출.
//   레터박스가 이미 걸린 채 + 검은 화면으로 시작 → 검은 화면 페이드 아웃 → 플레이어에게 중력이
//   돌아와 화면 위에서 떨어짐(이 동안 카메라 고정) → Trigger-Action에 닿으면 "오른쪽으로 나아가자"
//   → F로 닫으면 레터박스 퇴장 · 카메라 추적 재개 · 이동 가능
//
// IntroScene의 IntroStartSequence(클로즈업 → born → 줌 아웃)와는 흐름이 아예 달라 별도로 뒀다.
// 대사 표시는 IntroTextSequence.ShowMessage를 그대로 재사용한다(타이핑 · text-tr-2 맥동 · F 대기 ·
// 페이드아웃). 반면 IntroTextSequence.Play()는 쓰지 않는다 — 그쪽은 freezeCamera를 영구히 꺼 버리는
// IntroScene 전용 경로라, 여기서 쓰면 대사 이후 카메라가 오른쪽으로 따라가지 못한다.
//
// ⚠️ 전부 Start()에서 한다(Awake 아님). IntroStartSequence와 같은 이유다.
//   1) TutorialMover.Awake가 InputLocked를 false로 초기화한다 — Awake에서 잠그면 그게 풀려 버린다.
//   2) SectionCamera.Awake가 씬에 저장된 위치·크기를 기준값(basePos/baseOrthoSize)으로 캡처한다.
//   Awake가 전부 끝난 뒤 Start가 돌고, 화면에 그려지는 건 그다음이라 깜빡임은 없다.
public class IntroFallStartSequence : MonoBehaviour
{
    public CinematicLetterbox letterbox;
    public IntroTextSequence textSequence;
    public ScreenBlackout blackout;
    [Tooltip("낙하 동안 꺼서 카메라를 씬에 저장된 자리에 묶어 둔다")]
    public SectionCamera sectionCamera;
    public Rigidbody2D player;
    [Tooltip("여기에 닿으면 대사가 뜬다 (Trigger-Action)")]
    public IntroActionTrigger actionTrigger;
    [Tooltip("낙하 중 born이 재생되지 않게 Idle로 시작시킨다(비워두면 안 건드림)")]
    public Animator playerAnimator;

    [Header("문구")]
    [TextArea] public string message = "오른쪽으로 나아가자";

    [Header("타이밍(초)")]
    [Tooltip("검은 화면이 걷히는 시간")]
    public float fadeOutDuration = 1.2f;
    [Tooltip("검은 화면이 완전히 걷힌 뒤 낙하가 시작되기까지의 뜸")]
    public float beatBeforeFall = 0.3f;

    static readonly int IdIdle = Animator.StringToHash("Idle");

    float savedGravityScale;
    bool gravityHeld;

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

        // Animator 기본 상태가 IntroScene용 Born-reverse(빛에서 태어나는 연출)라, 그대로 두면 하늘에서
        // 떨어지는 동안 그게 재생된다. 컨트롤러는 IntroScene과 공유라 기본 상태를 바꾸면 그쪽 오프닝이
        // 깨지므로, 이 씬에서만 Idle로 밀어 넣는다(사용자 확정).
        if (playerAnimator != null) playerAnimator.Play(IdIdle, 0, 0f);

        HoldGravity();

        // 낙하 동안 구간 추적을 끈다(사용자 지시: "이때는 카메라가 움직이지 말아야해").
        // 씬에 저장된 카메라 자리가 착지 지점의 구간이라, 플레이어는 화면 위 바깥에서 안으로
        // 떨어져 들어온다. 끄지 않으면 카메라가 플레이어를 따라 같이 내려와 낙하감이 사라진다.
        sectionCamera.enabled = false;

        yield return blackout.FadeTo(0f, fadeOutDuration);

        if (beatBeforeFall > 0f) yield return new WaitForSecondsRealtime(beatBeforeFall);

        ReleaseGravity();

        while (!actionTrigger.Fired) yield return null;

        yield return textSequence.ShowMessage(message);

        yield return letterbox.Hide();
        sectionCamera.enabled = true;
        TutorialMover.InputLocked = false;
    }

    // 중력만 0으로 둔다 — bodyType을 Kinematic으로 바꾸면 TutorialMover가 매 FixedUpdate에 넣는
    // linearVelocity와 물리 해석이 어긋나고, 착지 콜라이더 판정도 다시 켤 때 한 프레임 샌다.
    void HoldGravity()
    {
        if (gravityHeld) return;
        savedGravityScale = player.gravityScale;
        player.gravityScale = 0f;
        player.linearVelocity = Vector2.zero;
        gravityHeld = true;
    }

    void ReleaseGravity()
    {
        if (!gravityHeld) return;
        player.gravityScale = savedGravityScale;
        gravityHeld = false;
    }

    // 연출이 중간에 끊겨도 검은 화면 · 이동 잠금 · 꺼진 카메라 · 멈춘 중력이 남지 않게 한다.
    void OnDisable()
    {
        TutorialMover.InputLocked = false;
        if (sectionCamera != null) sectionCamera.enabled = true;
        if (blackout != null) blackout.Set(0f);
        if (player != null) ReleaseGravity();
    }
}
