using System.Collections.Generic;
using UnityEngine;

// 초월(Transcendence) 공격 예고 — T-3b (docs/dev/TRANSCENDENCE_PLAN.md §6).
// 적 1마리의 다음 공격 판정을 시각화한다. DummyEnemy.AttackHitPointBase/AttackHitPoint/AttackHitRadius
// (실제 피격 판정에 쓰이는 그 캡슐)를 그대로 그리므로 "보이는 것과 맞는 것이 다르다"는 부류의 버그가
// 구조적으로 불가능하다.
//
// ⚠️ 사용자 지시(2026-08-02): "원이 아니라 실제 공격 범위와 같게" — 창끝 한 점(원)이 아니라 창을 든
// 위치(밑동)부터 창끝까지 훑는 캡슐(스타디움) 모양으로 바뀌었다. DummyEnemy의 실제 피격 판정도
// 같은 날 캡슐로 확장됐다(FindPlayerAtHitPoint) — 이 파일은 그 결과를 그대로 그린다.
//
// RampageEnemyOutlineFx와 같은 부착/SetAlpha/Detach/OnDestroy 구조를 따르되, 적 실루엣이 아니라
// 절차 생성 캡슐(링+채움)을 그린다는 점이 다르다. ⚠️ Custom/RampageOutline 셰이더의 두 번째 소비처다 —
// 이름은 폭주용이지만 "알파 마스크 + HDR 단색 출력"이 정확히 필요한 동작이라 신규 셰이더 없이 재사용한다.
// (셰이더 파일 자체는 안 건드림 — 이름 변경은 별도 승인이 필요한 리팩터라 하지 않는다.)
public class AttackTelegraphFx : MonoBehaviour
{
    const string ShaderName = "Custom/RampageOutline";
    const float TexDensity = 128f; // 반지름 방향 텍셀 밀도(px/world unit) — 원 시절 TexSize=128와 동일 해상도
    // 사용자 지시(2026-08-02): "더 크게 표시" — 반지름(=실제 판정 범위)은 그대로 두고 링만 두껍고
    // 진하게 키운다. 반지름을 키우면 시각과 실제 피격 판정이 어긋나 이 시각화의 존재 이유(정확한 예고)가
    // 무너지므로, RampageEnemyOutlineFx.OutlineThicknessWorld(0.045) 대비 2배로만 두께를 키웠다.
    const float RingThicknessWorld = 0.09f;
    const float FillAlpha = 0.22f;    // 캡슐 안의 적·플레이어가 가려지면 안 된다(PLAN §6)
    const float BurstDuration = 0.10f;
    const float BurstScale = 1.25f;
    const float FizzleDuration = 0.12f;

    // HDR 주황빛 적색 — 위험 표시의 관습(PLAN §6 초안값). 링을 두껍게 키운 것과 같은 지시로 세기도
    // 올렸다(기존 대비 RGB ×1.3) — 씬 Bloom(1.15)이 잡아 링이 더 진하게 발광한다.
    static readonly Color TelegraphColor = new Color(2.6f, 0.455f, 0.156f, 1f);
    static readonly int IdColor = Shader.PropertyToID("_Color");

    // (반지름, 길이)를 0.01 단위로 반올림한 정수 쌍으로 캐싱 — 이 프로젝트의 적 종류가 하나뿐이라
    // 사실상 항상 같은 키로 재사용되지만, 반지름·창 길이가 다른 적이 추가돼도 새로 구워질 뿐 안전하다.
    static readonly Dictionary<(int r, int len), (Sprite ring, Sprite fill)> spriteCache = new();

    DummyEnemy owner;
    SpriteRenderer ringSr, fillSr;
    Material ringMat, fillMat;
    float alpha = 1f;
    float radius, length;

    bool resolving;         // 예고가 끝나 터짐/흐지부지 애니메이션이 진행 중인가
    float resolveTimer;
    bool resolvedNeutralized;

