using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ESC 일시정지 메뉴 — [계속하기 / 설정 / 타이틀로]. 설정은 <see cref="SettingsPanelUI"/>를 그대로 띄운다.
///
/// GameSfx·GameCursor와 같은 방식으로 첫 씬이 로드된 뒤 스스로 생겨나므로 씬마다 배선할 필요가 없다.
/// DontDestroyOnLoad라 씬이 바뀌어도 하나만 산다.
///
/// ⚠️ timeScale은 덮어쓰지 않고 **직전 값을 기억했다가 되돌린다**. 이 게임은 처형·회피 카운터·사망이
///    각자 timeScale을 쥐고 흔들기 때문에 무조건 1로 되돌리면 슬로우모션 구간이 통째로 깨진다.
///    같은 이유로 이미 timeScale이 0인 동안(사망 화면 · 튜토리얼 정지)엔 일시정지에 들어가지 않는다.
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    const int SortingOrder = 5150;  // DeathScreenUI(5100) 위, SettingsPanelUI(5200) 아래
    const string FontResourceName = "Silver Bitmap";
    const string TitleSceneName = "TitleScene";
    const string ActionMapName = "Player";

    static readonly Color WhiteTint = new Color(0.92f, 0.92f, 0.92f);
    static readonly Color RedTint = new Color(0.85f, 0.12f, 0.14f);
    static readonly Color BackdropTint = new Color(0f, 0f, 0f, 0.7f);

    static PauseMenuUI _instance;

    public static bool IsPaused => _instance != null && _instance._paused;

    /// <summary>
    /// 컷씬처럼 "지금 멈추면 안 되는" 구간에서 켠다(BossStageDirector.BeginCutsceneLock).
    ///
    /// 컷씬 연출은 timeScale 0에서도 돌아야 해서 전부 unscaledDeltaTime · WaitForSecondsRealtime으로
    /// 짜여 있다(프로젝트 전체 177곳) — 그래서 일시정지가 시간을 멈춰도 컷씬만 혼자 계속 재생된다.
    /// 그 어중간한 상태(메뉴는 떠 있는데 연출은 진행)를 만드느니 컷씬 중엔 ESC를 아예 받지 않는다.
    /// </summary>
    public static bool CutsceneLock;

    GameObject _root;
    TMP_FontAsset _font;
    PlayerController _player;
    bool _paused;
    float _timeScaleBeforePause = 1f;
    bool _actionMapWasEnabled;

    // 도메인 리로드를 끄면 static이 Play를 나가도 살아남는다(GameSfx.ResetStatics와 같은 이유).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _instance = null;
        CutsceneLock = false;
        AudioListener.pause = false; // 일시정지 중 Play를 멈추면 음소거가 다음 세션까지 남는다
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (_instance != null) return;
        new GameObject("PauseMenuUI").AddComponent<PauseMenuUI>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        _font = Resources.Load<TMP_FontAsset>(FontResourceName);
        Build();
        _root.SetActive(false);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_instance == this) _instance = null;
    }

    /// <summary>씬이 바뀌면 이전 씬의 플레이어 참조는 죽는다 — 다음 ESC 때 다시 찾게 비운다.
    /// 컷씬 도중 씬을 빠져나가면 잠금이 켜진 채 남으므로 같이 푼다(다음 씬의 연출은 Start 코루틴에서
    /// 다시 걸기 때문에 여기서 풀어도 안전하다).</summary>
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _player = null;
        CutsceneLock = false;
    }

    void Update()
    {
        // 설정창이 떠 있는 동안 ESC는 그쪽 것이다(같은 프레임에 둘 다 반응해 창이 통째로 닫히는 것을 막는다).
        if (SettingsPanelUI.IsOpen) return;
        // 설정창을 ESC로 닫은 그 프레임 — 같은 ESC로 일시정지까지 풀려버리는 것을 막는다.
        if (Time.frameCount == SettingsPanelUI.LastCloseFrame) return;

        if (!KeyBinds.PressedRaw(Key.Escape)) return; // ESC를 누른 프레임에만 조건을 따진다(매 프레임 탐색 방지)

        if (_paused) Resume();
        else if (CanPause()) Pause();
    }

    /// <summary>
    /// 플레이어가 있는 씬에서, 아직 아무도 시간을 멈추지 않았을 때만 일시정지한다.
    /// 인트로·타이틀(플레이어 없음)과 사망 화면·컷신 정지(timeScale 0)는 자동으로 제외된다.
    /// </summary>
    bool CanPause()
    {
        if (CutsceneLock) return false;
        if (Time.timeScale <= 0f) return false;
        if (_player == null) _player = FindFirstObjectByType<PlayerController>();
        return _player != null && _player.isActiveAndEnabled;
    }

    void Pause()
    {
        _paused = true;
        _timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        // ⚠️ 오디오는 timeScale과 무관하게 계속 흐른다(AudioSource는 실시간으로 재생된다) —
        //    BGM·홀드음까지 확실히 멈추려면 리스너를 직접 세워야 한다.
        AudioListener.pause = true;
        LockGameplayInput(true);
        _root.SetActive(true);
    }

    void Resume()
    {
        _paused = false;
        _root.SetActive(false);
        AudioListener.pause = false;
        LockGameplayInput(false);
        Time.timeScale = _timeScaleBeforePause;
    }

    /// <summary>
    /// 일시정지 중 입력이 게임플레이로 새지 않게 막는다. Update는 timeScale 0에서도 계속 돌기 때문에
    /// 시간만 멈춰선 부족하다 — 액션은 맵을 통째로 끄고, 직접 폴링 키는 KeyBinds의 스위치로 막는다.
    /// </summary>
    void LockGameplayInput(bool locked)
    {
        KeyBinds.InputLocked = locked;

        InputActionAsset asset = KeyBinds.Actions;
        InputActionMap map = asset != null ? asset.FindActionMap(ActionMapName) : null;
        if (map == null) return;

        if (locked)
        {
            _actionMapWasEnabled = map.enabled;
            map.Disable();
        }
        else if (_actionMapWasEnabled)
        {
            map.Enable();
        }
    }

    void OpenSettings()
    {
        _root.SetActive(false);
        SettingsPanelUI.Open(() => { if (_paused) _root.SetActive(true); }); // 설정을 닫으면 일시정지 메뉴로 복귀
    }

    void GoToTitle()
    {
        // 씬을 넘기기 전에 이 메뉴가 건드린 전역 상태를 전부 원복한다 — 안 그러면 타이틀이 멈춘 채로 뜬다.
        _paused = false;
        _root.SetActive(false);
        AudioListener.pause = false;
        LockGameplayInput(false);
        Time.timeScale = 1f;
        GameSfx.StopAllLoops(); // 홀드음(일섬 차지 · 광원 방출)이 물려 있을 수 있다

        if (!SceneTransitionTrigger.IsInBuildSettings(TitleSceneName))
        {
            Debug.LogWarning($"[PauseMenuUI] \"{TitleSceneName}\"이 Build Settings에 없어 전환을 건너뜁니다.");
            return;
        }
        SceneManager.LoadScene(TitleSceneName);
    }

    // ── 빌드 ────────────────────────────────────────────────────────────────────────────────

    void Build()
    {
        EnsureEventSystem();

        _root = new GameObject("PauseCanvas");
        _root.transform.SetParent(transform, false);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        _root.AddComponent<GraphicRaycaster>();

        var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        var backdropRt = (RectTransform)backdropGo.transform;
        backdropRt.SetParent(_root.transform, false);
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.offsetMin = Vector2.zero;
        backdropRt.offsetMax = Vector2.zero;
        backdropGo.GetComponent<Image>().color = BackdropTint; // 뒤 게임 화면으로 클릭이 새지 않게 막는다

        MakeText("Title", "PAUSED", 96f, new Vector2(900f, 130f), new Vector2(0f, 240f), null);
        MakeText("ResumeButton", "계속하기", 52f, new Vector2(500f, 90f), new Vector2(0f, 40f), Resume);
        MakeText("SettingsButton", "설정", 52f, new Vector2(500f, 90f), new Vector2(0f, -70f), OpenSettings);
        MakeText("TitleButton", "타이틀로", 52f, new Vector2(500f, 90f), new Vector2(0f, -180f), GoToTitle);
    }

    /// <summary><paramref name="onClick"/>이 null이면 클릭 없는 순수 라벨이 된다.</summary>
    TextMeshProUGUI MakeText(string name, string label, float fontSize, Vector2 size, Vector2 pos, System.Action onClick)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_root.transform, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) tmp.font = _font;
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.color = WhiteTint;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.characterSpacing = 4f;

        if (onClick == null)
        {
            tmp.raycastTarget = false;
            return tmp;
        }

        var button = go.AddComponent<Button>();
        button.targetGraphic = tmp;
        button.transition = Selectable.Transition.None; // 색은 SettingsButtonHover가 직접 관리
        button.onClick.AddListener(() => onClick());
        go.AddComponent<SettingsButtonHover>().Init(tmp, RedTint);
        return tmp;
    }

    // 씬에 EventSystem이 없으면 포인터 이벤트가 아예 안 들어온다(DeathScreenUI와 같은 이유).
    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }
}
