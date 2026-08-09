using System;
using System.IO;
using UnityEngine;

/// <summary>
/// <see cref="GameData"/>를 디스크에 JSON으로 읽고 쓰는 가장 아래층. 게임 로직은 전혀 모르고
/// 파일 입출력만 한다(그 위 정책 = <see cref="GameDataManager"/>).
///
/// 저장 위치는 <c>Application.persistentDataPath</c> — 에디터/빌드 모두에서 쓰기 가능하고
/// 프로젝트를 다시 임포트해도 남는 유일한 경로다(Assets/ 밑에 쓰면 빌드에 안 따라간다).
/// </summary>
public static class SaveSystem
{
    public const string FileName = "save_slot0.json";

    public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    static string TempPath => SavePath + ".tmp";

    public static bool Exists() => File.Exists(SavePath);

    /// <summary>
    /// 저장. 임시 파일에 먼저 다 쓴 뒤 교체한다 — 쓰는 도중에 게임이 죽어도 기존 세이브가
    /// 반쪽짜리로 덮여 날아가지 않게 하는 표준 방식.
    /// </summary>
    public static bool Save(GameData data)
    {
        if (data == null) return false;
        data.version = GameData.CurrentVersion;
        data.savedAtUtc = DateTime.UtcNow.ToString("o");

        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(TempPath, JsonUtility.ToJson(data, true));

            if (File.Exists(SavePath)) File.Replace(TempPath, SavePath, null);
            else File.Move(TempPath, SavePath);

            TestLog.Event("game_data", $"saved path={SavePath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] 저장 실패: {e.Message}");
            return false;
        }
    }

    /// <summary>불러오기. 파일이 없거나 깨졌으면 <c>null</c>(호출자가 새 게임으로 처리한다).</summary>
    public static GameData Load()
    {
        if (!Exists()) return null;

        try
        {
            var data = JsonUtility.FromJson<GameData>(File.ReadAllText(SavePath));
            if (data == null)
            {
                Debug.LogWarning("[SaveSystem] 세이브 파일이 비었거나 형식이 맞지 않습니다.");
                return null;
            }

            // 지금은 버전이 1뿐이라 마이그레이션이 없다. 포맷을 바꿀 땐 여기서 갈라 옛 데이터를 올린다.
            if (data.version != GameData.CurrentVersion)
                Debug.LogWarning($"[SaveSystem] 세이브 버전 불일치 (파일 {data.version} / 현재 {GameData.CurrentVersion}).");

            // JsonUtility는 파일에 없는 필드를 기본값이 아니라 null/0으로 둘 수 있다(옛 세이브 대응).
            if (data.player == null) data.player = new PlayerData();
            if (data.progress == null) data.progress = new ProgressData();
            data.player.ClampToRange();

            TestLog.Event("game_data", $"loaded hp={data.player.currentHealth}/{data.player.maxHealth} " +
                $"energy={data.player.currentEnergy}/{data.player.maxEnergy}");
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] 불러오기 실패: {e.Message}");
            return null;
        }
    }

    public static bool Delete()
    {
        try
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
            if (File.Exists(TempPath)) File.Delete(TempPath);
            TestLog.Event("game_data", "deleted");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] 삭제 실패: {e.Message}");
            return false;
        }
    }
}
