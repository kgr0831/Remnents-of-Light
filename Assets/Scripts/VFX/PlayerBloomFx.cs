using System.Collections.Generic;
using UnityEngine;

// 일섬 차지 · 발동 구간에 플레이어만 블룸시키는 가산 오버레이.
// URP엔 오브젝트별 블룸이 없어서(임계값 위 HDR 출력만이 유일한 방법) 플레이어 스프라이트와 똑같은
// 실루엣을 한 겹 더 그리고 Custom/PlayerBloomOverlay로 HDR을 얹는다. 원본 SpriteRenderer는 전혀
// 건드리지 않으므로 머티리얼을 되돌리다 실패해 플레이어가 이상하게 남는 사고가 구조적으로 없다.
//
// 세기는 _Intensity 하나로 제어된다 — 차지 진행도를 그대로 먹이면 페이드 인이 되고,
// 끝날 때 FadeOut()을 부르면 페이드 아웃 후 스스로 파괴된다.
// 런타임 전용 오브젝트 — 씬에 저장되지 않는다.
public class PlayerBloomFx : MonoBehaviour
{
    const string ShaderName = "Custom/PlayerBloomOverlay";
    const string MaskFolder = "Assets/Sprites/Player/Mask/";

    static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
    static readonly int IdEmissionMask = Shader.PropertyToID("_EmissionMask");
    static readonly int IdColor = Shader.PropertyToID("_Color");

    // 시트 텍스처 이름 → 발광 마스크. 없으면(부록A 마스크가 아직 없는 시트, 또는 에디터 밖) null을
    // 그대로 캐싱해 매 프레임 재조회하지 않는다. AssetDatabase는 에디터 전용이라 빌드에서는 항상
    // 폴백(흰색 = 기존 _Flatten 동작)으로 빠진다 — Resources/Addressables 이관은 실제 빌드가 필요해질 때.
#if UNITY_EDITOR
    static readonly Dictionary<string, Texture2D> _maskCache = new Dictionary<string, Texture2D>();
#endif

    SpriteRenderer sr;
    SpriteRenderer ownerSr;
    Material mat;
    Texture lastMainTex;  // 마스크 재조회를 매 프레임 하지 않기 위한 캐시 키(시트가 바뀔 때만 다시 찾는다)

    float intensity;      // 호출자가 지정한 목표 세기(0~1)
    float fadeOutDuration = -1f;
    float fadeOutFrom;
    float fadeOutTimer;

    /// <summary>
    /// 지정한 셰이더로 오버레이를 붙인다. 폭주는 가산(PlayerBloomOverlay)이 아니라 덮어쓰기
    /// (Custom/PlayerMaskEmissive)를 써야 "마스크 부위만 아예 그 색"이 된다 — 가산은 원본 색과 섞여
    /// 분홍이 되거나 세기를 올리면 몸 전체가 물든다(사용자 피드백 2026-08-01).
    /// </summary>
    public static PlayerBloomFx AttachWithShader(Transform owner, string shaderName, int sortingOffset)
    {
        Shader sh = Shader.Find(shaderName);
        if (sh == null)
        {
            Debug.LogWarning("[Bloom] " + shaderName + " 셰이더를 찾을 수 없어 오버레이를 건너뜁니다.");
            return null;
        }
        return AttachInternal(owner, sh, null, sortingOffset);
    }

    public static PlayerBloomFx Attach(Transform owner, Material source, int sortingOffset)
    {
        Shader sh = source != null ? source.shader : Shader.Find(ShaderName);
        if (sh == null)
        {
            Debug.LogWarning("[Ilseom] " + ShaderName + " 셰이더를 찾을 수 없어 플레이어 블룸을 건너뜁니다.");
            return null;
        }
        return AttachInternal(owner, sh, source, sortingOffset);
    }

    static PlayerBloomFx AttachInternal(Transform owner, Shader sh, Material source, int sortingOffset)
    {
        var ownerSr = owner.GetComponent<SpriteRenderer>();
        if (ownerSr == null) return null;

        var go = new GameObject("PlayerBloomFx");
        go.transform.SetParent(owner, false);
        // 그레이스케일 확산 중에도 빛이 원색으로 남도록 보호 레이어에 올린다(DashAfterImage와 동일).
        int noGrayscaleLayer = LayerMask.NameToLayer("VFXNoGrayscale");
        if (noGrayscaleLayer >= 0) go.layer = noGrayscaleLayer;

        var fx = go.AddComponent<PlayerBloomFx>();
        fx.ownerSr = ownerSr;
        fx.mat = source != null ? new Material(source) : new Material(sh);
        fx.mat.SetFloat(IdIntensity, 0f);

        var r = go.AddComponent<SpriteRenderer>();
        r.sharedMaterial = fx.mat;
        r.sortingLayerID = ownerSr.sortingLayerID;
        r.sortingOrder = ownerSr.sortingOrder + sortingOffset;
        fx.sr = r;
        fx.SyncSprite();
        return fx;
    }

    /// <summary>세기를 직접 지정한다(0~1). 차지 진행도를 매 프레임 넣으면 그대로 페이드 인이 된다.</summary>
    public void SetIntensity(float k)
    {
        if (fadeOutDuration >= 0f) return; // 이미 꺼지는 중이면 무시
        // 사용자 요청에 따라 기본 블룸 강도를 50%로 낮춤
        intensity = Mathf.Clamp01(k) * 0.5f;
    }

