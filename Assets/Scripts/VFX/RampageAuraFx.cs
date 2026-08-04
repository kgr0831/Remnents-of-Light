using UnityEngine;

// 플레이어를 감싸는 아우라 — 원래 폭주 전용이었다가 제거된 죽은 코드였는데(2026-08-01, 아래
// 원본 주석 참고), 초월의 두 번째 소비처로 되살렸다(사용자 지시 2026-08-02: "초월 상태 유지동안
// 아우라 느낌... cyan 색상 블룸"). `Begin(player, color)`로 색을 받아 폭주·초월 어느 쪽이든
// 재사용 가능하게 일반화했다 — 클래스 이름은 안 바꿨다(이름 변경은 참조 다건을 건드리는 리팩터라
// 별도 승인 필요, `Custom/RampageOutline` 셰이더와 같은 처리).
//
// 1차 시도는 "피어오르는 수증기"(퍼프가 위로 올라가는 방식)였는데 사용자가 원한 건 연기가 아니라
// **아우라**였다(2026-08-01). 그래서 올라가는 입자를 버리고, 캐릭터를 감싼 채 숨쉬듯 맥동하는
// 부드러운 후광 두 겹으로 바꿨다.
//
// 가산 합성이라 어두운 몸 위에 은은하게 얹히고 배경 쪽으로는 후광이 퍼진다 — 알파 블렌딩이면
// 몸을 덮어버려서(렌더 순서상 플레이어보다 뒤로 못 간다) 예전처럼 "몸이 물든" 그림이 된다.
// 프로젝트 VFX 규약대로 프리팹·텍스처 의존 0 — 방사형 알파 텍스처를 코드로 만든다.
public class RampageAuraFx : MonoBehaviour
{
    const string ShaderName = "Custom/PlayerBloomOverlay"; // 가산(Blend One One) + _Intensity 제어
    static readonly int IdColor = Shader.PropertyToID("_Color");
    static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
    static readonly int IdBoost = Shader.PropertyToID("_BloomBoost");
    static readonly int IdFlatten = Shader.PropertyToID("_Flatten");

    const float Boost = 2.0f;
    const float InnerScale = 1.25f;  // 월드 유닛(캐릭터 몸 높이가 대략 1.2) — 몸에 붙게
    // 사용자 피드백(2026-08-02, 초월 재사용 후): "여러 방향에서 좀 더 많이" — 2.0은 화면을 덮는
    // 돔처럼 보였다는 원래 폭주 시절 지적에 맞춰 낮춰뒀던 값인데, 초월에선 오히려 약해 보인다는
    // 반대 피드백이 와서 다시 올렸다(2.0→2.3).
    const float OuterScale = 2.3f;
    const float InnerAlpha = 0.24f;  // 0.14 → 더 진하게
    const float OuterAlpha = 0.14f;  // 0.07 → 더 진하게, 더 멀리까지 읽히도록
    const float PulsePerSecond = 0.75f; // 숨쉬는 주기
    const float PulseDepth = 0.35f;     // 세기 진폭(±35%)
    const float ScalePulse = 0.06f;     // 크기 진폭(±6%)
    const float MaxSmoothDelta = 0.05f;

    static Sprite _haloSprite;

    Transform target;
    SpriteRenderer targetSr;
    SpriteRenderer innerSr, outerSr;
    Material innerMat, outerMat;
    Color color;
    float t, alpha = 1f;
    float fadeOutDuration = -1f, fadeOutTimer, fadeOutFrom; // FadeOut() 진행 중이면 fadeOutDuration>=0

    public static RampageAuraFx Instance { get; private set; }

    public static void Begin(Transform player, Color color)
    {
        if (player == null || Instance != null) return;
        Shader sh = Shader.Find(ShaderName);
        if (sh == null) return;

        var go = new GameObject("RampageAuraFx");
        go.transform.SetParent(player, false);
        int protectedLayer = LayerMask.NameToLayer("VFXNoGrayscale");
        if (protectedLayer >= 0) go.layer = protectedLayer;

        var fx = go.AddComponent<RampageAuraFx>();
        fx.target = player;
        fx.targetSr = player.GetComponent<SpriteRenderer>();
        fx.color = color;
        fx.innerSr = fx.CreateLayer(sh, "AuraInner", InnerScale, -2);
        fx.outerSr = fx.CreateLayer(sh, "AuraOuter", OuterScale, -3);
        fx.innerMat = fx.innerSr.sharedMaterial;
        fx.outerMat = fx.outerSr.sharedMaterial;
        Instance = fx;
    }

