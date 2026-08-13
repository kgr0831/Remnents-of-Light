using UnityEngine;
using UnityEngine.InputSystem;

// 튜토리얼 플레이어를 A/D로 좌우 이동시키는 최소 구현 — 점프·대시는 없다(요청 범위: 이동만).
// 애니메이터의 Moving 불값을 같이 밀어 Idle ↔ Run 전이를 만든다.
//
// 이동은 transform이 아니라 Rigidbody2D 속도로 넣는다 — Dynamic 바디에 transform을 직접 쓰면
// 물리 해석과 싸워서 타일맵에 박히거나 떨린다. y속도는 건드리지 않아 중력이 그대로 작동한다.
//
// ⚠️ Keyboard.current를 쓰지 않는다: current는 "가장 최근에 입력이 들어온 키보드"라 PlayTest가 남긴
//    가상 키보드가 붙어 있으면 그쪽을 가리켜 실제 키보드 입력이 통째로 무시된다.
//    (PlayerController.KeyHeld와 같은 이유 — 그쪽 주석 참고)
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class TutorialMover : MonoBehaviour
{
    public float moveSpeed = 5f;   // PlayerController.moveSpeed와 같은 기본값

    /// <summary>컷신 등에서 이동 입력을 잠근다(IntroTextSequence가 레터박스가 떠 있는 동안 켠다).</summary>
    public static bool InputLocked;

    static readonly int IdMoving = Animator.StringToHash("Moving");

    // 착지음 판정 — 접지 판정이 없는 스크립트라 수직 속도만 본다. 이 속도보다 빠르게 떨어진 적이
    // 있어야 "떨어지는 중"으로 치므로, 물리 솔버의 미세 진동으로는 절대 무장되지 않는다
    // (IntroScene_2 오프닝 낙하는 10.9유닛 낙하 = 착지 속도 약 14.6 u/s로 여유가 크다).
    const float FallSpeedToArmLand = 2f;
    const float SettledSpeed = 0.5f;   // 이보다 느리면 땅에 닿아 속도가 죽은 것으로 본다

    SpriteRenderer sr;
    Animator anim;
    Rigidbody2D rb;
    InputAction moveAction; // PlayerActions "Move" — 설정에서 이동 키를 바꿔도 따라가야 하므로 액션을 읽는다
    float input;
    float footstepTimer; // PlayerController와 같은 규칙으로 GameSfx.TickFootstep이 관리한다
    bool landSfxArmed;   // 떨어져 본 적이 있어야 착지음이 울린다(시작 첫 프레임 헛울림 방지)

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        // static이라 씬을 다시 로드해도 값이 남는다 — 컷신 도중 씬이 바뀌면 잠금이 영영 안 풀리므로
        // 플레이어가 새로 생길 때 초기화한다.
        InputLocked = false;

        // 이 플레이어가 있는 씬(인트로·튜토리얼)엔 PlayerInput이 없어서 액션을 아무도 켜주지 않는다 —
        // 직접 켠다. 끄지는 않는다: 액션이 켜져 있어도 비용이 없고, 씬이 바뀌면 실제 플레이어의
        // PlayerInput이 같은 맵을 자기 방식대로 다시 켠다.
        var asset = KeyBinds.Actions;
        if (asset != null) moveAction = asset.FindAction("Move");
        if (moveAction != null) moveAction.Enable();
    }

    void Update()
    {
        // 액션을 못 찾았을 때만 기본 A/D를 직접 훑는다(에셋이 빠져도 인트로가 멈추지 않게 하는 폴백).
        float raw = moveAction != null
            ? moveAction.ReadValue<Vector2>().x
            : (KeyHeld(Key.D) ? 1f : 0f) - (KeyHeld(Key.A) ? 1f : 0f);
        input = InputLocked ? 0f : raw;

        if (input != 0f) sr.flipX = input < 0f;
        if (anim != null) anim.SetBool(IdMoving, input != 0f);

        float vy = rb.linearVelocity.y;

        // 발소리. 접지 판정이 없는 최소 구현이라 "수직 속도가 거의 0"을 접지 대용으로 쓴다 —
        // 인트로 낙하 중에 발소리가 나지 않게 막는 용도(이 플레이어는 점프가 없어 이걸로 충분하다).
        bool walking = input != 0f && Mathf.Abs(vy) < SettledSpeed;
        GameSfx.TickFootstep(ref footstepTimer, walking, Time.deltaTime);

        // 착지음 — IntroScene_2 오프닝(하늘에서 떨어져 착지)이 이 경로다.
        // ⚠️ 무장 조건을 먼저 통과해야 하므로, 낙하가 시작되며 속도가 0을 스쳐 지나갈 때는 울리지 않는다.
        //    Intro-cutScene처럼 transform으로 직접 내리는 연출은 속도가 0이라 아예 무장되지 않는다.
        if (vy < -FallSpeedToArmLand) landSfxArmed = true;
        else if (landSfxArmed && Mathf.Abs(vy) < SettledSpeed)
        {
            landSfxArmed = false;
            GameSfx.Play(Sfx.Land);
        }
    }

    void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(input * moveSpeed, rb.linearVelocity.y);
    }

    static bool KeyHeld(Key key)
    {
        var devices = InputSystem.devices;
        for (int i = 0; i < devices.Count; i++)
            if (devices[i] is Keyboard kb && kb[key].isPressed) return true;
        return false;
    }
}
