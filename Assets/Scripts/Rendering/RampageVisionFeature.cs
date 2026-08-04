using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// URP Renderer Feature: 폭주(Rampage) 시야 제한 — 플레이어 주변 반경만 남기고 화면을 암전시킨다.
/// Renderer2D.asset에 추가 후 RampageVisionFeature.Instance 로 제어(RampageVisionFx가 매 프레임 갱신).
///
/// GrayscaleRendererFeature와 구조가 같다(같은 Blit 2패스 + 보호 레이어 덧그리기). 다른 것은 셰이더뿐 —
/// 그레이스케일은 반경 "안"을 흑백으로 만들지만 이쪽은 반경 "밖"을 어둡게 만든다.
/// 두 피처를 하나로 합치지 않은 이유: 회피 저스트(흑백)와 폭주(암전)가 동시에 걸릴 수 있고,
/// 그때 Center/Radius를 서로 덮어쓰면 두 연출이 다 깨진다.
/// </summary>
[System.Serializable]
public class RampageVisionFeature : ScriptableRendererFeature
{
    // 런타임 제어용 정적 참조(GrayscaleRendererFeature와 같은 패턴)
    public static RampageVisionFeature Instance { get; private set; }

    [Range(0f, 1f)]
    public float Intensity = 0f;   // 진입 0.25s / 해제 0.30s 페이드에 쓴다

    [Header("가시 영역 (이 밖은 어둠)")]
    public Vector2 Center = new Vector2(0.5f, 0.5f); // 스크린 UV. 플레이어 위치로 매 프레임 갱신
    // ★ 사용자 지시(2026-08-01): "폭주 상태에서 아예 플레이어만 보이게".
    //   그래서 가시 원을 아예 없앤다 — RampageVisionFx가 음수 반경을 넣어 화면 전체를 암전시키고,
    //   플레이어와 아웃라인은 보호 레이어 덧그리기로만 살아남는다(월드 조명이 아니라 "그것만 보인다").
    public float Radius = -1f;
    public float Softness = 0.0001f;
    [Range(0f, 1f)]
    public float Darkness = 1f; // 완전 암전. 맵이 비쳐 보이던 문제(사용자 스크린샷)의 직접 원인이 0.92였다

    [Header("보호 레이어 (어둠 위에 덧그림 — 아웃라인 · 플레이어)")]
    // GrayscaleRendererFeature와 같은 방식: 어둠이 끝난 뒤 이 레이어들을 카메라 색 버퍼 위에 한 번 더
    // 그린다. 이미 어두워진 픽셀 위에 원색이 알파 블렌딩으로 덮여 복원된다.
    // 기본 대상: Player(8) + PlayerInvincible(14) + VFXNoGrayscale(15) = 49408
    public LayerMask ProtectedLayerMask = 49408;

    private DarknessPass _pass;
    private Material     _material;
    private Material     _protectedMaterial;

    // ── ScriptableRendererFeature 구현 ────────────────────────────────────

