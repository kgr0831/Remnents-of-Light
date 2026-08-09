using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 초월 진입 순간에만 재생되는 1회성 "방출" 연출 — TRANSCENDENCE_PLAN.md §13 B안("가속 릴리즈").
// RampageHeartbeatFx(폭주의 대응 연출)와 같은 자리·같은 구조(정적 Begin, 자기 파괴,
// Time.unscaledDeltaTime 기반)를 쓰지만, 구성 요소는 의도적으로 더 적다 — 폭주가 "박동(2회)·
// 카메라 펀치(2회)·충격파 링·화면 글리치·스프라이트 프리즈·브리프 슬로우모"로 혼란을 표현한다면,
// 초월은 "플래시·링·카메라 펀치 각 1회"만으로 정교함을 표현한다(사용자 지시 2026-08-02: 폭주와
// 정반대 느낌 — 박동 대신 스냅, 글리치 대신 클린).
// 진입 흡수 버스트(LightPixelFx.SpawnAbsorb, StartTranscend())가 "빛을 모으는" 그림이라, 이 연출은
// 그 픽셀들이 도착하는 타이밍(BurstDelay)에 맞춰 "모아서 → 터뜨린다"는 서사로 이어붙인다.
public class TranscendBurstFx : MonoBehaviour
{
    // cyan-화이트 — 순수 흰색보다 살짝 청록으로 치우쳐 초월의 팔레트(TranscendBloomTint)와 이어진다.
    static readonly Color FlashColor = new Color(0.85f, 1f, 1f);
    // TranscendBloomTint(PlayerController)와 같은 값 — 마스크 블룸·아우라·상승 픽셀과 톤을 통일한다.
    static readonly Color RingColor = new Color(0.10f, 0.95f, 1.00f, 1f);

    const float BurstDelay = 0.45f; // 흡수 픽셀 수명(0.35~0.55s, LightPixelFx.SpawnAbsorb)의 중간값 — 도착 타이밍에 맞춘다

    const float FlashPeak = 0.45f;
    const float FlashRise = 0.02f;
    const float FlashFall = 0.06f;

    const float RingLifetime = 0.25f;  // 폭주(0.35s)보다 타이트하게 — 정교하고 빠른 인상
    const float RingStartScale = 0.3f;
    const float RingEndScale = 2.5f;
    const string RingShaderName = "Custom/PlayerBloomOverlay";
    static readonly int IdColor = Shader.PropertyToID("_Color");
    static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
    static readonly int IdBoost = Shader.PropertyToID("_BloomBoost");
    static readonly int IdFlatten = Shader.PropertyToID("_Flatten");
    static Sprite _ringSprite;

    const float CamZoom = 0.25f; // 폭주(CamZoom=0.5)의 절반 — 단발 펀치라 세기도 약하게

    Image img;

    public static void Begin(Transform player, SectionCamera cam, float camRampIn, float camHold, float camRampOut)
    {
        if (player == null) return;
        var go = new GameObject("TranscendBurstFx");
        var fx = go.AddComponent<TranscendBurstFx>();
        fx.Build();
        fx.StartCoroutine(fx.Sequence(player, cam, camRampIn, camHold, camRampOut));
    }

    void Build()
    {
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) canvas = CreateOverlayCanvas();

        var go = new GameObject("TranscendFlash", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetAsFirstSibling(); // HUD보다 뒤에 그려지게(RampageHeartbeatFx·PlayerDamageFlashUI와 같은 규칙)

        img = go.GetComponent<Image>();
        img.color = new Color(FlashColor.r, FlashColor.g, FlashColor.b, 0f);
        img.raycastTarget = false;
    }

    IEnumerator Sequence(Transform player, SectionCamera cam, float camRampIn, float camHold, float camRampOut)
    {
        yield return WaitUnscaled(BurstDelay); // 흡수 픽셀이 도착할 때까지 대기 — "모아서 → 터뜨린다"

        SpawnShockwaveRing(player);
        if (cam != null) cam.FocusPulse(player.position, 0f, CamZoom, camRampIn, camHold, camRampOut);
        yield return Pulse(FlashColor, FlashPeak, FlashRise, FlashFall);

        // ⚠️ RingCo는 SpawnShockwaveRing()이 이 컴포넌트(this)에서 StartCoroutine으로 띄운 자식
        // 코루틴이다 — 아래 Destroy(gameObject)가 먼저 실행되면 호스트가 사라지면서 링 애니메이션이
        // 중간에 강제로 끊긴 채 멈춰버린다(스스로 정리도 못 하고 고정된 스케일로 남는다). 링 수명이
        // 끝날 때까지 기다린 뒤에 파괴한다.
        float remaining = RingLifetime - (FlashRise + FlashFall);
        if (remaining > 0f) yield return WaitUnscaled(remaining);

        Destroy(gameObject);
    }

    void SpawnShockwaveRing(Transform player)
    {
        Shader sh = Shader.Find(RingShaderName);
        if (sh == null || player == null) return;

        var go = new GameObject("TranscendShockwaveRing");
        int protectedLayer = LayerMask.NameToLayer("VFXNoGrayscale");
        if (protectedLayer >= 0) go.layer = protectedLayer;
        // RampageAuraFx·RampageHeartbeatFx와 같은 몸통 중심 보정(발밑 피봇 대응).
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

    // 중심이 비고 가장자리 쪽에 밝은 띠가 있는 절차 생성 링 텍스처 — RampageHeartbeatFx.GetRingSprite()와
    // 같은 기법(신규 셰이더 없음), cyan 색만 다르다.
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

    // PlayerDamageFlashUI·RampageHeartbeatFx와 같은 패턴 — 기존 오버레이 캔버스가 있으면 그걸 같이 쓴다(중복 캔버스 방지).
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
        var go = new GameObject("TranscendBurstCanvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }
}
