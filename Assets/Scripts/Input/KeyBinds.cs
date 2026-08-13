using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerActions.inputactions에 액션이 없어 예전부터 키보드를 직접 훑어 온 동작들
/// (처형 R · 광원 방출 E · 시간 가속 LeftAlt · 상호작용 F — PlayerController의 폴링 주석 참고).
/// </summary>
public enum RawKey
{
    Execution,   // 처형 발동
    LightSpend,  // 광원 방출(홀드)
    TimeAccel,   // 시간 가속 토글
    Interact,    // 대사 진행 · 상호작용
}

/// <summary>
/// 키 설정의 단일 창구. 갈라져 있는 두 갈래 입력을 한곳에서 저장·복원한다.
///  · PlayerActions의 6개 액션(Move · Jump · Dash · Attack · Parry · Charge) → 바인딩 오버라이드 JSON
///  · 액션이 없어 직접 폴링하는 4개 키(<see cref="RawKey"/>) → Key 값
/// 둘 다 PlayerPrefs에 남긴다 — 설정은 세이브 슬롯과 무관해야 하므로 GameDataManager를 쓰지 않는다
/// (GameAudio와 같은 원칙).
///
/// 바인딩 오버라이드는 .inputactions 에셋 파일을 건드리지 않는다
/// (실측 2026-08-13: 오버라이드 적용 후 EditorUtility.IsDirty=false, 파일 크기 변화 0바이트)
/// — 그래서 에디터에서 리바인딩을 해봐도 에셋에 흔적이 남지 않는다.
/// </summary>
public static class KeyBinds
{
    /// <summary>Assets/Resources/PlayerActions.inputactions — PlayerInput이 없는 씬(타이틀 · 인트로)에서도
    /// 액션을 얻으려고 Resources에 둔다.</summary>
    public const string ActionsResourcePath = "PlayerActions";

    const string PrefOverrides = "Keys.Overrides";
    const string PrefRawPrefix = "Keys.Raw.";

    static readonly Key[] DefaultKeys = { Key.R, Key.E, Key.LeftAlt, Key.F };
    static readonly string[] RawLabels = { "처형", "광원 방출", "시간 가속", "상호작용" };

    static Key[] rawKeys;
    static InputActionAsset actions;
    static bool loaded;

    /// <summary>
    /// 플레이어가 실제로 쓰는 액션 에셋 인스턴스. PlayerInput은 **다른 PlayerInput이 같은 에셋을 쓸 때만**
    /// 복제하므로(PlayerInput.InitializeActions), 단일 플레이어인 이 게임에선 Resources로 얻은 것과
    /// 같은 인스턴스다 — 여기서 건 오버라이드가 플레이어에게 그대로 간다.
    /// </summary>
    public static InputActionAsset Actions { get { Ensure(); return actions; } }

    /// <summary>일시정지 중 게임플레이 입력을 막는다(PauseMenuUI가 켠다). 액션 쪽은 액션 맵을 통째로
    /// 끄는 것으로 막히고, 여기 직접 폴링 키는 이 스위치로 막는다.
    /// ⚠️ <see cref="PressedRaw"/>·<see cref="HeldRaw"/>는 일부러 막지 않는다 — 설정창의 ESC처럼
    /// 잠긴 동안에도 살아 있어야 하는 UI 입력이 그 경로를 쓴다.</summary>
    public static bool InputLocked;

    public static string Label(RawKey id) => RawLabels[(int)id];
    public static Key DefaultOf(RawKey id) => DefaultKeys[(int)id];
    public static Key Get(RawKey id) { Ensure(); return rawKeys[(int)id]; }

    public static void Set(RawKey id, Key key)
    {
        Ensure();
        rawKeys[(int)id] = key;
        PlayerPrefs.SetInt(PrefRawPrefix + (int)id, (int)key);
    }

    public static bool Pressed(RawKey id) => !InputLocked && PressedRaw(Get(id));
    public static bool Held(RawKey id) => !InputLocked && HeldRaw(Get(id));

    // ⚠️ Keyboard.current를 보면 안 된다. current는 "가장 최근에 입력이 들어온 키보드"라, 테스트용
    //    가상 키보드(InputInjector.AddDevice)가 붙어 있으면 그쪽을 가리켜 실제 키보드 입력이 통째로
    //    무시된다(실측: PlayTest가 남긴 가상 키보드 3개가 장치 목록에 살아 있었다, 2026-08-01).
    //    연결된 모든 키보드를 훑으면 어느 장치에서 왔든 입력이 잡힌다.
    public static bool PressedRaw(Key key)
    {
        if (key == Key.None) return false;
        var devices = InputSystem.devices;
        for (int i = 0; i < devices.Count; i++)
            if (devices[i] is Keyboard kb && kb[key].wasPressedThisFrame) return true;
        return false;
    }

    public static bool HeldRaw(Key key)
    {
        if (key == Key.None) return false;
        var devices = InputSystem.devices;
        for (int i = 0; i < devices.Count; i++)
            if (devices[i] is Keyboard kb && kb[key].isPressed) return true;
        return false;
    }

    // ── 표기 ────────────────────────────────────────────────────────────────────────────────
    // 설정창의 키 목록과 튜토리얼 대사가 **같은 말**을 쓰게 하려고 표기를 여기 한곳에 모은다.

    /// <summary>바인딩 경로를 화면에 그대로 넣을 수 있는 표기로 바꾼다.
    /// 마우스 버튼만은 InputSystem의 "Left Button"이 아니라 이 게임의 문구가 쓰던 한국어 표기로 돌려준다.</summary>
    public static string Readable(string effectivePath)
    {
        if (string.IsNullOrEmpty(effectivePath)) return "-";
        if (effectivePath == "<Mouse>/leftButton") return "좌클릭";
        if (effectivePath == "<Mouse>/rightButton") return "우클릭";
        if (effectivePath == "<Mouse>/middleButton") return "휠클릭";
        return Shorten(InputControlPath.ToHumanReadableString(
            effectivePath, InputControlPath.HumanReadableStringOptions.OmitDevice));
    }