    public override void Create()
    {
        Instance  = this;
        _material = CoreUtils.CreateEngineMaterial("Hidden/ScreenDarkness");
        // 덧그리기 전용 언릿 머티리얼(2D 라이트 전역 바인딩 의존 제거) — 그레이스케일과 같은 이유.
        _protectedMaterial = CoreUtils.CreateEngineMaterial("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        _pass = new DarknessPass(_material, _protectedMaterial);
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
        if (renderingData.cameraData.renderType == CameraRenderType.Overlay) return;
        _pass.Setup(Intensity, Center, Radius, Softness, Darkness, ProtectedLayerMask);
        renderer.EnqueuePass(_pass);
    }

    // ── Inner Pass ────────────────────────────────────────────────────────

    private class DarknessPass : ScriptableRenderPass
    {
        private readonly Material _mat;
        private readonly Material _protectedMat;
        private static readonly int _intensityId = Shader.PropertyToID("_Intensity");
        private static readonly int _centerId    = Shader.PropertyToID("_Center");
        private static readonly int _radiusId    = Shader.PropertyToID("_Radius");
        private static readonly int _softnessId  = Shader.PropertyToID("_Softness");
        private static readonly int _aspectId    = Shader.PropertyToID("_Aspect");
        private static readonly int _darknessId  = Shader.PropertyToID("_Darkness");
        private static readonly ShaderTagId _sprite2DTag = new ShaderTagId("Universal2D");
        // 라인 메시(Sprites/Default)·TMP 텍스트처럼 LightMode 태그가 없는 패스는 여기로 수집된다.
        private static readonly ShaderTagId _unlitTag = new ShaderTagId("SRPDefaultUnlit");

        private int _protectedLayerMask;

        public DarknessPass(Material mat, Material protectedMat)
        {
            _mat = mat;
            _protectedMat = protectedMat;
            profilingSampler = new ProfilingSampler("RampageVision");
        }

        public void Setup(float intensity, Vector2 center, float radius, float softness,
                          float darkness, int protectedLayerMask)
        {
            _mat.SetFloat(_intensityId, intensity);
            _mat.SetVector(_centerId, new Vector4(center.x, center.y, 0f, 0f));
            _mat.SetFloat(_radiusId, radius);
            _mat.SetFloat(_softnessId, softness);
            _mat.SetFloat(_darknessId, darkness);
            float aspect = (Screen.height > 0) ? (float)Screen.width / Screen.height : 1.7777f;
            _mat.SetFloat(_aspectId, aspect);
            _protectedLayerMask = protectedLayerMask;
        }

        // RenderGraph 전용(Unity 6 기본 렌더 경로). Compatibility Mode는 이 프로젝트가 쓰지 않아 미구현.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_mat == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle src = resourceData.activeColorTexture;

            TextureDesc desc = renderGraph.GetTextureDesc(src);
            desc.depthBufferBits = 0;
            TextureHandle tempRT = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("RampageVisionPass", out var passData, profilingSampler))
            {
                passData.material = _mat;
                passData.src      = src;

                builder.UseTexture(src, AccessFlags.Read);
                builder.SetRenderAttachment(tempRT, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("RampageVisionApply", out var passData, profilingSampler))
            {
                passData.src = tempRT;
                builder.UseTexture(tempRT, AccessFlags.Read);
                builder.SetRenderAttachment(src, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), 0, false);
                });
            }

            // ── 보호 레이어(아웃라인 · 플레이어)를 어둠 위에 원색으로 덧그린다 ──
            // 이게 없으면 아웃라인도 같이 어두워져 시야 제한 자체가 성립하지 않는다.
            if (_protectedLayerMask != 0 && _protectedMat != null)
            {
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData    cameraData    = frameData.Get<UniversalCameraData>();
                UniversalLightData     lightData     = frameData.Get<UniversalLightData>();

                var filterSettings = new FilteringSettings(RenderQueueRange.all, _protectedLayerMask);

                // 1) 스프라이트(Universal2D): 2D 라이트 전역 바인딩 의존을 피해 언릿으로 오버라이드.
                var spriteDraw = RenderingUtils.CreateDrawingSettings(
                    _sprite2DTag, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
                spriteDraw.overrideMaterial = _protectedMat;
                spriteDraw.overrideMaterialPassIndex = 0;
                RendererListHandle spriteList = renderGraph.CreateRendererList(
                    new RendererListParams(renderingData.cullResults, spriteDraw, filterSettings));

                // 2) 지형 아웃라인 메시(Sprites/Default) · TMP 텍스트: 자기 머티리얼(정점 색 · 폰트 아틀라스)을
                //    그대로 써야 하므로 오버라이드하지 않는다.
                var unlitDraw = RenderingUtils.CreateDrawingSettings(
                    _unlitTag, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
                RendererListHandle unlitList = renderGraph.CreateRendererList(
                    new RendererListParams(renderingData.cullResults, unlitDraw, filterSettings));

                using (var builder = renderGraph.AddRasterRenderPass<ProtectPassData>("RampageVisionProtectLayer", out var pData, profilingSampler))
                {
                    pData.spriteList = spriteList;
                    pData.unlitList = unlitList;
                    builder.UseRendererList(spriteList);
                    builder.UseRendererList(unlitList);
                    // ReadWrite: 기존 화면 내용을 유지한 채 그 위에 알파 블렌딩(Write면 이전 내용이 버려짐)
                    builder.SetRenderAttachment(src, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((ProtectPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.spriteList);
                        context.cmd.DrawRendererList(data.unlitList);
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
            public RendererListHandle spriteList;
            public RendererListHandle unlitList;
        }
    }
}
