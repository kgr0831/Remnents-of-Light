using UnityEngine;

// 자아 고갈(HP 붕괴 디버프) 드라이버 — RampageVisionFx와 같은 정적 Begin/End + 페이드 패턴.
// 위치 개념이 없는 순수 전역 이펙트라 target/center 갱신이 필요 없다(RampageVisionFx보다 단순).
// PlayerController.DrainEgo()가 자아 0 진입/이탈, EndRampage()가 폭주 종료 시 각각 호출한다.
public class ScreenGlitchFx : MonoBehaviour
{
    const float FadeInDuration = 0.15f;
    const float FadeOutDuration = 0.2f;
    const float SeedStepRate = 20f; // 초당 시드 갱신 횟수 — 매끈한 이동이 아니라 뚝뚝 끊기는 아날로그 잡음 느낌
    const float MaxSmoothDelta = 0.05f; // 원격/비포커스 에디터 프레임 튐 대비(PlayerHudUI·RampageVisionFx 선례)

    public static ScreenGlitchFx Instance { get; private set; }

    float k; // 현재 세기 0~1
    bool ending;
    float seed;

    // [ASSERT] 판독구
    public float Intensity => k;

    public static void Begin()
    {
        if (Instance != null) { Instance.ending = false; return; } // 페이드아웃 중 재진입 시 되살린다
        var go = new GameObject("ScreenGlitchFx");
        Instance = go.AddComponent<ScreenGlitchFx>();
    }

    /// <summary>페이드아웃 후 스스로 파괴된다(오브젝트 누수 0).</summary>
    public static void End()
    {
        if (Instance != null) Instance.ending = true;
    }

    void LateUpdate()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, MaxSmoothDelta);
        float dur = ending ? FadeOutDuration : FadeInDuration;
        k = Mathf.MoveTowards(k, ending ? 0f : 1f, dt / Mathf.Max(0.0001f, dur));

        seed = Mathf.Floor(Time.unscaledTime * SeedStepRate);

        var f = ScreenGlitchFeature.Instance;
        // 피처가 URP 렌더러에 등록돼 있지 않으면 Instance가 null이다 → 조용히 무시(에러 없음).
        if (f != null)
        {
            f.Intensity = k;
            f.Seed = seed;
        }

        if (ending && k <= 0f) Destroy(gameObject);
    }

    void OnDestroy()
    {
        var f = ScreenGlitchFeature.Instance;
        if (f != null) f.Intensity = 0f; // 어떤 경로로 끝나도 화면은 반드시 원복된다
        if (Instance == this) Instance = null;
    }
}
