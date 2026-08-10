using UnityEngine;
using UnityEngine.Rendering.Universal;

// 튜토리얼 보스룸의 차폐 기믹 - 가동 중인 통풍 팬. 3프레임(fan activ n_0~2, Sprite Editor에서
// Grid By Cell Count 3x1로 슬라이스)을 LaserDoor.cs와 같은 패턴(Animator 없이 자체 타이머)으로
// 반복 재생한다.
// 보스 눈(BossEyeTracker)과 플레이어 사이에 이 오브젝트가 걸리면 ① ShadowCaster2D가 보스 빔
// Light2D를 실제로 가려 시각적으로 통과하지 못하게 하고 ② BossEyeTracker.IsPlayerInBeam이 이
// 오브젝트의 BoxCollider2D(BossBeamOccluder 레이어)에 Physics2D.Linecast로 걸리면 노출(응시)
// 판정을 끈다. 렌더링 차단과 게임로직 차단이 서로 다른 경로라 레이어를 반드시 맞춰야 한다.
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(ShadowCaster2D))]
public class FanActiv : MonoBehaviour
{
    [Tooltip("이 오브젝트를 보스 빔 차폐 레이어로 강제한다 - 인스펙터에서 다른 레이어에 둬도 항상 이 레이어로 맞춰진다")]
    public string occluderLayerName = "BossBeamOccluder";

    [Header("애니메이션 (fan activ n_0, _1, _2 3프레임)")]
    [Tooltip("순서대로 재생될 프레임 - fan activ n_0, _1, _2를 순서대로 넣는다")]
    public Sprite[] frames = new Sprite[3];
    [Tooltip("초당 프레임 수")]
    public float frameRate = 8f;

    SpriteRenderer sr;
    int frameIndex;
    float frameTimer;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        int layer = LayerMask.NameToLayer(occluderLayerName);
        if (layer >= 0) gameObject.layer = layer;

        var col = GetComponent<BoxCollider2D>();
        col.isTrigger = true; // 물리적으로 막지 않는다 - 시야(빔) 차폐 판정 전용

        if (frames != null && frames.Length > 0) sr.sprite = frames[0];

        // ShadowCaster2D 셰이프(m_ShapePath)는 프리팹에 미리 구워 둔다(FanActiv1/2.prefab) —
        // 런타임에 useRendererSilhouette로 셰이프를 만들려 했으나 Unity 6/URP 17에서 실제
        // 실루엣 추적이 안 되고 오브젝트와 무관한 고정 플레이스홀더 도형이 나오는 버그를 실측으로
        // 확인했다(사용자 리포트 2026-08-10, "빛이 그대로 보임"). Shape Editor 모드(에디터에서
        // 좌표를 직접 굽는 방식)만 신뢰할 수 있어 그쪽으로 전환 — castsShadows만 여기서 보장한다.
        var caster = GetComponent<ShadowCaster2D>();
        caster.castsShadows = true;
    }

    void Update()
    {
        if (frames == null || frames.Length < 2) return; // 프레임이 없거나 1장뿐이면 재생할 게 없다

        float frameDuration = 1f / Mathf.Max(1f, frameRate);
        frameTimer += Time.deltaTime;
        if (frameTimer < frameDuration) return;
        frameTimer -= frameDuration;

        frameIndex = (frameIndex + 1) % frames.Length;
        sr.sprite = frames[frameIndex];
    }
}
