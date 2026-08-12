using UnityEngine;
using UnityEngine.Rendering;

// 보스 픽셀 디스플레이 리그(BossPixelCamera + BossPixelDisplay Quad)를 메인 카메라에 맞춰 유지한다.
// 인스펙터 슬라이더로 픽셀레이션 해상도를 즉시 조절할 수 있고(플레이 모드가 아니어도 반영),
// RenderTexture는 Create() 이후엔 크기를 못 바꾸므로 값이 바뀌면 Release 후 다시 만든다.
//
// OnDisable에서 RT를 Release하지 않는다 — ExecuteAlways라 플레이모드 진입/종료마다
// Disable→Enable이 타이밍 어긋나게 불릴 수 있는데, 그 사이에 디스플레이 머티리얼이 이미
// Release된(파괴된) RT를 계속 참조해 흰 화면(텍스처 유실)으로 보이는 문제가 있었다. 대신
// Update에서 RT가 유실된 걸 감지하면 스스로 다시 만든다(자가복구).
//
// ⚠️ 버그 수정(2026-08-09, 사용자 스크린샷 "보스가 네모나게 짤림"):
//    예전엔 이 카메라의 orthographicSize(9)와 Quad 스케일(32x18)이 상수로 박혀 있었다. 그런데
//    실제 화면은 SectionCamera.EnterRoom이 룸 크기대로 orthographicSize를 바꾼다 —
//    BossRoomTrigger들은 48x27 / 40x22.5 / 30x16.875라서 ortho가 13.5 / 11.25 / 8.44가 된다.
//    ① 픽셀 카메라는 여전히 32x18 월드 영역만 찍으니 그 밖의 보스 몸통(스케일 30)이 RT 경계에서
//       잘려 나가고, ② Quad도 32x18 그대로라 48x27 화면에서는 가운데 66%만 차지해 그 잘린
//       사각형이 화면 한복판에 그대로 보였다. 그래서 매 프레임 메인 카메라의 ortho/aspect를
//       그대로 따라가게 하고 Quad도 정확히 그 화면 크기로 맞춘다(=RT가 화면과 1:1).
//       이러면 보스는 "화면 밖"에서만 잘리므로 눈에 보이는 잘림이 사라진다.
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class BossPixelResolutionController : MonoBehaviour
{
    // 낮을수록 더 거칠게(강하게) 픽셀화되고, 높을수록 세밀해진다.
    // 상한 480 → 1500(사용자 지시 2026-08-13) — Map-test가 이미 480(옛 상한)에 붙어 있어 더
    // 세밀하게 올릴 여지가 없었다. 1500이면 16:9 기준 RT가 2668x1500(≈16MB)까지 커진다.
    [Range(16, 1500)] public int pixelHeight = 90;

    // 사용자 리포트(2026-08-09): "보스가 일부 오브젝트를 감춘다" — Quad의 SortingOrder가 1이라
    // 광원 오브젝트(LightObjects/OBJ_*, order 0) 위에 그려져 그것들을 덮어버렸다.
    //
    // ⚠️ 왜 하필 0인가(플레이 모드 실측으로 결정, 2026-08-09):
    //    처음엔 -100으로 내렸더니 보스가 아예 사라졌다 — 배경 하늘 타일맵(Grid (4)/d)이 order -1로
    //    화면을 100% 덮고 있어서 그 뒤로 숨은 것. 즉 보스는 "하늘(-1)보다 앞, 나머지 전부보다 뒤"라는
    //    좁은 자리에 들어가야 한다. 씬에서 order -1인 지형 스프라이트(Floor·Plat·Wall)는 전부
    //    비활성(옛 테스트 잔재)이라 실질적으로 -1은 하늘 하나뿐이므로 0이면 하늘을 이긴다.
    //    같은 order 0인 광원 오브젝트(OBJ_*)와는 거리로 갈린다 — 이 Quad는 카메라 자식으로 60유닛
    //    앞(월드 z=+50)에 있고 OBJ는 z≈0(카메라에서 10유닛)이라, 직교 카메라의 뒤→앞 정렬에서
    //    항상 보스가 먼저 그려진다(=뒤에 깔린다). 씬을 건드리지 않고 원하는 순서가 나오는 값이다.
    //    더 명시적으로 가고 싶으면 하늘 'd'를 -100쯤으로 내리고 이 값을 -50으로 두면 된다.
    //
    // ⚠️ 일부러 인스펙터에 노출하지 않는다(직렬화 금지). public 필드로 뒀더니 씬에 예전 값(-100)이
    //    남아 스크립트 기본값을 고쳐도 계속 -100이 적용됐다 — 이 값이 틀리면 보스가 통째로 사라지는데
    //    원인이 코드에 안 보인다. 튜닝이 필요하면 이 상수를 고친다.
    const int DisplaySortingOrder = 0;

    // 화면 전체를 덮는 이 Quad는 아웃라인 마스크(BossOverlapOutlineFeature)에서 반드시 빠져야
    // 한다(자기 자신을 "겹치는 요소"로 잡으면 화면 테두리에 흰 선이 생긴다). 레이어를 새로 파지
    // 않고 Renderer.renderingLayerMask 비트1로 구분한다 — 마스크 패스는 비트0만 그린다.
    public const uint DisplayRenderingLayerMask = 2u;

    const string DisplayName = "BossPixelDisplay";
    static readonly int IdMainTex = Shader.PropertyToID("_MainTex");

    Camera cam;
    Camera mainCam;
    Transform display;
    MeshRenderer displayRenderer;
    MaterialPropertyBlock mpb;

    RenderTexture rt;
    int lastWidth = -1, lastHeight = -1;

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        lastWidth = lastHeight = -1; // 강제로 다시 만들도록
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        Sync();
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    void OnValidate()
    {
        if (cam == null) cam = GetComponent<Camera>();
        Sync();
    }

    void LateUpdate()
    {
        Sync();
    }

    // SectionCamera는 LateUpdate에서 orthographicSize/rect를 바꾼다. 스크립트 실행 순서에 따라
    // 우리 LateUpdate가 먼저 돌면 한 프레임 늦은 값으로 Quad를 맞추게 되고, 룸 전환 슬라이드처럼
    // ortho가 매 프레임 변하는 구간에서 화면 가장자리에 얇은 어긋남이 보인다. 실제로 카메라가
    // 그려지기 직전에 한 번 더 맞춰 그 한 프레임을 없앤다.
    void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera rendering)
    {
        if (rendering == cam || (mainCam != null && rendering == mainCam)) Sync();
    }

    void Sync()
    {
        if (cam == null) return;

        if (mainCam == null || !mainCam.isActiveAndEnabled)
        {
            // 이 카메라는 Main Camera의 자식으로 붙어 있다(로컬 0,0,0). 부모에서 먼저 찾고 없으면 태그로.
            mainCam = transform.parent != null ? transform.parent.GetComponent<Camera>() : null;
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == cam) mainCam = null;
        }
        if (mainCam == null) return;

        float ortho = Mathf.Max(0.01f, mainCam.orthographicSize);
        // mainCam.aspect는 cam.rect(레터박스)까지 반영된 실제 화면비다 — 룸 레터박스가 걸린
        // 상태에서도 Quad가 화면에 정확히 들어맞게 하려면 Screen.width/height가 아니라 이걸 써야 한다.
        float aspect = Mathf.Max(0.01f, mainCam.aspect);

        cam.orthographic = true;
        cam.orthographicSize = ortho;
        // 메인 카메라보다 먼저 그려야 이번 프레임의 보스 그림이 이번 프레임 화면에 쓰인다.
        // (뒤에 그리면 Quad는 지금 위치, RT 내용은 직전 프레임 카메라 위치라 카메라가 움직일 때
        //  보스만 한 프레임 뒤처져 미끄러진다.)
        if (cam.depth >= mainCam.depth) cam.depth = mainCam.depth - 1f;
        // RT 가로 픽셀 수를 반올림하면서 생기는 미세한 비율 오차가 Quad와 어긋나지 않도록
        // 카메라 화면비를 직접 고정한다(매 프레임 다시 넣으므로 값이 굳지 않는다).
        cam.aspect = aspect;

        EnsureRenderTexture(aspect);
        SyncDisplay(ortho, aspect);
    }

    void EnsureRenderTexture(float aspect)
    {
        int height = Mathf.Max(16, pixelHeight);
        int width = Mathf.Max(16, Mathf.RoundToInt(height * aspect));

        // 플레이모드 진입/종료 등으로 RT가 유실된 경우까지 여기서 같이 자가복구된다.
        if (rt != null && rt.width == width && rt.height == height && lastWidth == width && lastHeight == height)
        {
            if (cam.targetTexture != rt) cam.targetTexture = rt;
            return;
        }

        lastWidth = width;
        lastHeight = height;

        if (rt != null)
        {
            cam.targetTexture = null;
            rt.Release();
        }

        rt = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Point;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.name = "RT_BossPixel_Live";
        cam.targetTexture = rt;

        // 화면에서 보스와 겹치는 요소에 흰 아웃라인을 그리는 렌더러 피처가 이 실루엣(RT 알파)을
        // 마스크로 쓴다. RT는 런타임 생성이라 에셋 참조로 연결할 수 없어 여기서 넘겨준다.
        BossOverlapOutlineFeature.BossTexture = rt;

        ApplyTextureToDisplay();
    }

    void SyncDisplay(float ortho, float aspect)
    {
        if (display == null)
        {
            display = transform.parent != null ? transform.parent.Find(DisplayName) : null;
            if (display == null) return;
            displayRenderer = display.GetComponent<MeshRenderer>();
            ApplyTextureToDisplay();
        }

        // Quad(1x1)를 메인 카메라의 화면 크기 그대로 키운다 → RT 1픽셀 = 화면 1픽셀 영역(1:1).
        Vector3 scale = new Vector3(ortho * 2f * aspect, ortho * 2f, 1f);
        if (display.localScale != scale) display.localScale = scale;

        Vector3 p = display.localPosition;
        if (p.x != 0f || p.y != 0f) display.localPosition = new Vector3(0f, 0f, p.z);

        if (displayRenderer == null) return;
        if (displayRenderer.sortingOrder != DisplaySortingOrder) displayRenderer.sortingOrder = DisplaySortingOrder;
        if (displayRenderer.renderingLayerMask != DisplayRenderingLayerMask)
            displayRenderer.renderingLayerMask = DisplayRenderingLayerMask;
    }

    // 화면에 합성하는 Quad(BossPixelDisplay)에 새 RT를 물린다.
    // ⚠️ sharedMaterial(공유 에셋)에 넣으면 안 된다 — RT는 저장 불가능한 런타임 텍스처라 씬/에셋
    //    저장 시 그 참조가 깨져 흰 화면(텍스처 유실)이 된다. 그렇다고 renderer.material을 쓰면
    //    에디트 모드에서 호출될 때마다 머티리얼이 복제돼 씬에 눌러앉는다(실제로 "(Instance)"가
    //    36번 중첩된 머티리얼이 씬에 저장돼 있었다). 둘 다 피하는 방법이 MaterialPropertyBlock이다.
    void ApplyTextureToDisplay()
    {
        if (displayRenderer == null)
        {
            if (display == null)
                display = transform.parent != null ? transform.parent.Find(DisplayName) : null;
            if (display != null) displayRenderer = display.GetComponent<MeshRenderer>();
            if (displayRenderer == null) return;
        }

        if (mpb == null) mpb = new MaterialPropertyBlock();
        displayRenderer.GetPropertyBlock(mpb);
        mpb.SetTexture(IdMainTex, rt);
        displayRenderer.SetPropertyBlock(mpb);
    }
}
