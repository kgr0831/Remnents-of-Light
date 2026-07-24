using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// URP Renderer Feature: 화면 전체 흑백(그레이스케일).
/// Renderer2D.asset에 추가 후 GrayscaleRendererFeature.Instance.Intensity (0~1) 로 제어.
/// 회피 저스트(위치타임) 슬로우모션 동안 화면을 흑백으로 만드는 데 사용.
/// (UniTrio-Game-2026 Assets/Scripts/Rendering/GrayscaleRendererFeature.cs 그대로 이식)
/// </summary>
[System.Serializable]
public class GrayscaleRendererFeature : ScriptableRendererFeature
{
    // 런타임에서 Intensity를 조작하기 위한 정적 참조
    public static GrayscaleRendererFeature Instance { get; private set; }

    [Range(0f, 1f)]
    public float Intensity = 0f;

    [Header("방사형 (중심에서 퍼짐)")]
    public Vector2 Center   = new Vector2(0.5f, 0.5f); // 스크린 UV(0~1). 보통 플레이어 위치
    public float   Radius   = 10f;                     // UV 기준 반경. 크게 잡으면 화면 전체 흑백
    // 0.06(원본 기본값)은 반경이 프레임당 늘어나는 폭보다 경계가 얇아 계단식으로 "뚝뚝" 끊겨 보이는
    // 문제가 있어(사용자 피드백) 훨씬 넓게 잡음 — 프레임 레이트가 들쭉날쭉해도 경계가 부드럽게 블렌딩됨.
    public float   Softness = 0.35f;

    [Header("포커스 (컬러 유지 영역 — 적 강조)")]
    public Vector2 FocusCenter   = new Vector2(0.5f, 0.5f); // 컬러로 남길 중심(스크린 UV)
    public float   FocusRadius   = 0f;                       // 0이면 비활성
    public float   FocusSoftness = 0.04f;

    [Header("보호 레이어 (그레이스케일 영향 안 받음 — 플레이어 스프라이트 · 대시 잔상)")]
    // 구현 방식: 이 레이어들을 "그레이스케일이 끝난 뒤 카메라 색 버퍼 위에 한 번 더 그린다".
    //  - 1차 시도(오버레이 카메라 + cullingMask): Unity 6 RenderGraph 버그로 포스트가 스택 카메라에
    //    그대로 적용돼 실패.
    //  - 2차 시도(별도 마스크 텍스처 + 전역 텍스처로 셰이더에서 재합성): 마스크 대상 오브젝트가 실제로
    //    존재하는 순간 화면 전체가 검게 깨지는 치명적 회귀 발생 → 폐기.
    //  - 현재(3차): 별도 텍스처도, 전역 텍스처 등록도, 셰이더 분기도 없이 같은 대상만 위에 덧그린다.
    //    이미 그려져 흑백이 된 픽셀 위에 원색이 알파 블렌딩으로 덮여 원색이 복원된다.
    public LayerMask ProtectedLayerMask;

    private GrayscalePass _pass;
    private Material       _material;
    private Material       _protectedMaterial;

    // ── ScriptableRendererFeature 구현 ────────────────────────────────────

