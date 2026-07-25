using UnityEngine;

// 적 스프라이트 위에 같은 실루엣을 붉은 HDR로 한 겹 더 얹어 "처형 가능" 신호를 보낸다.
// PlayerBloomFx.cs와 동일한 구조 — 원본 SpriteRenderer를 건드리지 않으므로
// 머티리얼 복원 실패 사고가 구조적으로 없다.
// EnemyExecutionGlow.shader(Custom/EnemyExecutionGlow)를 사용한다.
public class EnemyExecutionGlowFx : MonoBehaviour
{
    const string ShaderName = "Custom/EnemyExecutionGlow";

    static readonly int IdIntensity = Shader.PropertyToID("_Intensity");

    SpriteRenderer sr;
    SpriteRenderer ownerSr;
    Material mat;

    float intensity;
    float fadeOutDuration = -1f;
    float fadeOutFrom;
    float fadeOutTimer;

    // fadeIn 지원 — 커서를 댔을 때 부드럽게 나타나도록
    float fadeInDuration = -1f;
    float fadeInTarget;
    float fadeInTimer;

    public static EnemyExecutionGlowFx Attach(Transform owner, Material source, int sortingOffset)
    {
        Shader sh = source != null ? source.shader : Shader.Find(ShaderName);
        if (sh == null)
        {
            Debug.LogWarning("[Execution] " + ShaderName + " 셰이더를 찾을 수 없어 적 글로우를 건너뜁니다.");
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

    /// <summary>세기를 직접 지정한다(0~1).</summary>
    public void SetIntensity(float k)
    {
        if (fadeOutDuration >= 0f) return; // 이미 꺼지는 중이면 무시
        intensity = Mathf.Clamp01(k);
        fadeInDuration = -1f; // 직접 세팅이면 페이드 인 취소
    }

    /// <summary>현재 세기에서 목표까지 부드럽게 올린다.</summary>
    public void FadeIn(float target, float duration)
    {
        if (fadeOutDuration >= 0f) return;
        fadeInTarget = Mathf.Clamp01(target);
        fadeInDuration = Mathf.Max(0.01f, duration);
        fadeInTimer = 0f;
    }

    /// <summary>현재 세기에서 0까지 내린 뒤 스스로 파괴된다.</summary>
    public void FadeOut(float duration)
    {
        if (fadeOutDuration >= 0f) return;
        fadeOutDuration = Mathf.Max(0.01f, duration);
        fadeOutFrom = intensity;
        fadeOutTimer = 0f;
    }

    void LateUpdate()
    {
        if (ownerSr == null) { Destroy(gameObject); return; }

        SyncSprite();

        float k = intensity;

        // 페이드 인 처리
        if (fadeInDuration >= 0f && fadeOutDuration < 0f)
        {
            fadeInTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(fadeInTimer / fadeInDuration);
            intensity = Mathf.Lerp(0f, fadeInTarget, t);
            k = intensity;
            if (t >= 1f) fadeInDuration = -1f;
        }

        // 페이드 아웃 처리
        if (fadeOutDuration >= 0f)
        {
            fadeOutTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(fadeOutTimer / fadeOutDuration);
            k = Mathf.Lerp(fadeOutFrom, 0f, t);
            if (t >= 1f) { Destroy(gameObject); return; }
        }

        mat.SetFloat(IdIntensity, k * ownerSr.color.a);
    }

    void SyncSprite()
    {
        sr.sprite = ownerSr.sprite;
        // 적의 localScale.x 반전에 대응
        transform.localScale = new Vector3(ownerSr.flipX ? -1f : 1f, ownerSr.flipY ? -1f : 1f, 1f);
        sr.enabled = ownerSr.enabled;
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
