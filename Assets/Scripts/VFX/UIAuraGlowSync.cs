using UnityEngine;
using UnityEngine.UI;

// Custom/UIAuraGlow를 쓰는 이미지를 본체(로고·아이콘)와 계속 맞춰 준다.
//
// ⚠️ 이 오브젝트는 본체의 **자식이 아니라 형제**여야 하고, 형제 순서상 본체보다 앞(먼저 그려지는 쪽)에
//    있어야 한다. UGUI는 계층 순서대로 그리므로 자식으로 두면 아우라가 본체 위에 덮여 그림이 묻힌다.
//
// 손으로 맞추면 어긋나기 쉬운 것들을 자동화한다.
//   1) 위치·앵커 = 본체와 동일 (형제라서 따로 놀 수 있다)
//   2) 쿼드 크기 = 본체 크기 x 머티리얼의 _Expand  (둘이 다르면 아우라가 잘리거나 스프라이트가 찌그러진다)
//   3) 스프라이트 = 본체 스프라이트
//   4) _MainTex_TexelSize — CanvasRenderer는 이 값을 자동으로 안 채운다(PlayerHudUI와 같은 함정)
//
// 에디터에서 값을 만지는 동안만 따라가면 되므로 플레이 중에는 갱신하지 않는다(불필요한 머티리얼 쓰기 방지).
[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class UIAuraGlowSync : MonoBehaviour
{
    [Tooltip("아우라를 두를 본체 이미지(로고)")]
    public Image source;

    Image self;

    void OnEnable()
    {
        self = GetComponent<Image>();
        Apply();
    }

    void Update()
    {
        if (!Application.isPlaying) Apply();
    }

    void Apply()
    {
        if (self == null) self = GetComponent<Image>();
        if (source == null || self.material == null) return;

        if (self.sprite != source.sprite) self.sprite = source.sprite;

        var rt = (RectTransform)transform;
        var srcRt = (RectTransform)source.transform;

        // 형제로 두기 때문에 위치를 따로 맞춰야 한다(같은 부모라는 전제).
        if (rt.parent == srcRt.parent)
        {
            rt.anchorMin = srcRt.anchorMin;
            rt.anchorMax = srcRt.anchorMax;
            rt.pivot = srcRt.pivot;
            rt.anchoredPosition = srcRt.anchoredPosition;
            rt.localScale = srcRt.localScale;
        }

        float expand = self.material.HasProperty("_Expand") ? self.material.GetFloat("_Expand") : 1f;
        Vector2 wanted = srcRt.rect.size * expand;
        if ((rt.sizeDelta - wanted).sqrMagnitude > 0.01f) rt.sizeDelta = wanted;

        var tex = source.sprite != null ? source.sprite.texture : null;
        if (tex == null) return;
        var wantedTexel = new Vector4(1f / tex.width, 1f / tex.height, tex.width, tex.height);
        // 매 프레임 쓰면 머티리얼 에셋이 계속 dirty로 잡힌다 — 값이 다를 때만 쓴다.
        if (self.material.GetVector("_MainTex_TexelSize") != wantedTexel)
            self.material.SetVector("_MainTex_TexelSize", wantedTexel);
    }
}
