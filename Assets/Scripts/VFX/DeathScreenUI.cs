using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// 사망 화면 — 검은 배경 + "SIGNAL LOST" + Reconnect/Exit 버튼(사용자 지시 2026-08-10 전면 재설계,
// 실측 리포트 반영 3차 수정 — 아래 ⚠️ 항목들).
//
// ⚠️ 렌더 방식 전환(2026-08-10, 사용자 지시 "그냥 아예 패널로 다 안보이게"): 처음엔 ScreenGlitchFeature가
//    이 UI에도 노이즈를 먹이도록 Screen Space - Camera로 만들었는데, 그러면 Main Camera가 그리는
//    "무엇이든" 이 패널과 같은 카메라 파이프라인을 공유하게 돼 실측으로 3개나 뚫렸다 — Canvas
//    sortingOrder(→500으로 상향), 독립 Base 카메라(BossPixelCamera), Light2D(카메라 On/Off와 무관하게
//    라이트 텍스처 합성 단계에서 그려짐), 정적 텍스처 참조로 그리는 렌더러 피처(BossOverlapOutlineFeature).
//    하나씩 막을 때마다 다음 게 튀어나오는 구조라 근본적으로 신뢰할 수 없다고 판단 — **Screen Space -
//    Overlay**(ScreenFadeUI와 같은 방식)로 바꿨다. Overlay 캔버스는 카메라 렌더 패스가 전부 끝난 뒤
//    완전히 분리된 별도 합성 단계에서 그려지므로, 카메라·조명·렌더러 피처가 무엇을 하든 물리적으로
//    가려진다(추가 예외 처리 불필요). 대신 카메라 셰이더가 이 패널까지 못 건드리므로, 노이즈는
//    Custom/UINoiseOverlay 셰이더로 패널 자체에 내장했다(아래 Update 참고).
//
// PlayerDamageFlashUI 등과 같은 런타임 절차 생성 패턴(프리팹 의존 0, 씬 미저장). 폰트는
// Resources.Load(빌드 호환 — AssetDatabase는 에디터 전용이라 안 됨, PlayerBloomFx 마스크 로드와 동일 원칙).
[ExecuteAlways]
public class DeathScreenUI : MonoBehaviour
{
    public enum ButtonResult { None, Reconnect, Exit }

    [Header("레이아웃 (UISandbox에서 조절 가능)")]
    public Vector2 menuSize = new Vector2(1200f, 700f);
    public Vector2 labelSize = new Vector2(1180f, 160f);
    public float labelFontSize = 120f;
    public Vector2 labelPosition = new Vector2(0f, 200f);
    public Vector2 buttonSize = new Vector2(560f, 110f);
    public float buttonFontSize = 58f;
    public Vector2 reconnectButtonPosition = new Vector2(0f, -20f);
    public Vector2 exitButtonPosition = new Vector2(0f, -170f);

    const int SortingOrder = 5100; // ScreenFadeUI(5000)보다 위 — Overlay끼리는 sortingOrder로만 겨룬다
    const string FontResourceName = "Silver Bitmap"; // Assets/Fonts/Resources/Silver Bitmap.asset (도트 폰트라 SDF 대신 Raster/Bitmap — 2026-08-10)
    const string NoiseShaderName = "Custom/UINoiseOverlay";
    const float NoiseSeedStepRate = 20f; // ScreenGlitchFx와 같은 "초당 20회 계단식" 아날로그 느낌
    static readonly Color RedTint = new Color(0.85f, 0.12f, 0.14f);
    static readonly Color WhiteTint = Color.white;
    static readonly int IdNoiseIntensity = Shader.PropertyToID("_NoiseIntensity");
    static readonly int IdSeed = Shader.PropertyToID("_Seed");

    static DeathScreenUI _instance;
    static ButtonResult _lastResult = ButtonResult.None;

    GameObject _canvasGo;
    Image _bg;
    Material _bgMat;
    GameObject _menuRoot;
    CanvasGroup _menuGroup;
    DeathMenuButtonHover[] _hoverButtons;
    Coroutine _fadeCo;
    Coroutine _menuFadeCo;
    TMP_FontAsset _font;
    float _noiseSeedTimer;

