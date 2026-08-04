using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 처형 프롬프트 UI(스펙 1) — 커서를 대면 페이드 인, 뗄 때까지 계속 떠 있다가 페이드 아웃.
/// 프리팹: Resources/Prefabs/ExecutionUI ("R" + "처형"). 대시-카운터의 DodgeUI(DashUI 프리팹, "F" +
/// "카운터 공격")와 같은 자가완결 패턴 — 씬에 Overlay Canvas가 없으면 런타임에 만든다.
/// 알파는 CanvasGroup 하나로만 만진다(Graphic 색을 일일이 저장·복원하지 않아도 된다).
/// </summary>
public class ExecutionUI : MonoBehaviour
{
    [Tooltip("프롬프트를 화면 중앙에서 얼마나 오른쪽/위로 옮길지(px)")]
    public Vector2 PromptOffset = new Vector2(320f, 0f);

    public float fadeDuration = 0.2f;

    const float FlashDuration = 0.28f; // R키 증발 연출 길이(DodgeUI와 동일)

    // GetOrCreate가 매 프레임 호출될 수 있어(타겟이 없어도 HidePrompt가 불린다) 인스턴스를 캐시한다.
    static ExecutionUI _instance;

    GameObject  _prompt;
    CanvasGroup _group;
    Graphic[]   _graphics;
    Color[]     _baseColors;
    Coroutine   _fadeCo;
    bool        _shown;   // 목표 상태 — 페이드가 진행 중이어도 "보이려는 중"인지 알 수 있다

    public bool IsVisible => _shown && _group != null && _group.alpha > 0.99f;

    public static ExecutionUI GetOrCreate()
    {
        // 파괴된 오브젝트는 Unity의 == null이 true를 돌려주므로 씬 전환 후에도 안전하다.
        if (_instance != null) return _instance;
        return new GameObject("ExecutionUI").AddComponent<ExecutionUI>(); // Awake가 _instance를 세운다
    }

    void Awake()
    {
        // 씬에 직접 얹어둔 경우에도 초기화가 되게 Awake에서 Init한다
        // (예전엔 GetOrCreate가 새로 만들 때만 Init을 불러, 씬에 컴포넌트가 있으면 조용히 아무것도 안 했다).
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        Init();
    }

    void Init()
    {
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) canvas = CreateOverlayCanvas();

        var prefab = Resources.Load<GameObject>("Prefabs/ExecutionUI");
        if (prefab == null)
        {
            Debug.LogWarning("[ExecutionUI] Resources/Prefabs/ExecutionUI 를 찾지 못했습니다.");
            return;
        }

        _prompt = Instantiate(prefab, canvas.transform);
        _prompt.transform.localScale    = Vector3.one;
        _prompt.transform.localPosition = new Vector3(PromptOffset.x, PromptOffset.y, 0f);

        _group = _prompt.GetComponent<CanvasGroup>();
        if (_group == null) _group = _prompt.AddComponent<CanvasGroup>();
        _group.alpha = 0f;

        _graphics = _prompt.GetComponentsInChildren<Graphic>(true);
        _baseColors = new Color[_graphics.Length];
        for (int i = 0; i < _graphics.Length; i++) _baseColors[i] = _graphics[i].color;

        _prompt.SetActive(false);
    }

    // 비활성 캔버스는 재사용하지 않는다(그 밑에 프롬프트를 달면 같이 숨어 렌더되지 않음) —
    // 활성 상태인 것만 후보로 삼고, 없으면 새로 만든다.
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
        var go = new GameObject("ExecutionUICanvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // ── 프롬프트 ──────────────────────────────────────────────────

    /// <summary>페이드 인. 커서를 떼기 전까지 계속 떠 있는다.</summary>
    public void ShowPrompt()
    {
        if (_prompt == null || _shown) return;
        _shown = true;
        RestoreColors();
        _prompt.transform.localScale    = Vector3.one;
        _prompt.transform.localPosition = new Vector3(PromptOffset.x, PromptOffset.y, 0f);
        _prompt.SetActive(true);
        StartFade(1f, fadeDuration, false);
    }

    /// <summary>페이드 아웃(커서를 뗐을 때).</summary>
    public void HidePrompt()
    {
        if (_prompt == null || !_shown) return;
        _shown = false;
        StartFade(0f, fadeDuration, false);
    }

    /// <summary>R키 입력 시: 흰색으로 점멸하며 커지고 사라짐(DodgeUI와 같은 증발 연출).</summary>
    public void FlashHidePrompt()
    {
        if (_prompt == null || !_shown) return;
        _shown = false;
        if (_graphics != null) foreach (var g in _graphics) g.color = Color.white;
        if (_group != null) _group.alpha = 1f;
        StartFade(0f, FlashDuration, true);
    }

    /// <summary>연출 없이 즉시 숨김.</summary>
    public void HidePromptImmediate()
    {
        if (_prompt == null) return;
        _shown = false;
        if (_fadeCo != null) { StopCoroutine(_fadeCo); _fadeCo = null; }
        if (_group != null) _group.alpha = 0f;
        _prompt.transform.localScale = Vector3.one;
        _prompt.SetActive(false);
        RestoreColors();
    }

    void StartFade(float target, float duration, bool grow)
    {
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeRoutine(target, duration, grow));
    }

    // 인·아웃·증발을 알파 페이드 하나로 처리한다(grow=true면 커지면서 사라지는 R키 연출).
    // 시간은 항상 unscaled — 히트스톱/슬로우모션 중에도 같은 속도로 진행돼야 한다.
    IEnumerator FadeRoutine(float target, float duration, bool grow)
    {
        float from = _group != null ? _group.alpha : 0f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            if (_group != null) _group.alpha = Mathf.Lerp(from, target, k);
            if (grow) _prompt.transform.localScale = Vector3.one * (1f + 0.45f * k);
            yield return null;
        }

        if (_group != null) _group.alpha = target;
        if (grow) _prompt.transform.localScale = Vector3.one;
        if (target <= 0f) { _prompt.SetActive(false); RestoreColors(); }
        _fadeCo = null;
    }

    void RestoreColors()
    {
        if (_graphics == null || _baseColors == null) return;
        for (int i = 0; i < _graphics.Length && i < _baseColors.Length; i++)
            _graphics[i].color = _baseColors[i];
    }
}
