using UnityEngine;
using UnityEngine.UI;

// 플레이어 피격 시 화면 전체에 옅은 붉은 점멸 — 씬에 Global Volume/Vignette가 없어서
// (LIGHT_ENERGY_RAMPAGE_PLAN.md §4-3c 실측) PlayerHudUI와 같은 "런타임 Canvas 절차 생성" 패턴을 쓴다.
// 텍스처·프리팹 의존 0, 씬 미저장.
[ExecuteAlways]
public class PlayerDamageFlashUI : MonoBehaviour
{
    public float fadeIn = 0.06f;
    public float fadeOut = 0.22f;
    [Range(0f, 1f)] public float maxAlpha = 0.35f;
    // 폭주 팔레트(RampageEdge 계열, LIGHT_ENERGY_RAMPAGE_PLAN.md §4-3)를 이 시점부터 선점 —
    // 나중에 B/C-5가 같은 값을 그대로 쓴다.
    public Color flashColor = new Color(0.55f, 0.02f, 0.04f);

    static PlayerDamageFlashUI _instance;
    Image _img;
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
    }

    void Build()
    {
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) canvas = CreateOverlayCanvas();

        // 이름으로 찾아서 지운다 — _img는 private 필드라 도메인 리로드마다 null로 초기화되지만
        // 이미 만든 자식은 씬에 남아있어 필드 체크만으론 못 잡는다(PlayerHudUI.Build 참고).
        var existing = canvas.transform.Find("DamageFlash");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject); else DestroyImmediate(existing.gameObject);
        }

        var go = new GameObject("DamageFlash", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        // HUD(체력칸·게이지)가 위에 그려지도록 맨 뒤로 보낸다(PlayerHudUI의 회피/처형 프롬프트 규칙과 동일).
        rt.SetAsFirstSibling();

        _img = go.GetComponent<Image>();
        _img.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        _img.raycastTarget = false;
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
    }

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
        var go = new GameObject("PlayerDamageFlashCanvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }
}
