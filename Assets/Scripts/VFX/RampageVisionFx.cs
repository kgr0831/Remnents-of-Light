using System.Collections.Generic;
using UnityEngine;

// 폭주 시야 제한(B-2)의 드라이버. 폭주 진입/해제에서 한 줄씩 호출되고, 3층을 전부 여기서 굴린다.
//   ① 어둠   : RampageVisionFeature(URP 렌더러 피처)의 세기·중심·반경을 매 프레임 갱신
//   ② 적     : 컬링 반경 안의 살아 있는 적에게 RampageEnemyOutlineFx 부착/해제
//   ③ 지형   : RampageTerrainOutlineFx 하나를 만들어 글리치 라인을 그림
//
// 페이드 중에는 아웃라인 알파도 같은 계수(k)로 따라간다 — 어둠만 걷히고 붉은 선이 남아 있으면
// 평상시 화면에 아웃라인이 튀어 보인다.
public class RampageVisionFx : MonoBehaviour
{
    // 0이면 "가시 원 없음" = 화면 전체 암전. 플레이어와 아웃라인만 보호 레이어로 덧그려져 보인다
    // (사용자 지시 2026-08-01: "아예 폭주상태에서 플레이어만 보이게").
    public const float VisionRadiusWorld = 0f;
    public const float CullRadius = 12f;         // 이 밖의 적은 아웃라인 자체를 만들지 않는다
    // 지형은 별도의(더 좁은) 반경을 쓴다(사용자 지시 2026-08-03, Map1 스크린샷으로 지적) — Map1은
    // 방 하나가 카메라 한 화면(오쏘사이즈 8, 세로 절반 높이 8유닛)에 딱 맞게 배치돼 있는데, 기존
    // CullRadius(12)로 지형을 스캔하면 화면 밖 옆방·위아래 방 타일 조각까지 걸려들어와 "동떨어져
    // 뜬 조각"처럼 보였다(실측: 세그먼트가 플레이어에서 최대 15.9유닛까지 나옴, 화면엔 최대 8유닛
    // 세로 반높이만 보이는데). 적 아웃라인은 사용자가 문제 삼지 않았으니 CullRadius는 그대로 두고
    // 지형만 화면 절반 높이에 맞춰 좁힌다.
    const float TerrainCullRadius = 7f;

    const float FadeInDuration = 0.25f;
    const float FadeOutDuration = 0.30f;
    const float EnemyScanInterval = 0.25f;
    const float MaxSmoothDelta = 0.05f; // 원격 에디터 프레임 튐 대비(PlayerHudUI 선례)
    const float EyeHeight = 0.6f;       // 피봇이 발밑이라 눈높이 보정(SetDodgeGrayscale와 같은 값)

    public static RampageVisionFx Instance { get; private set; }

    Transform target;
    RampageTerrainOutlineFx terrain;
    // 지형 아웃라인과 같은 레이어(Ground/Wall) — 시야선(라인오브사이트) 차단 체크에 쓴다.
    LayerMask visionBlockMask;
    readonly Dictionary<DummyEnemy, RampageEnemyOutlineFx> outlines =
        new Dictionary<DummyEnemy, RampageEnemyOutlineFx>();
    readonly List<DummyEnemy> scratch = new List<DummyEnemy>();

    float k;        // 현재 세기 0~1
    bool ending;
    float scanTimer;

    // [ASSERT] 판독구
    public float Intensity => k;
    public int OutlineCount => outlines.Count;
    public RampageTerrainOutlineFx Terrain => terrain;

    public static void Begin(Transform player)
    {
        if (player == null) return;

        if (Instance != null)
        {
            // 페이드아웃 중에 다시 폭주하면 되살린다(해제→즉시 재발동 시 깜빡임 방지).
            Instance.ending = false;
            Instance.target = player;
            return;
        }

        var go = new GameObject("RampageVisionFx");
        var fx = go.AddComponent<RampageVisionFx>();
        fx.target = player;
        fx.visionBlockMask = LayerMask.GetMask("Ground", "Wall");
        Instance = fx;

        fx.terrain = RampageTerrainOutlineFx.Create(player, TerrainCullRadius);
        fx.ScanEnemies();
        fx.Apply();
    }

    /// <summary>페이드아웃 후 스스로 파괴된다(오브젝트 누수 0).</summary>
    public static void End()
    {
        if (Instance != null) Instance.ending = true;
    }

    void LateUpdate()
    {
        if (target == null) ending = true;

        float dt = Mathf.Min(Time.unscaledDeltaTime, MaxSmoothDelta);
        float dur = ending ? FadeOutDuration : FadeInDuration;
        k = Mathf.MoveTowards(k, ending ? 0f : 1f, dt / Mathf.Max(0.0001f, dur));

        if (!ending)
        {
            scanTimer += dt;
            if (scanTimer >= EnemyScanInterval)
            {
                scanTimer = 0f;
                ScanEnemies();
            }
        }

        Apply();

        if (ending && k <= 0f) Destroy(gameObject);
    }

