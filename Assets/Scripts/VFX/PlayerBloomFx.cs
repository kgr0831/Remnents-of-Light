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

    static readonly int IdIntensity = Shader.PropertyToID("_Intensity");

    SpriteRenderer sr;
    SpriteRenderer ownerSr;
    Material mat;

    float intensity;      // 호출자가 지정한 목표 세기(0~1)
    float fadeOutDuration = -1f;
    float fadeOutFrom;
    float fadeOutTimer;

    public static PlayerBloomFx Attach(Transform owner, Material source, int sortingOffset)
    {
        Shader sh = source != null ? source.shader : Shader.Find(ShaderName);
        if (sh == null)
        {
            Debug.LogWarning("[Ilseom] " + ShaderName + " 셰이더를 찾을 수 없어 플레이어 블룸을 건너뜁니다.");
            return null;
        }

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
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
