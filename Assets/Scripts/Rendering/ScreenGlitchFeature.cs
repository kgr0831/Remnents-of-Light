using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// URP Renderer Feature: 자아 고갈 화면 글리치 — 미세 노이즈 + 스캔라인 떨림(사용자 지시 2026-08-01).
/// Renderer2D.asset에 추가 후 ScreenGlitchFeature.Instance 로 제어(ScreenGlitchFx가 매 프레임 갱신).
///
/// GrayscaleRendererFeature/RampageVisionFeature와 같은 Blit 2패스 구조를 재사용한다. 다만 이쪽은
/// "보호 레이어" 덧그리기가 없다 — 저 둘은 화면 일부를 숨기고 특정 레이어만 복원해야 하지만,
/// 글리치는 화면 전체(플레이어 포함)에 잡음을 더하기만 하면 되므로 복원할 대상이 없다.
/// </summary>
[System.Serializable]
public class ScreenGlitchFeature : ScriptableRendererFeature
{
    public static ScreenGlitchFeature Instance { get; private set; }

    [Range(0f, 1f)]
    public float Intensity = 0f; // 진입 0.15s / 해제 0.20s 페이드에 쓴다(ScreenGlitchFx)
    public float Seed = 0f;      // ScreenGlitchFx가 초당 20회 계단식으로 갱신

    [Header("스캔라인 떨림")]
    public float ScanlineDensity = 90f;
    public float ScanlineJitter = 0.02f;

    [Header("미세 노이즈")]
    [Range(0f, 0.5f)] public float NoiseAmount = 0.06f;

    DarknessLikePass _pass;
    Material _material;

    public override void Create()
    {
        Instance = this;
        _material = CoreUtils.CreateEngineMaterial("Hidden/ScreenGlitch");
        _pass = new DarknessLikePass(_material);
        // BeforeRenderingPostProcessing — GrayscaleRendererFeature/RampageVisionFeature와 같은 시점.
        // AfterRenderingPostProcessing을 쓰면 이 패스가 파이프라인의 마지막이 되어 activeColorTexture가
        // 백버퍼로 바로 연결되고, 그러면 아래 RecordRenderGraph의 isActiveTargetBackBuffer 가드에 걸려
        // 패스가 조용히 아예 안 그려진다(두 선례 피처가 같은 이유로 이 시점을 쓴다).
        // 같은 이벤트를 쓰는 패스는 Renderer2D.asset의 피처 목록 순서로 실행되므로, 폭주 중에도 이
        // 글리치가 RampageVisionFeature의 보호 레이어(플레이어) 위에 겹치도록 그 피처보다 뒤에 등록한다.
        _pass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (Intensity <= 0.001f) return;
        if (renderingData.cameraData.renderType == CameraRenderType.Overlay) return;
        _pass.Setup(Intensity, Seed, ScanlineDensity, ScanlineJitter, NoiseAmount);
        renderer.EnqueuePass(_pass);
    }

    private class DarknessLikePass : ScriptableRenderPass
    {
        private readonly Material _mat;
        private static readonly int _intensityId = Shader.PropertyToID("_Intensity");
        private static readonly int _seedId = Shader.PropertyToID("_Seed");
        private static readonly int _scanlineDensityId = Shader.PropertyToID("_ScanlineDensity");
        private static readonly int _scanlineJitterId = Shader.PropertyToID("_ScanlineJitter");
        private static readonly int _noiseAmountId = Shader.PropertyToID("_NoiseAmount");

        public DarknessLikePass(Material mat)
        {
            _mat = mat;
            profilingSampler = new ProfilingSampler("ScreenGlitch");
        }

        public void Setup(float intensity, float seed, float scanlineDensity, float scanlineJitter, float noiseAmount)
        {
            _mat.SetFloat(_intensityId, intensity);
            _mat.SetFloat(_seedId, seed);
            _mat.SetFloat(_scanlineDensityId, scanlineDensity);
            _mat.SetFloat(_scanlineJitterId, scanlineJitter);
            _mat.SetFloat(_noiseAmountId, noiseAmount);
        }

        // RenderGraph 전용(Unity 6 기본 렌더 경로). Compatibility Mode는 이 프로젝트가 쓰지 않아 미구현
        // (RampageVisionFeature/GrayscaleRendererFeature와 같은 전제).
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_mat == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle src = resourceData.activeColorTexture;

            TextureDesc desc = renderGraph.GetTextureDesc(src);
            desc.depthBufferBits = 0;
            TextureHandle tempRT = renderGraph.CreateTexture(desc);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("ScreenGlitchPass", out var passData, profilingSampler))
            {
                passData.material = _mat;
                passData.src = src;

                builder.UseTexture(src, AccessFlags.Read);
                builder.SetRenderAttachment(tempRT, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("ScreenGlitchApply", out var passData, profilingSampler))
            {
                passData.src = tempRT;
                builder.UseTexture(tempRT, AccessFlags.Read);
                builder.SetRenderAttachment(src, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }

        private class PassData
        {
            public Material material;
            public TextureHandle src;
        }
    }
}
