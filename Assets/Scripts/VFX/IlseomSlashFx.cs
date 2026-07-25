using UnityEngine;

// 일섬 이동 궤적에 남는 섬광. 출발점→도착점을 잇는 쿼드 1개를 깔고,
// Custom/IlseomSlashStreak 셰이더의 _Progress로 선두를 훑어 "빠르게 지나갔다"를 만든다.
// 스윕이 끝나면 _Alpha를 내려 사라진다. 전부 unscaled 시간이라 히트스톱에 멈추지 않는다.
//
// DashAfterImage / IlseomChargeFx와 같은 컨벤션: 런타임 전용 오브젝트, 스스로 파괴된다.
public class IlseomSlashFx : MonoBehaviour
{
    const string ShaderName = "Custom/IlseomSlashStreak";

    Material mat;
    Mesh mesh;
    float age;
    float sweepDuration;
    float fadeDuration;

    static readonly int IdProgress = Shader.PropertyToID("_Progress");
    static readonly int IdAlpha = Shader.PropertyToID("_Alpha");
    static readonly int IdPixelStep = Shader.PropertyToID("_PixelStep");
    static readonly int IdSeed = Shader.PropertyToID("_Seed");

    /// <summary>
    /// from→to 궤적에 섬광을 깐다. sweep 동안 선두가 궤적을 훑고, 이어서 fade 동안 사라진다.
    /// </summary>
    /// <param name="height">궤적 두께(월드 단위).</param>
    /// <param name="pixelSize">도트 스냅 단위(월드 단위). 플레이어와 같은 1/32×스케일을 넣으면 톤이 맞는다.</param>
    public static IlseomSlashFx Spawn(Vector3 from, Vector3 to, float height, float pixelSize,
        Material source, float sweep, float fade, int sortingLayerID, int sortingOrder)
    {
        Vector3 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.01f) return null; // 이동이 없으면 그릴 궤적도 없음

        Shader sh = source != null ? source.shader : Shader.Find(ShaderName);
        if (sh == null)
        {
            Debug.LogWarning("[Ilseom] " + ShaderName + " 셰이더를 찾을 수 없어 궤적 섬광을 건너뜁니다.");
            return null;
        }

        var go = new GameObject("IlseomSlashFx");
        go.transform.position = (from + to) * 0.5f;
        // 수평 이동(일섬)일 때는 180도 회전(Z축 회전 시 위아래가 뒤집힘) 대신
        // SpriteRenderer.flipX처럼 localScale.x를 반전해 좌우만 미러링한다.
        // Glitch Sweep 시트가 기본적으로 왼쪽을 바라보므로, VFX도 왼쪽을 기본(scale 1)으로 맞춘다.
        if (Mathf.Abs(delta.y) < 0.001f)
        {
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(-Mathf.Sign(delta.x), 1f, 1f);
        }
        else
        {
            go.transform.rotation = Quaternion.FromToRotation(Vector3.left, delta.normalized);
        }

        var fx = go.AddComponent<IlseomSlashFx>();
        fx.Build(source, sh, length, height, pixelSize, sweep, fade, sortingLayerID, sortingOrder);
        return fx;
    }

    void Build(Material source, Shader sh, float length, float height, float pixelSize,
        float sweep, float fade, int sortingLayerID, int sortingOrder)
    {
        sweepDuration = Mathf.Max(0.01f, sweep);
        fadeDuration = Mathf.Max(0.01f, fade);

        mat = source != null ? new Material(source) : new Material(sh);
        mat.SetFloat(IdSeed, Random.Range(0f, 128f));
        mat.SetFloat(IdProgress, 0f);
        mat.SetFloat(IdAlpha, 1f);
        // 쿼드가 가로로 길어 UV당 월드 크기가 축마다 다르다 → 정사각 도트가 되도록 축별로 따로 넘긴다.
        mat.SetVector(IdPixelStep, new Vector4(pixelSize / length, pixelSize / height, 0f, 0f));

        mesh = BuildQuad(length, height);
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

    // uv.x = 0(출발)~1(도착), uv.y = 0~1(0.5가 중심선) — 셰이더의 UV 규약과 짝을 맞춘다.
    static Mesh BuildQuad(float length, float height)
    {
        float hx = length * 0.5f, hy = height * 0.5f;
        var m = new Mesh();
        m.name = "IlseomSlashStreak";
        m.vertices = new[]
        {
            new Vector3(-hx, -hy, 0f),
            new Vector3( hx, -hy, 0f),
            new Vector3( hx,  hy, 0f),
            new Vector3(-hx,  hy, 0f),
        };
        m.uv = new[]
        {
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
        };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        m.RecalculateBounds();
        return m;
    }

    void Update()
    {
        age += Time.unscaledDeltaTime;

        if (age <= sweepDuration)
        {
            mat.SetFloat(IdProgress, Mathf.Clamp01(age / sweepDuration));
            return;
        }

        mat.SetFloat(IdProgress, 1f);
        float k = Mathf.Clamp01((age - sweepDuration) / fadeDuration);
        mat.SetFloat(IdAlpha, 1f - k);
        if (k >= 1f) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
        if (mesh != null) Destroy(mesh);
    }
}
