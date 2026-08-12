using UnityEngine;

/// <summary>튜토리얼 공간을 "실제 장소"가 아니라 **시뮬레이션 안**처럼 보이게 하는 배경 연출.
/// 카메라의 자식으로 붙어 어느 구역으로 순간이동하든 그대로 따라간다.
///
/// 전부 색·세기 조절이 아니라 **움직임**으로 만드는 신호다(사용자 지시 2026-08-12
/// "수치 조절 말고 다른 방식으로").
///   1) 스캔라인 스윕 — 여러 줄이 서로 다른 축·방향·속도로 화면을 훑는다.
///   2) 그리드 호흡     — 배경 격자의 밝기가 아주 느리게 오르내린다(정지 화면이 아니라 "돌고 있는 시스템").
///   3) 리프레시 깜빡임 — 아주 가끔 그리드가 한 프레임 튄다. 브라운관/투사 장치가 갱신되는 느낌.
///
/// ⚠️ 전부 unscaled 시간으로 돈다 — 튜토리얼은 연출 구간에서 Time.timeScale = 0으로 세계를 멈춘다.
///    그때도 배경은 계속 살아 있어야 "시스템은 돌고 있고 세계만 멈췄다"로 읽힌다.
///
/// ⚠️ 스캔라인은 **지형·캐릭터 뒤**에 그린다(사용자 지시) — 앞에 두면 화면 전체를 덮는 필터처럼 보여
///    "공간을 훑는 빛"이 아니라 "화면 효과"가 된다.
public class TutorialSimFx : MonoBehaviour
{
    [Header("스캔라인 — 원본 1개를 복제해 각자 다르게 굴린다")]
    [Tooltip("복제 원본. 가로선 기준으로 만들어 두면 세로 줄은 이 스크립트가 90도 돌려 쓴다")]
    public Transform scanline;
    [Tooltip("동시에 굴릴 줄 수")]
    [Range(1, 24)] public int scanlineCount = 8;

    [Header("줄마다 무작위로 갈리는 값")]
    [Tooltip("한 번 훑는 데 걸리는 시간(초) 범위. 짧을수록 빠르다")]
    public Vector2 sweepDurationRange = new Vector2(1.4f, 5.5f);
    [Tooltip("한 줄이 다 훑고 다시 시작하기까지 쉬는 시간(초) 범위")]
    public Vector2 sweepIntervalRange = new Vector2(0.2f, 3.5f);
    [Tooltip("세로 줄(좌우로 훑는다)이 될 확률. 0이면 전부 가로선")]
    [Range(0f, 1f)] public float verticalChance = 0.35f;
    [Tooltip("진행 방향이 뒤집힐 확률(가로선은 위→아래, 세로선은 오른→왼)")]
    [Range(0f, 1f)] public float reverseChance = 0.4f;
    [Tooltip("두께·진하기 편차. 0.4면 ±40% 안에서 갈린다(기계적으로 안 보이게)")]
    [Range(0f, 0.9f)] public float scanlineVariance = 0.45f;

    [Header("훑는 범위 (카메라 로컬)")]
    [Tooltip("가로선이 오르내리는 y 범위의 절반. 화면 세로 절반(=orthographicSize 9)보다 넉넉하게")]
    public float sweepRangeY = 11f;
    [Tooltip("세로선이 오가는 x 범위의 절반. 화면 가로 절반(16)보다 넉넉하게")]
    public float sweepRangeX = 19f;
    // 줄의 중심은 cross(가로선 ±3 · 세로선 ±2)만큼 치우쳐 있으므로 화면 크기보다 그 두 배 이상
    // 여유가 필요하다 — 36이면 cross=+3인 가로선이 화면 좌단(-16)에 1유닛 못 미쳐 끝이 보였다.
    [Tooltip("가로선의 길이(화면 폭 32 + 치우침 여유) / 세로선의 길이(화면 높이 18 + 치우침 여유)")]
    public float lineLengthX = 44f;
    public float lineLengthY = 28f;

    [Header("그리드 호흡 · 깜빡임")]
    public SpriteRenderer[] gridRenderers;
    [Tooltip("호흡의 밝기 범위(원래 알파에 곱한다). x=가장 어두울 때 y=가장 밝을 때")]
    public Vector2 breathRange = new Vector2(0.75f, 1.15f);
    [Tooltip("호흡 한 주기(초)")]
    public float breathPeriod = 5f;
    [Tooltip("리프레시 깜빡임이 일어나는 평균 간격(초). 0이면 안 한다")]
    public float flickerInterval = 7f;
    [Tooltip("깜빡임이 지속되는 시간(초)")]
    public float flickerDuration = 0.06f;
    [Tooltip("깜빡임 순간의 밝기 배율")]
    public float flickerMultiplier = 2.2f;

    // 줄 하나의 상태. 축·방향·속도·위상을 각자 들고 있어서 서로 절대 안 맞물린다.
    struct Lane
    {
        public Transform tr;
        public bool vertical;   // true면 세로 줄이 좌우로 훑는다
        public bool reverse;
        public float duration;
        public float cycle;     // duration + 쉬는 시간
        public float offset;    // 위상
        public float cross;     // 진행 축과 직교하는 고정 좌표(줄이 화면 어디를 지나는지)
    }

    Lane[] lanes;
    float[] baseAlpha;
    float clock;
    float flickerTimer;
    float flickerLeft;