    /// <summary>0.25초에 걸쳐 옅어진 뒤 스스로 파괴된다(cyan 블룸·시야 FX와 같은 페이드아웃 길이).</summary>
    public static void End()
    {
        if (Instance != null) Instance.FadeOut(0.25f);
    }

    void FadeOut(float duration)
    {
        if (fadeOutDuration >= 0f) return; // 이미 페이드아웃 중이면 다시 걸지 않는다
        fadeOutDuration = Mathf.Max(0.01f, duration);
        fadeOutFrom = alpha;
        fadeOutTimer = 0f;
    }

    /// <summary>진입·해제 페이드에 맞춰 아우라도 같이 옅어진다.</summary>
    public void SetAlpha(float a) => alpha = Mathf.Clamp01(a);

    /// <summary>[ASSERT]/디버그용 — 아우라 겹 수(항상 2).</summary>
    public int LayerCount => (innerSr != null ? 1 : 0) + (outerSr != null ? 1 : 0);

    SpriteRenderer CreateLayer(Shader sh, string name, float worldScale, int sortingOffset)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;

        var mat = new Material(sh);
        mat.SetColor(IdColor, color);
        mat.SetFloat(IdBoost, Boost);
        mat.SetFloat(IdFlatten, 1f);   // 방사형 텍스처를 통짜로 — 색은 _Color가 정한다
        mat.SetFloat(IdIntensity, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetHaloSprite();
        sr.sharedMaterial = mat;
        if (targetSr != null)
        {
            sr.sortingLayerID = targetSr.sortingLayerID;
            sr.sortingOrder = targetSr.sortingOrder + sortingOffset;
        }
        go.transform.localScale = new Vector3(worldScale, worldScale, 1f);
        return sr;
    }

    void LateUpdate()
    {
        if (target == null || targetSr == null) { Destroy(gameObject); return; }

        if (fadeOutDuration >= 0f)
        {
            fadeOutTimer += Time.unscaledDeltaTime;
            alpha = Mathf.Lerp(fadeOutFrom, 0f, Mathf.Clamp01(fadeOutTimer / fadeOutDuration));
            if (fadeOutTimer >= fadeOutDuration) { Destroy(gameObject); return; }
        }

        t += Mathf.Min(Time.unscaledDeltaTime, MaxSmoothDelta);

        // 실루엣 중심에 맞춘다(프레임 여백이 넓어 transform.position을 그대로 쓰면 아우라가 발밑에 몰린다).
        Bounds b = targetSr.bounds;
        Vector3 center = new Vector3(b.center.x, b.min.y + b.size.y * 0.5f, 0f);
        // 스프라이트 프레임 여백 때문에 bounds 중심은 실제 몸보다 위/옆으로 치우친다 → 몸 쪽으로 보정.
        center = new Vector3(target.position.x, target.position.y + 0.55f, 0f);
        transform.position = center;

        float pulse = Mathf.Sin(t * Mathf.PI * 2f * PulsePerSecond);
        float k = 1f + pulse * PulseDepth;
        float s = 1f + pulse * ScalePulse;

        ApplyLayer(innerSr, innerMat, InnerAlpha * k, InnerScale * s);
        // 바깥 겹은 반 박자 늦게 뛰게 해서 "숨쉬는" 느낌을 준다.
        float pulse2 = Mathf.Sin((t - 0.25f / PulsePerSecond) * Mathf.PI * 2f * PulsePerSecond);
        ApplyLayer(outerSr, outerMat, OuterAlpha * (1f + pulse2 * PulseDepth), OuterScale * (1f + pulse2 * ScalePulse));
    }

    void ApplyLayer(SpriteRenderer sr, Material mat, float intensity, float scale)
    {
        if (sr == null || mat == null) return;
        mat.SetFloat(IdIntensity, Mathf.Max(0f, intensity) * alpha);
        sr.transform.localScale = new Vector3(scale, scale, 1f);
    }

    // 중심이 진하고 가장자리로 갈수록 사라지는 방사형 텍스처 — 후광 한 겹의 기본 모양.
    static Sprite GetHaloSprite()
    {
        if (_haloSprite != null) return _haloSprite;

        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[N * N];
        float c = (N - 1) * 0.5f;
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
            // 가운데는 완만하게, 바깥은 부드럽게 0으로 — 경계가 보이면 "원판"처럼 읽힌다.
            float a = Mathf.Clamp01(1f - d);
            a = a * a * (3f - 2f * a); // smoothstep
            px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply();
        _haloSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N); // 1유닛 = N픽셀
        return _haloSprite;
    }

    void OnDestroy()
    {
        if (innerMat != null) Destroy(innerMat);
        if (outerMat != null) Destroy(outerMat);
        if (Instance == this) Instance = null;
    }
}
