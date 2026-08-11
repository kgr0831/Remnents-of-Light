using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 시네마틱 레터박스 — 화면 위/아래에 검은 바를 만들어 목표 화면비(기본 1.85:1)로 좁힌다.
// 위 바는 아래를 향해, 아래 바는 위를 향해 자라나며 나타나고, 감출 때는 그 반대로 줄어든다.
//
// SectionCamera의 ApplyLetterbox(cam.rect를 줄이는 방식)와는 별개다. 저쪽은 룸 트리거 전용이고
// 빌드에서 백버퍼가 안 지워지는 문제 때문에 전용 클리어 카메라까지 딸려 있다. 이건 UI 이미지 두 장이라
// 그 문제가 없고, 자라나는 연출도 줄 수 있다.
//
// 바 높이는 캔버스 rect에서 매번 계산한다 — CanvasScaler가 match=0(너비 기준)이라 화면비가 바뀌면
// 캔버스 높이가 따라 바뀌는데, 그때도 목표 비율이 유지된다.
public class CinematicLetterbox : MonoBehaviour
{
    [Tooltip("목표 화면비. 1.85:1 = 시네마스코프 계열 와이드")]
    public float targetAspect = 1.85f;

    [Tooltip("바가 자라나고 줄어드는 시간(초)")]
    public float duration = 0.6f;

    RectTransform canvasRect, top, bottom;
    Coroutine anim;

    void Awake()
    {
        canvasRect = (RectTransform)GetComponentInParent<Canvas>().transform;
        top    = CreateBar("LetterboxTop",    1f, new Vector2(0.5f, 1f));
        bottom = CreateBar("LetterboxBottom", 0f, new Vector2(0.5f, 0f));
        SetHeight(0f);
    }

    // edgeY: 1=위쪽 가장자리, 0=아래쪽 가장자리. 가로는 앵커로 꽉 채우고 세로만 sizeDelta로 제어한다.
    RectTransform CreateBar(string barName, float edgeY, Vector2 pivot)
    {
        var go = new GameObject(barName, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvasRect, false);
        rt.anchorMin = new Vector2(0f, edgeY);
        rt.anchorMax = new Vector2(1f, edgeY);
        rt.pivot = pivot;                       // 가장자리에 피벗을 두어 안쪽으로 자라게 한다
        rt.anchoredPosition = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
        // 다른 UI보다 **아래**에 그린다 — 대사를 검은 바 위에 얹는 영화 자막 스타일(사용자 선택).
        // 바를 맨 위로 올리면 아래쪽 대사가 통째로 가려진다.
        rt.SetAsFirstSibling();
        return rt;
    }

    public float BarHeight()
    {
        float visible = canvasRect.rect.width / targetAspect;
        return Mathf.Max(0f, (canvasRect.rect.height - visible) * 0.5f);
    }

    public Coroutine Show() { return Run(BarHeight()); }
    public Coroutine Hide() { return Run(0f); }

    /// <summary>애니메이션 없이 즉시 펼친다 — 씬이 이미 레터박스가 걸린 상태로 시작할 때.</summary>
    public void ShowInstant()
    {
        if (anim != null) { StopCoroutine(anim); anim = null; }
        // 씬 시작 첫 프레임에는 CanvasScaler가 아직 레이아웃을 안 잡아 canvasRect.rect가 실제 화면과
        // 다를 수 있다 — 그 값으로 바 높이를 재면 두께가 틀어진다. 강제로 한 번 갱신하고 계산한다.
        Canvas.ForceUpdateCanvases();
        SetHeight(BarHeight());
    }

    Coroutine Run(float target)
    {
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(Animate(target));
        return anim;
    }

    IEnumerator Animate(float target)
    {
        float from = top.sizeDelta.y;
        float t = 0f;
        // 컷신 중 timeScale이 흔들려도(히트스톱·시간가속) 연출 속도가 일정하도록 unscaled를 쓴다.
        // 단 unscaledDeltaTime은 maximumDeltaTime으로 클램프되지 않아 씬 로드 직후 첫 프레임이 1초를
        // 넘길 수 있다 — 그대로 두면 바가 자라는 게 한 프레임에 끝난다(ScreenBlackout 주석 참고).
        while (t < duration)
        {
            t += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            SetHeight(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration))));
            yield return null;
        }
        SetHeight(target);
        anim = null;
    }

    void SetHeight(float h)
    {
        top.sizeDelta = new Vector2(0f, h);
        bottom.sizeDelta = new Vector2(0f, h);
    }
}
