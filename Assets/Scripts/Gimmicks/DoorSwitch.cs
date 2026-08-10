using System.Collections;
using UnityEngine;

// 문/스위치 기믹 - 스위치. LightObject.cs와 같은 원리로 Enemy 레이어에 둬서 플레이어 물리는
// 통과하되(PlayerController.Awake의 rb.excludeLayers), PlayerController.CheckAttackHit의 공격
// 판정(OverlapBoxAll(enemyLayer))에는 걸려 Toggle()이 호출된다.
// On/Off는 공격할 때마다 뒤집히는 토글이며, LaserDoor가 매 프레임 IsOn을 폴링해 On/Off를 판단한다
// (이벤트 구독 없이 폴링 — 스위치 1개:문 여러개, 문 1개:스위치 여러개(AND) 배선을 코드 변경 없이 지원).
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class DoorSwitch : MonoBehaviour
{
    public const string Channel = "door_switch";

    [Header("초기 상태")]
    [Tooltip("체크하면 씬 시작 시 이 스위치가 이미 켜진(On) 상태로 시작한다. 기본은 꺼짐(Off)")]
    public bool initialOn = false;

    [Header("자동 Off (선택)")]
    [Tooltip("체크하면 On이 된 뒤 아래 시간이 지났을 때 자동으로 Off된다(제한시간 안에 통과해야 하는 퍼즐용). 끄면 다시 때릴 때까지 계속 켜져 있다")]
    public bool autoOffEnabled = false;
    [Tooltip("autoOffEnabled가 켜져 있을 때, On이 된 뒤 자동으로 Off되기까지 걸리는 시간(초). 시간이 다 되기 전에 다시 타격해서 끄면 타이머는 취소된다")]
    public float autoOffDelay = 5f;

    [Header("발광 (On일 때만 켜짐 — 빨간 부위만 앰버색으로 빛남)")]
    [Tooltip("빛나야 할 부분(빨간 픽셀)만 표시한 마스크 텍스처. Assets/Sprites/Gimmicks/SwitchEmissionMask.png를 그대로 연결하면 됨 — 이미 만들어져 있으니 새로 만들 필요 없음")]
    public Texture2D emissionMask; // Assets/Sprites/Gimmicks/SwitchEmissionMask.png
    [Tooltip("On일 때 빛나는 색. 기본값은 주황~노랑 사이(앰버)")]
    public Color glowColor = new Color(1f, 0.65f, 0.1f, 1f); // 주황~노랑 사이(앰버)
    [Tooltip("빛나는 강도 — 값이 클수록 더 쨍하게 빛난다")]
    public float bloomBoost = 2.2f;

    public bool IsOn { get; private set; }

    SpriteRenderer sr;
    SpriteRenderer glowSr;
    Material glowMat;
    Coroutine autoOffRoutine;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        BuildGlowOverlay();
        IsOn = initialOn;
        ApplyVisual();
    }

    void BuildGlowOverlay()
    {
        Shader sh = Shader.Find("Custom/PlayerBloomOverlay");
        if (sh == null) return;

        var go = new GameObject("Glow");
        go.hideFlags = HideFlags.DontSave; // LightObject.BuildGlowOverlay와 동일 — 씬에 저장되지 않는 런타임 전용 오브젝트
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
        glowSr.sprite = sr.sprite;
    }

    void ApplyVisual()
    {
        if (glowSr != null) glowSr.enabled = IsOn;
    }

    /// <summary>플레이어 공격이 이 스위치를 때렸을 때 호출(PlayerController.CheckAttackHit).</summary>
    public void Toggle()
    {
        IsOn = !IsOn;
        ApplyVisual();
        TestLog.Event(Channel, $"{name} toggled on={IsOn}");

        if (autoOffRoutine != null) { StopCoroutine(autoOffRoutine); autoOffRoutine = null; }
        if (IsOn && autoOffEnabled) autoOffRoutine = StartCoroutine(AutoOffAfterDelay());
    }

    IEnumerator AutoOffAfterDelay()
    {
        yield return new WaitForSeconds(autoOffDelay);
        IsOn = false;
        ApplyVisual();
        autoOffRoutine = null;
        TestLog.Event(Channel, $"{name} auto_off");
    }

    void OnDestroy()
    {
        if (glowMat != null) Destroy(glowMat);
    }
}
