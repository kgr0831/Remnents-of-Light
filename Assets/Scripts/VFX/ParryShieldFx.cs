using UnityEngine;

// 패링 성공 시 플레이어를 감싸는 구형(링) 실드. Custom/ParryShield 셰이더를 쿼드 1개에 물린다.
// 플레이어 자식으로 붙어 같이 움직이고, 적 공격을 1회 막으면 Break()로 유리처럼 깨진다.
//
// 부모 스케일(1.3)이 반지름·두께에 그대로 걸린다 — IlseomChargeFx와 같은 규약이라
// 반지름·오프셋을 "플레이어 로컬 단위"로 다루면 스프라이트와 크기가 자동으로 맞는다.
// 런타임 전용 오브젝트 — 씬에 저장되지 않는다.
public class ParryShieldFx : MonoBehaviour
{
    const string ShaderName = "Custom/ParryShield";

    static readonly int IdBreak = Shader.PropertyToID("_Break");
    static readonly int IdSeed = Shader.PropertyToID("_Seed");

    Material mat;
    Mesh mesh;
    SpriteRenderer ownerSr;     // 바라보는 방향에 따라 중심을 미러링하기 위해 참조
    Vector2 localOffset;

    float breakDuration;
    float breakTimer = -1f;     // <0 이면 아직 안 깨짐

    public bool IsBreaking => breakTimer >= 0f;

    /// <summary>플레이어 자식으로 실드를 붙인다. offset·radius는 플레이어 로컬 단위.</summary>
    public static ParryShieldFx Attach(Transform owner, Material source, float radius, Vector2 offset,
        float breakDuration, int sortingLayerID, int sortingOrder)
    {
        Shader sh = ResolveShader(source);
        if (sh == null) return null;

        var go = new GameObject("ParryShieldFx");
        go.transform.SetParent(owner, false);
        go.transform.localPosition = new Vector3(offset.x, offset.y, 0f);

        var fx = go.AddComponent<ParryShieldFx>();
        fx.ownerSr = owner.GetComponent<SpriteRenderer>();
        fx.localOffset = offset;
        fx.breakDuration = Mathf.Max(0.01f, breakDuration);
        fx.Build(source, sh, radius, sortingLayerID, sortingOrder);
        if (fx.ownerSr != null) fx.ApplyFollow();
        return fx;
    }

    static Shader ResolveShader(Material source)
    {
        Shader sh = source != null ? source.shader : Shader.Find(ShaderName);
        if (sh == null)
            Debug.LogWarning("[Parry] " + ShaderName + " 셰이더를 찾을 수 없어 실드 연출을 건너뜁니다.");
        return sh;
    }

    void Build(Material source, Shader sh, float radius, int sortingLayerID, int sortingOrder)
    {
        // 그레이스케일 확산(회피 카운터) 중에도 원색으로 남도록 보호 레이어에 올린다(DashAfterImage와 동일).
        int noGrayscaleLayer = LayerMask.NameToLayer("VFXNoGrayscale");
        if (noGrayscaleLayer >= 0) gameObject.layer = noGrayscaleLayer;

        // source를 복제하면 _Alpha·색·두께는 그대로 따라온다 — 여기선 인스턴스별로 달라야 하는 것만 덮는다.
        mat = source != null ? new Material(source) : new Material(sh);
        mat.SetFloat(IdSeed, Random.Range(0f, 128f));
        mat.SetFloat(IdBreak, 0f);

        // 셰이더가 링을 |p|=0.5에 놓으므로 쿼드 반너비는 반지름의 2배 — 남은 절반이 파편이 날아갈 공간.
        mesh = BuildQuad(radius * 2f);
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;

        var mr = gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.sortingLayerID = sortingLayerID;
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    static Mesh BuildQuad(float halfExtent)
    {
        var m = new Mesh();
        m.name = "ParryShield";
        m.vertices = new[]
        {
            new Vector3(-halfExtent, -halfExtent, 0f),
            new Vector3( halfExtent, -halfExtent, 0f),
            new Vector3( halfExtent,  halfExtent, 0f),
            new Vector3(-halfExtent,  halfExtent, 0f),
        };
        m.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f),
        };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        m.RecalculateBounds();
        return m;
    }

    /// <summary>적 공격을 막아낸 순간 호출 — 유리처럼 조각나 흩어지고 다 흩어지면 스스로 파괴된다.</summary>
    public void Break()
    {
        if (breakTimer >= 0f) return;
        breakTimer = 0f;
    }

    // 플레이어 실루엣 중심은 피봇에서 벗어나 있고, flipX로 좌우가 뒤집히면 그 중심도 같이 미러링된다.
    void ApplyFollow()
    {
        float dirX = (ownerSr != null && ownerSr.flipX) ? -1f : 1f;
        transform.localPosition = new Vector3(localOffset.x * dirX, localOffset.y, 0f);
    }

    void Update()
    {
        if (ownerSr != null) ApplyFollow();

        // 히트스톱(timeScale=0) 중에도 연출이 진행돼야 하므로 unscaled 시간을 쓴다.
        if (breakTimer < 0f) return;

        breakTimer += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(breakTimer / breakDuration);
        mat.SetFloat(IdBreak, k);
        if (k >= 1f) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
        if (mesh != null) Destroy(mesh);
    }
}
