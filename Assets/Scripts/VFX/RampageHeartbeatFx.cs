using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 폭주 진입 순간에만 재생되는 1회성 "두근" 연출 — 화면 붉은 펄스(lub-dub 두 겹) × 심장박동 2번 +
// 카메라 펀치(SectionCamera.FocusPulse 재사용). 폭주가 유지되는 동안엔 관여하지 않고 재생이 끝나면
// 스스로 파괴된다. 몸을 감싸는 상시 아우라(RampageAuraFx)는 이미 두 번 시도 후 사용자 지시로 완전히
// 철회됐다(task.md:2271) — 이번엔 "진입할 때"만 느껴지면 된다(사용자 지시 2026-08-02).
public class RampageHeartbeatFx : MonoBehaviour
{
    // §4-3의 RampageEdge — 화면 비네트용으로 이미 예약된 색(LIGHT_ENERGY_RAMPAGE_PLAN.md:263).
    // PlayerDamageFlashUI가 피격 점멸에 먼저 썼다 — 같은 팔레트를 재사용해 톤을 통일한다.
    static readonly Color FlashColor = new Color(0.55f, 0.02f, 0.04f);
    static readonly Color WhiteFlashColor = Color.white;
    // RampageCore — §4-3 팔레트(LIGHT_ENERGY_RAMPAGE_PLAN.md:261), 마스크 블룸(RampageBloomTint)과 같은 계열.
    static readonly Color RingColor = new Color(1f, 0.14f, 0.10f, 1f);

    const float LubPeakAlpha = 0.30f;
    const float DubPeakAlpha = 0.16f;
    const float RiseTime = 0.05f;
    const float FallTime = 0.10f;
    const float BeatGap = 0.07f;            // lub 끝 → dub 시작
    const float HeartbeatGap = 0.20f;       // 첫 박동 → 둘째 박동
    const float SecondBeatStrength = 0.6f;  // 둘째 박동은 약하게 — 진정되며 폭주 상태로 정착

    // 진입 임팩트(사용자 지시 2026-08-02: 화이트 플래시 + 충격파 링 + 화면 글리치를 한 번에)
    const float WhiteFlashPeak = 0.55f;
    const float WhiteFlashRise = 0.02f;
    const float WhiteFlashFall = 0.06f;

    const float RingLifetime = 0.35f;
    const float RingStartScale = 0.4f;
    const float RingEndScale = 3.2f;
    const string RingShaderName = "Custom/PlayerBloomOverlay";
    static readonly int IdColor = Shader.PropertyToID("_Color");
    static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
    static readonly int IdBoost = Shader.PropertyToID("_BloomBoost");
    static readonly int IdFlatten = Shader.PropertyToID("_Flatten");
    static Sprite _ringSprite;

    const float GlitchHold = 0.12f; // ScreenGlitchFx 자체 페이드(인 0.15s/아웃 0.2s) 위에 얹는 유지 시간

    const float CamZoom = 0.5f; // orthographicSize 감소량(FocusPulse), 박동마다 파고드는 정도

    // 진입 슬로우모션(사용자 지시 2026-08-02: "약하게, 짧게") — 첫 박동 구간만 살짝 늘어지게.
    // 하트비트 자체는 unscaledDeltaTime이라 이 슬로우모 중에도 정상 속도로 뛴다(세계는 느려지고
    // 연출만 제 속도로 터지는 대비 효과).
    const float SlowMoScale = 0.5f;
    const float SlowMoHold = 0.30f;
    const float SlowMoRampOut = 0.15f;

    Image img;

    public static void Begin(Transform player, SectionCamera cam, float camRampIn, float camHold, float camRampOut)
    {
        if (player == null) return;
        var go = new GameObject("RampageHeartbeatFx");
        var fx = go.AddComponent<RampageHeartbeatFx>();
        fx.Build();
        fx.StartCoroutine(fx.Sequence(player, cam, camRampIn, camHold, camRampOut));
        fx.StartCoroutine(fx.SlowMo());
    }

    IEnumerator SlowMo()
    {
        float prev = Time.timeScale;
        Time.timeScale = SlowMoScale;

        float t = 0f;
        while (t < SlowMoHold) { t += Time.unscaledDeltaTime; yield return null; }

        t = 0f;
        while (t < SlowMoRampOut)
        {
            t += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(SlowMoScale, prev, t / SlowMoRampOut);
            yield return null;
        }
        Time.timeScale = prev;
    }

    void Build()
    {
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) canvas = CreateOverlayCanvas();

        var go = new GameObject("HeartbeatFlash", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetAsFirstSibling(); // HUD보다 뒤에 그려지게(PlayerDamageFlashUI와 같은 규칙)

        img = go.GetComponent<Image>();
        img.color = new Color(FlashColor.r, FlashColor.g, FlashColor.b, 0f);
        img.raycastTarget = false;
    }