    public override void Create()
    {
        Instance  = this;
        _material = CoreUtils.CreateEngineMaterial("Hidden/ScreenGrayscale");
        // 덧그리기 전용 언릿 머티리얼. 2D 라이트 텍스처(_ShapeLightTexture*)에 의존하는 Lit 패스를 우리
        // 커스텀 패스에서 그대로 쓰면 라이트 전역 바인딩에 의존하게 되므로, 라이트가 필요 없는 언릿으로
        // 오버라이드해 결과를 결정적으로 만든다(씬 조명은 Global Light 1개/intensity 1이라 룩 차이 없음).
        _protectedMaterial = CoreUtils.CreateEngineMaterial("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        _pass     = new GrayscalePass(_material, _protectedMaterial);
        // 포스트 프로세싱 직전에 적용해 CA 등과 자연스럽게 합성
        _pass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
        CoreUtils.Destroy(_protectedMaterial);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (Intensity <= 0.001f) return;
        // 오버레이 카메라(붉은 아웃라인 전용)는 흑백을 적용하지 않는다 → 아웃라인이 컬러로 남는다.
        if (renderingData.cameraData.renderType == CameraRenderType.Overlay) return;
        _pass.Setup(Intensity, Center, Radius, Softness, FocusCenter, FocusRadius, FocusSoftness, ProtectedLayerMask);
        renderer.EnqueuePass(_pass);
    }

    // ── Inner Pass ────────────────────────────────────────────────────────

    private class GrayscalePass : ScriptableRenderPass
    {
        private readonly Material _mat;
        private readonly Material _protectedMat;
        private static readonly int _intensityId = Shader.PropertyToID("_Intensity");
        private static readonly int _centerId    = Shader.PropertyToID("_Center");
        private static readonly int _radiusId    = Shader.PropertyToID("_Radius");
        private static readonly int _softnessId  = Shader.PropertyToID("_Softness");
        private static readonly int _aspectId    = Shader.PropertyToID("_Aspect");
        private static readonly int _focusCenterId   = Shader.PropertyToID("_FocusCenter");
        private static readonly int _focusRadiusId   = Shader.PropertyToID("_FocusRadius");
        private static readonly int _focusSoftnessId = Shader.PropertyToID("_FocusSoftness");
        private static readonly ShaderTagId _sprite2DTag = new ShaderTagId("Universal2D");

        private int _protectedLayerMask;

        public GrayscalePass(Material mat, Material protectedMat)
        {
            _mat = mat;
            _protectedMat = protectedMat;
            profilingSampler = new ProfilingSampler("Grayscale");
        }

        public void Setup(float intensity, Vector2 center, float radius, float softness,
                          Vector2 focusCenter, float focusRadius, float focusSoftness, int protectedLayerMask)
        {
            _mat.SetFloat(_intensityId, intensity);
            _mat.SetVector(_centerId, new Vector4(center.x, center.y, 0f, 0f));
            _mat.SetFloat(_radiusId, radius);
            _mat.SetFloat(_softnessId, softness);
            _mat.SetVector(_focusCenterId, new Vector4(focusCenter.x, focusCenter.y, 0f, 0f));
            _mat.SetFloat(_focusRadiusId, focusRadius);
            _mat.SetFloat(_focusSoftnessId, focusSoftness);
            float aspect = (Screen.height > 0) ? (float)Screen.width / Screen.height : 1.7777f;
            _mat.SetFloat(_aspectId, aspect);
            _protectedLayerMask = protectedLayerMask;
        }

        // ── RenderGraph 전용(Unity 6 기본 렌더 경로) — Compatibility Mode(OnCameraSetup/Execute)는
        // 우리 프로젝트가 켠 적 없는 레거시 경로라 미구현(죽은 코드 방지, 최신 API만 유지).
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_mat == null) return;

            // 유니버설 리소스에서 가용한 카메라 색상 리소스를 가져옵니다.
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle src = resourceData.activeColorTexture;

            // 임시 텍스처(Blit 전용) 설명자 설정
            TextureDesc desc = renderGraph.GetTextureDesc(src);
            desc.depthBufferBits = 0;
            TextureHandle tempRT = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("GrayscalePass", out var passData, profilingSampler))
            {
                passData.material = _mat;
                passData.src      = src;

                // src는 읽기 전용, tempRT에 결과 쓰기
                builder.UseTexture(src, AccessFlags.Read);
                builder.SetRenderAttachment(tempRT, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    // Blitter를 사용하여 src -> tempRT로 블릿 (Material 적용)
                    Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // 결과를 다시 src로 되돌리는 패스
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("GrayscaleApply", out var passData, profilingSampler))
            {
                passData.src = tempRT;
                builder.UseTexture(tempRT, AccessFlags.Read);
                builder.SetRenderAttachment(src, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), 0, false);
                });
            }

            // ── 보호 레이어(플레이어 스프라이트 · 대시/카운터 잔상)를 흑백 결과 위에 원색으로 덧그린다 ──
            // 이미 화면에 그려져 흑백이 된 같은 픽셀 위에 알파 블렌딩으로 덮으므로 원색이 복원된다.
            // 별도 텍스처/전역 텍스처/셰이더 분기가 전혀 없어 이전 마스크 방식의 "화면 전체가 검게
            // 깨지는" 회귀 경로 자체가 존재하지 않는다.
            if (_protectedLayerMask != 0 && _protectedMat != null)
            {
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData    cameraData    = frameData.Get<UniversalCameraData>();
                UniversalLightData     lightData     = frameData.Get<UniversalLightData>();

                var filterSettings = new FilteringSettings(RenderQueueRange.all, _protectedLayerMask);
                var drawSettings = RenderingUtils.CreateDrawingSettings(
                    _sprite2DTag, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
                drawSettings.overrideMaterial = _protectedMat;
                drawSettings.overrideMaterialPassIndex = 0;
                var rendererListParams = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);
                RendererListHandle rendererListHandle = renderGraph.CreateRendererList(rendererListParams);

                using (var builder = renderGraph.AddRasterRenderPass<ProtectPassData>("GrayscaleProtectLayer", out var pData, profilingSampler))
                {
                    pData.rendererListHandle = rendererListHandle;
                    builder.UseRendererList(rendererListHandle);
                    // ReadWrite: 기존 화면 내용을 유지한 채 그 위에 알파 블렌딩(Write면 이전 내용이 버려짐)
                    builder.SetRenderAttachment(src, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((ProtectPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.rendererListHandle);
                    });
                }
            }
        }

        private class PassData
        {
            public Material      material;
            public TextureHandle src;
        }

        private class ProtectPassData
        {
            public RendererListHandle rendererListHandle;
        }
    }
}
