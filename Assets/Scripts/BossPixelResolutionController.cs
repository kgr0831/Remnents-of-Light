using UnityEngine;

// 인스펙터 슬라이더로 보스 픽셀레이션 해상도를 즉시 조절한다 — 플레이 모드가 아니어도 에디터에서
// 바로 반영된다. RenderTexture는 Create() 이후엔 크기를 못 바꾸므로, 값이 바뀌면 기존 RT를
// Release하고 같은 종횡비(16:9, BossPixelCamera의 orthographic 설정 기준)로 다시 만든다.
//
// OnDisable에서 RT를 Release하지 않는다 — ExecuteAlways라 플레이모드 진입/종료마다
// Disable→Enable이 타이밍 어긋나게 불릴 수 있는데, 그 사이에 디스플레이 머티리얼이 이미
// Release된(파괴된) RT를 계속 참조해 흰 화면(텍스처 유실)으로 보이는 문제가 있었다. 대신
// Update에서 RT가 유실된 걸 감지하면 스스로 다시 만든다(자가복구).
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class BossPixelResolutionController : MonoBehaviour
{
    // 낮을수록 더 거칠게(강하게) 픽셀화되고, 높을수록 세밀해진다.
    [Range(16, 480)] public int pixelHeight = 90;

    const float Aspect = 16f / 9f;

    Camera cam;
    RenderTexture rt;
    int lastHeight = -1;

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        lastHeight = -1; // 강제로 다시 만들도록
        Rebuild();
    }

    void OnValidate()
    {
        if (cam == null) cam = GetComponent<Camera>();
        Rebuild();
    }

    void Update()
    {
        // 플레이모드 진입/종료 등으로 RT가 유실된 경우를 대비한 자가복구.
        if (rt == null) Rebuild();
    }

    void Rebuild()
    {
        if (cam == null) return;
        if (pixelHeight == lastHeight && rt != null) return;
        lastHeight = pixelHeight;

        int width = Mathf.Max(1, Mathf.RoundToInt(pixelHeight * Aspect));

        if (rt != null)
        {
            cam.targetTexture = null;
            rt.Release();
        }

        rt = new RenderTexture(width, pixelHeight, 16, RenderTextureFormat.ARGB32);
        rt.filterMode = FilterMode.Point;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.name = "RT_BossPixel_Live";
        cam.targetTexture = rt;

        // 화면에 합성하는 Quad(BossPixelDisplay, 같은 부모 아래 형제)의 머티리얼 텍스처를 새 RT로 갱신.
        // sharedMaterial(공유 에셋)이 아니라 material(런타임 전용 인스턴스)에 설정한다 — RT는 저장
        // 불가능한 런타임 텍스처라, sharedMaterial에 넣으면 씬/에셋 저장 시 그 참조가 깨져
        // 흰 화면(텍스처 유실)으로 돌아가는 문제가 있었다.
        Transform display = transform.parent != null ? transform.parent.Find("BossPixelDisplay") : null;
        if (display != null)
        {
            MeshRenderer mr = display.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null) mr.material.SetTexture("_MainTex", rt);
        }
    }
}