    // 텍스트(SIGNAL LOST·버튼)도 "기존과 같이"(ScreenGlitch.shader) 글리치가 나야 한다는 재지시 —
    // 글자를 통째로 옮기면 아무리 일부 글자만 골라도 결국 "이동"으로만 보인다. 실제 기존 효과는
    // 스캔라인 방식: 화면을 가로 밴드로 쪼개 일부 밴드만 옆으로 밀어(ScanlineDensity/Jitter, 아래
    // ApplyScanlineGlitch 참고) 같은 글자 안에서도 위/아래가 서로 다르게 어긋나며 "찢어지는" 것.
    // 같은 Hash(p)=frac(sin(dot(p,12.9898,78.233))*43758.5453) 공식을 정점 공간에 그대로 이식해
    // 문자 내부까지 밴드 단위로 쪼갠다 — 배경 노이즈와 같은 스텝 시드를 공유해 한 몸처럼 튄다.
    TextMeshProUGUI[] _glitchTexts;
    const float GlitchBandHeight = 10f;          // 스캔라인 밴드 높이(px, ScanlineDensity에 대응)
    const float GlitchBandActiveThreshold = 0.82f; // 상위 18% 밴드만 튄다(ScreenGlitch.shader의 0.85와 같은 원리)
    const float GlitchBandJitterX = 14f;         // 밴드가 튈 때 최대 가로 이동량(px, ScanlineJitter에 대응)

    public static void FadeInBlack(float duration) => GetOrCreate().DoFadeBlack(1f, duration);
    public static void FadeOutBlack(float duration) => GetOrCreate().DoFadeBlack(0f, duration);

    /// <summary>메뉴를 켜고 알파 0→1로 페이드인한다("흑백 쉐이더 풀리면서 UI들 페이드 인" — 그레이스케일
    /// 해제와 같은 duration을 쓰면 자연히 같이 끝난다). 매번 켤 때마다 버튼 호버 상태를 리셋한다
    /// (⚠️ 실측 버그: 리셋 안 하면 두 번째 사망에서 직전 호버 색이 그대로 남는다).</summary>
    public static void ShowMenuFaded(float duration) => GetOrCreate().DoShowMenuFaded(duration);
    public static void HideMenu() => GetOrCreate().DoHideMenu();

    /// <summary>연출이 중간에 끊겨도(Play 종료 등) 검은 화면·메뉴가 남지 않게 하는 비상 복구
    /// (ScreenFadeUI.ClearImmediate와 동일 역할).</summary>
    public static void ClearImmediate()
    {
        if (_instance == null) return;
        if (_instance._fadeCo != null) { _instance.StopCoroutine(_instance._fadeCo); _instance._fadeCo = null; }
        if (_instance._menuFadeCo != null) { _instance.StopCoroutine(_instance._menuFadeCo); _instance._menuFadeCo = null; }
        if (_instance._bg != null) _instance._bg.color = new Color(0f, 0f, 0f, 0f);
        if (_instance._menuGroup != null) _instance._menuGroup.alpha = 0f;
        if (_instance._menuRoot != null) _instance._menuRoot.SetActive(false);
        _instance.ResetTextGlitch();
        _lastResult = ButtonResult.None;
    }

    /// <summary>마지막 버튼 클릭 결과를 읽고 즉시 리셋한다(폴링 전용 — DieRoutine의 while 루프가 소비).</summary>
    public static ButtonResult Consume()
    {
        var r = _lastResult;
        _lastResult = ButtonResult.None;
        return r;
    }

    static DeathScreenUI GetOrCreate()
    {
        if (_instance != null) return _instance;
        return new GameObject("DeathScreenUI").AddComponent<DeathScreenUI>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            return;
        }
        _instance = this;
        _font = Resources.Load<TMP_FontAsset>(FontResourceName);
        Build();
        if (!Application.isPlaying) ShowMenuForPreview();
    }

#if UNITY_EDITOR
    // Edit 모드 실시간 프리뷰 — Inspector에서 레이아웃 값을 바꾸면 즉시 다시 그린다.
    void OnValidate()
    {
        if (Application.isPlaying || _canvasGo == null) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            Build();
            ShowMenuForPreview();
        };
    }
