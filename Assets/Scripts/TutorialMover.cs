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

    SpriteRenderer sr;
    Animator anim;
    Rigidbody2D rb;
    float input;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        // static이라 씬을 다시 로드해도 값이 남는다 — 컷신 도중 씬이 바뀌면 잠금이 영영 안 풀리므로
        // 플레이어가 새로 생길 때 초기화한다.
        InputLocked = false;
    }

    void Update()
    {
        input = InputLocked
            ? 0f
            : (KeyHeld(Key.D) ? 1f : 0f) - (KeyHeld(Key.A) ? 1f : 0f);

        if (input != 0f) sr.flipX = input < 0f;
        if (anim != null) anim.SetBool(IdMoving, input != 0f);
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
