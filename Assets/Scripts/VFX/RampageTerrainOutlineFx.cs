using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

// 폭주 시야 제한(B-2 ③): 어둠 속에서 발판 외곽선만 글리치 라인으로 보이게 한다.
// 이게 없으면 시야 제한이 그냥 "낙사기"가 된다(계획서 §9 리스크 3).
//
// ⚠️ 대상은 타일맵이 아니라 "Ground/Wall 레이어 콜라이더 전부"다.
//    현재 씬의 활성 지형은 대부분 BoxCollider2D 스프라이트(Floor1~3 · Plat1~4 · Wall1~2)이고
//    타일맵(Grid/Ground)은 60셀뿐이라, 타일맵 셀만 순회하면 실제로 밟는 발판이 어둠에 그대로 남는다.
//
// 사용자 지시: "선이 노이즈처럼 흔들리고 전체적으로 조금씩 깨지는 형태", "선들은 계속 글리치처럼
// 랜덤으로 깨져야 함" → 결손 패턴을 만들 때 한 번 뽑고 마는 게 아니라 글리치 스텝마다 재추첨한다.
public class RampageTerrainOutlineFx : MonoBehaviour
{
    const string ShaderName = "Custom/RampageOutline";
    // ⚠️ HDR 색이다(1을 넘는다). 씬 Global Volume의 Bloom(threshold 1.15, intensity 2.2)이 이 값을
    //    잡아 "선 자체가 빛나게" 만든다 — URP 2D Light가 아니라서 주위를 비추지는 않는다
    //    (사용자 지시: "그것만 빛나고 주위를 비추면 안 됨").
    static readonly Color EdgeColor = new Color(1.7f, 0.08f, 0.12f, 1f);
    static readonly int IdColor = Shader.PropertyToID("_Color");

    const float SegmentLength = 0.42f;  // 변을 이 길이로 잘라 조각을 만든다
    const float LineThickness = 0.045f; // 월드 단위(텍셀 단위로 잡으면 줌에 따라 사라진다)
    // 균일한 12Hz 백색잡음은 "지글거리는 모래"처럼 보여 엉성했다(사용자 피드백). 진짜 글리치는
    // ① 대부분의 프레임은 거의 멀쩡하고 ② 가끔 한 띠가 통째로 수평으로 밀리고 ③ 아주 가끔 전체가 끊긴다.
    // 그래서 스텝을 빠르게(20Hz) 가져가되 기본 지터는 아주 작게 두고, 사건(밴드 시프트·블랙아웃)을 드물게 넣는다.
    const float StepInterval = 1f / 20f;
    const float AliveChance = 0.86f;    // 평상시 결손. 너무 높으면(0.93) 그냥 실선이라 글리치가 안 보인다
    const float JitterAmp = 0.035f;     // 기본 지터
    const float BandChance = 0.5f;      // 이 확률로 한 스텝에 "찢긴 띠"가 생긴다
    const float BandHeight = 1.6f;      // 띠의 월드 높이
    const float BandShiftMax = 0.55f;   // 띠가 수평으로 밀리는 최대 거리(데이터모싱 느낌)
    const float BandLiftMax = 0.12f;    // 세로로도 살짝 어긋나게(수평만 밀면 "미끄러짐"으로만 읽힌다)
    const float BandAliveChance = 0.4f; // 띠 안에서는 결손이 훨씬 심하다
    const float BlackoutChance = 0.03f; // 이 확률로 한 스텝 전체가 사라진다(순간 끊김)
    const float RebuildInterval = 0.5f; // 토폴로지 재수집 주기(플레이어 이동 · 부서진 발판 반영)
    const float BuildMargin = 4f;       // 컬링 반경보다 넉넉히 모아 두고 표시만 반경으로 자른다
    const int MaxSegments = 4000;       // 폭주 아레나에서 폭주하지 않도록 상한
    const float MaxSmoothDelta = 0.05f; // 원격 에디터 프레임 튐 대비(PlayerHudUI 선례)

