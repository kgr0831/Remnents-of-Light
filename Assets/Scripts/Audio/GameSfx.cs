using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전역 효과음 재생기. 첫 씬이 로드된 뒤 스스로 DontDestroyOnLoad 오브젝트를 만들고
/// <c>Resources/GameSfxSet</c>을 읽는다 — 어느 씬에서 플레이를 시작해도 배선 없이 동작한다.
///
/// 2D(spatialBlend 0)로 재생하므로 카메라와의 거리와 무관하고, AudioSource는 Time.timeScale의
/// 영향을 받지 않는다 — 시간 가속 · 회피 카운터 슬로우모션 중에도 피치가 그대로다.
/// </summary>
public static class GameSfx
{
    const string ResourcePath = "GameSfxSet";

    static GameSfxSet set;
    static AudioSource oneShotSource;
    static AudioSource offsetSource;   // PlayFrom 전용(시작 지점 지정이 필요해 PlayOneShot을 못 쓴다)
    static Dictionary<Sfx, AudioSource> loopSources;

    public static float FootstepInterval => set != null ? set.footstepInterval : 0.5f;

    // 도메인 리로드를 끄면 static이 Play를 나가도 살아남는다(GameDataManager.ResetStatics와 같은 이유).
    // 지난 판에서 만든 오디오 오브젝트는 이미 파괴됐으니 참조를 버리고 Bootstrap이 다시 만들게 한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        set = null;
        oneShotSource = null;
        offsetSource = null;
        loopSources = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        set = Resources.Load<GameSfxSet>(ResourcePath);
        if (set == null)
        {
            Debug.LogWarning($"[GameSfx] Resources/{ResourcePath}.asset 을 찾지 못했다 — 효과음이 전부 무음이 된다.");
            return;
        }

        var go = new GameObject("~GameSfx");
        Object.DontDestroyOnLoad(go);
        oneShotSource = NewSource(go);
        loopSources = new Dictionary<Sfx, AudioSource>();
    }

    /// <summary>1회 재생. 클립이 여러 개 등록돼 있으면 랜덤으로 고른다. 겹쳐 울려도 서로 끊지 않는다.</summary>
    public static void Play(Sfx id)
    {
        if (oneShotSource == null) return;
        var entry = Find(id);
        AudioClip clip = PickClip(entry);
        if (clip == null) return;
        oneShotSource.PlayOneShot(clip, entry.volume);
    }

    /// <summary>
    /// 클립의 앞부분을 건너뛰고 1회 재생한다. 리드인이 긴 클립을 화면 연출과 맞출 때 쓴다
    /// (폭주·초월 진입음은 앞 1초가 리드인이라 그만큼 잘라야 하트비트와 임팩트가 겹친다).
    /// PlayOneShot은 시작 지점을 지정할 수 없어 전용 소스를 쓴다 — 겹쳐 울릴 일이 없는 소리 전용이다.
    /// </summary>
    public static void PlayFrom(Sfx id, float startTime)
    {
        if (oneShotSource == null) return;
        var entry = Find(id);
        AudioClip clip = PickClip(entry);
        if (clip == null) return;

        if (offsetSource == null) offsetSource = NewSource(oneShotSource.gameObject);
        offsetSource.clip = clip;
        offsetSource.volume = entry.volume;
        // 클립 길이를 넘기면 재생 자체가 안 된다 — 끝자락을 남겨 클램프한다.
        offsetSource.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - 0.01f));
        offsetSource.Play();
    }

    /// <summary>홀드 중 계속 울리는 소리를 켠다(일섬 차지 · 광원 방출). 이미 같은 클립이 돌고 있으면 무시한다.</summary>
    public static void PlayLoop(Sfx id)
    {
        if (oneShotSource == null) return;
        var entry = Find(id);
        AudioClip clip = PickClip(entry);
        if (clip == null) return;

        if (!loopSources.TryGetValue(id, out AudioSource src))
        {
            src = NewSource(oneShotSource.gameObject);
            src.loop = true;
            loopSources[id] = src;
        }
        if (src.isPlaying && src.clip == clip) return;

        src.clip = clip;
        src.volume = entry.volume;
        src.Play();
    }

    public static void StopLoop(Sfx id)
    {
        if (loopSources != null && loopSources.TryGetValue(id, out AudioSource src)) src.Stop();
    }

    /// <summary>씬 전환 · 사망처럼 상태 정리 경로를 건너뛸 수 있는 지점에서 부른다.</summary>
    public static void StopAllLoops()
    {
        if (loopSources == null) return;
        // 앱 종료 시 ~GameSfx가 플레이어보다 먼저 파괴될 수 있다 — 그때 PlayerController.OnDisable이
        // 여기로 들어와 파괴된 AudioSource에 Stop()을 걸어 NullReferenceException이 났다(빌드 로그로 확인).
        foreach (AudioSource src in loopSources.Values) if (src != null) src.Stop();
    }

    /// <summary>
    /// 지상 이동 중 발소리를 간격에 맞춰 울린다 — 타이머는 호출자가 들고 있는다
    /// (플레이어와 튜토리얼 플레이어가 같은 규칙을 쓰기 위한 공용 헬퍼).
    /// 멈추면 타이머가 0으로 돌아가므로 다시 걷기 시작하는 첫 걸음은 즉시 울린다.
    /// </summary>
    public static void TickFootstep(ref float timer, bool walking, float deltaTime)
    {
        if (!walking) { timer = 0f; return; }
        timer -= deltaTime;
        if (timer > 0f) return;
        timer = FootstepInterval;
        Play(Sfx.Footstep);
    }

    static GameSfxSet.Entry Find(Sfx id)
    {
        if (set == null || set.entries == null) return null;
        for (int i = 0; i < set.entries.Length; i++)
            if (set.entries[i] != null && set.entries[i].id == id) return set.entries[i];
        return null;
    }

    static AudioClip PickClip(GameSfxSet.Entry entry)
    {
        if (entry == null || entry.clips == null || entry.clips.Length == 0) return null;
        return entry.clips.Length == 1 ? entry.clips[0] : entry.clips[Random.Range(0, entry.clips.Length)];
    }

    static AudioSource NewSource(GameObject go)
    {
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f; // 2D — 카메라와의 거리와 무관하게 같은 크기로 들린다
        return src;
    }
}
