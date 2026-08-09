using UnityEngine;

// 광원 픽셀 흡수/방출 연출 — 절차 생성 2x2 정사각 스프라이트(텍스처·프리팹 의존 0) 하나가
// 2차 베지어 궤적을 따라 이동한다. De Casteljau 보간에 이징된 t를 그대로 먹여
// 위치 보간과 ease-in 속도를 한 번에 해결한다(궤적=베지어, 속도=t의 제곱).
// 발광은 기존 Custom/PlayerBloomOverlay 셰이더(가산 블렌드)를 그대로 재사용한다 — 새 셰이더/머티리얼
// 에셋을 만들지 않는다(VFX 공통 규약 3). 이 셰이더는 SpriteRenderer.color가 아니라 머티리얼 _Intensity로
// 세기를 제어하므로, 알파 대신 그 값을 애니메이션해 페이드한다.
// DashAfterImage와 같은 컨벤션: 런타임 전용, 씬에 저장되지 않는다.
public class LightPixelFx : MonoBehaviour
{
    const string BloomShaderName = "Custom/PlayerBloomOverlay";
    static readonly Color LightColor = new Color(126f / 255f, 191f / 255f, 198f / 255f);
    static readonly int IdColor = Shader.PropertyToID("_Color");
    static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
    static readonly int IdBloomBoost = Shader.PropertyToID("_BloomBoost");

    static Sprite _pixelSprite;

    SpriteRenderer sr;
    Material mat;
    Vector3 start, control, fixedEnd;
    Transform trackTarget;      // null이 아니면 매 프레임 이 트랜스폼의 SpriteRenderer 기준 피봇을 끝점으로 추적
    Vector2 trackPivotOffset;   // trackTarget 추적 시 함께 적용할 오프셋(흡수 전용, ComputePivot에 그대로 전달)
    float duration, age;
    int amount;
    System.Action<int> onArrive;
    bool fadeOnArrive;

    /// <summary>흡수/방출이 모이고 흩어지는 기준점 — 스프라이트 중심(bounds.center)에서 offset만큼
    /// 치우친 지점. flipX에 따라 x가 미러링된다(PlayerController.GatherOffset과 같은 패턴).</summary>
    public static Vector3 ComputePivot(SpriteRenderer sr, Vector2 offset)
    {
        if (sr == null) return Vector3.zero;
        float dirX = sr.flipX ? -1f : 1f;
        return sr.bounds.center + new Vector3(offset.x * dirX, offset.y, 0f);
    }

    /// <summary>흡수: 적/오브젝트 몸 주위 여러 방향(원형 스캐터, 반경 sourceRadius)에서 튀어나와
    /// 플레이어(추적, pivotOffset만큼 치우친 지점)로 모여든다. 총량을 픽셀 개수(3~10)로 나눠 도착마다
    /// 나눠서 onArrivePixel로 알린다(게이지가 또르르 차오르게). color 미지정 시 기존 LightColor.</summary>
    public static void SpawnAbsorb(Vector3 centerPos, Transform player, int totalAmount,
        System.Action<int> onArrivePixel, float sourceRadius = 0.5f, Vector2 pivotOffset = default,
        Color? color = null)
    {
        if (totalAmount <= 0) return;
        int count = Mathf.Clamp(totalAmount, 3, 10);
        int baseShare = totalAmount / count;
        int remainder = totalAmount - baseShare * count;
        var playerSr = player != null ? player.GetComponent<SpriteRenderer>() : null;
        Vector3 playerPos = playerSr != null ? ComputePivot(playerSr, pivotOffset)
            : (player != null ? player.position : centerPos);

        for (int i = 0; i < count; i++)
        {
            int share = baseShare + (i == count - 1 ? remainder : 0);
            if (share <= 0) continue;

            // 원형 스캐터: 적 몸 주위 여러 방향에서 각각 다른 지점을 시작점으로 삼는다("여러 방향에서 튀어나옴").
            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
            dir.Normalize();
            Vector3 startPos = centerPos + (Vector3)(dir * Random.Range(sourceRadius * 0.6f, sourceRadius * 1.1f));

            Vector3 ctrl = Vector3.Lerp(startPos, playerPos, 0.5f) + Vector3.up * Random.Range(0.8f, 1.6f);
            Spawn(startPos, ctrl, player, Vector3.zero, Random.Range(0.35f, 0.55f), share, onArrivePixel,
                fadeOnArrive: false, trackPivotOffset: pivotOffset, color: color ?? LightColor);
        }
    }

    /// <summary>방출: 플레이어 쪽 기준점(고정 시작) → 근처 랜덤 지점(고정), 콜백 없음(장식용, 페이드아웃).
    /// 초당 12~20개 정도로 호출자가 직접 틱을 굴려 반복 호출한다(DashAfterImage의 간격 스폰과 동일 패턴).
    /// color 미지정 시 기존 LightColor.</summary>
    public static void SpawnEmitOne(Vector3 originCenter, Color? color = null)
    {
        Vector2 dir = Random.insideUnitCircle;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up;
        dir.Normalize();
        Vector3 end = originCenter + (Vector3)(dir * Random.Range(0.8f, 1.8f)) + Vector3.up * Random.Range(-0.2f, 0.6f);
        Vector3 ctrl = Vector3.Lerp(originCenter, end, 0.5f) + Vector3.up * Random.Range(0.2f, 0.5f);
        Spawn(originCenter, ctrl, null, end, Random.Range(0.3f, 0.5f), 0, null, fadeOnArrive: true, trackPivotOffset: default, color: color ?? LightColor);
    }

