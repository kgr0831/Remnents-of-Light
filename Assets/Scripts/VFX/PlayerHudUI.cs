using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 HUD — 체력 칸(갯수) + 빛 에너지 게이지
/// (기능_구현_명세서 5장 "게임 UI (HUD): 플레이어 체력바, 빛 에너지 게이지").
///
/// 체력은 수치가 아니라 <b>갯수</b>라서 바가 아니라 칸을 그린다 — 맞는 순간 "칸 하나가 꺼진다"가
/// 보여야 하므로, 잃은 칸은 즉시 사라지지 않고 줄어들며 흐려진다(일정표 1주차의 Lerp 스무딩은
/// 바 길이가 아니라 칸의 크기·투명도에 그대로 적용된다). 빛 에너지는 조금씩 차고 닳는 자원이라
/// 지금처럼 연속 게이지로 둔다.
///
/// DodgeUI/ExecutionUI와 같은 자가완결 방식 — 씬에 활성 Overlay Canvas가 없으면 런타임에 만든다.
/// 다만 저 둘은 프리팹(Resources/Prefabs/*)을 쓰는 반면 HUD용 아트 에셋은 아직 없어서, 칸과 바를
/// 단색 Image로 절차적으로 만든다(프리팹·텍스처 의존 0 → 씬/에셋 변경 없이 코드만으로 동작).
/// </summary>
public class PlayerHudUI : MonoBehaviour
{
    [Header("Layout (CanvasScaler 1920x1080 기준 px, 좌상단 원점)")]
    public Vector2 origin = new Vector2(56f, -52f);
    // 칸 5개 줄(5*64 + 4*12 = 368px)이 아래 에너지 게이지(460px)와 비슷한 폭이 되게 잡았다 —
    // 처음 46px로 만들었더니 줄 전체가 262px이라 게이지보다 한참 작아 주 자원처럼 안 보였다.
    public Vector2 pipSize = new Vector2(64f, 64f);
    public float pipGap = 12f;
    public float pipInset = 8f;          // 칸 테두리와 안쪽 채움 사이 여백
    public Vector2 energyBarSize = new Vector2(460f, 20f);
    public float barGap = 14f;
    public float border = 3f;

    [Header("Lerp 스무딩")]
    public float pipLerpSpeed = 8f;      // 잃은/얻은 칸이 사라지고 나타나는 속도
    public float pipLossDelay = 0.18f;   // 맞은 직후 칸이 잠깐 그대로 남아 있는 시간
    public float energyLerpSpeed = 10f;
    [Range(0f, 1f)] public float pipMinScale = 0.35f; // 꺼지는 칸이 줄어드는 최소 크기
    // 원격/비포커스 에디터는 프레임이 길게 튀는데(수백 ms), 그대로 쓰면 한 프레임에 목표까지
    // 도달해 "스무딩"이 사라진다. UI 보간에서 흔한 방어로 dt에 상한을 둔다.
    public float maxSmoothDelta = 0.05f;

    [Header("색")]
    public Color pipColor = new Color(0.95f, 0.30f, 0.34f);
    public Color pipEmptyColor = new Color(0.16f, 0.09f, 0.11f, 0.9f);
    // 광원바 색(2026-08-02 사용자 지시: "픽셀 이팩트 폭주=붉은, 초월=흰, 둘다 아니면 흰"과 통일):
    // 평상시·초월 둘 다 흰색이라 energyColor/transcendColor가 사실상 같은 값이지만, 상태별로
    // 독립 튜닝할 수 있도록 필드는 그대로 분리해 둔다(폭주만 rampageColor로 붉게 갈린다).
    public Color energyColor = Color.white;
    public Color energyLowColor = new Color(0.95f, 0.25f, 0.22f); // A-2/C-3: 에너지 부족 경고색
    public float energyColorLerpSpeed = 20f; // 정상↔경고색 전환 속도(대략 0.15s)
    public Color transcendColor = Color.white; // 초월 중(2026-08-02 지시로 청록→흰색)
    public float transcendColorLerpSpeed = 20f; // 저에너지 경고와 같은 속도(대략 0.15s)
    public Color rampageColor = new Color(0.95f, 0.20f, 0.18f); // 폭주 중(2026-08-02 신규 지시)
    public float rampageColorLerpSpeed = 20f; // 위 두 전환과 같은 속도

