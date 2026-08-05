using UnityEngine;
using System.Collections;

// 타격 가능한 발광 오브젝트(OBJ 시트, HP1). 켜진 상태에서만 플레이어 공격에 맞을 수 있다.
// 맞으면 광원을 나눠주고 fadeOutDuration에 걸쳐 서서히 꺼진 뒤(즉시 전환 아님), relightDelay 후 다시 켜진다.
// 플레이어 통과는 별도 처리가 필요 없다 — 이 오브젝트를 "Enemy" 레이어에 두면
// PlayerController.Awake의 rb.excludeLayers가 이미 그 레이어를 제외해 DummyEnemy와 같은 원리로 통과된다.
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class LightObject : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite onSprite;
    public Sprite offSprite;

    [Header("Hit / Energy")]
    [Range(0f, 1f)] public float energyChargePercent = 0.25f;
    public float relightDelay = 10f;
    public float fadeOutDuration = 0.6f; // 꺼질 때 즉시가 아니라 이 시간에 걸쳐 서서히 어두워진다

    [Header("Glow (켜졌을 때만 표시)")]
    // Light2D(주변 조명)는 뺐다 — 오브젝트마다 좌표가 맞지 않아 엉뚱한 위치에서 빛나는 문제가 있었고
    // (사용자 리포트 2026-08-05), 마스크 기반 블룸만으로도 "마스크에 표시된 부위만 빛난다"는 요구를
    // 이미 정확히 만족한다(같은 메시·UV를 쓰므로 위치 어긋날 일이 구조적으로 없음).
    public Color glowColor = new Color(1f, 0.6118f, 1f, 1f); // OBJ.png 발광 코어 색(255,156,255), 마스크에서 샘플링
    public float bloomBoost = 2.2f; // 과하지 않게(사용자 지시 2026-08-05) — 기존 4에서 낮춤
    public Texture2D emissionMask; // Assets/Mask/OBJ.png(시트 전체) — 픽셀 단위로 정확히 마스크 부위만 빛남

    [Header("숨쉬는 듯한 펄스 (켜져있는 동안, 사용자 지시 2026-08-05)")]
    public float pulseAmplitude = 0.15f; // 최대 세기에서 이 비율만큼 낮아졌다 돌아온다("약간 더 -> 약간 덜"반복)
    public float pulseSpeed = 1.5f;      // 느리게(사용자 지시: "느리게 숨쉬는 듯한 효과") — 주기 약 4.2초

    SpriteRenderer sr;
    SpriteRenderer glowSr;
    SpriteRenderer offOverlaySr; // 꺼짐 스프라이트를 위에 덮어써서 크로스페이드하는 용도(항상 sr=onSprite가 바탕)
    Material glowMat;
    bool lit = true;
    Coroutine fadeRoutine;
    float pulsePhase; // 오브젝트마다 위상을 다르게 둬서 전부 같은 박자로 숨쉬지 않게

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        pulsePhase = Random.Range(0f, Mathf.PI * 2f);
        BuildGlowOverlay();
        BuildOffOverlay();
        ApplyState();
    }

    // 꺼짐/페이드 중엔 손대지 않는다 — TryHit()이 lit을 즉시 false로 내려서 페이드·대기 구간 내내
    // 이 갱신이 자동으로 멈춘다(그 구간의 세기는 FadeOutThenRelight가 전담).
    void Update()
    {
        if (!lit || glowMat == null) return;
        float k = 1f - pulseAmplitude * 0.5f * (1f + Mathf.Sin(Time.time * pulseSpeed + pulsePhase));
        glowMat.SetFloat("_Intensity", k);
    }

    void BuildGlowOverlay()
    {
        Shader sh = Shader.Find("Custom/PlayerBloomOverlay");
        if (sh == null) return;

        var go = new GameObject("Glow");
        go.hideFlags = HideFlags.DontSave; // PlayerBloomFx와 동일 원칙: 씬에 저장되지 않는 런타임 전용 오브젝트
        go.transform.SetParent(transform, false);

        glowMat = new Material(sh);
        glowMat.SetColor("_Color", glowColor);
        glowMat.SetFloat("_BloomBoost", bloomBoost);
        glowMat.SetFloat("_Flatten", 1f);   // 마스크 부위를 정확히 _Color로 고정
        glowMat.SetFloat("_MaskFloor", 0f); // 마스크 밖은 발광 없음
        glowMat.SetFloat("_Intensity", 1f);
        if (emissionMask != null) glowMat.SetTexture("_EmissionMask", emissionMask);

        glowSr = go.AddComponent<SpriteRenderer>();
        glowSr.sharedMaterial = glowMat;
        glowSr.sortingLayerID = sr.sortingLayerID;
        glowSr.sortingOrder = sr.sortingOrder + 1;
        glowSr.sprite = onSprite;
    }

    // 켜짐→꺼짐 크로스페이드용 오버레이. sr은 항상 onSprite를 유지하고, 이 레이어(offSprite)의
    // 알파를 0→1로 올리면서 위에 덮어써 서서히 꺼진 모습으로 바뀌게 한다(on/off는 발광 픽셀만
    // 다르고 나머지는 완전히 동일한 실루엣이라 이 방식이 이음매 없이 자연스럽다).
    void BuildOffOverlay()
    {
        var go = new GameObject("OffOverlay");
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(transform, false);

        offOverlaySr = go.AddComponent<SpriteRenderer>();
        offOverlaySr.sprite = offSprite;
        offOverlaySr.sortingLayerID = sr.sortingLayerID;
        offOverlaySr.sortingOrder = sr.sortingOrder + 2; // Glow(+1)보다 위에 그려 발광까지 덮는다
        offOverlaySr.color = new Color(1f, 1f, 1f, 0f);
    }

    void ApplyState()
    {
        if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
        sr.sprite = lit ? onSprite : offSprite;
        if (glowSr != null) { glowSr.enabled = lit; glowMat.SetFloat("_Intensity", 1f); }
        if (offOverlaySr != null) offOverlaySr.color = new Color(1f, 1f, 1f, 0f);
    }

    /// <summary>플레이어 공격이 이 오브젝트를 때렸을 때 호출(PlayerController.CheckAttackHit).
    /// 꺼진 상태면 false를 반환해 판정을 무시하게 한다.</summary>
    public bool TryHit()
    {
        if (!lit) return false;
        lit = false;
        TestLog.Event("light_object", $"{name} hit_off relight_in={relightDelay}");
        fadeRoutine = StartCoroutine(FadeOutThenRelight());
        return true;
    }

    IEnumerator FadeOutThenRelight()
    {
        float t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            float k = fadeOutDuration > 0f ? Mathf.Clamp01(t / fadeOutDuration) : 1f;
            if (offOverlaySr != null) offOverlaySr.color = new Color(1f, 1f, 1f, k);
            if (glowMat != null) glowMat.SetFloat("_Intensity", 1f - k);
            yield return null;
        }

        // 페이드 완료 — 바탕 스프라이트 자체를 off로 바꾸고 오버레이/발광은 완전히 끈다.
        sr.sprite = offSprite;
        if (offOverlaySr != null) offOverlaySr.color = new Color(1f, 1f, 1f, 0f);
        if (glowSr != null) glowSr.enabled = false;

        yield return new WaitForSeconds(relightDelay);

        lit = true;
        fadeRoutine = null;
        ApplyState();
        TestLog.Event("light_object", $"{name} relit");
    }

    void OnDestroy()
    {
        if (glowMat != null) Destroy(glowMat);
    }
}
