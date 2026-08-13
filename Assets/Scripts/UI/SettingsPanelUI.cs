using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 설정창 — 사운드(마스터 · BGM · SFX) 탭과 조작(키 바인딩) 탭.
/// 일시정지 메뉴와 타이틀 화면이 같은 인스턴스를 공유한다(<see cref="Open"/>).
///
/// DeathScreenUI와 같은 런타임 절차 생성 패턴이다 — 프리팹·씬 의존이 0이라 어느 씬에서 열어도 된다.
/// Screen Space - Overlay를 쓰는 이유도 같다: 카메라 렌더 패스가 전부 끝난 뒤 별도 합성 단계에서
/// 그려지므로 카메라·조명·렌더러 피처가 무엇을 하든 확실히 맨 위에 뜬다.
///
/// 시간은 전부 unscaledDeltaTime 계열이다 — 일시정지(timeScale 0) 중에 떠 있는 UI이기 때문.
/// </summary>
public class SettingsPanelUI : MonoBehaviour
{
    const int SortingOrder = 5200; // DeathScreenUI(5100)보다 위 — Overlay끼리는 sortingOrder로만 겨룬다
    const string FontResourceName = "Silver Bitmap"; // DeathScreenUI와 같은 도트 폰트
    const string ActionMapName = "Player";

    static readonly Color WhiteTint = new Color(0.92f, 0.92f, 0.92f);
    static readonly Color DimTint = new Color(0.45f, 0.45f, 0.45f);
    static readonly Color RedTint = new Color(0.85f, 0.12f, 0.14f);
    static readonly Color PanelTint = new Color(0.04f, 0.04f, 0.05f, 0.96f);
    static readonly Color BackdropTint = new Color(0f, 0f, 0f, 0.75f);
    static readonly Color TrackTint = new Color(0.22f, 0.22f, 0.24f);

    /// <summary>조작 탭에서 라벨로 쓸 액션 이름(.inputactions의 액션명 → 한글).</summary>
    static readonly Dictionary<string, string> ActionLabels = new Dictionary<string, string>
    {
        { "Move", "이동" }, { "Jump", "점프" }, { "Dash", "대시" },
        { "Attack", "공격" }, { "Parry", "패링" }, { "Charge", "일섬 차지" },
    };

    /// <summary>Move 컴포짓의 부분 이름 → 한글.</summary>
    static readonly Dictionary<string, string> PartLabels = new Dictionary<string, string>
    {
        { "up", "위" }, { "down", "아래" }, { "left", "왼쪽" }, { "right", "오른쪽" },
    };

    static SettingsPanelUI _instance;

    public static bool IsOpen => _instance != null && _instance._root != null && _instance._root.activeSelf;

    /// <summary>설정창이 닫힌 프레임 번호. ESC로 창을 닫으면 그 ESC는 같은 프레임에 아직 눌린 상태라,
    /// 이 창이 닫히자마자 일시정지 메뉴·타이틀이 같은 입력을 또 집어간다(실측 아님 — 코드 경로상 확실).
    /// 그쪽에서 이 프레임이면 ESC를 무시하게 한다.</summary>
    public static int LastCloseFrame { get; private set; } = -1;

    GameObject _root;
    GameObject _soundTab;
    GameObject _keyTab;
    TMP_FontAsset _font;
    TextMeshProUGUI _soundTabLabel;
    TextMeshProUGUI _keyTabLabel;
    System.Action _onClose;

    // 사운드 탭 — 슬라이더가 스스로 값을 되읽지 않고 여기서 퍼센트 표시를 갱신한다
    readonly List<(Slider slider, TextMeshProUGUI value)> _volumeRows = new List<(Slider, TextMeshProUGUI)>();

    // 조작 탭 — 각 행이 자기 바인딩을 다시 그릴 수 있게 갱신 델리게이트를 들고 있는다
    readonly List<System.Action> _keyRowRefresh = new List<System.Action>();

