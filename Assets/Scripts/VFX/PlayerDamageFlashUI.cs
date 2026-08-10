using UnityEngine;
using UnityEngine.UI;

// 플레이어 피격 시 화면 전체에 붉은 점멸 — 씬에 Global Volume/Vignette가 없어서
// (LIGHT_ENERGY_RAMPAGE_PLAN.md §4-3c 실측) PlayerHudUI와 같은 "런타임 Canvas 절차 생성" 패턴을 쓴다.
// 텍스처·프리팹 의존 0, 씬 미저장.
//
// ⚠️ 전용 캔버스 + 최상위 sortingOrder(사용자 지시 2026-08-11 "노이즈나 어떤 요소든 그 위에 피격
//    효과가 존재") — 예전엔 HUD와 같은 Overlay 캔버스를 공유해서 그 캔버스 안에서만 맨 뒤(HUD보다
//    아래)였다. DeathScreenUI(5100)·ScreenFadeUI(5000)보다도 위인 전용 sortingOrder를 써서, 사망
//    화면·암전·HUD·카메라 노이즈(ScreenGlitchFeature — 카메라 렌더 패스라 애초에 Overlay보다 아래)
//    무엇이 떠 있든 피격 점멸이 항상 그 위에서 보인다.
[ExecuteAlways]
public class PlayerDamageFlashUI : MonoBehaviour
{
    const int SortingOrder = 5200;

    public float fadeIn = 0.04f;
    public float fadeOut = 0.22f;
    // 기존 0.35 → 0.6(사용자 지시 2026-08-11 "피격 효과 강화... 훨씬 잘보이게").
    [Range(0f, 1f)] public float maxAlpha = 0.6f;
    // 폭주 팔레트(RampageEdge 계열, LIGHT_ENERGY_RAMPAGE_PLAN.md §4-3)를 이 시점부터 선점 —
    // 나중에 B/C-5가 같은 값을 그대로 쓴다.
    public Color flashColor = new Color(0.55f, 0.02f, 0.04f);
    // 튕겨나가는 "충격" 느낌을 더하는 흰색 테두리 펄스(사용자 지시 "훨씬 잘보이게") — 빨간 점멸과
    // 같은 fadeIn에 최고조였다가 fadeIn 직후부터 붉은 점멸보다 훨씬 빠르게 사그라든다(격투게임의
    // 히트 플래시처럼 "번쩍"하고 빠지는 느낌, 점멸 자체(fadeOut)는 그대로 천천히 빠진다).
    public Color impactFlashColor = Color.white;
    [Range(0f, 1f)] public float impactMaxAlpha = 0.5f;
    public float impactFadeOut = 0.08f;

    static PlayerDamageFlashUI _instance;
    Image _img;
    Image _impactImg;
    float _timer;
    bool _active;

    public static PlayerDamageFlashUI GetOrCreate()
    {
        if (_instance != null) return _instance;
        return new GameObject("PlayerDamageFlashUI").AddComponent<PlayerDamageFlashUI>();
    }

    public static void Flash() => GetOrCreate().Trigger();

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            return;
        }
        _instance = this;
        Build();
        if (!Application.isPlaying) ApplyPreviewColor();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

#if UNITY_EDITOR
    // Edit 모드 실시간 프리뷰 — Inspector에서 flashColor/maxAlpha를 바꾸면 즉시 반영한다.
    void OnValidate()
    {
        if (Application.isPlaying || _img == null) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            ApplyPreviewColor();
        };
    }
#endif

    /// <summary>에디터 프리뷰용 — 점멸의 최대 상태(maxAlpha)를 정적으로 보여준다.</summary>
    void ApplyPreviewColor()
    {
        if (_img != null) _img.color = new Color(flashColor.r, flashColor.g, flashColor.b, maxAlpha);
        if (_impactImg != null) _impactImg.color = new Color(impactFlashColor.r, impactFlashColor.g, impactFlashColor.b, impactMaxAlpha);
    }

    // 이 GameObject 밑에 전용 캔버스를 직접 만든다(DeathScreenUI/ScreenFadeUI와 같은 패턴) — 다른
    // 기능과 캔버스를 공유하지 않으므로 sortingOrder가 항상 그대로 유지된다.
    void Build()
    {
        var existing = transform.Find("DamageFlashCanvas");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject); else DestroyImmediate(existing.gameObject);
        }

        var canvasGo = new GameObject("DamageFlashCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        _img = CreateFullscreenImage(canvasGo.transform, "DamageFlash");
        _img.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);

        // 충격 펄스는 붉은 점멸 위(같은 캔버스 안 뒤 자식일수록 위에 그려짐)에 겹친다.
        _impactImg = CreateFullscreenImage(canvasGo.transform, "ImpactFlash");
        _impactImg.color = new Color(impactFlashColor.r, impactFlashColor.g, impactFlashColor.b, 0f);
    }

    Image CreateFullscreenImage(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    public void Trigger()
    {
        _active = true;
        _timer = 0f;
    }

    // 히트스톱(timeScale=0) 중에도 점멸이 진행돼야 하므로 unscaled.
    void Update()
    {
        if (!Application.isPlaying) return;
        if (!_active) return;
        _timer += Time.unscaledDeltaTime;

        float a;
        if (_timer < fadeIn)
        {
            a = fadeIn > 0f ? Mathf.Lerp(0f, maxAlpha, _timer / fadeIn) : maxAlpha;
        }
        else
        {
            float t = _timer - fadeIn;
            if (t >= fadeOut) { a = 0f; _active = false; }
            else a = fadeOut > 0f ? Mathf.Lerp(maxAlpha, 0f, t / fadeOut) : 0f;
        }

        if (_img != null) _img.color = new Color(flashColor.r, flashColor.g, flashColor.b, a);

        // 충격 펄스 — fadeIn까지는 점멸과 같이 올라갔다가, 그 직후부터 impactFadeOut만큼 빠르게 빠진다.
        float ia;
        if (_timer < fadeIn)
        {
            ia = fadeIn > 0f ? Mathf.Lerp(0f, impactMaxAlpha, _timer / fadeIn) : impactMaxAlpha;
        }
        else
        {
            float it = _timer - fadeIn;
            ia = impactFadeOut > 0f ? Mathf.Lerp(impactMaxAlpha, 0f, it / impactFadeOut) : 0f;
            ia = Mathf.Max(0f, ia);
        }
        if (_impactImg != null) _impactImg.color = new Color(impactFlashColor.r, impactFlashColor.g, impactFlashColor.b, ia);
    }
}