    [Header("광원 변경치 표시 (격투게임 바)")]
    // 격투게임 체력바의 "칩 데미지" 관습 그대로 — 줄어든 만큼이 잠깐 그 자리에 남았다가(고스트)
    // 서서히 따라 내려오고, 늘어난 만큼은 밝은 예고 구간으로 먼저 보인 뒤 채움이 그 안으로 자란다.
    // 둘 다 채움(EnergyFill) "뒤"에 깔기만 하면 되므로 좌표 계산이 필요 없다 —
    // 더 긴 쪽이 채움 밖으로 삐져나온 부분만 보이고, 감소/증가는 동시에 일어나지 않는다.
    // 2026-08-02 사용자 지시: 채움 자체가 이제 상태별로 흰색/붉은색(위 energyColor·transcendColor·
    // rampageColor)으로 바뀌는데, 기존 변화량 색이 그중 하나와 겹쳐 안 보였다 — 얻은 구간(0.88,1,1)은
    // 흰색 채움(평상시·초월)에, 잃은 구간(0.95,0.35,0.30)은 붉은 채움(폭주)에 각각 파묻힘. 두 채움
    // 색 다 채도·명도가 뚜렷이 갈리는 색으로 교체해 항상(세 상태 전부) 대비가 유지되게 했다.
    public Color energyLossColor = new Color(0.20f, 0.16f, 0.38f, 0.95f);  // 잃은 구간(칩) — 짙은 남보라
    public Color energyGainColor = new Color(1f, 0.82f, 0.20f, 0.95f);    // 얻은 구간(예고) — 밝은 금색
    public float energyLossDelay = 0.25f;            // 줄어든 직후 고스트가 그대로 멈춰 있는 시간
    public float energyLossDrainPerSecond = 0.55f;   // 그 뒤 고스트가 따라 내려오는 속도(바 비율/초)
    public float energyGainHold = 0.15f;             // 얻은 구간이 목표에 머무는 시간
    public float energyGainFadePerSecond = 1.2f;     // 그 뒤 채움에 흡수되는 속도(바 비율/초)

    public Color backColor = new Color(0.05f, 0.05f, 0.07f, 0.85f);
    public Color borderColor = new Color(0.82f, 0.87f, 0.95f, 0.55f);

    // 자아 바를 따로 그리지 않고(사용자 지시 2026-08-01: "더이상 자아 게이지가 바로 표시되지 않고"),
    // HP 칸 자체가 자아 상태를 대신 표시한다 — 전부 폭주 중에만 켜진다(자아는 폭주 중에만 의미 있는 값).
    //   · 자아가 줄어드는 만큼 칸 위에 회색이 위→아래로 차올라 자아 0에서 완전한 회색이 된다(칸마다
    //     PipEgoGray 오버레이, Image.Filled/Vertical/origin=Top의 fillAmount = 1-자아비율). 처음엔
    //     깜빡임(알파 점멸)이었는데 "깜빡이는 대신 Fill Amount로"라는 사용자 지시(2026-08-02)로 교체.
    //   · 자아가 0이 되면 화면 전체에 글리치(ScreenGlitchFx)가 걸린다
    //   · 자아 0 상태에서 도는 5초 붕괴 타이머(PlayerController.EgoDepletedProgress) 동안, 마지막 칸이
    //     원래 "칸이 꺼질 때" 쓰는 축소+페이드 연출(_pipDisplay 기반, ApplyPips 참고) 그대로 5초에 걸쳐
    //     천천히 재생된다(사용자 지시 2026-08-02: "기존 사라지는 이펙트를 재활용해서 5초짜리로"). 새 셰이더나
    //     별도 오버레이 없이 Update()에서 그 칸의 _pipDisplay 값을 붕괴 진행률로 직접 덮어쓰기만 하면 된다.
    //   · 자아가 다시 차면(0이 아니게 되면) 두 효과 전부 즉시 사라진다
    [Header("자아 고갈 연출 (HP 칸에 표시 — 폭주 중에만)")]
    public Color pipDepletedColor = new Color(0.55f, 0.56f, 0.60f); // 자아가 줄어들며 차오르는 회색(오버레이 색)