    public static string Readable(Key key) => key == Key.None ? "-" : Shorten(key.ToString());

    /// <summary>수식 키는 문구에 그대로 넣기엔 길다("LeftAlt키로 속도를…") — 흔히 쓰는 짧은 이름으로 줄인다.
    /// Key.ToString()("LeftAlt")과 InputControlPath 표기("Left Alt")가 서로 달라 둘 다 받는다.</summary>
    static string Shorten(string name)
    {
        // 숫자키는 두 표기가 갈린다 — 경로("<Keyboard>/1")는 "1", Key.ToString()은 "Digit1".
        // 겹침 검사가 이 둘을 같은 키로 봐야 하므로 여기서 하나로 모은다.
        if (name.Length == 6 && name.StartsWith("Digit")) return name.Substring(5);

        switch (name)
        {
            case "LeftAlt": case "RightAlt": case "Left Alt": case "Right Alt": return "Alt";
            case "LeftShift": case "RightShift": case "Left Shift": case "Right Shift": return "Shift";
            case "LeftCtrl": case "RightCtrl": case "Left Control": case "Right Control": return "Ctrl";
            case "LeftCommand": case "RightCommand": return "Cmd";
            default: return name;
        }
    }

    public static string Readable(InputAction action, int bindingIndex) =>
        action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count
            ? "-" : Readable(action.bindings[bindingIndex].effectivePath);

    /// <summary>액션 이름(필요하면 컴포짓 부분 이름까지)으로 현재 바인딩 표기를 얻는다.
    /// 못 찾으면 액션 이름을 그대로 돌려준다 — 문구에 빈칸이 뚫리는 것보다 낫다.</summary>
    public static string Display(string actionName, string compositePart = null)
    {
        Ensure();
        InputAction action = actions != null ? actions.FindAction(actionName) : null;
        if (action == null) return actionName;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (binding.isComposite) continue; // "Dpad" 같은 헤더는 키가 아니다
            if (compositePart != null &&
                !string.Equals(binding.name, compositePart, System.StringComparison.OrdinalIgnoreCase)) continue;
            return Readable(binding.effectivePath);
        }
        return actionName;
    }

    public static string Display(RawKey id) => Readable(Get(id));

    /// <summary>키캡 아이콘(처형 · 카운터 · 획득 프롬프트)처럼 "키보드 키 하나"를 보여줘야 하는 곳에서 쓴다.
    /// 액션에 마우스와 키보드가 같이 걸려 있으면(패링 = 우클릭 + F) 키보드 쪽을 고른다 —
    /// 키캡 그림 안에 "좌클릭"이라고 적히면 어색하기 때문. 키보드 바인딩이 없으면 첫 바인딩을 그대로 쓴다.</summary>
    public static string DisplayKeyboard(string actionName)
    {
        Ensure();
        InputAction action = actions != null ? actions.FindAction(actionName) : null;
        if (action == null) return actionName;

        string firstAny = null;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (binding.isComposite) continue;
            string path = binding.effectivePath;
            if (string.IsNullOrEmpty(path)) continue;
            if (firstAny == null) firstAny = Readable(path);
            if (path.StartsWith("<Keyboard>")) return Readable(path);
        }
        return firstAny ?? actionName;
    }

    /// <summary>액션 리바인딩이 끝난 뒤 부른다 — 지금 걸려 있는 오버라이드 전체를 JSON으로 굳힌다.</summary>
    public static void SaveActionOverrides()
    {
        Ensure();
        if (actions == null) return;
        PlayerPrefs.SetString(PrefOverrides, actions.SaveBindingOverridesAsJson());
    }

    public static void ResetToDefaults()
    {
        Ensure();
        for (int i = 0; i < rawKeys.Length; i++) Set((RawKey)i, DefaultKeys[i]);
        if (actions == null) return;
        actions.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(PrefOverrides);
    }

    /// <summary>설정창을 닫을 때 한 번 부른다(GameAudio.Flush와 같은 역할).</summary>
    public static void Flush() => PlayerPrefs.Save();

    // 도메인 리로드를 끄면 static이 Play를 나가도 살아남는다(GameSfx.ResetStatics와 같은 이유).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        rawKeys = null;
        actions = null;
        loaded = false;
        InputLocked = false; // 일시정지 중 Play를 멈추면 잠금이 켜진 채로 남는다
    }

    // 액션 오버라이드는 플레이어(PlayerInput)가 액션을 켜기 전에 걸려 있어야 한다 —
    // AfterSceneLoad는 첫 씬의 Awake/OnEnable보다 뒤지만, 오버라이드는 켜진 액션에도 즉시 반영된다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() => Ensure();

    static void Ensure()
    {
        if (loaded) return;
        loaded = true;

        rawKeys = new Key[DefaultKeys.Length];
        for (int i = 0; i < rawKeys.Length; i++)
            rawKeys[i] = (Key)PlayerPrefs.GetInt(PrefRawPrefix + i, (int)DefaultKeys[i]);

        actions = Resources.Load<InputActionAsset>(ActionsResourcePath);
        if (actions == null)
        {
            Debug.LogWarning($"[KeyBinds] Resources/{ActionsResourcePath}.inputactions 를 찾지 못했다 — 액션 리바인딩이 저장되지 않는다.");
            return;
        }

        string json = PlayerPrefs.GetString(PrefOverrides, "");
        if (!string.IsNullOrEmpty(json)) actions.LoadBindingOverridesFromJson(json);
    }
}
