using UnityEngine;

// 적 스프라이트 테두리에 붉은 아웃라인을 씌워 "처형 가능" 신호를 보낸다(스펙 1).
// .shader/.mat 에셋은 건드리지 않고(이번 세션 에셋 편집 승인 범위 밖) 기존
// EnemyExecutionGlow.shader를 런타임에 _Flatten=1(단색 실루엣)로만 재사용한다.
// 그 단색 실루엣 복사본을 8방향으로 살짝 오프셋해 적 스프라이트 "뒤"에 깔면,
// 실제 스프라이트가 그 위를 그대로 덮어써서 원본 실루엣보다 튀어나온 테두리 링만 남는다
// (셰이더 없이도 되는 고전적인 2D 스프라이트 아웃라인 트릭). _BloomBoost는 1로 고정해
// HDR 오버브라이트를 끈다 — 예전엔 이 값이 커서 URP Bloom이 켜져 있어야만 제대로 보이는
// 흐릿한 붉은 실루엣이었는데, 이제는 Bloom 설정과 무관하게 항상 같은 붉은 아웃라인으로 보인다.
public class EnemyExecutionGlowFx : MonoBehaviour
{
    const string ShaderName = "Custom/EnemyExecutionGlow";
    const int RingCount = 8;
    // 스프라이트 텍스처의 PPU(예: 256)와 화면에 실제로 찍히는 픽셀 밀도(카메라 줌에 따라 다름)는
    // 서로 다르므로, 텍셀 단위가 아니라 월드 단위 고정값으로 둔다(안 그러면 고해상도 텍스처에서
    // 오프셋이 서브픽셀로 사라져 버린다 — 최초 구현에서 실제로 겪은 문제).
    // 0.15는 화면에서 너무 두꺼웠다(사용자 스크린샷 2026-08-01) → 0.06.
    const float OutlineThicknessWorld = 0.06f;

    static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
    static readonly int IdFlatten = Shader.PropertyToID("_Flatten");
    static readonly int IdBloomBoost = Shader.PropertyToID("_BloomBoost");

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

    // 페이드는 인/아웃 구분 없이 "현재 세기 → 목표 세기" 하나로 처리한다.
    // (예전엔 fadeIn/fadeOut 타이머를 따로 굴렸는데, 한 번 FadeOut에 들어가면 FadeIn이 무시돼
    //  커서를 뗐다 바로 다시 올리면 글로우가 되살아나지 않았다.)
    float intensity;
    float fadeFrom;
    float fadeTo;
    float fadeDuration = -1f; // <0이면 페이드 없음
    float fadeTimer;
    bool destroyWhenDone;

    /// <summary>
    /// 적에게 아웃라인을 붙인다. 이미 붙어 있으면 그것을 되살려 쓴다 —
    /// 커서를 왔다갔다 하면 페이드아웃 중인 아웃라인 위에 새 아웃라인이 계속 쌓여
    /// 같은 실루엣이 여러 겹 가산 합성되던(눈에 띄게 밝아지던) 문제를 막는다.
    /// </summary>
    public static EnemyExecutionGlowFx Attach(Transform owner, Material source, int sortingOffset)
    {
        var existing = owner.GetComponentInChildren<EnemyExecutionGlowFx>(true);
        if (existing != null) return existing;

        Shader sh = source != null ? source.shader : Shader.Find(ShaderName);
        if (sh == null)
        {
            Debug.LogWarning("[Execution] " + ShaderName + " 셰이더를 찾을 수 없어 적 아웃라인을 건너뜁니다.");
            return null;
        }

        var ownerSr = owner.GetComponent<SpriteRenderer>();
        if (ownerSr == null) return null;

        var go = new GameObject("EnemyExecutionGlowFx");
        go.transform.SetParent(owner, false);
        // 그레이스케일 확산 중에도 빛이 원색으로 남도록 보호 레이어에 올린다.
        int noGrayscaleLayer = LayerMask.NameToLayer("VFXNoGrayscale");
        if (noGrayscaleLayer >= 0) go.layer = noGrayscaleLayer;

        var fx = go.AddComponent<EnemyExecutionGlowFx>();
        fx.ownerSr = ownerSr;

        // 링은 반드시 실제 스프라이트보다 뒤에 있어야 트릭이 성립하므로, 넘어온 부호와 무관하게
        // 항상 owner보다 뒤(sortingOrder가 낮은 쪽)에 배치한다.
        int behindOffset = -Mathf.Max(1, Mathf.Abs(sortingOffset));

        for (int i = 0; i < RingCount; i++)
        {
            var ringGo = new GameObject("Ring" + i);
            ringGo.transform.SetParent(go.transform, false);
            if (noGrayscaleLayer >= 0) ringGo.layer = noGrayscaleLayer;

            var mat = source != null ? new Material(source) : new Material(sh);
            mat.SetFloat(IdIntensity, 0f);
            mat.SetFloat(IdFlatten, 1f);
            mat.SetFloat(IdBloomBoost, 1f);

            var r = ringGo.AddComponent<SpriteRenderer>();
            r.sharedMaterial = mat;
            r.sortingLayerID = ownerSr.sortingLayerID;
            r.sortingOrder = ownerSr.sortingOrder + behindOffset;

            fx.rings[i] = ringGo.transform;
            fx.ringSr[i] = r;
            fx.ringMat[i] = mat;
        }

        fx.SyncSprite();
        return fx;
    }

    /// <summary>현재 세기에서 목표까지 부드럽게 올린다(커서를 댔을 때).</summary>
    public void FadeIn(float target, float duration) => StartFade(target, duration, false);

    /// <summary>현재 세기에서 0까지 내린 뒤 스스로 파괴된다(커서를 뗐을 때).</summary>
    public void FadeOut(float duration) => StartFade(0f, duration, true);

    void StartFade(float target, float duration, bool destroyAtEnd)
    {
        fadeFrom = intensity;
        fadeTo = Mathf.Clamp01(target);
        fadeDuration = Mathf.Max(0.01f, duration);
        fadeTimer = 0f;
        destroyWhenDone = destroyAtEnd;
    }

    void LateUpdate()
    {
        if (ownerSr == null) { Destroy(gameObject); return; }

        SyncSprite();

        if (fadeDuration >= 0f)
        {
            fadeTimer += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(fadeTimer / fadeDuration);
            intensity = Mathf.Lerp(fadeFrom, fadeTo, k);
            if (k >= 1f)
            {
                fadeDuration = -1f;
                if (destroyWhenDone) { Destroy(gameObject); return; }
            }
        }

        float alpha = intensity * ownerSr.color.a;
        for (int i = 0; i < RingCount; i++) ringMat[i].SetFloat(IdIntensity, alpha);
    }

    void SyncSprite()
    {
        // 적의 localScale.x 반전에 대응(부모 트랜스폼 하나로 링 전부 같이 미러링된다)
        transform.localScale = new Vector3(ownerSr.flipX ? -1f : 1f, ownerSr.flipY ? -1f : 1f, 1f);

        for (int i = 0; i < RingCount; i++)
        {
            ringSr[i].sprite = ownerSr.sprite;
            ringSr[i].enabled = ownerSr.enabled;
            rings[i].localPosition = (Vector3)(RingDirections[i] * OutlineThicknessWorld);
        }
    }

    void OnDestroy()
    {
        for (int i = 0; i < RingCount; i++)
        {
            if (ringMat[i] != null) Destroy(ringMat[i]);
        }
    }
}
