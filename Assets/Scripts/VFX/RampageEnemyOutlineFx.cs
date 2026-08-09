using UnityEngine;

// 폭주 시야 제한(B-2 ②): 어둠 속에서 적을 "붉은 테두리 + 어두운 속"의 실루엣으로 보이게 한다.
//
// 기법은 처형 아웃라인(EnemyExecutionGlowFx)과 같은 8방향 오프셋 트릭이지만, 코어 한 장이 더 있다.
// ⚠️ 왜 코어가 필요한가: 처형 아웃라인은 "진짜 적 스프라이트가 링 위를 덮는다"를 전제로 링만 깐다.
//    그런데 폭주 어둠에서는 적 스프라이트가 암전에 눌려 있고 링만 보호 레이어로 덧그려지므로,
//    가운데를 덮어 줄 것이 없어 테두리가 아니라 "밝은 덩어리"가 된다.
//    그래서 같은 스프라이트를 어두운 색으로 한 장(코어) 링보다 앞에 깔아 속을 막는다.
// 처형용 코드는 회귀를 막기 위해 건드리지 않고 구조만 복제했다.
public class RampageEnemyOutlineFx : MonoBehaviour
{
    const string ShaderName = "Custom/RampageOutline";
    const int RingCount = 8;
    // 두께는 월드 단위. 텍셀(PPU) 단위로 잡으면 카메라 줌에 따라 서브픽셀로 사라진다(처형 때 겪은 버그).
    // 0.12는 화면에서 너무 두꺼웠다(사용자 스크린샷) → 0.05.
    const float OutlineThicknessWorld = 0.045f;

    // HDR 색 — 씬 Bloom(threshold 1.15)이 잡아 테두리 자체가 발광한다. 주위를 비추지는 않는다.
    // 3.0은 블룸 헤일로가 너무 퍼져 "선이 두껍다"는 인상을 줬다(사용자 스크린샷) → 1.9로 낮춰 halo를 조인다.
    static readonly Color RingColor = new Color(1.9f, 0.16f, 0.12f, 1f);
    // 속은 거의 검정 — 화면이 완전 암전이라 이보다 밝으면 실루엣이 덩어리로 보인다.
    static readonly Color CoreColor = new Color(0.03f, 0.008f, 0.008f, 1f);

    // 글리치(사용자 지시: "적 아웃라인에도 노이즈 효과"). 지형 아웃라인과 같은 20Hz 스텝을 쓴다.
    const float StepInterval = 1f / 20f;
    const float RingDropChance = 0.22f;  // 스텝마다 이 확률로 각 방향 링이 빠져 테두리가 끊긴다
    const float JitterAmp = 0.03f;       // 전체가 미세하게 떨린다
    const float TearChance = 0.18f;      // 이 확률로 한 스텝 동안 수평으로 찢긴다
    const float TearMax = 0.22f;

    static readonly int IdColor = Shader.PropertyToID("_Color");

    static readonly Vector2[] RingDirections =
    {
        new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
        new Vector2(0.7071f, 0.7071f), new Vector2(-0.7071f, 0.7071f),
        new Vector2(0.7071f, -0.7071f), new Vector2(-0.7071f, -0.7071f),
    };

    SpriteRenderer ownerSr;
    readonly Transform[] rings = new Transform[RingCount];
    readonly SpriteRenderer[] ringSr = new SpriteRenderer[RingCount];
    readonly Material[] ringMat = new Material[RingCount];
    SpriteRenderer coreSr;
    Material coreMat;

    float alpha = 1f;
    float stepTimer, tear;
    Vector2 jitter;
    readonly bool[] ringDropped = new bool[RingCount];

