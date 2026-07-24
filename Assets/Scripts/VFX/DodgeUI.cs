using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 회피 저스트 확인 프롬프트 UI. UniTrio-Game-2026의 JustDodgeUI를 참고해 이식(같은 DashUI 프리팹,
/// "F 누르기" 연출 동일) — 인벤토리 패널 연동/무기 미착용 경고 등 이 프로젝트에 없는 기능은 걷어냄.
/// 씬에 Overlay Canvas가 없으면 런타임에 자가생성(DamageText/HitVFX와 동일한 자기완결 방식).
/// </summary>
public class DodgeUI : MonoBehaviour
{
    [Tooltip("DashUI 프롬프트를 화면 중앙에서 얼마나 오른쪽/위로 옮길지(px)")]
    public Vector2 PromptOffset = new Vector2(320f, 0f);

    private Canvas     _canvas;
    private GameObject _prompt;
    private Color[]    _promptOrigColors;
    private Coroutine  _hideCo;
    private bool       _hiding;

    public static DodgeUI GetOrCreate()
    {
        var inst = FindFirstObjectByType<DodgeUI>();
        if (inst != null) return inst;

        var go = new GameObject("DodgeUI");
        inst = go.AddComponent<DodgeUI>();
        inst.Init();
        return inst;
    }

    private void Init()
    {
        _canvas = FindOverlayCanvas();
        if (_canvas == null) _canvas = CreateOverlayCanvas();

        var prefab = Resources.Load<GameObject>("Prefabs/DashUI");
        if (prefab == null)
        {
            Debug.LogWarning("[DodgeUI] Resources/Prefabs/DashUI 를 찾지 못했습니다.");
            return;
        }

        _prompt = Instantiate(prefab, _canvas.transform);
        _prompt.transform.localScale    = Vector3.one;
        _prompt.transform.localPosition = new Vector3(PromptOffset.x, PromptOffset.y, 0f);

        var graphics = _prompt.GetComponentsInChildren<Graphic>(true);
        _promptOrigColors = new Color[graphics.Length];
        for (int i = 0; i < graphics.Length; i++) _promptOrigColors[i] = graphics[i].color;

        _prompt.SetActive(false);
    }

    private Canvas FindOverlayCanvas()
    {
        // 비활성 캔버스는 재사용하지 않는다(씬에 용도 불명의 비활성 Canvas가 있어도 그 밑에 프롬프트를 달면
        // 같이 숨어버려 렌더되지 않음) — 활성 상태인 것만 후보로 삼고, 없으면 새로 만든다.
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
        var go = new GameObject("DodgeUICanvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // ── 프롬프트 ──────────────────────────────────────────────────

    public void ShowPrompt()
    {
        if (_prompt == null) return;
        if (_hideCo != null) { StopCoroutine(_hideCo); _hideCo = null; }
        _hiding = false;

        _prompt.transform.localScale    = Vector3.one;
        _prompt.transform.localPosition = new Vector3(PromptOffset.x, PromptOffset.y, 0f);
        RestorePromptColors();
        _prompt.SetActive(true);
    }

    /// <summary>확인키 입력 시: 흰색으로 점멸하며 커지고 사라짐(증발).</summary>
    public void FlashHidePrompt()
    {
        if (_prompt == null || !_prompt.activeSelf) return;
        if (_hideCo != null) StopCoroutine(_hideCo);
        _hideCo = StartCoroutine(FlashHideRoutine());
    }

    /// <summary>애니메이션 없이 즉시 숨김(윈도우 만료 등). 증발 연출 중이면 건드리지 않음.</summary>
    public void HidePromptImmediate()
    {
        if (_prompt == null || _hiding) return;
        if (_hideCo != null) { StopCoroutine(_hideCo); _hideCo = null; }
        _prompt.SetActive(false);
    }

    private IEnumerator FlashHideRoutine()
    {
        _hiding = true;
        var graphics = _prompt.GetComponentsInChildren<Graphic>(true);

        foreach (var g in graphics) g.color = Color.white;

        const float dur = 0.28f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            _prompt.transform.localScale = Vector3.one * (1f + 0.45f * k);
            float a = 1f - k;
            foreach (var g in graphics) { var c = g.color; c.a = a; g.color = c; }
            yield return null;
        }

        _prompt.SetActive(false);
        _prompt.transform.localScale = Vector3.one;
        RestorePromptColors();
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