    static PlayerHudUI _instance;
    public static PlayerHudUI Instance => _instance;

    PlayerController _player;
    GameObject _root, _pipRow;
    RectTransform _energyFill, _energyLoss, _energyGain;
    Image[] _pipFills;
    Image[] _pipEgoOverlays; // 자아가 줄어드는 만큼 위→아래로 차오르는 회색(칸마다 1개)
    float[] _pipDisplay;
    float _lossHoldTimer, _energyDisplay, _findTimer;
    float _energyColorLerp, _energyFlashTimer, _transcendColorLerp, _rampageColorLerp;
    Image _energyFillImage;
    int _builtPipCount, _prevLit = -1;
    // 광원 변경치(격투게임 바) 상태
    float _energyGhost, _energyGainDisplay, _energyLossTimer, _energyGainTimer, _prevEnergyTarget = -1f;

    // 테스트(PlayTestRunner)에서 "실제 수치"가 아니라 "화면에 그려지는 값"을 검증하기 위한 판독구.
    public int PipCount => _pipDisplay != null ? _pipDisplay.Length : 0;
    public float PipDisplay(int index) =>
        _pipDisplay != null && index >= 0 && index < _pipDisplay.Length ? _pipDisplay[index] : 0f;
    /// <summary>화면에 켜져 있는 것으로 보이는 칸 수(꺼지는 중인 칸은 세지 않는다).</summary>
    public int LitPipCount
    {
        get
        {
            int n = 0;
            for (int i = 0; _pipDisplay != null && i < _pipDisplay.Length; i++)
                if (_pipDisplay[i] > 0.95f) n++;
            return n;
        }
    }
    /// <summary>칸 표시값의 합 / 칸 수 — 칸 단위 표시에도 "부드럽게 줄었는가"를 볼 수 있는 집계값.</summary>
    public float HpDisplayRatio
    {
        get
        {
            if (_pipDisplay == null || _pipDisplay.Length == 0) return 0f;
            float sum = 0f;
            for (int i = 0; i < _pipDisplay.Length; i++) sum += _pipDisplay[i];
            return sum / _pipDisplay.Length;
        }
    }
    public float EnergyDisplayRatio => _energyDisplay;
    /// <summary>감소분 고스트(칩) 바의 현재 비율. 채움보다 길면 그 차이가 "방금 잃은 양"이다.</summary>
    public float EnergyGhostRatio => _energyGhost;
    /// <summary>증가분 예고 바의 현재 비율. 채움보다 길면 그 차이가 "방금 얻은 양"이다.</summary>
    public float EnergyGainRatio => _energyGainDisplay;
    public bool HasPlayer => _player != null;

    /// <summary>에너지 바를 잠깐 붉게 점멸시킨다(일섬 게이팅 실패 등 1회성 경고 피드백).</summary>
    public void FlashEnergyBarRed(float duration = 0.24f) => _energyFlashTimer = duration;

    bool _hidden; // SetVisible(false)로 강제 숨김 중(사망 연출) — Update의 자동 재활성화를 막는다

    /// <summary>사망 연출 등에서 HUD를 강제로 숨기거나 되돌린다(_player 존재 여부와 별개 스위치).</summary>
    public void SetVisible(bool visible)
    {
        _hidden = !visible;
        if (_root != null) _root.SetActive(visible && _player != null);
    }

    // 씬에 배치하지 않아도 항상 뜨게 한다(씬 편집 없이 HUD가 붙는 유일한 방법).
    // 플레이어가 없는 씬(VfxSandbox 등)에서는 아래 Update가 HUD를 숨긴다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate() { GetOrCreate(); }