    /// <summary>적에게 폭주 아웃라인을 붙인다. 이미 붙어 있으면 그것을 재사용한다.</summary>
    public static RampageEnemyOutlineFx Attach(Transform owner)
    {
        if (owner == null) return null;

        // ⚠️ Destroy()는 프레임 끝에 실제로 지워지므로, 같은 프레임에 재부착하면 "지워지는 중"인
        //    컴포넌트를 그대로 돌려주게 된다(그러면 다음 프레임 Sync에서 링이 이미 없어 NRE). 코어가
        //    살아 있는 것만 재사용한다.
        var existing = owner.GetComponentInChildren<RampageEnemyOutlineFx>(true);
        if (existing != null && existing.coreSr != null) return existing;

        var ownerSr = owner.GetComponent<SpriteRenderer>();
        if (ownerSr == null) return null;

        Shader sh = Shader.Find(ShaderName);
        if (sh == null)
        {
            Debug.LogWarning("[RampageVision] " + ShaderName + " 셰이더를 찾을 수 없어 적 아웃라인을 건너뜁니다.");
            return null;
        }

        var go = new GameObject("RampageEnemyOutlineFx");
        go.transform.SetParent(owner, false);
        int protectedLayer = LayerMask.NameToLayer("VFXNoGrayscale");
        if (protectedLayer >= 0) go.layer = protectedLayer;

        var fx = go.AddComponent<RampageEnemyOutlineFx>();
        fx.ownerSr = ownerSr;

        // 링은 적 스프라이트보다 앞(+1), 코어는 링보다 더 앞(+2)에 둔다.
        // 어둠 속에선 적 스프라이트 자체가 눌려 있어 뒤에 깔아 봐야 아무것도 덮어 주지 못한다.
        for (int i = 0; i < RingCount; i++)
        {
            var ringGo = new GameObject("Ring" + i);
            ringGo.transform.SetParent(go.transform, false);
            if (protectedLayer >= 0) ringGo.layer = protectedLayer;

            var mat = new Material(sh);
            mat.SetColor(IdColor, RingColor);

            var r = ringGo.AddComponent<SpriteRenderer>();
            r.sharedMaterial = mat;
            r.sortingLayerID = ownerSr.sortingLayerID;
            r.sortingOrder = ownerSr.sortingOrder + 1;

            fx.rings[i] = ringGo.transform;
            fx.ringSr[i] = r;
            fx.ringMat[i] = mat;
        }

        var coreGo = new GameObject("Core");
        coreGo.transform.SetParent(go.transform, false);
        if (protectedLayer >= 0) coreGo.layer = protectedLayer;
        fx.coreMat = new Material(sh);
        fx.coreMat.SetColor(IdColor, CoreColor);
        fx.coreSr = coreGo.AddComponent<SpriteRenderer>();
        fx.coreSr.sharedMaterial = fx.coreMat;
        fx.coreSr.sortingLayerID = ownerSr.sortingLayerID;
        fx.coreSr.sortingOrder = ownerSr.sortingOrder + 2;

        fx.Sync();
        return fx;
    }

    /// <summary>진입·해제 페이드에 맞춰 아웃라인도 같이 나타나고 사라진다.</summary>
    public void SetAlpha(float a) => alpha = Mathf.Clamp01(a);

    public void Detach()
    {
        if (this != null) Destroy(gameObject);
    }

    void LateUpdate()
    {
        if (ownerSr == null) { Destroy(gameObject); return; }

        stepTimer += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        if (stepTimer >= StepInterval)
        {
            stepTimer = 0f;
            RollGlitch(); // 결손·지터·찢김은 스텝마다만 다시 뽑는다(매 프레임이면 너무 빨라 모래처럼 보인다)
        }
        Sync();
    }

    // 이번 스텝의 글리치 상태를 뽑는다. 링을 통째로 빼면 테두리에 구멍이 생겨 "끊긴 윤곽"이 된다.
    void RollGlitch()
    {
        for (int i = 0; i < RingCount; i++) ringDropped[i] = Random.value < RingDropChance;
        jitter = new Vector2(Random.Range(-JitterAmp, JitterAmp), Random.Range(-JitterAmp, JitterAmp));
        tear = Random.value < TearChance ? Random.Range(-TearMax, TearMax) : 0f;
    }

    void Sync()
    {
        // 적의 flipX에 대응(부모 하나로 링·코어가 같이 미러링된다)
        transform.localScale = new Vector3(ownerSr.flipX ? -1f : 1f, ownerSr.flipY ? -1f : 1f, 1f);
        transform.localPosition = new Vector3(jitter.x, jitter.y, 0f);

        float a = alpha * ownerSr.color.a;
        bool visible = ownerSr.enabled && a > 0.001f;

        for (int i = 0; i < RingCount; i++)
        {
            // 파괴 중인 오브젝트를 재사용하는 경합에서 링이 먼저 사라질 수 있다(실측 NRE) → 방어.
            if (ringSr[i] == null || rings[i] == null || ringMat[i] == null) continue;
            ringSr[i].sprite = ownerSr.sprite;
            ringSr[i].enabled = visible && !ringDropped[i];
            // 위/아래 방향 링만 찢김(수평 변위)을 받는다 — 좌우 링까지 밀면 테두리가 통째로 이동해 보인다.
            float tx = Mathf.Abs(RingDirections[i].y) > 0.5f ? tear : 0f;
            rings[i].localPosition = (Vector3)(RingDirections[i] * OutlineThicknessWorld + new Vector2(tx, 0f));
            var c = RingColor; c.a = a;
            ringMat[i].SetColor(IdColor, c);
        }

        if (coreSr == null || coreMat == null) { Destroy(gameObject); return; }
        coreSr.sprite = ownerSr.sprite;
        coreSr.enabled = visible;
        var cc = CoreColor; cc.a = a;
        coreMat.SetColor(IdColor, cc);
    }

    void OnDestroy()
    {
        for (int i = 0; i < RingCount; i++)
            if (ringMat[i] != null) Destroy(ringMat[i]);
        if (coreMat != null) Destroy(coreMat);
    }
}