    void Apply()
    {
        var f = RampageVisionFeature.Instance;
        // 피처가 URP 렌더러에 등록돼 있지 않으면 Instance가 null이다 → 조용히 무시(에러 없음).
        // 아웃라인은 실제 월드 오브젝트라 어둠이 없어도 그대로 보인다.
        if (f != null)
        {
            f.Intensity = k;
            var cam = Camera.main;
            if (cam != null && target != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(target.position + Vector3.up * EyeHeight);
                f.Center = new Vector2(vp.x, vp.y);
                // 반경 0 = 가시 원 없음. smoothstep이 중심에서 0.5가 되어 반쯤 밝아지는 것을 피하려고
                // 음수 반경 + 아주 좁은 경계를 넣어 "어디서든 완전히 어둡다"로 만든다.
                if (VisionRadiusWorld <= 0f) { f.Radius = -1f; f.Softness = 0.0001f; }
                else { f.Radius = ViewportRadius(cam, VisionRadiusWorld); f.Softness = 0.06f; }
            }
        }

        if (terrain != null) terrain.SetAlpha(k);

        scratch.Clear();
        foreach (var kv in outlines)
        {
            if (kv.Value == null) { scratch.Add(kv.Key); continue; }
            kv.Value.SetAlpha(k);
        }
        for (int i = 0; i < scratch.Count; i++) outlines.Remove(scratch[i]); // 적이 파괴된 경우 정리
    }

    // 월드 반경을 스크린 UV 반경으로 환산한다. 화면 높이 = 2 * orthographicSize이므로
    // uv = world / (2 * halfHeight). 광원 소모의 지속 줌인(1.3배)에도 매 프레임 따라간다.
    static float ViewportRadius(Camera cam, float worldRadius)
    {
        float halfHeight;
        if (cam.orthographic) halfHeight = cam.orthographicSize;
        else
        {
            float dist = Mathf.Abs(cam.transform.position.z);
            halfHeight = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }
        return worldRadius / (2f * Mathf.Max(0.0001f, halfHeight));
    }

    void ScanEnemies()
    {
        if (target == null) return;
        Vector2 c = target.position;

        var enemies = FindObjectsByType<DummyEnemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            bool want = e.IsAlive && Vector2.Distance(c, e.transform.position) <= CullRadius
                && HasLineOfSight(c, e.transform.position);
            bool has = outlines.TryGetValue(e, out var fx) && fx != null;

            if (want && !has)
            {
                var made = RampageEnemyOutlineFx.Attach(e.transform);
                if (made != null) { made.SetAlpha(k); outlines[e] = made; }
            }
            else if (!want && has)
            {
                fx.Detach();
                outlines.Remove(e);
            }
        }

        // 씬에서 사라진 적의 항목 정리
        scratch.Clear();
        foreach (var kv in outlines)
            if (kv.Key == null || kv.Value == null) scratch.Add(kv.Key);
        for (int i = 0; i < scratch.Count; i++) outlines.Remove(scratch[i]);
    }

    // CullRadius는 순수 직선거리라 벽 뒤·옆방처럼 화면에 실제로 안 보여야 할 적도 반경 안이면
    // 아웃라인이 그대로 나타났다(지형 아웃라인이 화면 밖 옆방까지 걸려 나오던 것과 같은 종류의 버그,
    // RampageTerrainOutlineFx 주석 참고). 지형(Ground/Wall)에 가로막히면 "시야 밖"으로 취급한다.
    // 발밑 지형과 자체 교차하지 않도록 EyeHeight만큼 띄워서 쏜다(SetDodgeGrayscale와 같은 보정값).
    bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        Vector2 eyeFrom = from + Vector2.up * EyeHeight;
        Vector2 eyeTo = to + Vector2.up * EyeHeight;
        Vector2 delta = eyeTo - eyeFrom;
        float dist = delta.magnitude;
        if (dist < 0.001f) return true;
        return !Physics2D.Raycast(eyeFrom, delta / dist, dist, visionBlockMask);
    }

    void OnDestroy()
    {
        var f = RampageVisionFeature.Instance;
        if (f != null) f.Intensity = 0f; // 어떤 경로로 끝나도 화면은 반드시 원복된다

        foreach (var kv in outlines)
            if (kv.Value != null) kv.Value.Detach();
        outlines.Clear();

        if (terrain != null) Destroy(terrain.gameObject);
        if (Instance == this) Instance = null;
    }
}