    /// <summary>
    /// 위의 50% 감쇠 없이 세기를 그대로 넣는다. 폭주처럼 화면이 완전 암전이라 실제로 "빛나야" 하는
    /// 구간용 — 50%가 걸리면 실효 HDR 출력이 1.17까지밖에 안 올라가 씬 Bloom 임계값(1.15)을 겨우
    /// 넘어서 헤일로가 거의 안 생긴다(적 아웃라인은 1.9라 확실히 빛나는 것과 대비됐다).
    /// </summary>
    public void SetIntensityRaw(float k)
    {
        if (fadeOutDuration >= 0f) return;
        intensity = Mathf.Clamp01(k);
    }

    /// <summary>발광 색을 바꾼다(폭주는 붉게, 그 외는 셰이더 기본 청백). 마스크는 그대로 쓴다.</summary>
    public void SetColor(Color tint)
    {
        if (mat != null) mat.SetColor(IdColor, tint);
    }

    /// <summary>HDR 세기(_BloomBoost). 폭주처럼 화면이 완전 암전인 구간은 높게 잡아야 읽힌다.</summary>
    public void SetBoost(float boost)
    {
        if (mat != null) mat.SetFloat(Shader.PropertyToID("_BloomBoost"), Mathf.Clamp(boost, 1f, 8f));
    }

    /// <summary>
    /// 마스크 밖 영역의 발광 바닥값(0=마스크만 발광, 기본). 마스크 발광부가 몇 픽셀뿐이면 블룸이
    /// 점으로만 보이므로, 실루엣 전체가 타올라야 하는 구간(폭주)에서만 올린다.
    /// </summary>
    public void SetMaskFloor(float floor)
    {
        if (mat != null) mat.SetFloat(Shader.PropertyToID("_MaskFloor"), Mathf.Clamp01(floor));
    }

    /// <summary>
    /// 마스크와 별개로, 스프라이트에서 원래 밝은 부위(칼날 등)도 빛나게 한다.
    /// weight 0이면 꺼짐. Custom/PlayerMaskEmissive 전용 — 다른 셰이더면 조용히 무시된다.
    /// </summary>
    public void SetBrightEmission(float weight, Color tint)
    {
        if (mat == null) return;
        mat.SetFloat(Shader.PropertyToID("_BrightWeight"), Mathf.Clamp01(weight));
        mat.SetColor(Shader.PropertyToID("_BrightColor"), tint);
    }

    /// <summary>현재 세기에서 0까지 내린 뒤 스스로 파괴된다.</summary>
    public void FadeOut(float duration)
    {
        if (fadeOutDuration >= 0f) return;
        fadeOutDuration = Mathf.Max(0.01f, duration);
        fadeOutFrom = intensity;
        fadeOutTimer = 0f;
    }

    // Animator가 그 프레임의 스프라이트를 확정한 뒤 복사해야 한 프레임도 어긋나지 않는다.
    void LateUpdate()
    {
        if (ownerSr == null) { Destroy(gameObject); return; }

        SyncSprite();

        float k = intensity;
        if (fadeOutDuration >= 0f)
        {
            fadeOutTimer += Time.unscaledDeltaTime; // 히트스톱 중에도 페이드가 멈추지 않게
            float t = Mathf.Clamp01(fadeOutTimer / fadeOutDuration);
            k = Mathf.Lerp(fadeOutFrom, 0f, t);
            if (t >= 1f) { Destroy(gameObject); return; }
        }

        // 일섬 이동 중엔 본체 스프라이트가 반투명해진다 — 빛도 같이 옅어져야 한 몸으로 보인다.
        mat.SetFloat(IdIntensity, k * ownerSr.color.a);
    }

    void SyncSprite()
    {
        sr.sprite = ownerSr.sprite;
        // 커스텀 셰이더(Custom/PlayerBloomOverlay)는 URP 2D에서 SpriteRenderer의
        // flipX/Y를 자동으로 처리하지 않으므로, scale을 반전시켜 물리적으로 뒤집는다.
        transform.localScale = new Vector3(ownerSr.flipX ? -1f : 1f, ownerSr.flipY ? -1f : 1f, 1f);
        sr.enabled = ownerSr.enabled;

        // C-4: 애니메이션 상태(=시트)가 바뀔 때만 마스크를 다시 찾는다 — 매 프레임 조회는 낭비.
        Texture mainTex = ownerSr.sprite != null ? ownerSr.sprite.texture : null;
        if (mainTex != lastMainTex)
        {
            lastMainTex = mainTex;
            Texture2D mask = FindMask(mainTex);
            // 못 찾으면 null이 아니라 명시적으로 흰색을 넣는다 — null을 넣으면 이전에 물려 있던
            // 마스크(직전 시트 것)가 그대로 남아 다른 시트에 잘못 적용될 위험이 있다.
            mat.SetTexture(IdEmissionMask, mask != null ? (Texture)mask : Texture2D.whiteTexture);
        }
    }

    // 시트 텍스처 이름과 같은 파일명의 마스크를 부록A 산출물 폴더에서 찾는다(예: "Glitch Samurai-Idle"
    // → "Assets/Sprites/Player/Mask/Glitch Samurai-Idle.png"). 마스크가 아직 없는 시트(마스터 시트 등)는
    // null을 캐싱해 반복 조회를 막는다 — 호출부가 null을 흰색 폴백으로 치환한다.
    static Texture2D FindMask(Texture mainTex)
    {
        if (mainTex == null) return null;
#if UNITY_EDITOR
        if (_maskCache.TryGetValue(mainTex.name, out Texture2D cached)) return cached;
        Texture2D mask = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(MaskFolder + mainTex.name + ".png");
        _maskCache[mainTex.name] = mask;
        return mask;
#else
        return null;
#endif
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
