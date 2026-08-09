using UnityEngine;

// 일섬 차지 픽셀 연출의 런타임 홀더. Custom/IlseomChargePixels 셰이더에 넘길 메시(픽셀 하나 = 쿼드 1개)를
// 코드로 만들어 플레이어의 자식으로 붙인다. 위치·알파 애니메이션은 전부 버텍스 셰이더가 하므로
// 여기서는 _Progress / _Alpha / _Burst 세 값만 갱신한다.
//
// 플레이어의 자식이라 로컬 공간 = 플레이어 기준 공간이 되고, 부모 스케일(1.3)이 픽셀 크기에도 같이
// 걸려서 플레이어 스프라이트의 도트 크기와 자동으로 맞는다(둘 다 PPU 32 기준).
// DashAfterImage와 같은 컨벤션: 런타임 전용 오브젝트, 스스로 파괴된다.
public class IlseomChargeFx : MonoBehaviour
{
    const string ShaderName = "Custom/IlseomChargePixels";

    Material mat;
    MeshRenderer mr;
    Mesh mesh;
    Vector2 targetOffset;

    // 페이드아웃(취소/발동) 진행 상태. unscaled 시간을 쓰므로 히트스톱·슬로우모션에 멈추지 않는다.
    bool fading;
    float fadeAge;
    float fadeDuration;
    bool fadeWithBurst;

    static readonly int IdCount = Shader.PropertyToID("_Count");
    static readonly int IdProgress = Shader.PropertyToID("_Progress");
    static readonly int IdFlow = Shader.PropertyToID("_Flow");
    static readonly int IdAlpha = Shader.PropertyToID("_Alpha");
    static readonly int IdBurst = Shader.PropertyToID("_Burst");
    static readonly int IdTarget = Shader.PropertyToID("_Target");
    static readonly int IdSeed = Shader.PropertyToID("_Seed");

    /// <summary>플레이어에 픽셀 연출을 붙인다. source가 null이면 셰이더에서 머티리얼을 직접 만든다.</summary>
    public static IlseomChargeFx Attach(Transform parent, Material source, int pixelCount,
        Vector2 gatherOffset, int sortingLayerID, int sortingOrder)
    {
        Shader sh = source != null ? source.shader : Shader.Find(ShaderName);
        if (sh == null)
        {
            Debug.LogWarning("[Ilseom] " + ShaderName + " 셰이더를 찾을 수 없어 픽셀 연출을 건너뜁니다.");
            return null;
        }

        var go = new GameObject("IlseomChargeFx");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var fx = go.AddComponent<IlseomChargeFx>();
        fx.Build(source, sh, Mathf.Max(1, pixelCount), gatherOffset, sortingLayerID, sortingOrder);
        return fx;
    }

    void Build(Material source, Shader sh, int pixelCount, Vector2 gatherOffset,
        int sortingLayerID, int sortingOrder)
    {
        mat = source != null ? new Material(source) : new Material(sh);
        mat.SetFloat(IdSeed, Random.Range(0f, 128f));
        mat.SetFloat(IdCount, pixelCount); // 셰이더가 링 각도를 균등 분할하는 데 쓴다 — 메시 개수와 반드시 일치
        mat.SetFloat(IdFlow, 0f);
        mat.SetFloat(IdProgress, 0f);
        mat.SetFloat(IdAlpha, 1f);
        mat.SetFloat(IdBurst, 0f);

        mesh = BuildPixelMesh(pixelCount);
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;

        mr = gameObject.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.sortingLayerID = sortingLayerID;
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        SetGatherOffset(gatherOffset);
    }

    // 픽셀 하나당 쿼드 1개. 정점 위치는 셰이더가 전부 다시 쓰므로 0으로 두고,
    // corner(uv) + 파티클 인덱스(uv2)만 실어 보낸다. 대신 정점이 원점에 몰려 있으면
    // 바운즈가 0이 되어 프러스텀 컬링에 잘려버리므로 바운즈는 직접 넉넉하게 지정한다.
    static Mesh BuildPixelMesh(int count)
    {
        var verts = new Vector3[count * 4];
        var corners = new Vector2[count * 4];
        var ids = new Vector2[count * 4];
        var tris = new int[count * 6];

        for (int i = 0; i < count; i++)
        {
            int v = i * 4;
            corners[v + 0] = new Vector2(-0.5f, -0.5f);
            corners[v + 1] = new Vector2(0.5f, -0.5f);
            corners[v + 2] = new Vector2(0.5f, 0.5f);
            corners[v + 3] = new Vector2(-0.5f, 0.5f);
            for (int k = 0; k < 4; k++) ids[v + k] = new Vector2(i, 0f);

            int t = i * 6;
            tris[t + 0] = v + 0; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
            tris[t + 3] = v + 0; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
        }

        var m = new Mesh();
        m.name = "IlseomChargePixels";
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
        m.vertices = verts;
        m.uv = corners;   // TEXCOORD0
        m.uv2 = ids;      // TEXCOORD1
        m.triangles = tris;
        m.bounds = new Bounds(Vector3.zero, new Vector3(8f, 8f, 1f));
        return m;
    }

    /// <summary>수집 지점(플레이어 로컬 오프셋). 바라보는 방향이 바뀌면 매 프레임 갱신된다.</summary>
    public void SetGatherOffset(Vector2 offset)
    {
        targetOffset = offset;
        if (mat != null) mat.SetVector(IdTarget, new Vector4(offset.x, offset.y, 0f, 0f));
    }

    /// <summary>
    /// 차지 상태 갱신. elapsedSeconds는 픽셀이 링→수집점을 계속 순환하게 하는 시계(_Flow),
    /// progress01은 세기(참여하는 픽셀 수)를 결정한다. 둘을 분리해야 완충 후에도 흐름이 멈추지 않는다.
    /// </summary>
    public void SetCharge(float elapsedSeconds, float progress01)
    {
        if (mat == null || fading) return;
        mat.SetFloat(IdFlow, elapsedSeconds);
        mat.SetFloat(IdProgress, Mathf.Clamp01(progress01));
    }

    /// <summary>취소: 픽셀들이 왔던 방향으로 터져나가며 사라진다(유리 깨지는 연출).</summary>
    public void PlayCancel(float duration)
    {
        StartFade(duration, true);
    }

    /// <summary>발동: 모인 픽셀이 그 자리에서 페이드 아웃 된다(터지지 않음).</summary>
    public void PlayFinish(float duration)
    {
        StartFade(duration, false);
    }

    void StartFade(float duration, bool withBurst)
    {
        if (fading) return;
        fading = true;
        fadeAge = 0f;
        fadeDuration = Mathf.Max(0.01f, duration);
        fadeWithBurst = withBurst;
    }

    void Update()
    {
        if (!fading) return;

        fadeAge += Time.unscaledDeltaTime;
        float k = Mathf.Clamp01(fadeAge / fadeDuration);
        if (mat != null)
        {
            mat.SetFloat(IdAlpha, 1f - k);
            if (fadeWithBurst) mat.SetFloat(IdBurst, k);
        }
        if (k >= 1f) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
        if (mesh != null) Destroy(mesh);
    }
}
