using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// 전역 볼륨 설정. <c>Resources/GameAudioMixer.mixer</c>의 노출 파라미터(MasterVolume · BgmVolume ·
/// SfxVolume)를 만져 소리를 조절하고, 값은 PlayerPrefs에 남긴다 — 설정은 세이브 슬롯과 무관해야 하므로
/// GameDataManager 쪽 저장 경로를 쓰지 않는다.
///
/// ⚠️ AudioSource.volume에 배율을 곱하는 방식을 쓰지 않는 이유:
///    기존 코드는 volume에 쓰기만 하는 게 아니라 **되읽어서 기준값으로 삼는다**
///    (BossEyeTracker의 bgmVolumeBeforeDuck, TitleEvent의 bgmVolume, BossStageDirector의 sceneBgmVolume).
///    배율을 곱해 두면 그 값을 원본으로 착각해 더킹 복원 때마다 볼륨이 한 단계씩 영구히 낮아진다.
///    믹서 그룹 볼륨은 AudioSource.volume과 독립적으로 곱해지므로 그 코드들을 한 줄도 건드릴 필요가 없다.
/// </summary>
public static class GameAudio
{
    const string MixerResourcePath = "GameAudioMixer";
    const string MasterParam = "MasterVolume";
    const string BgmParam = "BgmVolume";
    const string SfxParam = "SfxVolume";
    const string PrefMaster = "Audio.Master";
    const string PrefBgm = "Audio.Bgm";
    const string PrefSfx = "Audio.Sfx";

    /// <summary>이 아래로는 -80dB(무음)로 떨어뜨린다. log10(0)이 -무한대라 0은 따로 막아야 한다.</summary>
    const float SilenceThreshold = 0.001f;

    static AudioMixer mixer;
    static AudioMixerGroup bgmGroup;
    static AudioMixerGroup sfxGroup;
    static bool loaded;
    static float master = 1f;
    static float bgm = 1f;
    static float sfx = 1f;

    public static float Master
    {
        get { Ensure(); return master; }
        set { Ensure(); master = Mathf.Clamp01(value); PlayerPrefs.SetFloat(PrefMaster, master); ApplyOne(MasterParam, master); }
    }

    public static float Bgm
    {
        get { Ensure(); return bgm; }
        set { Ensure(); bgm = Mathf.Clamp01(value); PlayerPrefs.SetFloat(PrefBgm, bgm); ApplyOne(BgmParam, bgm); }
    }

    public static float Sfx
    {
        get { Ensure(); return sfx; }
        set { Ensure(); sfx = Mathf.Clamp01(value); PlayerPrefs.SetFloat(PrefSfx, sfx); ApplyOne(SfxParam, sfx); }
    }

    /// <summary>GameSfx가 만드는 AudioSource가 붙을 그룹. 두 Bootstrap의 실행 순서는 보장되지 않으므로
    /// 지연 로드로 답한다 — 그룹 없이 태어난 소스는 아래 라우팅이 BGM으로 오인해 집어간다.</summary>
    public static AudioMixerGroup SfxGroup { get { Ensure(); return sfxGroup; } }

    /// <summary>설정창을 닫을 때 한 번 부른다. 슬라이더를 끄는 동안엔 SetFloat만 하고 디스크 쓰기는 미룬다.</summary>
    public static void Flush() => PlayerPrefs.Save();

    // 도메인 리로드를 끄면 static이 Play를 나가도 살아남는다(GameSfx.ResetStatics와 같은 이유).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        mixer = null;
        bgmGroup = null;
        sfxGroup = null;
        loaded = false;
        master = 1f;
        bgm = 1f;
        sfx = 1f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        Ensure();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        RouteUnassignedSources(); // 첫 씬은 sceneLoaded가 이미 지나갔다 — 여기서 한 번 훑는다
    }

    static void Ensure()
    {
        if (loaded) return;
        loaded = true;

        mixer = Resources.Load<AudioMixer>(MixerResourcePath);
        if (mixer == null)
        {
            Debug.LogWarning($"[GameAudio] Resources/{MixerResourcePath}.mixer 를 찾지 못했다 — 볼륨 설정이 먹지 않는다.");
            return;
        }

        AudioMixerGroup[] found = mixer.FindMatchingGroups("BGM");
        if (found.Length > 0) bgmGroup = found[0];
        found = mixer.FindMatchingGroups("SFX");
        if (found.Length > 0) sfxGroup = found[0];

        master = PlayerPrefs.GetFloat(PrefMaster, 1f);
        bgm = PlayerPrefs.GetFloat(PrefBgm, 1f);
        sfx = PlayerPrefs.GetFloat(PrefSfx, 1f);
        ApplyOne(MasterParam, master);
        ApplyOne(BgmParam, bgm);
        ApplyOne(SfxParam, sfx);
    }

    static void ApplyOne(string param, float linear)
    {
        // 에디트 모드에서 SetFloat을 하면 그 값이 .mixer 에셋에 눌러앉는다 — 실제 소리가 나는 건
        // 플레이 중뿐이므로 그때만 쓴다(UISandbox에서 패널을 미리보기할 때 에셋이 더러워지는 것을 막는다).
        if (mixer == null || !Application.isPlaying) return;
        mixer.SetFloat(param, ToDecibels(linear));
    }

    /// <summary>선형 0~1 슬라이더 값을 믹서가 쓰는 dB로 바꾼다(0.5 → -6dB, 0.1 → -20dB).</summary>
    static float ToDecibels(float linear) => linear <= SilenceThreshold ? -80f : Mathf.Log10(linear) * 20f;

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RouteUnassignedSources();

    /// <summary>
    /// 씬에 박혀 있는 BGM AudioSource는 믹서 그룹이 비어 있다 — 씬 파일을 고치는 대신 런타임에 붙인다.
    /// GameSfx의 소스는 태어날 때 스스로 SFX 그룹을 들고 나오므로 여기 걸리지 않는다.
    /// </summary>
    static void RouteUnassignedSources()
    {
        Ensure();
        if (bgmGroup == null) return;

        AudioSource[] sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++)
            if (sources[i].outputAudioMixerGroup == null) sources[i].outputAudioMixerGroup = bgmGroup;
    }
}