    IEnumerator Sequence(Transform player, SectionCamera cam, float camRampIn, float camHold, float camRampOut)
    {
        // 진입 임팩트 — 충격파 링·화면 글리치는 즉시 발사(자기 수명대로 알아서 사라짐),
        // 흰 플래시는 같은 Image를 쓰는 lub/dub 펄스와 겹치지 않게 먼저 끝내고 넘어간다.
        SpawnShockwaveRing(player);
        ScreenGlitchFx.Begin();
        StartCoroutine(EndGlitchAfter(GlitchHold));
        yield return Pulse(WhiteFlashColor, WhiteFlashPeak, WhiteFlashRise, WhiteFlashFall);

        yield return Beat(player, cam, 1f, camRampIn, camHold, camRampOut);
        yield return WaitUnscaled(HeartbeatGap);
        yield return Beat(player, cam, SecondBeatStrength, camRampIn, camHold, camRampOut);
        Destroy(gameObject);
    }

    IEnumerator EndGlitchAfter(float hold)
    {
        yield return WaitUnscaled(hold);
        ScreenGlitchFx.End();
    }

    void SpawnShockwaveRing(Transform player)
    {
        Shader sh = Shader.Find(RingShaderName);
        if (sh == null || player == null) return;

        var go = new GameObject("RampageShockwaveRing");
        int protectedLayer = LayerMask.NameToLayer("VFXNoGrayscale");
        if (protectedLayer >= 0) go.layer = protectedLayer;
        // RampageAuraFx와 같은 몸통 중심 보정(발밑 피봇 대응).
        go.transform.position = player.position + Vector3.up * 0.55f;
        go.transform.localScale = new Vector3(RingStartScale, RingStartScale, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetRingSprite();
        var mat = new Material(sh);
        mat.SetColor(IdColor, RingColor);
        mat.SetFloat(IdBoost, 2.5f);
        mat.SetFloat(IdFlatten, 1f);
        mat.SetFloat(IdIntensity, 1f);
        sr.sharedMaterial = mat;

        StartCoroutine(RingCo(go, mat));
    }

    IEnumerator RingCo(GameObject ringGo, Material mat)
    {
        float t = 0f;
        while (t < RingLifetime)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / RingLifetime);
            float ease = 1f - (1f - k) * (1f - k); // ease-out — 처음에 빠르게 퍼지고 끝에서 느려짐
            float scale = Mathf.Lerp(RingStartScale, RingEndScale, ease);
            ringGo.transform.localScale = new Vector3(scale, scale, 1f);
            mat.SetFloat(IdIntensity, 1f - k);
            yield return null;
        }
        Destroy(mat);
        Destroy(ringGo);
    }

    // 중심이 비고 가장자리 쪽에 밝은 띠가 있는 절차 생성 링 텍스처 — 충격파 한 장으로 재사용.
    static Sprite GetRingSprite()
    {
        if (_ringSprite != null) return _ringSprite;

        const int N = 64;
        const float RingCenter = 0.78f; // 반경 비율(0~1) — 링이 가장자리 쪽에 몰리게
        const float RingWidth = 0.16f;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[N * N];
        float c = (N - 1) * 0.5f;
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
            float a = Mathf.Clamp01(1f - Mathf.Abs(d - RingCenter) / RingWidth);
            a = a * a * (3f - 2f * a); // smoothstep
            px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply();
        _ringSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        return _ringSprite;
    }

    IEnumerator Beat(Transform player, SectionCamera cam, float strength, float camRampIn, float camHold, float camRampOut)
    {
        if (cam != null)
            cam.FocusPulse(player.position, 0f, CamZoom * strength, camRampIn, camHold, camRampOut);

        yield return Pulse(FlashColor, LubPeakAlpha * strength, RiseTime, FallTime);
        yield return WaitUnscaled(BeatGap);
        yield return Pulse(FlashColor, DubPeakAlpha * strength, RiseTime, FallTime);
    }

    IEnumerator Pulse(Color color, float peak, float rise, float fall)
    {
        float t = 0f;
        while (t < rise)
        {
            t += Time.unscaledDeltaTime;
            SetColor(color, Mathf.Lerp(0f, peak, t / rise));
            yield return null;
        }
        t = 0f;
        while (t < fall)
        {
            t += Time.unscaledDeltaTime;
            SetColor(color, Mathf.Lerp(peak, 0f, t / fall));
            yield return null;
        }
        SetColor(color, 0f);
    }

    IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
    }

    void SetColor(Color color, float a)
    {
        if (img != null) img.color = new Color(color.r, color.g, color.b, a);
    }

    // PlayerDamageFlashUI와 같은 패턴 — 기존 오버레이 캔버스가 있으면 그걸 같이 쓴다(중복 캔버스 방지).
    Canvas FindOverlayCanvas()
    {
        Canvas best = null;
        foreach (var cv in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (cv.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (best == null || cv.sortingOrder < best.sortingOrder) best = cv;
        }
        return best;
    }

    Canvas CreateOverlayCanvas()
    {
        var go = new GameObject("RampageHeartbeatCanvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }
}
