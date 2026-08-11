using UnityEngine;

// 마스크 시트(Resources/PlayerMask/<시트 텍스처명>.png)에 흰색으로 칠해진 부위만 상시 발광시킨다.
// PlayerBloomFx는 PlayerController가 일섬·폭주·초월·사망 같은 이벤트마다 붙였다 떼는 구조라 "계속
// 빛나는" 주체가 없다 — 튜토리얼 플레이어처럼 눈·코어가 상시 발광해야 하는 오브젝트가 이걸 단다.
//
// 마스크 조회·스프라이트 동기화는 전부 PlayerBloomFx가 한다(매 LateUpdate에 ownerSr.sprite를 복사하고,
// 시트가 바뀔 때만 마스크를 다시 찾는다). 여기서는 세기·색만 얹는다.
[RequireComponent(typeof(SpriteRenderer))]
public class MaskGlowFx : MonoBehaviour
{
    // 씬 Bloom 임계값(IlseomBloomProfile = 1.15)을 넘겨야 헤일로가 생긴다.
    // 발광부 기준 대략 출력 = 0.84 * tint * intensity * boost.
    // 임계값을 많이 넘길수록 번짐 반경이 커진다 — 0.8*3.0(≈2.0)은 "범위가 과하다"는 피드백을 받아
    // 0.65*2.6(≈1.42)으로 낮췄다. 임계값 1.15 바로 위라 눈·코어 주변에만 좁게 붙는다.
    [Range(0f, 1f)] public float intensity = 0.65f;
    [Range(1f, 8f)] public float boost = 2.6f;
    public Color tint = new Color(0.65f, 0.92f, 1f, 1f); // Custom/PlayerBloomOverlay의 기본 청백
    public int sortingOffset = 1;

    PlayerBloomFx fx;

    void OnEnable()
    {
        fx = PlayerBloomFx.Attach(transform, null, sortingOffset);
        Push();
    }

    void OnDisable()
    {
        if (fx != null) { Destroy(fx.gameObject); fx = null; }
    }

    // 플레이 중 인스펙터로 값을 굴려볼 수 있게 매 프레임 밀어넣는다(전부 단순 대입이라 비용이 없다).
    void LateUpdate() { Push(); }

    void Push()
    {
        if (fx == null) return;
        fx.SetColor(tint);
        fx.SetBoost(boost);
        fx.SetIntensityRaw(intensity); // SetIntensity는 값을 절반으로 깎는다 — 상시 발광은 그대로 쓴다
    }
}