#endif

    /// <summary>에디터 프리뷰용 — 배경은 투명(다른 UI가 가려지지 않게) 유지, 메뉴(문구·버튼)만 바로 보여준다.</summary>
    void ShowMenuForPreview()
    {
        if (_menuGroup == null || _menuRoot == null) return;
        _menuGroup.alpha = 1f;
        _menuRoot.SetActive(true);
    }

    void Build()
    {
        // 이름으로 찾아서 지운다 — _canvasGo는 private 필드라 도메인 리로드마다 null로 초기화되지만
        // 이미 만든 자식은 씬에 남아있어 필드 체크만으론 못 잡는다(PlayerHudUI.Build 참고).
        var existing = transform.Find("DeathScreenCanvas");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject); else DestroyImmediate(existing.gameObject);
        }

        EnsureEventSystem();

        _canvasGo = new GameObject("DeathScreenCanvas");
        _canvasGo.transform.SetParent(transform, false);
        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; // 위 클래스 주석 참고 — 카메라와 완전히 분리
        canvas.sortingOrder = SortingOrder;
        var scaler = _canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        _canvasGo.AddComponent<GraphicRaycaster>();

        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        var bgRt = (RectTransform)bgGo.transform;
        bgRt.SetParent(_canvasGo.transform, false);
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        _bg = bgGo.GetComponent<Image>();
        _bg.color = new Color(0f, 0f, 0f, 0f);
        _bg.raycastTarget = true; // 검은 화면인 동안 그 뒤 월드로 클릭이 새지 않게 막는다

        Shader noiseShader = Shader.Find(NoiseShaderName);
        if (noiseShader != null)
        {
            _bgMat = new Material(noiseShader);
            _bgMat.SetFloat(IdNoiseIntensity, 0f);
            _bg.material = _bgMat;
        }
        else
        {
            Debug.LogWarning("[DeathScreenUI] " + NoiseShaderName + " 셰이더를 찾을 수 없어 노이즈 없이 단색 배경으로 대체합니다.");
        }

        _menuRoot = new GameObject("Menu", typeof(RectTransform));
        var menuRt = (RectTransform)_menuRoot.transform;
        menuRt.SetParent(_canvasGo.transform, false);
        menuRt.anchorMin = new Vector2(0.5f, 0.5f);
        menuRt.anchorMax = new Vector2(0.5f, 0.5f);
        menuRt.anchoredPosition = Vector2.zero;
        menuRt.sizeDelta = menuSize;
        _menuGroup = _menuRoot.AddComponent<CanvasGroup>();
        _menuGroup.alpha = 0f;

        var label = CreateLabel("SignalLost", "SIGNAL LOST", labelFontSize, RedTint, labelPosition);
        var reconnectHover = CreateButton("ReconnectButton", "Reconnect", reconnectButtonPosition, () => Click(ButtonResult.Reconnect));
        var exitHover = CreateButton("ExitButton", "Exit", exitButtonPosition, () => Click(ButtonResult.Exit));
        _hoverButtons = new[] { reconnectHover, exitHover };

        _glitchTexts = new[]
        {
            label,
            reconnectHover.GetComponent<TextMeshProUGUI>(),
            exitHover.GetComponent<TextMeshProUGUI>(),
        };

        _menuRoot.SetActive(false);
    }

    // 검은 배경이 조금이라도 보이는 동안엔 항상 지지직거린다("검은 화면도 노이즈 쉐이더 적용") —
    // ScreenGlitchFx와 같은 계단식 시드 갱신(초당 20회)이라 매끈하지 않고 아날로그처럼 뚝뚝 끊긴다.
    // 같은 스텝에 SIGNAL LOST·버튼 텍스트도 같은 시드로 스캔라인 밴드가 찢어진다 — 배경과 텍스트가
    // 같은 시드를 공유해야 "하나의 글리치 사건"처럼 보인다(따로 놀면 어색하다).
    void Update()
    {
        if (!Application.isPlaying) return; // 에디터 프리뷰는 정적 배치만 보여준다(글리치·페이드는 Play 모드 전용)
        if (_bgMat == null || _bg == null) return;
        float alpha = _bg.color.a;
        _bgMat.SetFloat(IdNoiseIntensity, alpha);
        if (alpha <= 0.001f)
        {
            ResetTextGlitch();
            return;
        }

        _noiseSeedTimer += Time.unscaledDeltaTime;
        float step = 1f / NoiseSeedStepRate;
        if (_noiseSeedTimer >= step)
        {
            _noiseSeedTimer -= step;
            float seed = Random.Range(0f, 1000f);
            _bgMat.SetFloat(IdSeed, seed);
            StepTextGlitch(seed);
        }
    }

    void StepTextGlitch(float seed)
    {
        if (_glitchTexts == null) return;
        foreach (var tmp in _glitchTexts)
        {
            if (tmp == null) continue;
            ApplyScanlineGlitch(tmp, seed);
        }
    }

    // ScreenGlitch.shader의 스캔라인 떨림을 정점 공간으로 그대로 이식 — 그 셰이더는 화면을 가로
    // 밴드(ScanlineDensity)로 쪼개 일부(상위 15%)만 옆으로 미는데(uv.x += jitter*active), TMP는
    // 셰이더로 못 건드리므로 같은 수식을 정점의 y좌표에 직접 적용한다. 밴드 하나가 한 글자보다
    // 작아서(글자 높이 ~50~120px vs 밴드 10px) 같은 글자 안에서도 위/아래 정점이 서로 다른 밴드에
    // 걸려 다르게 밀리고 — 그래서 글자가 "옮겨가는" 게 아니라 "중간이 찢어지는" 것처럼 보인다.
    // 매 스텝 ForceMeshUpdate로 먼저 깨끗한 레이아웃으로 되돌린 뒤에만 오프셋을 다시 적용한다
    // (안 그러면 오프셋이 누적돼 글자가 화면 밖으로 날아간다).
    void ApplyScanlineGlitch(TextMeshProUGUI tmp, float seed)
    {
        tmp.ForceMeshUpdate();
        var textInfo = tmp.textInfo;
        bool changed = false;
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            var ci = textInfo.characterInfo[i];
            if (!ci.isVisible) continue;

            var verts = textInfo.meshInfo[ci.materialReferenceIndex].vertices;
            int vi = ci.vertexIndex;
            for (int k = 0; k < 4; k++)
            {
                float lineId = Mathf.Floor(verts[vi + k].y / GlitchBandHeight);
                float active = Hash(lineId, seed) > GlitchBandActiveThreshold ? 1f : 0f;
                if (active <= 0f) continue;
                float jitter = (Hash(lineId, seed + 91.7f) - 0.5f) * GlitchBandJitterX;
                verts[vi + k].x += jitter;
                changed = true;
            }
        }

        if (changed)
        {
            for (int m = 0; m < textInfo.meshInfo.Length; m++)
            {
                textInfo.meshInfo[m].mesh.vertices = textInfo.meshInfo[m].vertices;
                tmp.UpdateGeometry(textInfo.meshInfo[m].mesh, m);
            }
        }
    }

    // ScreenGlitch.shader / UINoiseOverlay.shader와 완전히 같은 해시 공식(CPU 이식본).
    static float Hash(float x, float y)
    {
        float s = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
        return s - Mathf.Floor(s);
    }

    void ResetTextGlitch()
    {
        if (_glitchTexts == null) return;
        foreach (var tmp in _glitchTexts)
            if (tmp != null) tmp.ForceMeshUpdate(); // 정점 오프셋을 지우고 원본 레이아웃으로 복구
    }

    void Click(ButtonResult result) => _lastResult = result;

    TextMeshProUGUI CreateLabel(string name, string text, float fontSize, Color color, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_menuRoot.transform, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = labelSize;
        rt.anchoredPosition = pos;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) tmp.font = _font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.characterSpacing = 6f;
        return tmp;
    }

    DeathMenuButtonHover CreateButton(string name, string label, Vector2 pos, System.Action onClick)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_menuRoot.transform, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = buttonSize;
        rt.anchoredPosition = pos;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) tmp.font = _font;
        tmp.text = label;
        tmp.fontSize = buttonFontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = WhiteTint;
        tmp.outlineWidth = 0f;

        var button = go.AddComponent<Button>();
        button.targetGraphic = tmp;
        button.transition = Selectable.Transition.None; // 색·아웃라인은 DeathMenuButtonHover가 직접 관리
        button.onClick.AddListener(() => onClick());

        var hover = go.AddComponent<DeathMenuButtonHover>();
        hover.Init(tmp, WhiteTint, RedTint);
        return hover;
    }

    // 씬에 EventSystem이 없으면(포인터 이벤트가 아예 안 들어옴) 새 Input System용으로 하나 만든다.
    static void EnsureEventSystem()
    {
        // EventSystem.current는 "현재 활성" 포인터라 ExecuteAlways 리빌드·도메인 리로드 타이밍에 따라
        // 아직 null일 수 있다(실측: 중복 EventSystem 4개 생성됨) — 존재 자체를 직접 찾는 편이 안전하다.
        if (FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    void DoFadeBlack(float target, float duration)
    {
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeBlackRoutine(target, duration));
    }

    System.Collections.IEnumerator FadeBlackRoutine(float target, float duration)
    {
        float from = _bg.color.a;
        float t = 0f;
        duration = Mathf.Max(0.0001f, duration);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(from, target, Mathf.Clamp01(t / duration));
            _bg.color = new Color(0f, 0f, 0f, a);
            yield return null;
        }
        _bg.color = new Color(0f, 0f, 0f, target);
        _fadeCo = null;
    }

    void DoShowMenuFaded(float duration)
    {
        _lastResult = ButtonResult.None;
        foreach (var h in _hoverButtons) h.ResetVisual(); // 직전 사망의 호버 상태가 남아있지 않게
        ResetTextGlitch(); // 직전 사망의 글리치 정점 오프셋이 남아있지 않게
        _menuGroup.alpha = 0f;
        _menuRoot.SetActive(true);
        if (_menuFadeCo != null) StopCoroutine(_menuFadeCo);
        _menuFadeCo = StartCoroutine(MenuFadeRoutine(1f, duration));
    }

    System.Collections.IEnumerator MenuFadeRoutine(float target, float duration)
    {
        float from = _menuGroup.alpha;
        float t = 0f;
        duration = Mathf.Max(0.0001f, duration);
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _menuGroup.alpha = Mathf.Lerp(from, target, Mathf.Clamp01(t / duration));
            yield return null;
        }
        _menuGroup.alpha = target;
        _menuFadeCo = null;
    }

    void DoHideMenu()
    {
        if (_menuFadeCo != null) { StopCoroutine(_menuFadeCo); _menuFadeCo = null; }
        _menuRoot.SetActive(false);
    }

    void OnDestroy()
    {
        if (_bgMat != null) { if (Application.isPlaying) Destroy(_bgMat); else DestroyImmediate(_bgMat); }
        if (_instance == this) _instance = null;
    }
}

// Reconnect/Exit 버튼의 커서 포커스(호버) 연출 — 기본 흰색, 호버 시 붉은색 + 흰색 아웃라인.
// Button의 기본 Transition(ColorTint 등)은 아웃라인까지 다루지 못해 별도 컴포넌트로 직접 처리한다.
public class DeathMenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    TextMeshProUGUI _text;
    Color _normalColor;
    Color _hoverColor;

    public void Init(TextMeshProUGUI text, Color normalColor, Color hoverColor)
    {
        _text = text;
        _normalColor = normalColor;
        _hoverColor = hoverColor;
        Apply(false);
    }

    /// <summary>메뉴를 다시 켤 때 호출 — 직전에 호버된 채로 남아있었어도 기본 상태로 되돌린다.</summary>
    public void ResetVisual() => Apply(false);

    public void OnPointerEnter(PointerEventData eventData) => Apply(true);
    public void OnPointerExit(PointerEventData eventData) => Apply(false);

    void Apply(bool hovered)
    {
        if (_text == null) return;
        _text.color = hovered ? _hoverColor : _normalColor;
        _text.outlineWidth = hovered ? 0.2f : 0f;
        _text.outlineColor = Color.white;
    }
}
