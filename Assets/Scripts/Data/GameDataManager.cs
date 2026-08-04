using UnityEngine;

/// <summary>
/// 런타임 데이터 정책 층. "지금 이 판의 데이터"(<see cref="Current"/>)를 들고 있고,
/// 플레이어 컴포넌트 ↔ 데이터 사이를 옮긴다. 파일 입출력 자체는 <see cref="SaveSystem"/>이 한다.
///
/// <b>불러오기는 자동이 아니다.</b> Play를 누를 때마다 옛 세이브가 스르륵 적용되면 플레이/테스트가
/// 매번 다른 상태에서 시작해 디버깅이 불가능해진다(상용 게임의 "이어하기"도 명시적 선택이다).
/// 시작 시엔 씬/인스펙터 값을 데이터로 <see cref="CaptureFrom"/>해 두고,
/// 복원은 <see cref="LoadGame"/>을 부른 쪽만 받는다.
/// </summary>
public static class GameDataManager
{
    static GameData _current;
    static PlayerController _player;

    public static GameData Current => _current ?? (_current = new GameData());
    public static bool HasSaveFile => SaveSystem.Exists();
    public static string SavePath => SaveSystem.SavePath;

    // static은 도메인 리로드를 끄면 Play를 나가도 살아남는다("Enter Play Mode Options" 사용 시).
    // 이전 판의 데이터가 다음 판으로 새지 않도록 씬 로드 전에 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _current = null;
        _player = null;
    }

    /// <summary>플레이어가 깨어날 때 자기 자신을 등록한다(<see cref="PlayerController.Awake"/>).</summary>
    public static void Bind(PlayerController player)
    {
        _player = player;
        CaptureFrom(player);
    }

    /// <summary>플레이어의 현재 자원을 데이터로 옮긴다(저장 직전에도 한 번 더 부른다).</summary>
    public static void CaptureFrom(PlayerController player)
    {
        if (player == null) return;
        var p = Current.player;
        p.maxHealth = player.maxHealth;
        p.currentHealth = player.currentHealth;
        p.maxEnergy = player.maxEnergy;
        p.currentEnergy = player.currentEnergy;
        p.ClampToRange();
    }

    /// <summary>데이터의 자원을 플레이어에 적용한다(불러오기 / 새 게임).</summary>
    public static void ApplyTo(PlayerController player)
    {
        if (player == null) return;
        var p = Current.player;
        p.ClampToRange();
        player.maxHealth = p.maxHealth;
        player.currentHealth = p.currentHealth;
        player.maxEnergy = p.maxEnergy;
        player.currentEnergy = p.currentEnergy;
        // HUD는 매 프레임 플레이어를 읽어 보간하므로 따로 알릴 필요가 없다
        // (체력 칸 수가 바뀌면 PlayerHudUI가 칸 줄을 스스로 다시 만든다).
    }

    public static bool SaveGame()
    {
        CaptureFrom(_player);
        return SaveSystem.Save(Current);
    }

    /// <summary>세이브 파일을 읽어 현재 데이터·플레이어에 반영한다. 파일이 없거나 깨졌으면 false.</summary>
    public static bool LoadGame()
    {
        var loaded = SaveSystem.Load();
        if (loaded == null) return false;

        _current = loaded;
        ApplyTo(_player);
        return true;
    }

    /// <summary>새 게임(데이터 초기화). 세이브 파일은 건드리지 않는다 — 지우려면 <see cref="SaveSystem.Delete"/>.</summary>
    public static void NewGame()
    {
        _current = new GameData();
        ApplyTo(_player);
        TestLog.Event("game_data", "new_game");
    }

    /// <summary>체크포인트 갱신(기능_구현_명세서 1장). 도달 지점을 진행도에 적어 두고 즉시 저장한다.</summary>
    public static bool SaveCheckpoint(string sceneName, Vector2 position)
    {
        Current.progress.SetCheckpoint(sceneName, position);
        return SaveGame();
    }
}
