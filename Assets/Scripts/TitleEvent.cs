using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement; // 씬 전환을 위해 추가

public class TitleEvent : MonoBehaviour
{
    [Header("Team Logo UI")]
    [SerializeField] Image _teamPanel;
    [SerializeField] Image _teamIcon;
    [SerializeField] Text _titleText;

    [Header("Press Any Key UI")]
    [SerializeField] Text _pressAnyKeyText; // "Press Any Key" 안내 텍스트

    [Header("BGM")]
    [SerializeField] AudioSource _bgm; // 암전과 같은 속도로 같이 잦아든다 (비워두면 무시)

    Coroutine _titleCo = null;
    Coroutine _anyKeyCo = null;
    bool _canAnyKey = false;
    Text _settingsHint = null; // "ESC 설정" 안내 — 씬을 고치지 않으려고 Press Any Key 텍스트를 복제해 만든다

    void Start()
    {
        _canAnyKey = false;

        // 시작할 때 Press Any Key 텍스트는 꺼둡니다.
        if (_pressAnyKeyText != null)
            _pressAnyKeyText.gameObject.SetActive(false);

        CreateSettingsHint();

        _titleCo = StartCoroutine(Co_TitleEvent());
    }

    // 원본을 복제하면 폰트·색·아웃라인 등 씬에서 잡아둔 스타일을 그대로 물려받는다
    // (같은 캔버스 아래에 들어가므로 별도 배선도 필요 없다).
    void CreateSettingsHint()
    {
        if (_pressAnyKeyText == null) return;

        _settingsHint = Instantiate(_pressAnyKeyText, _pressAnyKeyText.transform.parent);
        _settingsHint.name = "SettingsHint";
        _settingsHint.text = "ESC : 설정";
        _settingsHint.fontSize = Mathf.Max(8, Mathf.RoundToInt(_pressAnyKeyText.fontSize * 0.7f));
        _settingsHint.rectTransform.anchoredPosition =
            _pressAnyKeyText.rectTransform.anchoredPosition + new Vector2(0f, -70f);
        _settingsHint.gameObject.SetActive(false);
    }

    void Update()
    {
        // 설정창이 떠 있는 동안엔 어떤 입력도 씬 전환으로 이어지면 안 된다 — 슬라이더를 끄는 클릭까지
        // "아무 키"로 잡혀 게임이 시작돼버린다.
        if (SettingsPanelUI.IsOpen) return;
        // 설정창을 ESC로 닫은 그 프레임 — 그 ESC가 "아무 키"로 잡혀 게임이 시작되는 것을 막는다.
        if (Time.frameCount == SettingsPanelUI.LastCloseFrame) return;

        // ESC는 "아무 키"에서 빼내 설정창 전용으로 쓴다(anyKey보다 먼저 보고 빠져나가야 한다).
        if (_canAnyKey && _anyKeyCo == null && KeyBinds.PressedRaw(Key.Escape))
        {
            SettingsPanelUI.Open();
            return;
        }

        bool anyInput = (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            || (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame
                || Mouse.current.rightButton.wasPressedThisFrame
                || Mouse.current.middleButton.wasPressedThisFrame));

        // 모든 로고 연출이 끝난 후(_canAnyKey == true) 키/마우스 입력을 감지합니다.
        if (_canAnyKey && _anyKeyCo == null && anyInput)
        {
            _canAnyKey = false; // 중복 입력 방지
            _anyKeyCo = StartCoroutine(Co_AnyKeyEvent());
        }
    }

    // --- 1단계: 팀 로고 연출 ---
    IEnumerator Co_TitleEvent()
    {
        Color iconColor = _teamIcon.color;
        Color textColor = _titleText.color;
        Color panelColor = _teamPanel.color;

        float alphaIndex = 0f;

        // 1-1. 팀 로고 페이드 인
        while (alphaIndex < 1f)
        {
            alphaIndex += Time.unscaledDeltaTime;
            iconColor.a = alphaIndex;
            textColor.a = alphaIndex;
            _teamIcon.color = iconColor;
            _titleText.color = textColor;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.5f);

        // 1-2. 팀 로고 페이드 아웃
        while (alphaIndex > 0f)
        {
            alphaIndex -= Time.unscaledDeltaTime;
            iconColor.a = alphaIndex;
            textColor.a = alphaIndex;
            _teamIcon.color = iconColor;
            _titleText.color = textColor;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(0.3f);

        // 1-3. 검은 배경 패널 페이드 아웃 (화면이 밝아짐)
        alphaIndex = 1f;
        while (alphaIndex > 0f)
        {
            alphaIndex -= Time.unscaledDeltaTime;
            panelColor.a = alphaIndex;
            _teamPanel.color = panelColor;
            yield return null;
        }

        _titleCo = null;

        // 1-4. "Press Any Key" 텍스트 켜고 입력 대기 상태로 전환
        if (_pressAnyKeyText != null)
            _pressAnyKeyText.gameObject.SetActive(true);
        if (_settingsHint != null)
            _settingsHint.gameObject.SetActive(true);

        _canAnyKey = true; // 이제부터 키 입력 가능!
    }

    // --- 2단계: 아무 키나 눌렀을 때 (화면 암전 및 씬 전환) ---
    IEnumerator Co_AnyKeyEvent()
    {
        // Press Any Key 텍스트 숨기기
        if (_pressAnyKeyText != null)
            _pressAnyKeyText.gameObject.SetActive(false);
        if (_settingsHint != null)
            _settingsHint.gameObject.SetActive(false);

        Color panelColor = _teamPanel.color;
        float al = 0f;

        // BGM도 같은 루프에서 같이 줄인다 — 별도 코루틴을 돌리면 암전과 미묘하게 어긋난다.
        float bgmVolume = _bgm != null ? _bgm.volume : 0f;

        // 화면을 다시 검은색으로 페이드 인 (0 -> 1)
        while (al < 1f)
        {
            al += Time.unscaledDeltaTime;
            panelColor.a = al;
            _teamPanel.color = panelColor;
            if (_bgm != null) _bgm.volume = bgmVolume * (1f - al);
            yield return null;
        }

        panelColor.a = 1f;
        _teamPanel.color = panelColor;
        if (_bgm != null) _bgm.volume = 0f;

        _anyKeyCo = null;

        // 3단계: 화면이 완전히 검게 변했으므로 IntroScene으로 이동!
        // (IntroScene은 검은 화면에서 시작해 페이드 아웃하므로 여기서 넘어가는 순간이 이어진다)
        SceneManager.LoadScene("IntroScene");
    }
}