    public static PlayerHudUI GetOrCreate()
    {
        if (_instance != null) return _instance;
        return new GameObject("PlayerHudUI").AddComponent<PlayerHudUI>(); // Awake가 _instance를 세운다
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        _player = FindFirstObjectByType<PlayerController>();
        Build();
        SnapToPlayer();
    }

    void Build()
    {
        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) canvas = CreateOverlayCanvas();

        _root = new GameObject("PlayerHud", typeof(RectTransform));
        var rt = (RectTransform)_root.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = origin;
        rt.sizeDelta = Vector2.zero;
        // 회피/처형 프롬프트가 HUD 위에 그려지도록 맨 뒤로 보낸다(같은 캔버스를 공유할 수 있다).
        rt.SetAsFirstSibling();

        BuildPipRow(_player != null ? _player.maxHealth : 5);
        BuildEnergyBar();
    }

    /// <summary>체력 칸 줄을 만든다. 최대 칸 수가 바뀌면(세이브 로드 등) 통째로 다시 만든다.</summary>
    void BuildPipRow(int count)
    {
        count = Mathf.Max(1, count);
        if (_pipRow != null) Destroy(_pipRow);

        _pipRow = new GameObject("HealthPips", typeof(RectTransform));
        var rowRt = (RectTransform)_pipRow.transform;
        rowRt.SetParent(_root.transform, false);
        rowRt.anchorMin = rowRt.anchorMax = rowRt.pivot = new Vector2(0f, 1f);
        rowRt.anchoredPosition = Vector2.zero;
        rowRt.sizeDelta = Vector2.zero;

        _pipFills = new Image[count];
        _pipEgoOverlays = new Image[count];
        var previous = _pipDisplay;
        _pipDisplay = new float[count];

        for (int i = 0; i < count; i++)
        {
            float x = i * (pipSize.x + pipGap);
            AddImage(_pipRow.transform, "PipBorder" + i, pipSize + Vector2.one * (border * 2f),
                     new Vector2(x - border, border), borderColor);
            AddImage(_pipRow.transform, "PipEmpty" + i, pipSize, new Vector2(x, 0f), pipEmptyColor);

            // 채움만 중앙 피봇 — 칸이 꺼질 때 가운데를 기준으로 줄어들어야 자연스럽다.
            var fill = AddImage(_pipRow.transform, "PipFill" + i, pipSize - Vector2.one * (pipInset * 2f),
                                Vector2.zero, pipColor);
            fill.pivot = new Vector2(0.5f, 0.5f);
            fill.anchoredPosition = new Vector2(x + pipSize.x * 0.5f, -pipSize.y * 0.5f);
            _pipFills[i] = fill.GetComponent<Image>();

            // 자아 고갈 회색 오버레이 — 채움 바로 위, 같은 자리·같은 크기. 자아가 줄어드는 만큼
            // 위(origin=Top)에서부터 fillAmount만큼 회색이 차오른다. ⚠️ Unity Image는 sprite가 없으면
            // Type=Filled를 통째로 무시하고 항상 꽉 찬 사각형만 그린다(fillAmount는 정상 저장되지만
            // 실제 메시엔 반영 안 됨 — HP 칸 드레인 연출에서 실측으로 확인한 UGUI의 잘 알려진 함정,
            // 2026-08-02). 그래서 WhiteSprite()로 스프라이트를 반드시 물린다.
            var egoOverlay = AddImage(_pipRow.transform, "PipEgoGray" + i, pipSize - Vector2.one * (pipInset * 2f),
                                      Vector2.zero, pipDepletedColor);
            egoOverlay.pivot = new Vector2(0.5f, 0.5f);
            egoOverlay.anchoredPosition = new Vector2(x + pipSize.x * 0.5f, -pipSize.y * 0.5f);
            _pipEgoOverlays[i] = egoOverlay.GetComponent<Image>();
            _pipEgoOverlays[i].sprite = WhiteSprite();
            _pipEgoOverlays[i].type = Image.Type.Filled;
            _pipEgoOverlays[i].fillMethod = Image.FillMethod.Vertical;
            _pipEgoOverlays[i].fillOrigin = (int)Image.OriginVertical.Top;
            _pipEgoOverlays[i].fillAmount = 0f;

            // 칸 수만 바뀌었을 땐 남아 있던 표시값을 이어받아 화면이 튀지 않게 한다.
            _pipDisplay[i] = previous != null && i < previous.Length ? previous[i] : 0f;
        }

        _builtPipCount = count;
    }

    void BuildEnergyBar()
    {
        float top = -(pipSize.y + barGap);
        AddImage(_root.transform, "EnergyBorder", energyBarSize + Vector2.one * (border * 2f),
                 new Vector2(-border, top + border), borderColor);
        AddImage(_root.transform, "EnergyBack", energyBarSize, new Vector2(0f, top), backColor);
        // 순서가 곧 레이어다 — 고스트/획득은 채움보다 먼저 넣어 "뒤"에 깔린다.
        _energyLoss = AddImage(_root.transform, "EnergyLoss", energyBarSize, new Vector2(0f, top), energyLossColor);
        _energyGain = AddImage(_root.transform, "EnergyGain", energyBarSize, new Vector2(0f, top), energyGainColor);
        _energyFill = AddImage(_root.transform, "EnergyFill", energyBarSize, new Vector2(0f, top), energyColor);
        _energyFillImage = _energyFill.GetComponent<Image>();
    }

    RectTransform AddImage(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        // 피봇을 좌상단에 두면 sizeDelta.x만 줄여도 바가 왼쪽 기준으로 줄어든다
        // (Image.type=Filled는 스프라이트가 필요해서, 스프라이트 없는 단색 바에는 폭 조절이 더 단순하다).
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    Canvas FindOverlayCanvas()
    {
        // 비활성 캔버스는 재사용하지 않는다(그 밑에 붙으면 같이 숨는다) — DodgeUI와 같은 규칙.
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
        var go = new GameObject("PlayerHudCanvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    void Update()
    {
        // 히트스톱(timeScale=0)·회피 슬로우모션 중에도 게이지는 정상 속도로 움직여야 하므로 unscaled.
        float dt = Mathf.Min(Time.unscaledDeltaTime, maxSmoothDelta);

        if (_player == null)
        {
            _findTimer -= dt;
            if (_findTimer <= 0f)
            {
                _findTimer = 0.5f; // 매 프레임 씬 탐색은 비싸다
                _player = FindFirstObjectByType<PlayerController>();
                if (_player != null) SnapToPlayer();
            }
        }

        bool shouldShow = _player != null && !_hidden;
        if (_root != null && _root.activeSelf != shouldShow) _root.SetActive(shouldShow);
        if (_player == null) return;

        // 최대 칸 수는 세이브 불러오기로도 바뀔 수 있다 → 바뀌면 줄을 다시 만든다.
        if (_player.maxHealth != _builtPipCount) BuildPipRow(_player.maxHealth);

        int lit = Mathf.Clamp(_player.currentHealth, 0, _pipDisplay.Length);

        // 칸을 잃은 "그 순간"에만 지연을 건다(꺼지는 중인 상태를 조건으로 삼으면 지연이 영원히 갱신된다).
        if (_prevLit >= 0 && lit < _prevLit) _lossHoldTimer = pipLossDelay;
        _prevLit = lit;
        if (_lossHoldTimer > 0f) _lossHoldTimer -= dt;

        for (int i = 0; i < _pipDisplay.Length; i++)
        {
            float target = i < lit ? 1f : 0f;
            // 회복(꺼진 칸이 켜지는 것)은 지연 없이 바로 차오른다.
            if (target < _pipDisplay[i] && _lossHoldTimer > 0f) continue;
            _pipDisplay[i] = Smooth(_pipDisplay[i], target, pipLerpSpeed, dt);
        }

        bool rampaging = _player.IsRampaging;
        bool transcending = _player.IsTranscending; // T-4: 폭주 재스케일 장치를 반대로 쓴다
        // 폭주 중엔 "회복치 100"을 만들어야 한다(사용자 지시 2026-08-02) — 실제로 필요한 회복량은
        // maxEnergy가 아니라 RampageExitEnergy(25%)뿐이라, 그 값을 분모로 써서 바가 처음부터 끝까지
        // 꽉 차는 것처럼 보이게 한다. 그대로 두면 폭주 내내 바가 최대 25%까지만 차 회복이 안 되는
        // 것처럼 보인다(사용자 스크린샷 2026-08-02).
        // 초월 중엔 반대로 "남은 초월 시간"이 100%에서 0%로 완전히 빠지는 것으로 재스케일한다
        // (TranscendExitEnergy=70을 바닥으로 삼아, 100→70 드레인이 바 전체를 쓴다).
        float energyTarget = rampaging
            ? Ratio(_player.currentEnergy, _player.RampageExitEnergy)
            : transcending
                ? Ratio(_player.currentEnergy - _player.TranscendExitEnergy, _player.maxEnergy - _player.TranscendExitEnergy)
                : Ratio(_player.currentEnergy, _player.maxEnergy);
        _energyDisplay = Smooth(_energyDisplay, energyTarget, energyLerpSpeed, dt);
        UpdateEnergyDelta(energyTarget, dt);

        // 저에너지 경고(10% 이하) + 1회성 점멸(A-2 일섬 게이팅 실패 피드백)이 같은 색 전환 장치를 공유한다.
        // 폭주·초월 중엔 위 재스케일과 뜻이 달라지므로(폭주는 항상 낮게, 초월은 항상 높게 나옴)
        // 경고색 전환은 평상시에만 건다(폭주 중 끄던 것과 같은 이유 — PlayerHudUI.cs:324 선례).
        if (_energyFlashTimer > 0f) _energyFlashTimer -= dt;
        bool lowEnergy = !rampaging && !transcending && _player.maxEnergy > 0 && _player.currentEnergy <= _player.maxEnergy * 0.1f;
        _energyColorLerp = Smooth(_energyColorLerp, (lowEnergy || _energyFlashTimer > 0f) ? 1f : 0f, energyColorLerpSpeed, dt);
        _transcendColorLerp = Smooth(_transcendColorLerp, transcending ? 1f : 0f, transcendColorLerpSpeed, dt);
        _rampageColorLerp = Smooth(_rampageColorLerp, rampaging ? 1f : 0f, rampageColorLerpSpeed, dt);
        if (_energyFillImage != null)
        {
            Color blended = Color.Lerp(Color.Lerp(energyColor, energyLowColor, _energyColorLerp), transcendColor, _transcendColorLerp);
            _energyFillImage.color = Color.Lerp(blended, rampageColor, _rampageColorLerp);
        }

        // 자아 상태 → HP 칸 연출(자아는 폭주 중에만 의미 있는 값이라 전부 폭주 게이트를 공유한다).
        bool egoDepleted = rampaging && _player.currentEgo <= 0;
        float egoRatio = rampaging ? Ratio(_player.currentEgo, _player.maxEgo) : 1f;
        // 자아가 줄어드는 만큼 칸 위에 회색이 위→아래로 차오른다(사용자 지시 2026-08-02: 깜빡임 대신
        // Fill Amount로). egoRatio가 폭주 아닐 때 1로 고정되므로 별도 게이트 없이도 0이 나온다.
        float grayFill = Mathf.Clamp01(1f - egoRatio);

        // 자아 0 붕괴 — 마지막 칸의 표시값을 5초 붕괴 진행률로 직접 덮어써서, 원래 "칸이 꺼질 때" 쓰는
        // 축소+페이드 연출(ApplyPips가 _pipDisplay로 그리는 그 연출)을 5초짜리로 늘려 재생한다(사용자
        // 지시 2026-08-02: "기존 사라지는 이펙트를 재활용해서 5초짜리로"). 이 칸은 아직 살아 있어 위
        // 스무딩 루프가 target=1로 계속 끌어올리려 하므로, 그 다음에 값을 덮어써야 한다 — 새 칸이 드레인
        // 대상이 될 때도 그 칸은 원래 1이었으므로(1 - 진행률≈0 = 1) 이어서 자연스럽고, 틱이 나간 직후
        // 칸은 이 덮어쓰기에서 빠지고 원래 스무딩(_pipDisplay가 이미 0에 가까움)으로 넘어가 꽉 찬 채로
        // 되돌아가는 깜빡임도 없다.
        // ⚠️ 여기에 Smooth()를 한 번 얹었다가 되돌렸다(사용자 피드백 2026-08-02) — EgoDepletedProgress는
        // PlayerController가 실시간(uncapped) deltaTime으로 이미 선형으로 채운 값인데, Smooth()를 쓰면
        // 이 파일의 dt가 maxSmoothDelta(0.05s)에 상한 걸려 있어(위 주석 참고) 실제 프레임 간격이 그보다
        // 크면 목표를 못 따라잡고 계속 뒤처지다가 틱이 나갈 때 "덜 줄어든 채로 갑자기 사라지는" 것처럼
        // 보였다(실측: progress 0.87일 때 표시값이 0.71로 남음). 원본 값 자체가 이미 매끈한 선형이라
        // 그대로 대입하면 충분하다.
        if (egoDepleted && lit > 0) _pipDisplay[lit - 1] = 1f - _player.EgoDepletedProgress;

        ApplyPips(grayFill);
        ApplyBar(_energyLoss, energyBarSize, _energyGhost);
        ApplyBar(_energyGain, energyBarSize, _energyGainDisplay);
        ApplyBar(_energyFill, energyBarSize, _energyDisplay);
    }

    // 광원이 바뀐 "그 순간"을 잡아 고스트(감소)·예고(증가) 구간을 굴린다.
    // 폭주 드레인·광원 소모처럼 매 프레임 조금씩 깎이는 경로에서도 지연이 계속 갱신돼
    // 고스트가 실제 값보다 한 박자 뒤에서 따라오게 되고, 그게 격투게임 바의 그 느낌이다.
    void UpdateEnergyDelta(float energyTarget, float dt)
    {
        if (_prevEnergyTarget < 0f) { _prevEnergyTarget = energyTarget; _energyGhost = energyTarget; _energyGainDisplay = energyTarget; }

        if (energyTarget < _prevEnergyTarget - 0.0001f)
        {
            _energyLossTimer = energyLossDelay;                       // 줄어든 자리에 잠깐 멈춰 선다
            _energyGainDisplay = Mathf.Min(_energyGainDisplay, energyTarget); // 남아 있던 예고는 즉시 접는다
        }
        else if (energyTarget > _prevEnergyTarget + 0.0001f)
        {
            _energyGainDisplay = Mathf.Max(_energyGainDisplay, energyTarget);
            _energyGainTimer = energyGainHold;
            _energyGhost = Mathf.Max(_energyGhost, energyTarget);     // 회복분 위로 고스트가 남지 않게
        }
        _prevEnergyTarget = energyTarget;

        if (_energyLossTimer > 0f) _energyLossTimer -= dt;
        else _energyGhost = Mathf.MoveTowards(_energyGhost, _energyDisplay, energyLossDrainPerSecond * dt);
        _energyGhost = Mathf.Max(_energyGhost, _energyDisplay);       // 채움보다 짧아지면 의미가 없다

        if (_energyGainTimer > 0f) _energyGainTimer -= dt;
        else _energyGainDisplay = Mathf.MoveTowards(_energyGainDisplay, _energyDisplay, energyGainFadePerSecond * dt);
        _energyGainDisplay = Mathf.Max(_energyGainDisplay, _energyDisplay);
    }

    /// <summary>보간 없이 현재 수치로 맞춘다(HUD 생성 직후 칸이 0에서 차오르지 않도록).</summary>
    public void SnapToPlayer()
    {
        if (_player == null || _pipDisplay == null) return;
        if (_player.maxHealth != _builtPipCount) BuildPipRow(_player.maxHealth);

        int lit = Mathf.Clamp(_player.currentHealth, 0, _pipDisplay.Length);
        for (int i = 0; i < _pipDisplay.Length; i++) _pipDisplay[i] = i < lit ? 1f : 0f;
        bool rampaging = _player.IsRampaging;
        bool transcending = _player.IsTranscending;
        _energyDisplay = rampaging
            ? Ratio(_player.currentEnergy, _player.RampageExitEnergy)
            : transcending
                ? Ratio(_player.currentEnergy - _player.TranscendExitEnergy, _player.maxEnergy - _player.TranscendExitEnergy)
                : Ratio(_player.currentEnergy, _player.maxEnergy);
        _energyFlashTimer = 0f;
        _energyColorLerp = (!rampaging && !transcending && _player.maxEnergy > 0 && _player.currentEnergy <= _player.maxEnergy * 0.1f) ? 1f : 0f;
        _transcendColorLerp = transcending ? 1f : 0f;
        _rampageColorLerp = rampaging ? 1f : 0f;
        if (_energyFillImage != null)
        {
            Color blended = Color.Lerp(Color.Lerp(energyColor, energyLowColor, _energyColorLerp), transcendColor, _transcendColorLerp);
            _energyFillImage.color = Color.Lerp(blended, rampageColor, _rampageColorLerp);
        }
        _lossHoldTimer = 0f;
        _prevLit = lit;

        _energyGhost = _energyGainDisplay = _prevEnergyTarget = _energyDisplay;
        _energyLossTimer = _energyGainTimer = 0f;

        ApplyPips(0f);
        ApplyBar(_energyLoss, energyBarSize, _energyGhost);
        ApplyBar(_energyGain, energyBarSize, _energyGainDisplay);
        ApplyBar(_energyFill, energyBarSize, _energyDisplay);
    }

    /// <summary>
    /// grayFill: 자아가 줄어든 만큼(0~1) 칸 위에 회색이 위→아래로 차오르는 비율 — 자아 0에서 1(칸 전체가
    /// 회색)이 된다. 칸 자체는 그저 _pipDisplay[i](0~1)를 그대로 그린다 — 자아 0 붕괴 중 마지막 칸이
    /// 5초짜리로 줄어들어 보이는 것도 Update()가 그 값을 직접 덮어쓴 결과일 뿐, 여기선 다르게 다룰 게 없다.
    /// </summary>
    void ApplyPips(float grayFill)
    {
        for (int i = 0; i < _pipFills.Length; i++)
        {
            if (_pipFills[i] == null) continue;
            float d = _pipDisplay[i];
            float scale = Mathf.Lerp(pipMinScale, 1f, d);

            _pipFills[i].rectTransform.localScale = Vector3.one * scale;
            var c = pipColor;
            c.a = pipColor.a * d;
            _pipFills[i].color = c;

            if (_pipEgoOverlays[i] == null) continue;
            // 회색 오버레이도 칸과 같은 축소+페이드를 따라간다 — 칸이 통째로 사라질 때 회색만 남아
            // 어색하게 떠 있지 않도록.
            _pipEgoOverlays[i].rectTransform.localScale = Vector3.one * scale;
            _pipEgoOverlays[i].fillAmount = grayFill;
            var gc = pipDepletedColor;
            gc.a = pipDepletedColor.a * d;
            _pipEgoOverlays[i].color = gc;
        }
    }

    void ApplyBar(RectTransform bar, Vector2 size, float ratio)
    {
        if (bar != null) bar.sizeDelta = new Vector2(size.x * Mathf.Clamp01(ratio), size.y);
    }

    static float Ratio(int current, int max) => max <= 0 ? 0f : Mathf.Clamp01((float)current / max);

    // 프레임레이트에 독립적인 지수 보간(같은 dt 총합이면 같은 결과).
    static float Smooth(float current, float target, float speed, float dt)
        => Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * dt));

    static Sprite _whiteSprite;
    // Image.Type.Filled가 실제로 동작하려면 sprite가 있어야 한다(PipEgoGray 오버레이 참고 — sprite
    // 없이는 UGUI가 Filled 자체를 무시하고 항상 꽉 찬 사각형만 그린다). 에셋 의존을 피하려고 Unity
    // 내장 흰 텍스처로 1회만 스프라이트를 만들어 재사용한다.
    static Sprite WhiteSprite()
    {
        if (_whiteSprite == null)
        {
            var tex = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return _whiteSprite;
    }
}
