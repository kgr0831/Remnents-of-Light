using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 처형 프롬프트 UI — 커서 호버로 페이드 인/아웃.
/// DodgeUI.cs(대시-카운터 F키 프롬프트)와 동일한 자가완결 패턴:
/// 씬에 Overlay Canvas가 없으면 런타임에 자가생성.
/// 프리팹: Resources/Prefabs/ExecutionUI ("R" + "처형").
/// </summary>
public class ExecutionUI : MonoBehaviour
{
    [Tooltip("ExecutionUI 프롬프트를 화면 중앙에서 얼마나 오른쪽/위로 옮길지(px)")]
    public Vector2 PromptOffset = new Vector2(320f, 0f);

    public float fadeDuration = 0.2f;

    private Canvas     _canvas;
    private GameObject _prompt;
    private CanvasGroup _canvasGroup;
    private Color[]    _promptOrigColors;
    private Coroutine  _fadeCo;
    private Coroutine  _hideCo;
    private bool       _hiding;
    private bool       _visible;

    public bool IsVisible => _visible;

    public static ExecutionUI GetOrCreate()
    {
        var inst = FindFirstObjectByType<ExecutionUI>();
        if (inst != null) return inst;

        var go = new GameObject("ExecutionUI");
        inst = go.AddComponent<ExecutionUI>();
        inst.Init();
        return inst;
    }

    private void Init()
    {
        _canvas = FindOverlayCanvas();
        if (_canvas == null) _canvas = CreateOverlayCanvas();

        var prefab = Resources.Load<GameObject>("Prefabs/ExecutionUI");
        if (prefab == null)
        {
            Debug.LogWarning("[ExecutionUI] Resources/Prefabs/ExecutionUI 를 찾지 못했습니다.");
            return;
        }

        _prompt = Instantiate(prefab, _canvas.transform);
        _prompt.transform.localScale    = Vector3.one;
        _prompt.transform.localPosition = new Vector3(PromptOffset.x, PromptOffset.y, 0f);

        // CanvasGroup으로 페이드 제어
        _canvasGroup = _prompt.GetComponent<CanvasGroup>();
        if (_canvasGroup == null) _canvasGroup = _prompt.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;

        var graphics = _prompt.GetComponentsInChildren<Graphic>(true);
        _promptOrigColors = new Color[graphics.Length];
        for (int i = 0; i < graphics.Length; i++) _promptOrigColors[i] = graphics[i].color;

        _prompt.SetActive(false);
    }

    private Canvas FindOverlayCanvas()
    {
        Canvas best = null;
        foreach (var cv in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (cv.renderMode != RenderMode.ScreenSpaceOverlay) continue;
            if (best == null || cv.sortingOrder < best.sortingOrder) best = cv;
        }
        return best;
    }

    private Canvas CreateOverlayCanvas()
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

    /// <summary>페이드 인으로 프롬프트를 보여준다. 이미 보이는 중이면 무시.</summary>
    public void ShowPrompt()
    {
        if (_prompt == null) return;
        if (_visible && !_hiding) return; // 이미 보이고 있으면 중복 호출 무시
        if (_hideCo != null) { StopCoroutine(_hideCo); _hideCo = null; }
        if (_fadeCo != null) { StopCoroutine(_fadeCo); _fadeCo = null; }
        _hiding = false;

        _prompt.transform.localScale    = Vector3.one;
        _prompt.transform.localPosition = new Vector3(PromptOffset.x, PromptOffset.y, 0f);
        RestorePromptColors();
        _prompt.SetActive(true);
        _fadeCo = StartCoroutine(FadeInRoutine());
    }

    /// <summary>페이드 아웃으로 프롬프트를 숨긴다.</summary>
    public void HidePrompt()
    {
        if (_prompt == null || !_prompt.activeSelf || _hiding) return;
        if (_fadeCo != null) { StopCoroutine(_fadeCo); _fadeCo = null; }
        _fadeCo = StartCoroutine(FadeOutRoutine());
    }

    /// <summary>R키 입력 시: 흰색으로 점멸하며 커지고 사라짐(DodgeUI.FlashHidePrompt과 동일).</summary>
    public void FlashHidePrompt()
    {
        if (_prompt == null || !_prompt.activeSelf) return;
        if (_fadeCo != null) { StopCoroutine(_fadeCo); _fadeCo = null; }
        if (_hideCo != null) StopCoroutine(_hideCo);
        _hideCo = StartCoroutine(FlashHideRoutine());
    }

    /// <summary>애니메이션 없이 즉시 숨김.</summary>
    public void HidePromptImmediate()
    {
        if (_prompt == null || _hiding) return;
        if (_fadeCo != null) { StopCoroutine(_fadeCo); _fadeCo = null; }
        if (_hideCo != null) { StopCoroutine(_hideCo); _hideCo = null; }
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        _prompt.SetActive(false);
        _visible = false;
    }

    private IEnumerator FadeInRoutine()
    {
        float from = _canvasGroup != null ? _canvasGroup.alpha : 0f;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);
            if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Lerp(from, 1f, k);
            yield return null;
        }
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;
        _visible = true;
        _fadeCo = null;
    }

    private IEnumerator FadeOutRoutine()
    {
        _hiding = true;
        float from = _canvasGroup != null ? _canvasGroup.alpha : 1f;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);
            if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Lerp(from, 0f, k);
            yield return null;
        }
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        _prompt.SetActive(false);
        _visible = false;
        _hiding = false;
        _fadeCo = null;
    }

    private IEnumerator FlashHideRoutine()
    {
        _hiding = true;
        var graphics = _prompt.GetComponentsInChildren<Graphic>(true);

        foreach (var g in graphics) g.color = Color.white;
        if (_canvasGroup != null) _canvasGroup.alpha = 1f;

        const float dur = 0.28f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            _prompt.transform.localScale = Vector3.one * (1f + 0.45f * k);
            float a = 1f - k;
            if (_canvasGroup != null) _canvasGroup.alpha = a;
            yield return null;
        }

        _prompt.SetActive(false);
        _prompt.transform.localScale = Vector3.one;
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        RestorePromptColors();
        _visible = false;
        _hiding = false;
        _hideCo = null;
    }

    private void RestorePromptColors()
    {
        if (_prompt == null || _promptOrigColors == null) return;
        var graphics = _prompt.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length && i < _promptOrigColors.Length; i++)
            graphics[i].color = _promptOrigColors[i];
    }
}
