using UnityEngine;
using TMPro;

// UniTrio-Game-2026(Assets/Scripts/Player/Weapon/DamageText.cs)에서 이식.
// 원본의 SimpleObjectPool 반환·ElementalWeaponSystem 색상 연동은 이 프로젝트에 해당 시스템이 없어 제거.
// (자가 파괴는 DashAfterImage.cs/HitVfxAutoReturn.cs와 동일한 이 프로젝트 컨벤션)
[RequireComponent(typeof(TextMeshPro))]
public class DamageText : MonoBehaviour
{
    [Header("Settings")]
    public float lifetime = 0.6f;
    public float floatSpeed = 2f;
    public Vector3 randomScatter = new Vector3(0.5f, 0.5f, 0f);

    [Header("Outline")]
    public Color outlineColor = Color.black;
    [Range(0f, 1f)] public float outlineWidth = 0.12f;

    [Header("Emphasis (크리티컬 · 처형)")]
    public float emphasizedScale = 2f;      // 강조 시 크기 배율
    public string emphasizedSuffix = "!!!"; // 강조 시 숫자 뒤에 붙는 문자열

    TextMeshPro textMesh;
    Color currentColor;
    float timer;

    // emphasized=true면 크리티컬/처형 표기 — 크기 2배 + "숫자!!!" 형식.
    public void Setup(int damage, Color color, bool emphasized = false)
    {
        SetupText(emphasized ? damage.ToString() + emphasizedSuffix : damage.ToString(), color, emphasized);
    }

    // 숫자 대신 문구를 띄울 때(패링 "막아냄!" 등). 이미 완성된 문구라 emphasizedSuffix는 붙이지 않고,
    // 강조 표기(2배 크기)만 숫자 텍스트와 동일하게 적용한다.
    public void SetupText(string text, Color color, bool emphasized = false)
    {
        if (textMesh == null) textMesh = GetComponent<TextMeshPro>();

        textMesh.sortingOrder = 9999;
        textMesh.outlineColor = outlineColor;
        textMesh.outlineWidth = outlineWidth;
        // 프리팹 RectTransform이 숫자 1~2자리 크기(0.78×0.41)라 "막아냄!" 같은 문구는 줄바꿈된다.
        // overflowMode가 Overflow + 정렬이 Center라 줄바꿈만 끄면 가운데 기준으로 한 줄로 뻗는다.
        textMesh.textWrappingMode = TextWrappingModes.NoWrap;
        textMesh.text = text;
        if (emphasized) transform.localScale *= emphasizedScale;
        color.a = 1f;
        currentColor = color;
        textMesh.color = color;

        transform.position += new Vector3(
            Random.Range(-randomScatter.x, randomScatter.x),
            Random.Range(-randomScatter.y, randomScatter.y),
            0f);

        timer = 0f;
    }

    void Update()
    {
        // ⚠️ 스케일 시간(Time.deltaTime)으로 세면 Time.timeScale = 0인 구간에서 타이머가 아예 안 늘어
        //    화면에 얼어붙은 채 영영 안 사라진다 — 튜토리얼 스텝 종료 연출(TutorialDirector.Freeze)에서
        //    실제로 남았다(사용자 리포트 2026-08-12 "일부 이펙트 스프라이트가 남아있음").
        //    HitVfxAutoReturn을 같은 이유로 이미 실시간으로 바꿨고, 여기가 남아 있던 두 번째 자리다.
        //    씬 로드 직후 unscaledDeltaTime이 1초 넘게 튀는 사례가 있어 프로젝트 관례대로 클램프한다.
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        transform.position += Vector3.up * (floatSpeed * dt);

        timer += dt;
        currentColor.a = 1f - Mathf.Clamp01(timer / lifetime);
        if (textMesh != null) textMesh.color = currentColor;

        if (timer >= lifetime) Destroy(gameObject);
    }
}