    /// <summary>적에게 공격 예고 캡슐을 붙인다. 월드 오브젝트라 적의 자식이 아니다 — 적이 파괴돼도
    /// 좌표가 튀지 않는다(PLAN §6). 호출부(TranscendVisionFx)가 중복 부착을 막는다.</summary>
    public static AttackTelegraphFx Attach(DummyEnemy owner)
    {
        if (owner == null) return null;

        Shader sh = Shader.Find(ShaderName);
        if (sh == null)
        {
            Debug.LogWarning("[AttackTelegraphFx] " + ShaderName + " 셰이더를 찾을 수 없어 예고를 건너뜁니다.");
            return null;
        }

        float radius = owner.AttackHitRadius;
        Vector2 basePt = owner.AttackHitPointBase;
        Vector2 tipPt = owner.AttackHitPoint;
        float length = Vector2.Distance(basePt, tipPt);
        var (ringSprite, fillSprite) = GetOrBuildSprites(radius, length);

        var go = new GameObject("AttackTelegraphFx");
        int protectedLayer = LayerMask.NameToLayer("VFXNoGrayscale"); // 저스트 닷지 흑백 중에도 색 유지
        if (protectedLayer >= 0) go.layer = protectedLayer;

        var fx = go.AddComponent<AttackTelegraphFx>();
        fx.owner = owner;
        fx.radius = radius;
        fx.length = length;

        var ownerSr = owner.GetComponent<SpriteRenderer>();
        int sortingLayer = ownerSr != null ? ownerSr.sortingLayerID : 0;
        // 사용자 지시(2026-08-02): "적 공격 예측 범위가 플레이어 위에 표시되어야 합니다" — 예전엔
        // ownerSr(적) 기준 +1(=6)이라, 플레이어(sortingOrder=10)와 겹치면 플레이어 스프라이트에
        // 가려졌다(위험 구역을 보여주는 게 목적인데 정작 그 구역에 서 있으면 안 보이는 문제).
        // LightPixelFx가 이미 쓰는 "플레이어(10)·타일맵보다 앞" 고정값(15)과 동일한 값으로 맞춘다.
        const int TelegraphSortingOrder = 15;
        int sortingOrder = TelegraphSortingOrder;

        var ringGo = new GameObject("Ring");
        ringGo.transform.SetParent(go.transform, false);
        if (protectedLayer >= 0) ringGo.layer = protectedLayer;
        fx.ringMat = new Material(sh);
        fx.ringMat.SetColor(IdColor, TelegraphColor);
        fx.ringSr = ringGo.AddComponent<SpriteRenderer>();
        fx.ringSr.sharedMaterial = fx.ringMat;
        fx.ringSr.sprite = ringSprite;
        fx.ringSr.sortingLayerID = sortingLayer;
        fx.ringSr.sortingOrder = sortingOrder;
        ringGo.transform.localScale = Vector3.one; // 스프라이트가 이미 실제 크기로 구워져 있다(스케일 불필요)

        var fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(go.transform, false);
        if (protectedLayer >= 0) fillGo.layer = protectedLayer;
        var fillColor = TelegraphColor; fillColor.a = FillAlpha;
        fx.fillMat = new Material(sh);
        fx.fillMat.SetColor(IdColor, fillColor);
        fx.fillSr = fillGo.AddComponent<SpriteRenderer>();
        fx.fillSr.sharedMaterial = fx.fillMat;
        fx.fillSr.sprite = fillSprite;
        fx.fillSr.sortingLayerID = sortingLayer;
        fx.fillSr.sortingOrder = sortingOrder;
        fillGo.transform.localScale = Vector3.zero; // 채움: 안쪽부터 차오른다(스케일이 곧 채움)

        fx.SyncTransform(basePt, tipPt);
        return fx;
    }

    // 캡슐 축(밑동→창끝)에 맞춰 중심에 놓고 회전시킨다 — 텍스처가 로컬 +X를 캡슐의 긴 축으로 굽는다.
    void SyncTransform(Vector2 basePt, Vector2 tipPt)
    {
        transform.position = (basePt + tipPt) * 0.5f;
        Vector2 dir = tipPt - basePt;
        if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.FromToRotation(Vector3.right, dir.normalized);
    }

