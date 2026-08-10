using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 전체 화면 암전(검은 페이드). 낙사 복귀 연출이 쓴다.
// PlayerDamageFlashUI와 같은 "런타임 Canvas 절차 생성" 패턴이라 텍스처·프리팹 의존이 0이고
// 씬에 저장되지 않는다.
//
// ⚠️ 두 가지가 이 클래스의 전제다.
//  1) 반드시 unscaled로 돈다 — 낙사 연출은 Time.timeScale=0으로 세계를 멈춘 채 진행된다.
//  2) HUD보다 위에 그려야 한다 — 암전인데 체력칸이 그 위에 떠 있으면 안 되므로, 기존 오버레이
//     캔버스에 얹지 않고 sortingOrder를 크게 준 전용 캔버스를 따로 만든다
//     (PlayerDamageFlashUI는 반대로 HUD 아래에 깔려야 해서 기존 캔버스의 맨 뒤로 들어간다).
[ExecuteAlways]
public class ScreenFadeUI : MonoBehaviour
{
    const int SortingOrder = 5000;

    static ScreenFadeUI _instance;

    GameObject _canvasGo;
    Image _img;
    float _alpha;
    float _target;
    float _speed; // 초당 알파 변화량

    // [ASSERT] 판독구
    public static float Alpha => _instance != null ? _instance._alpha : 0f;

    public static ScreenFadeUI GetOrCreate()
    {
        if (_instance != null) return _instance;
        return new GameObject("ScreenFadeUI").AddComponent<ScreenFadeUI>();
    }

    /// <summary>duration초(실시간) 동안 target 알파까지 간다. 도달할 때까지 yield할 수 있다.</summary>
    public static IEnumerator FadeTo(float target, float duration)
    {
        var fade = GetOrCreate();
        fade._target = Mathf.Clamp01(target);
        fade._speed = duration > 0.0001f ? 1f / duration : float.PositiveInfinity;
        while (fade != null && fade._alpha != fade._target) yield return null;
    }

    /// <summary>연출이 중간에 끊겨도 화면이 검은 채로 남지 않게 하는 비상 복구.</summary>
    public static void ClearImmediate()
    {
        if (_instance == null) return;
        _instance._alpha = 0f;
        _instance._target = 0f;
        _instance.Apply();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            return;
        }
        _instance = this;
        Build();
    }

    void Build()
    {
        // 이름으로 찾아서 지운다 — _canvasGo는 private 필드라 도메인 리로드마다 null로 초기화되지만
        // 이미 만든 자식은 씬에 남아있어 필드 체크만으론 못 잡는다(PlayerHudUI.Build 참고).
        var existing = transform.Find("ScreenFadeCanvas");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject); else DestroyImmediate(existing.gameObject);
        }

        _canvasGo = new GameObject("ScreenFadeCanvas");
        _canvasGo.transform.SetParent(transform, false);
        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = _canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var go = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_canvasGo.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _img = go.GetComponent<Image>();
        _img.raycastTarget = false;
        Apply();
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (_alpha == _target) return;
        _alpha = Mathf.MoveTowards(_alpha, _target, _speed * Time.unscaledDeltaTime);
        Apply();
    }

    void Apply()
    {
        if (_img != null) _img.color = new Color(0f, 0f, 0f, _alpha);
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
}