    /// <summary>위로 천천히 떠오르며 사라지는 픽셀 — 초월 상시 연출 전용(사용자 지시 2026-08-02).
    /// SpawnEmitOne(짧고 옆으로 흩어지는 방출)과 달리 상승폭이 크고(riseHeight) 오래 걸린다(1.1~1.6s).</summary>
    public static void SpawnRiseOne(Vector3 originCenter, Color color, float riseHeight = 1.2f, float horizontalJitter = 0.35f)
    {
        Vector3 end = originCenter + Vector3.up * riseHeight + Vector3.right * Random.Range(-horizontalJitter, horizontalJitter);
        Vector3 ctrl = Vector3.Lerp(originCenter, end, 0.5f) + Vector3.right * Random.Range(-horizontalJitter * 0.5f, horizontalJitter * 0.5f);
        Spawn(originCenter, ctrl, null, end, Random.Range(1.1f, 1.6f), 0, null, fadeOnArrive: true, trackPivotOffset: default, color: color);
    }

    static void Spawn(Vector3 start, Vector3 control, Transform trackTarget, Vector3 fixedEnd,
        float duration, int amount, System.Action<int> onArrive, bool fadeOnArrive, Vector2 trackPivotOffset,
        Color color)
    {
        var go = new GameObject("LightPixelFx");
        int noGrayscaleLayer = LayerMask.NameToLayer("VFXNoGrayscale");
        if (noGrayscaleLayer >= 0) go.layer = noGrayscaleLayer;
        go.transform.position = start;

        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = GetPixelSprite();
        r.sortingOrder = 15; // 플레이어(10)·타일맵보다 앞

        Material mat = BuildBloomMaterial(color);
        if (mat != null) r.sharedMaterial = mat;
        else r.color = color; // 셰이더를 못 찾았을 때의 폴백(기본 Sprites-Default로 평범하게라도 보이게)

        var fx = go.AddComponent<LightPixelFx>();
        fx.sr = r;
        fx.mat = mat;
        fx.start = start;
        fx.control = control;
        fx.trackTarget = trackTarget;
        fx.trackPivotOffset = trackPivotOffset;
        fx.fixedEnd = fixedEnd;
        fx.duration = Mathf.Max(0.01f, duration);
        fx.amount = amount;
        fx.onArrive = onArrive;
        fx.fadeOnArrive = fadeOnArrive;
    }

    // 새 셰이더/머티리얼 에셋을 만들지 않는다 — 일섬·광원소모 블룸이 이미 쓰는 가산 셰이더를
    // Shader.Find로 그대로 가져와 이 픽셀 전용 런타임 머티리얼만 인스턴스화한다.
    static Material BuildBloomMaterial(Color color)
    {
        Shader sh = Shader.Find(BloomShaderName);
        if (sh == null)
        {
            Debug.LogWarning("[LightPixelFx] " + BloomShaderName + " 셰이더를 찾을 수 없어 기본 스프라이트로 대체합니다.");
            return null;
        }
        var m = new Material(sh);
        m.SetColor(IdColor, color);
        m.SetFloat(IdBloomBoost, 5f); // 사용자 요청으로 더 강하게(기존 3 → 5, 셰이더 상한 8)
        m.SetFloat(IdIntensity, 1f);
        return m;
    }

    // 사용자 지시(2026-08-02): "원 형태 대신 픽셀로" — 부드러운 원형 낙차 대신 각진 사각 블록 하나로.
    // Point 필터(하드엣지, 안티에일리어싱 없음)라 확대해도 계단현상 없이 또렷한 "픽셀 한 알"로 읽힌다.
    // 이 스프라이트는 SpawnAbsorb·SpawnEmitOne·SpawnRiseOne 전부가 공유하므로 이 한 곳만 고치면
    // 모든 픽셀 VFX(광원 획득·방출·초월 상승)가 한 번에 바뀐다.
    static Sprite GetPixelSprite()
    {
        if (_pixelSprite != null) return _pixelSprite;
        const int size = 8; // 낮은 해상도라 확대했을 때 블록 느낌이 더 또렷하다
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point; // 각진 픽셀 — Bilinear면 다시 둥글게 뭉개진다
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white; // 꽉 찬 사각형, 페이드 없음
        tex.SetPixels(pixels);
        tex.Apply();
        _pixelSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        return _pixelSprite;
    }

    // 히트스톱·슬로우모션 중에도 픽업이 정상 속도로 날아가야 하므로 unscaled.
    void Update()
    {
        age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(age / duration);
        float eased = t * t; // ease-in: 처음엔 느리게 떠오르다 도착 근처에서 빨라짐

        Vector3 end = trackTarget != null ? TrackedCenter() : fixedEnd;
        Vector3 p0 = Vector3.Lerp(start, control, eased);
        Vector3 p1 = Vector3.Lerp(control, end, eased);
        transform.position = Vector3.Lerp(p0, p1, eased);

        // 이 셰이더는 가산 블렌드라 SpriteRenderer.color가 아니라 _Intensity로 세기(=페이드)를 제어한다.
        if (mat != null && fadeOnArrive) mat.SetFloat(IdIntensity, Mathf.Lerp(1f, 0f, t));

        if (t >= 1f)
        {
            onArrive?.Invoke(amount);
            Destroy(gameObject);
        }
    }

    Vector3 TrackedCenter()
    {
        var trSr = trackTarget.GetComponent<SpriteRenderer>();
        return trSr != null ? ComputePivot(trSr, trackPivotOffset) : trackTarget.position;
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