    struct Segment { public Vector2 a, b; public int source; }

    Transform target;
    float cullRadius = 12f;
    LayerMask terrainMask;

    readonly List<Segment> segments = new List<Segment>();
    readonly List<Collider2D> sources = new List<Collider2D>();

    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Vector2> uvs = new List<Vector2>();
    readonly List<int> tris = new List<int>();

    Mesh mesh;
    Material mat;
    float alpha = 1f;
    float rebuildTimer, stepTimer;

    // [ASSERT] 판독구
    public int SegmentCount => segments.Count;
    public int AliveCount { get; private set; }
    public int AliveSignature { get; private set; } // 스텝마다 결손 집합이 실제로 바뀌는지 확인용

    public static RampageTerrainOutlineFx Create(Transform target, float cullRadius)
    {
        Shader sh = Shader.Find(ShaderName);
        if (sh == null)
        {
            Debug.LogWarning("[RampageVision] " + ShaderName + " 셰이더를 찾을 수 없어 지형 아웃라인을 건너뜁니다.");
            return null;
        }

        var go = new GameObject("RampageTerrainOutlineFx");
        int protectedLayer = LayerMask.NameToLayer("VFXNoGrayscale");
        if (protectedLayer >= 0) go.layer = protectedLayer;

        var fx = go.AddComponent<RampageTerrainOutlineFx>();
        fx.target = target;
        fx.cullRadius = cullRadius;
        // Ground(9) + Wall(10). 이름으로 잡아 레이어 슬롯이 바뀌어도 따라간다.
        fx.terrainMask = LayerMask.GetMask("Ground", "Wall");

        fx.mesh = new Mesh { name = "RampageTerrainOutline" };
        fx.mesh.MarkDynamic();

        fx.mat = new Material(sh);
        fx.mat.SetColor(IdColor, EdgeColor);

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = fx.mesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = fx.mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.sortingOrder = 100; // 지형 스프라이트보다 앞

        fx.Rebuild();
        fx.BuildMesh();
        return fx;
    }

    public void SetAlpha(float a)
    {
        alpha = Mathf.Clamp01(a);
        var c = EdgeColor; c.a = alpha;
        if (mat != null) mat.SetColor(IdColor, c);
    }

    void LateUpdate()
    {
        if (target == null) { Destroy(gameObject); return; }

        float dt = Mathf.Min(Time.unscaledDeltaTime, MaxSmoothDelta);

        rebuildTimer += dt;
        if (rebuildTimer >= RebuildInterval)
        {
            rebuildTimer = 0f;
            Rebuild();
        }

        stepTimer += dt;
        if (stepTimer >= StepInterval)
        {
            stepTimer = 0f;
            BuildMesh(); // 정점 지터 + 결손 재추첨은 여기서만 — 매 프레임이면 지글거림이 너무 빨라진다
        }
    }

    // ── 토폴로지 수집(캐시) ────────────────────────────────────────────────
    // 조각 목록 자체는 여기서만 만든다. 글리치(지터·결손)는 이 목록을 건드리지 않는다.

