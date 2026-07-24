using UnityEngine;

// 타격 시 공통 연출(히트 스파크 스폰, 데미지 텍스트 스폰)을 PlayerController/DummyEnemy 양쪽에서
// 재사용하기 위한 정적 헬퍼. UniTrio-Game-2026(SwordHitbox.SpawnHitVFX/SpawnDamageText) 로직 참고.
public static class CombatFx
{
    public static void SpawnHitVfx(GameObject[] prefabs, Vector3 targetPos, Vector2 facing, float offset)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        Vector3 spawnPos = targetPos + (Vector3)(facing * offset);
        float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        Object.Instantiate(prefab, spawnPos, Quaternion.Euler(0f, 0f, angle + Random.Range(-15f, 15f)));
    }

    public static void SpawnDamageText(GameObject prefab, Vector3 targetPos, int damage, Color color)
    {
        if (prefab == null) return;
        Vector3 spawnPos = targetPos + Vector3.up * 0.5f;
        GameObject go = Object.Instantiate(prefab, spawnPos, Quaternion.identity);
        DamageText dmgText = go.GetComponent<DamageText>();
        if (dmgText != null) dmgText.Setup(damage, color);
    }
}