    // 리바인딩 대기 상태. 액션은 InputSystem의 대화형 리바인딩이, 직접 폴링 키는 아래 Update가 처리한다.
    InputActionRebindingExtensions.RebindingOperation _rebindOp;
    bool _waitingRawKey;
    RawKey _waitingRawId;
    TextMeshProUGUI _waitingRawText; // 겹쳤을 때 붉게 만들 행
    System.Action _waitingRawDone;

    // 겹침 경고 — 이미 쓰이는 키를 고르면 저장하지 않고 되돌린 뒤 이 팝업을 띄운다.
    GameObject _conflictPopup;
    TextMeshProUGUI _conflictMessage;
    TextMeshProUGUI _conflictRow;    // 붉게 표시 중인 행(다시 그려도 색이 유지되게 기억한다)

    /// <summary>설정창을 연다. <paramref name="onClose"/>는 닫히는 순간 한 번 불린다(일시정지 메뉴 복귀용).</summary>
    public static void Open(System.Action onClose = null)
    {
        var ui = GetOrCreate();
        ui._onClose = onClose;
        ui._conflictRow = null;   // 지난번 겹침 경고가 붉은 채로 남아 있지 않게
        ui.HideConflict();
        ui.Refresh();
        ui._root.SetActive(true);
    }

    public static void Close()
    {
        if (_instance != null) _instance.DoClose();
    }

