using UnityEngine;

// 대시 중 스폰되는 산데비스탄 스타일 잔상(에코) 한 조각.
// 스폰 시점의 스프라이트/방향/스케일/틴트를 복사한 뒤 알파를 페이드아웃하고 스스로 파괴된다.
// 런타임 전용 오브젝트 — 씬에 저장되지 않는다.
//
// 그레이스케일 제외: 1차 시도(오버레이 카메라+cullingMask 분리)는 Unity 6 URP RenderGraph의 알려진
// 버그(issuetracker.unity3d.com "Post processing effects do not work when an Overlay Camera is
// enabled in the Camera Stack")로 실제 작동 안 함을 직접 픽셀 측정(R=G=B 동일)으로 확인 후 되돌림.
// 2차: GrayscaleRendererFeature가 이 레이어(VFXNoGrayscale)를 별도 텍스처로 다시 그려 그레이스케일
// 결과 위에 원색으로 재합성하는 방식(마스크 재합성, 카메라 스택 불필요)으로 재시도.
public class DashAfterImage : MonoBehaviour
{
    SpriteRenderer sr;
    Color startColor;
    float lifetime;
    float age;

    // source(플레이어 SpriteRenderer)의 현재 프레임을 복제한 잔상을 생성한다.
    // 위치/회전/스케일을 그대로 복사해 플레이어 스프라이트와 형태(실루엣)가 항상 일치하도록 한다.
    public static void Spawn(SpriteRenderer source, Color tint, float lifetime, int sortingOffset)
    {
        var go = new GameObject("DashAfterImage");
        int noGrayscaleLayer = LayerMask.NameToLayer("VFXNoGrayscale");
        if (noGrayscaleLayer >= 0) go.layer = noGrayscaleLayer;
        var t = source.transform;
        go.transform.position = t.position;
        go.transform.rotation = t.rotation;
        go.transform.localScale = t.lossyScale;

        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = source.sprite;
        r.flipX = source.flipX;
        r.flipY = source.flipY;
        r.sortingLayerID = source.sortingLayerID;
        r.sortingOrder = source.sortingOrder + sortingOffset;
        r.color = tint;

        var img = go.AddComponent<DashAfterImage>();
        img.sr = r;
        img.startColor = tint;
        img.lifetime = lifetime;
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = lifetime > 0f ? age / lifetime : 1f;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        var c = startColor;
        c.a = startColor.a * (1f - t);
        sr.color = c;
    }
}
