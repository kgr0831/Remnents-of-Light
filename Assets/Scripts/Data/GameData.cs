using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 저장/불러오기 대상 데이터 한 덩어리.
/// 기능_구현_명세서 1장 "데이터 저장/불러오기" = 체크포인트 위치 · 해금된 폼 체인지 능력 ·
/// 보스 격파 진행도. 여기에 플레이어 자원(체력 갯수 · 광원)을 더해 한 파일로 다룬다.
///
/// JsonUtility로 직렬화하므로 <b>public 필드 + [Serializable]</b>만 쓴다
/// (프로퍼티·Dictionary·인터페이스는 JsonUtility가 무시한다).
/// </summary>
[Serializable]
public class GameData
{
    /// <summary>저장 포맷 버전. 필드 구조를 바꿀 때 올리고, 로드 시 마이그레이션 분기점으로 쓴다.</summary>
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public string savedAtUtc = "";

    public PlayerData player = new PlayerData();
    public ProgressData progress = new ProgressData();
}

/// <summary>
/// 플레이어 자원. 체력은 <b>수치가 아니라 갯수(칸)</b> — 젤다/할로우나이트류로 "몇 대 맞으면 죽는가"가
/// 바로 읽히게 한다. 광원(= 기획안의 "빛 에너지")은 일섬 소모·패링 충전이 조금씩 움직여야 해서
/// 연속 게이지로 둔다. 필드명은 <see cref="PlayerController"/> 쪽과 일부러 똑같이 맞췄다
/// (두 이름이 갈리면 어느 쪽이 진짜인지 매번 헷갈린다).
/// </summary>
[Serializable]
public class PlayerData
{
    public int maxHealth = 5;
    public int currentHealth = 5;

    public int maxEnergy = 100;
    public int currentEnergy = 50;

    public void ClampToRange()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        maxEnergy = Mathf.Max(1, maxEnergy);
        currentEnergy = Mathf.Clamp(currentEnergy, 0, maxEnergy);
    }
}

/// <summary>진행도. 아직 체크포인트/폼 체인지/보스가 구현되지 않아 지금은 저장 슬롯만 준비된 상태다.</summary>
[Serializable]
public class ProgressData
{
    public bool hasCheckpoint;
    public string checkpointScene = "";
    public Vector2 checkpointPosition;

    public List<string> unlockedAbilities = new List<string>();
    public List<string> defeatedBosses = new List<string>();

    public void SetCheckpoint(string sceneName, Vector2 position)
    {
        hasCheckpoint = true;
        checkpointScene = sceneName;
        checkpointPosition = position;
    }

    public bool IsUnlocked(string abilityId) => unlockedAbilities.Contains(abilityId);

    public void Unlock(string abilityId)
    {
        if (!string.IsNullOrEmpty(abilityId) && !unlockedAbilities.Contains(abilityId))
            unlockedAbilities.Add(abilityId);
    }

    public bool IsBossDefeated(string bossId) => defeatedBosses.Contains(bossId);

    public void MarkBossDefeated(string bossId)
    {
        if (!string.IsNullOrEmpty(bossId) && !defeatedBosses.Contains(bossId))
            defeatedBosses.Add(bossId);
    }
}