    static (Sprite ring, Sprite fill) GetOrBuildSprites(float radius, float length)
    {
        var key = (Mathf.RoundToInt(radius * 100f), Mathf.RoundToInt(length * 100f));
        if (spriteCache.TryGetValue(key, out var cached)) return cached;

        // 캡슐 축을 텍스처 로컬 X([-length/2, length/2])에 눕히고, 각 텍셀에서 그 선분까지의 거리를
        // 반지름으로 나눈 값(r)을 "원 시절의 반경 0~1"과 같은 의미로 재사용한다 — 링·채움 알파 공식은
        // 원과 완전히 동일하게 남는다(캡슐 SDF만 바뀜).
        float halfLen = length * 0.5f;
        float texDensity = TexDensity; // px per world unit (반지름 방향 해상도 기준)
        int texHeight = Mathf.Max(8, Mathf.RoundToInt(radius * 2f * texDensity));
        int texWidth = Mathf.Max(texHeight, Mathf.RoundToInt((length + radius * 2f) * texDensity));
        float aa = 1.5f / (texDensity * Mathf.Max(0.0001f, radius)); // 텍셀 1.5개 폭의 앤티에일리어싱 경계(r 단위)

        float inner = 1f - RingThicknessWorld / Mathf.Max(0.0001f, radius);

        var ringTex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var fillTex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var ringPixels = new Color[texWidth * texHeight];
        var fillPixels = new Color[texWidth * texHeight];
        float cx = (texWidth - 1) * 0.5f;
        float cy = (texHeight - 1) * 0.5f;

        for (int y = 0; y < texHeight; y++)
        {
            for (int x = 0; x < texWidth; x++)
            {
                float wx = (x - cx) / texDensity;
                float wy = (y - cy) / texDensity;
                float segX = Mathf.Clamp(wx, -halfLen, halfLen); // 캡슐 축(선분) 위 최근접점
                float dist = Mathf.Sqrt((wx - segX) * (wx - segX) + wy * wy);
                float r = dist / Mathf.Max(0.0001f, radius);

                float ringA = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner - aa, inner + aa, r))
                            * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - aa, 1f + aa, r)));
                ringPixels[y * texWidth + x] = new Color(1f, 1f, 1f, ringA);

                float fillA = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - aa, 1f + aa, r));
                fillPixels[y * texWidth + x] = new Color(1f, 1f, 1f, fillA);
            }
        }

        ringTex.SetPixels(ringPixels);
        ringTex.Apply();
        fillTex.SetPixels(fillPixels);
        fillTex.Apply();

        // pixelsPerUnit = texDensity → localScale 1이 정확히 실제 캡슐 크기(월드 단위)가 되도록.
        var ring = Sprite.Create(ringTex, new Rect(0, 0, texWidth, texHeight), new Vector2(0.5f, 0.5f), texDensity);
        var fill = Sprite.Create(fillTex, new Rect(0, 0, texWidth, texHeight), new Vector2(0.5f, 0.5f), texDensity);
        spriteCache[key] = (ring, fill);
        return (ring, fill);
    }

    /// <summary>진입·해제 페이드에 맞춰 예고도 같이 나타나고 사라진다(TranscendVisionFx가 매 프레임 호출).</summary>
    public void SetAlpha(float a) => alpha = Mathf.Clamp01(a);

    public void Detach()
    {
        if (this != null) Destroy(gameObject);
    }

    void Update()
    {
        if (owner == null) { Destroy(gameObject); return; }

        // 매 프레임 갱신 — 적의 flip·이동을 따라간다. DummyEnemy 기본형은 Windup부터 밑동·창끝이
        // 고정돼 실질적으로 안 변하지만, GehennaHound의 점프 공격(사용자 지시 2026-08-05: "적의 공격
        // 예측 범위가 움직여도 되니까 적 콜라이더와 동일하게")은 Windup~Thrust 동안 몸 전체가 실제로
        // 위/앞으로 도약한다 — hitboxRight가 자식이라 판정 캡슐도 그만큼 같이 움직이므로, 예고도 매
        // 프레임 다시 읽어야 실제 판정 위치와 계속 일치한다(캡슐 길이·반지름은 로컬 오프셋이라 불변,
        // Attach 시 구운 스프라이트 그대로 재사용해도 안전).
        SyncTransform(owner.AttackHitPointBase, owner.AttackHitPoint);

        if (!resolving)
        {
            float progress = owner.AttackTelegraphProgress;
            if (progress < 0f)
            {
                // 예고가 끝났다 — 무효화 여부(패링·회피·적 피격)로 터짐/흐지부지를 가른다.
                resolving = true;
                resolveTimer = 0f;
                resolvedNeutralized = owner.LastAttackNeutralized;
                return;
            }

            ringSr.enabled = true;
            fillSr.enabled = true;

            var rc = TelegraphColor; rc.a = alpha;
            ringMat.SetColor(IdColor, rc);
            var fc = TelegraphColor; fc.a = FillAlpha * alpha;
            fillMat.SetColor(IdColor, fc);
            fillSr.transform.localScale = Vector3.one * progress; // 스프라이트가 이미 실제 크기이므로 0~1이 곧 0~풀 크기
            return;
        }

        // ⚠️ 시간축 규약 예외 — scaled Time.deltaTime을 쓴다(VFX 공통 규약은 unscaled).
        // 진행률은 적이 scaled time으로 굴리는 값이라, 페이드·터짐만 unscaled면 히트스톱·슬로우 중에
        // 예고가 창보다 먼저 끝나버린다(PLAN §6 T-3b 경고).
        resolveTimer += Time.deltaTime;
        float dur = resolvedNeutralized ? FizzleDuration : BurstDuration;
        float t = dur > 0f ? Mathf.Clamp01(resolveTimer / dur) : 1f;
        float fadeAlpha = alpha * (1f - t);
        float scaleMul = resolvedNeutralized ? 1f : Mathf.Lerp(1f, BurstScale, t); // 흐지부지는 팽창 없이 페이드만

        ringSr.transform.localScale = Vector3.one * scaleMul;
        var rc2 = TelegraphColor; rc2.a = fadeAlpha;
        ringMat.SetColor(IdColor, rc2);
        fillSr.transform.localScale = Vector3.one * scaleMul;
        var fc2 = TelegraphColor; fc2.a = FillAlpha * fadeAlpha;
        fillMat.SetColor(IdColor, fc2);

        if (t >= 1f) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (ringMat != null) Destroy(ringMat);
        if (fillMat != null) Destroy(fillMat);
    }
}