    void Awake()
    {
        BuildLanes();

        if (gridRenderers != null)
        {
            baseAlpha = new float[gridRenderers.Length];
            for (int i = 0; i < gridRenderers.Length; i++)
                baseAlpha[i] = gridRenderers[i] != null ? gridRenderers[i].color.a : 0f;
        }
        flickerTimer = flickerInterval;
    }

    /// <summary>원본 스캔라인을 복제해 줄마다 축·방향·속도·두께를 다르게 굴린다.
    /// 씬에 줄을 여러 개 배치해 두는 대신 런타임에 만드는 이유: 개수를 인스펙터 숫자 하나로 바꾸고 싶고,
    /// "서로 안 맞물리는 무작위"를 손으로 배치해 유지하는 것은 사실상 불가능하기 때문이다.</summary>
    void BuildLanes()
    {
        if (scanline == null) { lanes = new Lane[0]; return; }

        int n = Mathf.Max(1, scanlineCount);
        lanes = new Lane[n];

        Vector3 baseScale = scanline.localScale;
        var baseSr = scanline.GetComponent<SpriteRenderer>();
        float baseA = baseSr != null ? baseSr.color.a : 1f;
        float thickness = baseScale.y;

        for (int i = 0; i < n; i++)
        {
            Transform tr;
            if (i == 0) tr = scanline;
            else
            {
                var clone = Instantiate(scanline.gameObject, scanline.parent);
                clone.name = scanline.name + "_" + i;
                tr = clone.transform;
            }

            var lane = new Lane();
            lane.tr = tr;
            lane.vertical = Random.value < verticalChance;
            lane.reverse = Random.value < reverseChance;
            lane.duration = Random.Range(sweepDurationRange.x, sweepDurationRange.y);
            lane.cycle = lane.duration + Random.Range(sweepIntervalRange.x, sweepIntervalRange.y);
            // 위상을 통째로 흩어 놓는다 — 균등 분할하면 개수가 적을 때 규칙적으로 보인다.
            lane.offset = Random.Range(0f, lane.cycle);
            // 줄이 화면의 어디를 지나는지도 조금씩 다르게(전부 정중앙을 지나면 한 덩어리로 보인다).
            lane.cross = lane.vertical ? Random.Range(-2f, 2f) : Random.Range(-3f, 3f);

            float v = 1f + Random.Range(-scanlineVariance, scanlineVariance);
            if (lane.vertical)
            {
                tr.localRotation = Quaternion.Euler(0f, 0f, 90f);
                tr.localScale = new Vector3(lineLengthY, thickness * v, 1f);
            }
            else
            {
                tr.localRotation = Quaternion.identity;
                tr.localScale = new Vector3(lineLengthX, thickness * v, 1f);
            }

            var sr = tr.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                c.a = Mathf.Clamp01(baseA * v);
                sr.color = c;
            }

            lanes[i] = lane;
        }
    }

    void Update()
    {
        // 씬 로드 직후 첫 프레임은 unscaledDeltaTime이 1초 넘게 찍히는 경우가 있다(ScreenBlackout 선례) —
        // 그대로 쓰면 스윕이 한 프레임에 화면을 통과해 버린다.
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        clock += dt;

        UpdateSweep();
        UpdateGrid(dt);
    }

    void UpdateSweep()
    {
        if (lanes == null) return;

        for (int i = 0; i < lanes.Length; i++)
        {
            Lane lane = lanes[i];
            if (lane.tr == null) continue;

            float t = (clock + lane.offset) % lane.cycle;
            bool sweeping = t < lane.duration;
            if (lane.tr.gameObject.activeSelf != sweeping) lane.tr.gameObject.SetActive(sweeping);
            if (!sweeping) continue;

            float k = t / lane.duration;
            if (lane.reverse) k = 1f - k;

            float range = lane.vertical ? sweepRangeX : sweepRangeY;
            float moving = Mathf.Lerp(-range, range, k);

            Vector3 p = lane.tr.localPosition;
            lane.tr.localPosition = lane.vertical
                ? new Vector3(moving, lane.cross, p.z)
                : new Vector3(lane.cross, moving, p.z);
        }
    }

    void UpdateGrid(float dt)
    {
        if (gridRenderers == null || baseAlpha == null) return;

        // 사인 호흡 — 0.5±0.5를 breathRange로 옮긴다.
        float breath = Mathf.Lerp(breathRange.x, breathRange.y,
            0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(0.01f, breathPeriod)));

        if (flickerInterval > 0f)
        {
            flickerTimer -= dt;
            if (flickerTimer <= 0f)
            {
                // 간격을 매번 다시 뽑아 규칙적으로 안 보이게 한다(규칙적이면 "장치"가 아니라 "애니메이션"이 된다).
                flickerTimer = flickerInterval * Random.Range(0.5f, 1.5f);
                flickerLeft = flickerDuration;
            }
        }

        float mul = breath;
        if (flickerLeft > 0f) { flickerLeft -= dt; mul *= flickerMultiplier; }

        for (int i = 0; i < gridRenderers.Length; i++)
        {
            var sr = gridRenderers[i];
            if (sr == null) continue;
            Color c = sr.color;
            c.a = Mathf.Clamp01(baseAlpha[i] * mul);
            sr.color = c;
        }
    }
}
