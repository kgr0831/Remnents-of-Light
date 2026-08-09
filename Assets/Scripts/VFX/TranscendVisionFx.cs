using System.Collections.Generic;
using UnityEngine;

// 초월(Transcendence) 드라이버 — T-3c (docs/dev/TRANSCENDENCE_PLAN.md §6).
// RampageVisionFx에서 어둠 층·지형 층을 뺀 축소판. 컬링 반경 안의 적을 스캔해 공격 예고 원
// (AttackTelegraphFx)을 부착/해제한다.
//
// ⚠️ 스캔을 두 층으로 쪼갠다(PLAN §6 "왜 스캔을 두 층으로 쪼개는가"): 컬링(반경 안 적 목록 수집)은
// RampageVisionFx와 같은 0.25s 주기로 충분하지만, 예고는 0.402초짜리 단발 이벤트라 같은 주기로
// 보면 최대 0.25초 늦게 떠서 창의 62%가 날아간다. → 컬링만 0.25s, 예고 판정(캐시된 목록에 대해
// AttackTelegraphProgress 평가)은 매 프레임.
public class TranscendVisionFx : MonoBehaviour
{
    public const float CullRadius = 12f; // RampageVisionFx.CullRadius와 동일 — "보이는 거리"가 같아야 혼란이 없다
    const float EnemyScanInterval = 0.25f; // RampageVisionFx.EnemyScanInterval와 동일
    const float FadeInDuration = 0.20f;
    const float FadeOutDuration = 0.25f;

    public static TranscendVisionFx Instance { get; private set; }

    Transform target;
    float scanTimer;
    float k; // 현재 세기 0~1
    bool ending;

    readonly List<DummyEnemy> nearby = new List<DummyEnemy>();
    readonly Dictionary<DummyEnemy, AttackTelegraphFx> active = new Dictionary<DummyEnemy, AttackTelegraphFx>();
    readonly List<DummyEnemy> scratch = new List<DummyEnemy>();

    // [ASSERT] 판독구
    public float Intensity => k;
    public int TelegraphCount => active.Count;

    public static void Begin(Transform player)
    {
        if (player == null) return;

        if (Instance != null)
        {
            Instance.ending = false; // 페이드아웃 중에 다시 초월하면 되살린다(폭주와 같은 정책)
            Instance.target = player;
            return;
        }

        var go = new GameObject("TranscendVisionFx");
        var fx = go.AddComponent<TranscendVisionFx>();
        fx.target = player;
        Instance = fx;

        fx.ScanEnemies();
        fx.Apply();
    }

    /// <summary>페이드아웃 후 스스로 파괴된다(오브젝트 누수 0).</summary>
    public static void End()
    {
        if (Instance != null) Instance.ending = true;
    }

    void Update()
    {
        if (target == null) ending = true;

        float dur = ending ? FadeOutDuration : FadeInDuration;
        k = Mathf.MoveTowards(k, ending ? 0f : 1f, Time.deltaTime / Mathf.Max(0.0001f, dur));

        if (!ending)
        {
            scanTimer += Time.deltaTime;
            if (scanTimer >= EnemyScanInterval)
            {
                scanTimer = 0f;
                ScanEnemies();
            }
        }

        Apply();

        if (ending && k <= 0f) Destroy(gameObject);
    }

    // 컬링 반경 안의 적 목록만 갱신한다(0.25s 주기). 목록 자체가 바뀌지 않는 프레임엔 아무 것도 안 한다.
    void ScanEnemies()
    {
        if (target == null) return;
        Vector2 c = target.position;

        nearby.Clear();
        var enemies = FindObjectsByType<DummyEnemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (e.IsAlive && Vector2.Distance(c, e.transform.position) <= CullRadius)
                nearby.Add(e);
        }

        // 컬링 밖으로 나간 적의 예고는 즉시 뗀다(터짐 애니메이션 없이) — 폭주 아웃라인과 같은 정책.
        scratch.Clear();
        foreach (var kv in active)
            if (!nearby.Contains(kv.Key)) scratch.Add(kv.Key);
        for (int i = 0; i < scratch.Count; i++) DetachIfPresent(scratch[i]);
    }

    // 예고 판정 — 캐시된 목록(nearby)에 대해서만, 매 프레임.
    void Apply()
    {
        for (int i = nearby.Count - 1; i >= 0; i--)
        {
            var e = nearby[i];
            if (e == null || !e.IsAlive) { nearby.RemoveAt(i); DetachIfPresent(e); continue; }

            // AttackTelegraphFx는 예고가 끝나면(progress<0) 스스로 터짐/흐지부지 애니메이션 후
            // 자기 파괴한다 — 여기서 progress<0이라고 강제로 떼면 그 연출이 잘린다. 그래서 이 루프는
            // "새로 붙일지"만 판단하고, 이미 붙은 것을 progress만으로 떼지 않는다.
            if (active.TryGetValue(e, out var fx))
            {
                if (fx == null) active.Remove(e); // 스스로 파괴된 뒤 — 다음 공격이 새로 붙을 수 있게 슬롯을 비운다
                else if (ending && e.AttackTelegraphProgress >= 0f)
                {
                    // 초월이 꺼지는 순간 **아직 날아오는 중인** 공격의 예고는 시야 버프와 함께 걷어내지
                    // 않는다(사용자 리포트 2026-08-05 "타이밍이 이상함") — 예전엔 창이 절반 넘게 뻗은
                    // 채로 경고만 0.25초에 걸쳐 사라지고 공격은 그대로 날아와 맞았다.
                    // ⚠️ 알파를 1로 붙잡아 두는 것만으로는 부족하다: 페이드가 끝나면 이 오브젝트가 스스로
                    // 파괴되고(Update 말미) OnDestroy가 active에 남은 예고를 전부 Detach(=파괴)하므로
                    // 결국 공격 도중에 사라진다. 그래서 **소유권을 놓아준다** — AttackTelegraphFx는 부모
                    // 없는 루트 오브젝트이고 owner만 보고 스스로 갱신·정리하므로(progress<0이 되면
                    // burst/fizzle 후 자기 파괴, owner가 사라져도 자기 파괴) 목록에서 빼도 누수가 없고
                    // 오히려 끝까지 정상적으로 산다.
                    // ⚠️ 놓아준 뒤 페이드가 끝나기 전에 다시 초월에 진입하면 같은 적에게 예고가 하나 더
                    // 붙을 수 있다 — 해제(70%)에서 진입(100%)까지 0.25초 안에 회복하는 건 실질적으로
                    // 불가능해 방치한다(겹쳐도 같은 자리·같은 모양이라 링이 조금 진해지는 정도).
                    fx.SetAlpha(1f);
                    active.Remove(e);
                }
                else fx.SetAlpha(k);
            }

            bool wantAttach = !ending && e.AttackTelegraphProgress >= 0f && !active.ContainsKey(e);
            if (wantAttach)
            {
                var made = AttackTelegraphFx.Attach(e);
                if (made != null) { made.SetAlpha(k); active[e] = made; }
            }
        }
    }

    void DetachIfPresent(DummyEnemy e)
    {
        if (e != null && active.TryGetValue(e, out var fx))
        {
            if (fx != null) fx.Detach();
            active.Remove(e);
        }
    }

    void OnDestroy()
    {
        foreach (var kv in active)
            if (kv.Value != null) kv.Value.Detach();
        active.Clear();

        if (Instance == this) Instance = null;
    }
}