    static SettingsPanelUI GetOrCreate()
    {
        if (_instance != null) return _instance;
        return new GameObject("SettingsPanelUI").AddComponent<SettingsPanelUI>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject); // 타이틀에서 연 창이 씬 전환에 휩쓸리지 않게
        _font = Resources.Load<TMP_FontAsset>(FontResourceName);
        Build();
        _root.SetActive(false);
    }

    void OnDestroy()
    {
        CancelRebind();
        if (_instance == this) _instance = null;
    }

    void Update()
    {
        if (_root == null || !_root.activeSelf) return;

        // 겹침 팝업이 떠 있는 동안엔 ESC가 팝업만 닫는다(창까지 닫히면 경고를 읽기도 전에 사라진다).
        if (_conflictPopup != null && _conflictPopup.activeSelf)
        {
            if (KeyBinds.PressedRaw(Key.Escape)) HideConflict();
            return;
        }

        if (_waitingRawKey)
        {
            PollRawKeyRebind();
            return; // 키를 받는 동안엔 ESC가 창을 닫지 않는다(취소 전용)
        }

        // 리바인딩 중이면 ESC는 InputSystem의 취소 경로가 가져간다(WithCancelingThrough).
        if (_rebindOp == null && KeyBinds.PressedRaw(Key.Escape)) DoClose();
    }

    void DoClose()
    {
        CancelRebind();
        // 슬라이더를 끄는 동안엔 PlayerPrefs에 쓰기만 했다 — 닫을 때 한 번 디스크로 내린다.
        GameAudio.Flush();
        KeyBinds.Flush();
        LastCloseFrame = Time.frameCount;
        _root.SetActive(false);
        var cb = _onClose;
        _onClose = null;
        if (cb != null) cb();
    }

    // ── 빌드 ────────────────────────────────────────────────────────────────────────────────

    void Build()
    {
        EnsureEventSystem();

        _root = new GameObject("SettingsCanvas");
        _root.transform.SetParent(transform, false);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        _root.AddComponent<GraphicRaycaster>();

        // 뒤 게임 화면으로 클릭이 새지 않게 전체를 덮는다
        var backdrop = NewImage("Backdrop", _root.transform, BackdropTint);
        backdrop.rectTransform.anchorMin = Vector2.zero;
        backdrop.rectTransform.anchorMax = Vector2.one;
        backdrop.rectTransform.offsetMin = Vector2.zero;
        backdrop.rectTransform.offsetMax = Vector2.zero;

        var panel = NewImage("Panel", _root.transform, PanelTint);
        Place(panel.rectTransform, new Vector2(1280f, 860f), Vector2.zero);

        MakeLabel("Title", panel.transform, "SETTINGS", 72f, WhiteTint,
            new Vector2(600f, 90f), new Vector2(0f, 350f));

        _soundTabLabel = MakeButton("SoundTab", panel.transform, "사운드", 46f,
            new Vector2(300f, 70f), new Vector2(-170f, 258f), () => ShowTab(true));
        _keyTabLabel = MakeButton("KeyTab", panel.transform, "조작", 46f,
            new Vector2(300f, 70f), new Vector2(170f, 258f), () => ShowTab(false));

        _soundTab = new GameObject("SoundTab", typeof(RectTransform));
        Place((RectTransform)_soundTab.transform, new Vector2(1240f, 480f), new Vector2(0f, -20f), panel.transform);
        _keyTab = new GameObject("KeyTab", typeof(RectTransform));
        Place((RectTransform)_keyTab.transform, new Vector2(1240f, 480f), new Vector2(0f, -20f), panel.transform);

        BuildSoundTab();
        BuildKeyTab();

        MakeButton("Defaults", panel.transform, "기본값으로", 40f,
            new Vector2(360f, 70f), new Vector2(-200f, -360f), ResetAll);
        MakeButton("CloseButton", panel.transform, "닫기", 40f,
            new Vector2(360f, 70f), new Vector2(200f, -360f), DoClose);

        BuildConflictPopup(_root.transform); // 패널이 아니라 캔버스 바로 아래 — 탭·버튼까지 통째로 덮는다

        ShowTab(true);
    }

    void BuildConflictPopup(Transform parent)
    {
        _conflictPopup = new GameObject("ConflictPopup", typeof(RectTransform));
        var root = (RectTransform)_conflictPopup.transform;
        root.SetParent(parent, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        // 팝업이 떠 있는 동안 뒤쪽 버튼이 눌리지 않게 전체를 덮는다(raycastTarget이 켜진 Image).
        var block = NewImage("Block", _conflictPopup.transform, new Color(0f, 0f, 0f, 0.6f));
        block.rectTransform.anchorMin = Vector2.zero;
        block.rectTransform.anchorMax = Vector2.one;
        block.rectTransform.offsetMin = Vector2.zero;
        block.rectTransform.offsetMax = Vector2.zero;

        var box = NewImage("Box", _conflictPopup.transform, PanelTint);
        Place(box.rectTransform, new Vector2(860f, 340f), Vector2.zero);

        _conflictMessage = MakeLabel("Message", box.transform, "", 38f, RedTint,
            new Vector2(800f, 180f), new Vector2(0f, 40f));
        MakeButton("ConflictOk", box.transform, "확인", 40f,
            new Vector2(240f, 70f), new Vector2(0f, -110f), HideConflict);

        _conflictPopup.SetActive(false);
    }

    void BuildSoundTab()
    {
        string[] labels = { "마스터", "배경음 (BGM)", "효과음 (SFX)" };
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i; // 클로저가 루프 변수를 잡지 않게 복사
            float y = 140f - i * 130f;

            MakeLabel("Label" + i, _soundTab.transform, labels[i], 42f, WhiteTint,
                new Vector2(400f, 60f), new Vector2(-380f, y), TextAlignmentOptions.Left);

            Slider slider = MakeSlider("Slider" + i, _soundTab.transform, new Vector2(560f, 30f), new Vector2(120f, y));
            var valueText = MakeLabel("Value" + i, _soundTab.transform, "100%", 38f, DimTint,
                new Vector2(160f, 60f), new Vector2(500f, y), TextAlignmentOptions.Right);

            slider.onValueChanged.AddListener(v =>
            {
                if (index == 0) GameAudio.Master = v;
                else if (index == 1) GameAudio.Bgm = v;
                else GameAudio.Sfx = v;
                valueText.text = Mathf.RoundToInt(v * 100f) + "%";
            });

            _volumeRows.Add((slider, valueText));
        }
    }

    /// <summary>
    /// 조작 탭. 행은 두 갈래에서 온다 — .inputactions의 바인딩(컴포짓 헤더는 제외)과
    /// 액션이 없어 직접 폴링하는 <see cref="RawKey"/> 4개. 14행이라 2열로 나눠 담는다.
    /// </summary>
    void BuildKeyTab()
    {
        var rows = new List<(string label, System.Action<TextMeshProUGUI> bind, System.Func<string> read)>();

        InputActionAsset asset = KeyBinds.Actions;
        InputActionMap map = asset != null ? asset.FindActionMap(ActionMapName) : null;
        if (map != null)
        {
            foreach (InputAction action in map.actions)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (action.bindings[i].isComposite) continue; // "Dpad" 같은 헤더는 키가 아니다

                    int bindingIndex = i;
                    InputAction captured = action;
                    rows.Add((
                        BindingLabel(action, action.bindings[i]),
                        text => StartActionRebind(captured, bindingIndex, text),
                        () => ReadableBinding(captured, bindingIndex)));
                }
            }
        }
        else
        {
            Debug.LogWarning($"[SettingsPanelUI] \"{ActionMapName}\" 액션 맵을 찾지 못했다 — 액션 리바인딩 행이 비어 있다.");
        }

        foreach (RawKey id in System.Enum.GetValues(typeof(RawKey)))
        {
            RawKey captured = id;
            rows.Add((
                KeyBinds.Label(captured),
                text => StartRawRebind(captured, text),
                () => ReadableKey(KeyBinds.Get(captured))));
        }

        int perColumn = Mathf.CeilToInt(rows.Count / 2f);
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            float x = i < perColumn ? -310f : 310f;
            float y = 190f - (i % perColumn) * 58f;

            MakeLabel("KeyLabel" + i, _keyTab.transform, row.label, 34f, WhiteTint,
                new Vector2(280f, 52f), new Vector2(x - 150f, y), TextAlignmentOptions.Left);

            TextMeshProUGUI keyText = null;
            keyText = MakeButton("KeyButton" + i, _keyTab.transform, "", 34f,
                new Vector2(240f, 52f), new Vector2(x + 150f, y), () => row.bind(keyText));

            var read = row.read;
            var target = keyText;
            _keyRowRefresh.Add(() =>
            {
                target.text = read();
                target.color = target == _conflictRow ? RedTint : WhiteTint; // 겹친 행만 붉게 남는다
            });
        }
    }

    // ── 표시 ────────────────────────────────────────────────────────────────────────────────

    void ShowTab(bool sound)
    {
        CancelRebind();
        _soundTab.SetActive(sound);
        _keyTab.SetActive(!sound);
        _soundTabLabel.color = sound ? RedTint : DimTint;
        _keyTabLabel.color = sound ? DimTint : RedTint;
    }

    /// <summary>저장된 값을 UI에 되돌려 그린다(열 때 · 리바인딩 직후 · 기본값 복원 후).</summary>
    void Refresh()
    {
        float[] values = { GameAudio.Master, GameAudio.Bgm, GameAudio.Sfx };
        for (int i = 0; i < _volumeRows.Count && i < values.Length; i++)
        {
            // SetValueWithoutNotify로 넣어야 콜백이 돌아 같은 값을 PlayerPrefs에 되쓰지 않는다
            _volumeRows[i].slider.SetValueWithoutNotify(values[i]);
            _volumeRows[i].value.text = Mathf.RoundToInt(values[i] * 100f) + "%";
        }
        foreach (var refresh in _keyRowRefresh) refresh();
    }

    void ResetAll()
    {
        CancelRebind();
        HideConflict();
        _conflictRow = null;
        GameAudio.Master = 1f;
        GameAudio.Bgm = 1f;
        GameAudio.Sfx = 1f;
        KeyBinds.ResetToDefaults();
        Refresh();
    }

    static string BindingLabel(InputAction action, InputBinding binding)
    {
        string label = ActionLabels.TryGetValue(action.name, out string korean) ? korean : action.name;
        if (!binding.isPartOfComposite) return label;
        string part = binding.name != null ? binding.name.ToLowerInvariant() : "";
        return PartLabels.TryGetValue(part, out string partKorean) ? label + " " + partKorean : label;
    }

    // 표기는 KeyBinds가 전담한다 — 튜토리얼 대사가 같은 함수를 쓰므로 설정창과 문구가 갈라지지 않는다.
    static string ReadableBinding(InputAction action, int bindingIndex) => KeyBinds.Readable(action, bindingIndex);

    static string ReadableKey(Key key) => KeyBinds.Readable(key);

    // ── 리바인딩 ────────────────────────────────────────────────────────────────────────────

    /// <summary>.inputactions 액션의 바인딩 하나를 대화형으로 다시 잡는다.</summary>
    void StartActionRebind(InputAction action, int bindingIndex, TextMeshProUGUI text)
    {
        CancelRebind();
        if (text != null) text.text = "...";

        // 대화형 리바인딩은 액션이 꺼져 있어야 한다. 원래 켜져 있었을 때만 되돌려 켠다 —
        // 일시정지 중에는 PauseMenuUI가 맵을 통째로 꺼 두므로 여기서 함부로 켜면 입력이 샌다.
        bool wasEnabled = action.enabled;
        // 겹치면 되돌려야 하므로 지금 걸려 있는 오버라이드를 기억해 둔다(null이면 에셋 기본값 상태).
        string previousOverride = action.bindings[bindingIndex].overridePath;
        action.Disable();

        _rebindOp = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(op =>
            {
                op.Dispose();
                _rebindOp = null;
                if (wasEnabled) action.Enable();

                string owner = FindConflict(KeyBinds.Readable(action, bindingIndex), action, bindingIndex, null);
                if (owner != null)
                {
                    // 저장하지 않고 원래 바인딩으로 되돌린다 — 겹친 채로 굳어버리면 조작이 깨진다.
                    if (previousOverride == null) action.RemoveBindingOverride(bindingIndex);
                    else action.ApplyBindingOverride(bindingIndex, previousOverride);
                    RejectConflict(text, owner);
                    return;
                }

                KeyBinds.SaveActionOverrides();
                _conflictRow = null;
                Refresh();
            })
            .OnCancel(op =>
            {
                op.Dispose();
                _rebindOp = null;
                if (wasEnabled) action.Enable();
                Refresh();
            })
            .Start();
    }

    /// <summary>직접 폴링 키를 다시 잡는다 — 액션이 아니라서 InputSystem의 리바인딩 경로를 못 쓴다.</summary>
    void StartRawRebind(RawKey id, TextMeshProUGUI text)
    {
        CancelRebind();
        if (text != null) text.text = "...";
        _waitingRawKey = true;
        _waitingRawId = id;
        _waitingRawText = text;
        _waitingRawDone = Refresh;
    }

    void PollRawKeyRebind()
    {
        if (!TryReadAnyKey(out Key pressed)) return;

        _waitingRawKey = false;
        _waitingRawDone = null;

        if (pressed == Key.Escape) { Refresh(); return; } // ESC는 취소

        // 액션 쪽과 달리 여기선 **적용 전에** 검사할 수 있다 — 되돌릴 일 자체를 만들지 않는다.
        string owner = FindConflict(KeyBinds.Readable(pressed), null, -1, _waitingRawId);
        if (owner != null) { RejectConflict(_waitingRawText, owner); return; }

        KeyBinds.Set(_waitingRawId, pressed);
        _conflictRow = null;
        Refresh();
    }

    // ── 겹침 검사 ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 같은 키가 이미 다른 동작에 물려 있으면 그 동작 이름을 돌려준다(없으면 null).
    ///
    /// 비교는 **화면 표시 이름**으로 한다 — 플레이어가 "겹친다"고 느끼는 기준이 그것이고,
    /// &lt;Keyboard&gt;/shift와 &lt;Keyboard&gt;/leftShift처럼 경로가 달라도 같은 키인 경우까지 잡힌다.
    ///
    /// ⚠️ 패링 ↔ 일섬 차지는 예외로 둔다. 이 둘은 기본값부터 같은 우클릭을 나눠 쓰도록 설계돼 있고
    ///    (짧게 = 패링, 길게 = 차지) 겹침으로 막아 버리면 기본 조작으로 되돌릴 수가 없다.
    /// </summary>
    static string FindConflict(string display, InputAction selfAction, int selfBindingIndex, RawKey? selfRaw)
    {
        if (string.IsNullOrEmpty(display) || display == "-") return null;

        InputActionAsset asset = KeyBinds.Actions;
        InputActionMap map = asset != null ? asset.FindActionMap(ActionMapName) : null;
        if (map != null)
            foreach (InputAction other in map.actions)
            {
                if (selfAction != null && IsTapHoldPair(selfAction.name, other.name)) continue;
                for (int i = 0; i < other.bindings.Count; i++)
                {
                    if (other.bindings[i].isComposite) continue;
                    if (selfAction != null && other == selfAction && i == selfBindingIndex) continue;
                    if (KeyBinds.Readable(other, i) == display) return BindingLabel(other, other.bindings[i]);
                }
            }

        foreach (RawKey id in System.Enum.GetValues(typeof(RawKey)))
        {
            if (selfRaw.HasValue && selfRaw.Value == id) continue;
            if (KeyBinds.Display(id) == display) return KeyBinds.Label(id);
        }
        return null;
    }

    static bool IsTapHoldPair(string a, string b) =>
        (a == "Parry" && b == "Charge") || (a == "Charge" && b == "Parry");

    /// <summary>겹친 입력을 물리고 사용자에게 알린다 — 해당 행은 붉게 남고, 저장은 하지 않는다.</summary>
    void RejectConflict(TextMeshProUGUI row, string ownerLabel)
    {
        _conflictRow = row;
        Refresh();  // 되돌린 바인딩으로 다시 그린다(붉은 행만 그대로 남는다)
        ShowConflict("이미 \"" + ownerLabel + "\"에 할당된 키입니다.\n다른 키로 다시 설정하세요.");
    }

    void ShowConflict(string message)
    {
        if (_conflictPopup == null) return;
        if (_conflictMessage != null) _conflictMessage.text = message;
        _conflictPopup.transform.SetAsLastSibling(); // 항상 패널 위에
        _conflictPopup.SetActive(true);
    }

    void HideConflict()
    {
        if (_conflictPopup != null) _conflictPopup.SetActive(false);
    }

    // ⚠️ Keyboard.current를 보지 않는다 — PlayTest가 남긴 가상 키보드가 current를 가로채면 실제 입력이
    //    통째로 무시된다(KeyBinds.PressedRaw 주석 참고). allKeys를 훑으면 어느 키가 눌렸는지도 알 수 있다.
    static bool TryReadAnyKey(out Key key)
    {
        var devices = InputSystem.devices;
        for (int d = 0; d < devices.Count; d++)
        {
            if (!(devices[d] is Keyboard kb)) continue;
            foreach (var control in kb.allKeys)
                if (control.wasPressedThisFrame) { key = control.keyCode; return true; }
        }
        key = Key.None;
        return false;
    }

    void CancelRebind()
    {
        if (_rebindOp != null) { _rebindOp.Cancel(); _rebindOp.Dispose(); _rebindOp = null; }
        _waitingRawKey = false;
        _waitingRawText = null;
        _waitingRawDone = null;
        foreach (var refresh in _keyRowRefresh) refresh(); // "..." 로 남은 행을 원래 표시로 되돌린다
    }

    // ── 생성 헬퍼 ───────────────────────────────────────────────────────────────────────────

    static void Place(RectTransform rt, Vector2 size, Vector2 pos, Transform parent = null)
    {
        if (parent != null) rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    TextMeshProUGUI MakeLabel(string name, Transform parent, string text, float fontSize, Color color,
        Vector2 size, Vector2 pos, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Place((RectTransform)go.transform, size, pos, parent);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) tmp.font = _font;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }

    TextMeshProUGUI MakeButton(string name, Transform parent, string label, float fontSize,
        Vector2 size, Vector2 pos, System.Action onClick)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Place((RectTransform)go.transform, size, pos, parent);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) tmp.font = _font;
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.color = WhiteTint;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = true;

        var button = go.AddComponent<Button>();
        button.targetGraphic = tmp;
        button.transition = Selectable.Transition.None; // 색은 SettingsButtonHover가 직접 관리
        button.onClick.AddListener(() => onClick());

        go.AddComponent<SettingsButtonHover>().Init(tmp, RedTint);
        return tmp;
    }

    /// <summary>슬라이더는 배경(트랙) · Fill · Handle 세 조각을 손으로 조립해야 한다(프리팹을 안 쓰므로).</summary>
    Slider MakeSlider(string name, Transform parent, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Place((RectTransform)go.transform, size, pos, parent);
        var slider = go.AddComponent<Slider>();

        var track = NewImage("Track", go.transform, TrackTint);
        track.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        track.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        track.rectTransform.sizeDelta = new Vector2(0f, 10f);
        track.rectTransform.anchoredPosition = Vector2.zero;

        var fillArea = new GameObject("FillArea", typeof(RectTransform));
        var fillAreaRt = (RectTransform)fillArea.transform;
        fillAreaRt.SetParent(go.transform, false);
        fillAreaRt.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRt.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRt.sizeDelta = new Vector2(0f, 10f);
        fillAreaRt.anchoredPosition = Vector2.zero;

        var fill = NewImage("Fill", fillArea.transform, RedTint);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.sizeDelta = new Vector2(0f, 0f);

        var handleArea = new GameObject("HandleArea", typeof(RectTransform));
        var handleAreaRt = (RectTransform)handleArea.transform;
        handleAreaRt.SetParent(go.transform, false);
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.sizeDelta = Vector2.zero;

        var handle = NewImage("Handle", handleArea.transform, WhiteTint);
        handle.rectTransform.sizeDelta = new Vector2(18f, 34f);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.transition = Selectable.Transition.None;
        return slider;
    }

    // 씬에 EventSystem이 없으면(포인터 이벤트가 아예 안 들어옴) 새 Input System용으로 하나 만든다.
    // DeathScreenUI.EnsureEventSystem과 같은 이유 — EventSystem.current는 타이밍에 따라 null일 수 있어
    // 존재 자체를 직접 찾는다.
    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }
}

/// <summary>설정창 버튼의 호버 연출(기본 흰색 → 호버 시 붉은색). DeathMenuButtonHover와 같은 역할이지만
/// 탭 버튼은 선택 상태에 따라 색이 바뀌므로 "원래 색"을 매번 진입 시점에 다시 집는다.</summary>
public class SettingsButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    TextMeshProUGUI _text;
    Color _hoverColor;
    Color _normalColor;
    bool _hovering;

    public void Init(TextMeshProUGUI text, Color hoverColor)
    {
        _text = text;
        _hoverColor = hoverColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_text == null || _hovering) return;
        _hovering = true;
        _normalColor = _text.color;
        _text.color = _hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_text == null || !_hovering) return;
        _hovering = false;
        _text.color = _normalColor;
    }
}