    void Rebuild()
    {
        segments.Clear();
        sources.Clear();
        if (target == null) return;

        Vector2 c = target.position;
        float r = cullRadius + BuildMargin;

        var cols = Physics2D.OverlapCircleAll(c, r, terrainMask);
        for (int i = 0; i < cols.Length; i++)
        {
            var col = cols[i];
            if (col == null || col.isTrigger) continue;

            int src = sources.Count;
            sources.Add(col);

            // ⚠️ Map1 실측으로 잡은 버그(2026-08-03, 사용자 스크린샷): CompositeCollider2D일 때
            // Tilemap.HasTile()로 셀 인접을 재구성하는 방식(AddTilemapEdges)은 "타일 하나 = 셀 하나"를
            // 가정한다. Map1의 Base 레이어는 폭이 여러 셀인 타일(예: 4셀짜리 바닥 슬래브)을 듬성듬성
            // 배치해서 실제 충돌은 이어져 있는데 HasTile()은 "#...#...#..." 처럼 듬성듬성 true라, 채워진
            // 셀 하나하나가 이웃이 전부 비어 보여 따로 뜬 네모로 그려졌다(스크린샷의 "동떨어진 조각"
            // 정체). CompositeCollider2D는 물리 엔진이 이미 정확한 병합 폴리곤을 갖고 있으므로
            // GetPath()로 그 실제 모양을 직접 읽는 쪽이 타일 배치 방식에 안 흔들리고 훨씬 정확하다.
            var composite = col as CompositeCollider2D;
            if (composite != null)
            {
                AddCompositeColliderEdges(composite, src, c, r);
            }
            else
            {
                // ⚠️ 콜라이더 타입이 아니라 "같은 오브젝트에 Tilemap이 있는가"로 판단한다(컴포짓이
                // 아닌 순수 TilemapCollider2D는 셀 스캔 방식이 여전히 맞다).
                var tm = col.GetComponent<Tilemap>();
                if (tm != null) AddTilemapEdges(tm, src, c, r);
                else AddBoundsEdges(col.bounds, src, c, r);
            }

            if (segments.Count >= MaxSegments) break;
        }
    }

    static readonly List<Vector2> _pathScratch = new List<Vector2>();

    /// <summary>CompositeCollider2D가 이미 갖고 있는 병합 폴리곤 경로를 그대로 선분으로 바꾼다(2026-08-03,
    /// Map1 실측 버그 수정 — 타일 배치가 어떻든 물리적으로 실제 이어진 모양 그대로 나온다).
    /// ⚠️ 실측으로 확인: GetPath()는(일반적인 "로컬 좌표" 문서 설명과 달리, 적어도 이 프로젝트의
    /// 회전 없는 Base 콜라이더 기준으로는) 이미 월드 좌표를 준다 — transform.TransformPoint를
    /// 추가로 적용하면 부모 체인의 6.25배 스케일이 다시 곱해져 실제 위치에서 100유닛 넘게 어긋난다
    /// (raw 그대로 쓸 때 플레이어와의 최소 거리 1.5유닛, TransformPoint를 씌우면 171유닛으로 확인).</summary>
    void AddCompositeColliderEdges(CompositeCollider2D composite, int src, Vector2 c, float r)
    {
        int pathCount = composite.pathCount;
        for (int p = 0; p < pathCount; p++)
        {
            _pathScratch.Clear();
            int n = composite.GetPath(p, _pathScratch);
            if (n < 2) continue;

            for (int i = 0; i < n; i++)
            {
                Vector2 a = _pathScratch[i];
                Vector2 b = _pathScratch[(i + 1) % n];
                AddEdge(a, b, src, c, r);
                if (segments.Count >= MaxSegments) return;
            }
        }
    }

