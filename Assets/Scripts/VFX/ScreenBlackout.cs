using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 캔버스를 꽉 채우는 검은 패널. 씬 시작 암전과 씬 전환 페이드 아웃이 같이 쓴다.
//
// ScreenFadeUI 대신 이걸 쓰는 이유: 그쪽은 알파 반영이 자기 Update()에 달려 있어서, 오브젝트가
// Start에서 막 생성되는 첫 프레임에는 안 검게 새어 나올 수 있다(실측). 여기서는 Awake에서 미리
// 만들어 두고 SetBlack()이 그 자리에서 색을 넣는다.
//
// 스트레치 앵커라 캔버스 rect 값이나 해상도와 무관하게 항상 전체를 덮고, 맨 마지막 형제라
// 레터박스·텍스트 위에 그려진다.
[RequireComponent(typeof(Canvas))]
public class ScreenBlackout : MonoBehaviour
{
    Image panel;

    void Awake()
    {
        var go = new GameObject("ScreenBlackout", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();

        panel = go.GetComponent<Image>();
        panel.raycastTarget = false;
        Set(0f);
    }

    /// <summary>알파를 그 자리에서 적용한다(다음 Update를 기다리지 않는다).</summary>
    public void Set(float alpha)
    {
        float a = Mathf.Clamp01(alpha);
        panel.color = new Color(0f, 0f, 0f, a);
        panel.enabled = a > 0f;   // 완전 투명일 땐 드로우콜을 아예 뺀다
    }

    public IEnumerator FadeTo(float target, float duration)
    {
        float from = panel.color.a;
        if (duration <= 0f) { Set(target); yield break; }

        panel.enabled = true;   // 페이드 도중에는 알파가 0이어도 그려져야 한다
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            panel.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, target, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        Set(target);
    }
}
