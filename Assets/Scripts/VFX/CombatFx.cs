using UnityEngine;

// 타격 시 공통 연출(히트 스파크 스폰, 데미지 텍스트 스폰)을 PlayerController/DummyEnemy 양쪽에서
// 재사용하기 위한 정적 헬퍼. UniTrio-Game-2026(SwordHitbox.SpawnHitVFX/SpawnDamageText) 로직 참고.
public static class CombatFx
{
    // 일반 타격: 등록된 프리팹 중 하나를 무작위로 뽑아 스폰.
    public static void SpawnHitVfx(GameObject[] prefabs, Vector3 targetPos, Vector2 facing, float offset)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        SpawnHitVfx(prefabs[Random.Range(0, prefabs.Length)], targetPos, facing, offset);
    }

    // 크리티컬(Hit02) · 처형(Hit03)처럼 반드시 특정 프리팹이 떠야 하는 경우.
    public static void SpawnHitVfx(GameObject prefab, Vector3 targetPos, Vector2 facing, float offset)
    {
        if (prefab == null) return;
        Vector3 spawnPos = targetPos + (Vector3)(facing * offset);
        float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
        Object.Instantiate(prefab, spawnPos, Quaternion.Euler(0f, 0f, angle + Random.Range(-15f, 15f)));
    }

    public static void SpawnDamageText(GameObject prefab, Vector3 targetPos, int damage, Color color, bool emphasized = false)
    {
        DamageText dmgText = SpawnTextObject(prefab, targetPos, emphasized);
        if (dmgText != null) dmgText.Setup(damage, color, emphasized);
    }

    // 숫자 대신 문구를 띄울 때(패링 "막아냄!" 등) — 프리팹·부유 연출은 데미지 텍스트와 완전히 동일.
    public static void SpawnDamageText(GameObject prefab, Vector3 targetPos, string text, Color color, bool emphasized = false)
    {
        DamageText dmgText = SpawnTextObject(prefab, targetPos, emphasized);
        if (dmgText != null) dmgText.SetupText(text, color, emphasized);
    }

    static DamageText SpawnTextObject(GameObject prefab, Vector3 targetPos, bool emphasized)
    {
        if (prefab == null) return null;
        Vector3 spawnPos = targetPos + Vector3.up * 0.5f;
        GameObject go = Object.Instantiate(prefab, spawnPos, Quaternion.identity);
        if (emphasized)
        {
            // 크리티컬/처형 텍스트는 닷지 카운터 슬로우모션(흑백) 중에도 원색으로 남아야 한다 —
            // DashAfterImage와 동일하게 GrayscaleRendererFeature의 보호 레이어로 올린다.
            int noGrayscaleLayer = LayerMask.NameToLayer("VFXNoGrayscale");
            if (noGrayscaleLayer >= 0) go.layer = noGrayscaleLayer;
        }
        return go.GetComponent<DamageText>();
    }
}
