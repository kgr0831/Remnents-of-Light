using System.Collections;
using UnityEngine;

// IntroScene_3 시작 연출 — 검을 줍고 튜토리얼을 마친 뒤 같은 자리로 돌아온 장면.
//   레터박스가 이미 걸린 채 + 검은 화면으로 시작 → 검은 화면 페이드 아웃 → "카타나 모듈을 착용했다."
//   → F → "오른쪽으로 나아가자." → F로 닫으면(텍스트 페이드 아웃) 레터박스 퇴장 → 이동 가능
//
// IntroScene_2(IntroFallStartSequence)와 달리 낙하도, Trigger-Action 대기도 없다 — 암전이 걷히면
// 곧바로 대사가 뜬다. 이 씬의 Trigger-Action은 대사가 아니라 씬 전환(SceneTransitionTrigger)에 쓴다.
//
// ⚠️ 이동 잠금이 TutorialMover.InputLocked이 아니라 TutorialGate인 이유: 이 씬의 플레이어는
//    Player-Tutorial이 아니라 PlayerController가 붙은 본편 플레이어다. 그쪽 입력은
//    TutorialGate.Move로만 막힌다(PlayerController.Update 첫머리에서 moveInput을 지운다).
//    None으로 두면 이동뿐 아니라 점프·공격·대시까지 입력 자체가 막힌다 — 연출 중엔 그게 맞다.
//
// ⚠️ 전부 Start()에서 한다(Awake 아님). IntroStartSequence · IntroFallStartSequence와 같은 이유로,
//    SectionCamera.Awake가 씬에 저장된 위치·크기를 기준값으로 캡처한 뒤여야 하고, TutorialGate도
//    RuntimeInitializeOnLoadMethod가 All로 초기화한 뒤에 잠가야 한다.
public class IntroReturnStartSequence : MonoBehaviour
{
    public CinematicLetterbox letterbox;
    public IntroTextSequence textSequence;
    public ScreenBlackout blackout;

    // 대사 두 개를 F로 차례로 넘긴다(사용자 지시 2026-08-12). 첫 대사 동안만 text-tr-2 문구를
    // advancePrompt로 갈아 끼우고, 마지막 대사에는 인자를 안 넘겨 씬에 적힌 원본("F를 눌러 닫기")으로
    // 자동 복귀시킨다 — IntroTextSequence.ShowMessage가 매 대사마다 문구를 다시 대입하기 때문이다.
    [Header("문구 — F로 차례로 넘긴다")]
    [TextArea] public string message1 = "카타나 모듈을 착용했다.";
    [TextArea] public string message2 = "오른쪽으로 나아가자.";
    [Tooltip("첫 대사 동안만 쓰는 text-tr-2 문구. 비우면 씬에 적힌 원본을 그대로 쓴다")]
    public string advancePrompt = "F키로 넘기기";

    [Header("타이밍(초)")]
    [Tooltip("검은 화면이 걷히는 시간")]
    public float fadeOutDuration = 1.2f;
    [Tooltip("검은 화면이 완전히 걷힌 뒤 대사가 뜨기까지의 뜸")]
    public float beatBeforeText = 0.3f;

    void Start()
    {
        StartCoroutine(Sequence());
    }

    IEnumerator Sequence()
    {
        TutorialGate.Allowed = TutorialAbility.None;

        // 첫 프레임부터 완성된 그림이어야 하므로 둘 다 이 자리에서 동기 적용한다.
        blackout.Set(1f);
        letterbox.ShowInstant();

        // 레터박스가 걸려 있는 동안은 HUD를 감춘다(사용자 지시 2026-08-12) — 위쪽 검은 바가 HP
        // 아이콘을 반쯤 잘라 먹는 데다, 컷신 화면에 게이지가 떠 있을 이유도 없다.
        // ⚠️ Instance가 아니라 GetOrCreate를 쓴다 — AutoCreate(AfterSceneLoad)가 Start보다 먼저
        //    돌긴 하지만, 그 순서에 기대면 씬 전환 경로에서 한 프레임 HUD가 새어 나올 수 있다.
        var hud = PlayerHudUI.GetOrCreate();
        if (hud != null) hud.SetVisible(false);

        yield return blackout.FadeTo(0f, fadeOutDuration);

        if (beatBeforeText > 0f) yield return new WaitForSecondsRealtime(beatBeforeText);

        yield return textSequence.ShowMessage(message1, advancePrompt);
        yield return textSequence.ShowMessage(message2);

        yield return letterbox.Hide();

        if (hud != null) hud.SetVisible(true);

        TutorialGate.ResetAll();
    }

    // 연출이 중간에 끊겨도 검은 화면·입력 잠금·숨겨진 HUD가 남지 않게 한다.
    void OnDisable()
    {
        TutorialGate.ResetAll();
        if (blackout != null) blackout.Set(0f);
        if (PlayerHudUI.Instance != null) PlayerHudUI.Instance.SetVisible(true);
    }
}