    void AddTilemapEdges(Tilemap tm, int src, Vector2 c, float r)
    {
        // 셀 전체가 아니라 플레이어 주변 범위만 순회한다(큰 맵에서 cellBounds 전체는 낭비다).
        Vector3Int min = tm.WorldToCell(new Vector3(c.x - r, c.y - r, 0f));
        Vector3Int max = tm.WorldToCell(new Vector3(c.x + r, c.y + r, 0f));
        BoundsInt cb = tm.cellBounds;
        int x0 = Mathf.Max(min.x, cb.xMin), x1 = Mathf.Min(max.x, cb.xMax);
        int y0 = Mathf.Max(min.y, cb.yMin), y1 = Mathf.Min(max.y, cb.yMax);

        // ⚠️ tm.cellSize는 그리드 "로컬" 크기(이 씬에선 0.16)인데 CellToWorld는 월드 좌표를 준다
        //    — Grid 부모가 6.25배로 스케일돼 있어서 1타일이 월드 1유닛이다. 둘을 섞어 쓰면 셀 사각형이
        //    실제의 16%로 계산돼 변이 토막난다(실측으로 잡은 버그). 그래서 월드 셀 크기를 직접 잰다.
        Vector3 originW = tm.CellToWorld(Vector3Int.zero);
        Vector3 cs = tm.CellToWorld(new Vector3Int(1, 1, 0)) - originW;

        for (int x = x0; x <= x1; x++)
        {
            for (int y = y0; y <= y1; y++)
            {
                var p = new Vector3Int(x, y, 0);
                if (!tm.HasTile(p)) continue;

                Vector3 o = tm.CellToWorld(p);
                float lx = o.x, by = o.y, rx = o.x + cs.x, ty = o.y + cs.y;

                // 이웃이 비어 있는 변만 = 덩어리의 외곽선(전체 외곽선 — 사용자 선택)
                if (!tm.HasTile(new Vector3Int(x, y + 1, 0))) AddEdge(new Vector2(lx, ty), new Vector2(rx, ty), src, c, r);
                if (!tm.HasTile(new Vector3Int(x, y - 1, 0))) AddEdge(new Vector2(lx, by), new Vector2(rx, by), src, c, r);
                if (!tm.HasTile(new Vector3Int(x - 1, y, 0))) AddEdge(new Vector2(lx, by), new Vector2(lx, ty), src, c, r);
                if (!tm.HasTile(new Vector3Int(x + 1, y, 0))) AddEdge(new Vector2(rx, by), new Vector2(rx, ty), src, c, r);

                if (segments.Count >= MaxSegments) return;
            }
        }
    }

    void AddBoundsEdges(Bounds b, int src, Vector2 c, float r)
    {
        Vector2 bl = new Vector2(b.min.x, b.min.y);
        Vector2 br = new Vector2(b.max.x, b.min.y);
        Vector2 tl = new Vector2(b.min.x, b.max.y);
        Vector2 tr = new Vector2(b.max.x, b.max.y);
        AddEdge(tl, tr, src, c, r); // 윗면
        AddEdge(bl, br, src, c, r); // 아랫면
        AddEdge(bl, tl, src, c, r); // 좌
        AddEdge(br, tr, src, c, r); // 우
    }

    void AddEdge(Vector2 a, Vector2 b, int src, Vector2 c, float r)
    {
        float len = Vector2.Distance(a, b);
        if (len < 0.001f) return;
        int pieces = Mathf.Max(1, Mathf.RoundToInt(len / SegmentLength));
        for (int i = 0; i < pieces; i++)
        {
            Vector2 p0 = Vector2.Lerp(a, b, i / (float)pieces);
            Vector2 p1 = Vector2.Lerp(a, b, (i + 1) / (float)pieces);
            if (Vector2.Distance((p0 + p1) * 0.5f, c) > r) continue; // 긴 변은 이 단계에서 잘려 나간다
            if (IsDuplicateSegment(p0, p1)) continue; // 같은 자리 중복선 방지(아래 주석 참고)
            segments.Add(new Segment { a = p0, b = p1, source = src });
            if (segments.Count >= MaxSegments) return;
        }
    }

