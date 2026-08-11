using UnityEngine;

// 컷신 중 오브젝트를 제자리에서 미세하게 흔든다 — 기준 위치·회전은 그대로 두고 그 위에 얹기만 한다.
// 플레이어에 붙이면 낙하 중 몸이 흔들리는 느낌, 카메라에 붙이면 공기 저항으로 화면이 떠는 느낌이 된다.
//
// 사인파 대신 Perlin 노이즈를 쓴다 — 사인은 같은 주기로 정직하게 왕복해서 몇 초만 봐도 기계처럼 보인다.
// 회전/보빙/떨림의 seed를 다르게 줘서 셋이 같은 박자로 움직이지 않게 했다.
public class CutsceneSwayFx : MonoBehaviour
{
    [Header("회전(도)")]
    public float rotationAmplitude = 2f;
    public float rotationSpeed = 0.5f;

    [Header("상하 보빙(유닛)")]
    public float bobAmplitude = 0.03f;
    public float bobSpeed = 1.2f;

    [Header("떨림(유닛) — 카메라 셰이크용, 0이면 끔")]
    public float shakeAmplitude = 0f;
    public float shakeSpeed = 7f;

    Vector3 basePos;
    Quaternion baseRot;
    float seed;

    void Awake()
    {
        basePos = transform.localPosition;
        baseRot = transform.localRotation;   // 플레이어의 180도 뒤집힘도 여기 담긴다
        seed = Random.Range(0f, 100f);
    }

    void Update()
    {
        // 컷신 연출은 timeScale에 영향받지 않아야 한다(프로젝트 컨벤션).
        float t = Time.unscaledTime;

        float rot = Signed(seed, t * rotationSpeed) * rotationAmplitude;
        float bob = Signed(seed + 11f, t * bobSpeed) * bobAmplitude;
        float sx = Signed(seed + 23f, t * shakeSpeed) * shakeAmplitude;
        float sy = Signed(seed + 37f, t * shakeSpeed) * shakeAmplitude;

        transform.localPosition = basePos + new Vector3(sx, bob + sy, 0f);
        transform.localRotation = baseRot * Quaternion.Euler(0f, 0f, rot);
    }

    // Perlin은 0~1이라 -1~1로 옮긴다.
    static float Signed(float x, float y)
    {
        return (Mathf.PerlinNoise(x, y) - 0.5f) * 2f;
    }

    // 연출이 꺼지면 기준 상태로 정확히 되돌린다 — 흔들린 자리에 굳지 않게.
    void OnDisable()
    {
        transform.localPosition = basePos;
        transform.localRotation = baseRot;
    }
}