    // Ground(바닥)와 Wall(벽) 콜라이더가 같은 경계에서 만나면(벽이 바닥 가장자리에 딱 붙어 서 있는
    // 흔한 배치) terrainMask가 둘 다 훑기 때문에 같은 물리적 선을 두 콜라이더가 각자 따로 그려서
    // "시간이 지나도 안 없어지는 이중선"이 생겼다(사용자 스크린샷·확인, 2026-08-06 — 글리치로 인한
    // 일시적 밴드 어긋남과는 다름). 부동소수 오차만 흡수할 정도로 좁은 허용치로 이미 그려진 선분과
    // 겹치면 건너뛴다 — 방향(a→b vs b→a)은 상관없이 같은 자리로 본다.
    const float DupEps = 0.02f;
    bool IsDuplicateSegment(Vector2 p0, Vector2 p1)
    {
        for (int i = 0; i < segments.Count; i++)
        {
            var s = segments[i];
            if ((Vector2.Distance(s.a, p0) < DupEps && Vector2.Distance(s.b, p1) < DupEps) ||
                (Vector2.Distance(s.a, p1) < DupEps && Vector2.Distance(s.b, p0) < DupEps))
                return true;
        }
        return false;
    }

    // ── 글리치 스텝(12Hz) ──────────────────────────────────────────────────

    void BuildMesh()
    {
        verts.Clear(); uvs.Clear(); tris.Clear();
        AliveCount = 0;
        int sig = 17;

        Vector2 c = target != null ? (Vector2)target.position : Vector2.zero;

        // 이번 스텝의 "사건"을 먼저 뽑는다 — 조각마다 따로 흔들면 균일한 잡음이 되고, 띠 단위로
        // 같은 변위를 주면 화면이 한 줄씩 찢긴 것처럼 보인다(진짜 글리치의 핵심).
        bool blackout = Random.value < BlackoutChance;
        bool hasBand = !blackout && Random.value < BandChance;
        float bandY = hasBand ? c.y + Random.Range(-cullRadius * 0.6f, cullRadius * 0.6f) : 0f;
        float bandShift = hasBand ? Random.Range(-BandShiftMax, BandShiftMax) : 0f;
        float bandLift = hasBand ? Random.Range(-BandLiftMax, BandLiftMax) : 0f;

        if (!blackout)
        for (int i = 0; i < segments.Count; i++)
        {
            var s = segments[i];
            var col = sources[s.source];
            // 부서지는 발판은 col.enabled를 끄므로(CrumblingPlatform) 선도 같이 사라진다.
            if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;

            Vector2 mid = (s.a + s.b) * 0.5f;
            if (Vector2.Distance(mid, c) > cullRadius) continue;

            bool inBand = hasBand && Mathf.Abs(mid.y - bandY) < BandHeight * 0.5f;

            // ★ 결손을 스텝마다 재추첨한다 — 패턴이 고정되면 "계속 깨지는" 글리치가 아니다.
            if (Random.value > (inBand ? BandAliveChance : AliveChance)) continue;

            Vector2 shift = inBand ? new Vector2(bandShift, bandLift) : Vector2.zero;
            AddQuad(s.a + shift + Rand2(JitterAmp), s.b + shift + Rand2(JitterAmp));

            AliveCount++;
            sig = sig * 31 + i;
        }

        AliveSignature = sig;

        mesh.Clear();
        if (verts.Count > 0)
        {
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }
    }

    static Vector2 Rand2(float amp) => new Vector2(Random.Range(-amp, amp), Random.Range(-amp, amp));

    void AddQuad(Vector2 a, Vector2 b)
    {
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len < 0.0001f) return;
        Vector2 n = new Vector2(-d.y, d.x) / len * (LineThickness * 0.5f);

        int i0 = verts.Count;
        verts.Add(new Vector3(a.x + n.x, a.y + n.y, 0f));
        verts.Add(new Vector3(b.x + n.x, b.y + n.y, 0f));
        verts.Add(new Vector3(b.x - n.x, b.y - n.y, 0f));
        verts.Add(new Vector3(a.x - n.x, a.y - n.y, 0f));

        // _MainTex는 기본 흰색이라 어느 UV를 써도 알파 1 — 단색 선이 된다.
        var uv = new Vector2(0.5f, 0.5f);
        uvs.Add(uv); uvs.Add(uv); uvs.Add(uv); uvs.Add(uv);

        tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
        tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 3);
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (mat != null) Destroy(mat);
    }
}
