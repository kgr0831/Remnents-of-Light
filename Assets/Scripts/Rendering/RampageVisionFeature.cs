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
    // 기본 대상: Player(8) + PlayerInvincible(14) + VFXNoGrayscale(15) + RampageOutline(21) = 2146560
    public LayerMask ProtectedLayerMask = 2146560;

    // ★ 이 레이어들은 보호 대상이되 **다른 보호 레이어보다 먼저** 그린다(2026-08-11).
    // 덧그리기는 "Universal2D 리스트 → SRPDefaultUnlit 리스트" 두 번으로 나뉘고, 정렬은 리스트
    // 안에서만 적용된다. 그래서 LightMode 태그가 없는 적·팬 아웃라인(Custom/RampageOutline)이
    // 언릿 리스트로 가면서, sortingOrder가 2 대 10인데도 플레이어(Sprite-Lit-Default =
    // Universal2D 리스트)를 덮어버렸다 — 코어가 owner 실루엣을 거의 검정·불투명으로 칠하므로
    // 플레이어가 통째로 사라졌다(사용자 리포트 2026-08-11 "팬이 플레이어보다 앞에 렌더링됨").
    // 아웃라인만 전용 레이어로 갈라 맨 먼저 그리면, 플레이어·블룸·지형 아웃라인의 기존 상대
    // 순서는 그대로 두고 이 문제만 없앨 수 있다.
    // 기본 대상: RampageOutline(21) = 2097152
    public LayerMask OutlineFirstLayerMask = 2097152;

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
        _pass.Setup(Intensity, Center, Radius, Softness, Darkness, ProtectedLayerMask, OutlineFirstLayerMask);
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

        private int _protectedLayerMask; // 보통 순서(스프라이트 → 언릿)로 그릴 레이어
        private int _outlineFirstMask;   // 그보다 앞서 그릴 레이어(적·팬 아웃라인)

        public DarknessPass(Material mat, Material protectedMat)
        {
            _mat = mat;
            _protectedMat = protectedMat;
            profilingSampler = new ProfilingSampler("RampageVision");
        }

        public void Setup(float intensity, Vector2 center, float radius, float softness,
                          float darkness, int protectedLayerMask, int outlineFirstMask)
        {
            _mat.SetFloat(_intensityId, intensity);
            _mat.SetVector(_centerId, new Vector4(center.x, center.y, 0f, 0f));
            _mat.SetFloat(_radiusId, radius);
            _mat.SetFloat(_softnessId, softness);
            _mat.SetFloat(_darknessId, darkness);
            float aspect = (Screen.height > 0) ? (float)Screen.width / Screen.height : 1.7777f;
            _mat.SetFloat(_aspectId, aspect);
            // 두 그룹은 서로 겹치지 않게 나눈다 — 안 나누면 아웃라인이 두 번 그려진다.
            _outlineFirstMask   = protectedLayerMask & outlineFirstMask;
            _protectedLayerMask = protectedLayerMask & ~outlineFirstMask;
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
            if ((_protectedLayerMask != 0 || _outlineFirstMask != 0) && _protectedMat != null)
            {
                CreateLists(renderGraph, frameData, _outlineFirstMask,   out var backSprite,  out var backUnlit);
                CreateLists(renderGraph, frameData, _protectedLayerMask, out var frontSprite, out var frontUnlit);

                using (var builder = renderGraph.AddRasterRenderPass<ProtectPassData>("RampageVisionProtectLayer", out var pData, profilingSampler))
                {
                    pData.backSpriteList  = backSprite;
                    pData.backUnlitList   = backUnlit;
                    pData.spriteList      = frontSprite;
                    pData.unlitList       = frontUnlit;
                    builder.UseRendererList(backSprite);
                    builder.UseRendererList(backUnlit);
                    builder.UseRendererList(frontSprite);
                    builder.UseRendererList(frontUnlit);
                    // ReadWrite: 기존 화면 내용을 유지한 채 그 위에 알파 블렌딩(Write면 이전 내용이 버려짐)
                    builder.SetRenderAttachment(src, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((ProtectPassData data, RasterGraphContext context) =>
                    {
                        // 적·팬 아웃라인 먼저 → 그 위에 플레이어 → 그 위에 나머지 언릿(블룸·지형 아웃라인·TMP).
                        // 리스트 사이에는 sortingOrder가 적용되지 않으므로 이 호출 순서가 곧 그리기 순서다.
                        context.cmd.DrawRendererList(data.backSpriteList);
                        context.cmd.DrawRendererList(data.backUnlitList);
                        context.cmd.DrawRendererList(data.spriteList);
                        context.cmd.DrawRendererList(data.unlitList);
                    });
                }
            }
        }

        /// <summary>
        /// 한 레이어 마스크에 대해 스프라이트(Universal2D)·언릿(SRPDefaultUnlit) 두 리스트를 만든다.
        /// 마스크가 0이면 아무것도 안 그리는 빈 리스트가 나오므로 별도 분기가 필요 없다.
        /// </summary>
        private void CreateLists(RenderGraph renderGraph, ContextContainer frameData, int layerMask,
                                 out RendererListHandle spriteList, out RendererListHandle unlitList)
        {
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData    cameraData    = frameData.Get<UniversalCameraData>();
            UniversalLightData     lightData     = frameData.Get<UniversalLightData>();

            var filterSettings = new FilteringSettings(RenderQueueRange.all, layerMask);

            // 1) 스프라이트(Universal2D): 2D 라이트 전역 바인딩 의존을 피해 언릿으로 오버라이드.
            var spriteDraw = RenderingUtils.CreateDrawingSettings(
                _sprite2DTag, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
            spriteDraw.overrideMaterial = _protectedMat;
            spriteDraw.overrideMaterialPassIndex = 0;
            spriteList = renderGraph.CreateRendererList(
                new RendererListParams(renderingData.cullResults, spriteDraw, filterSettings));

            // 2) 지형 아웃라인 메시(Sprites/Default) · TMP 텍스트: 자기 머티리얼(정점 색 · 폰트 아틀라스)을
            //    그대로 써야 하므로 오버라이드하지 않는다.
            var unlitDraw = RenderingUtils.CreateDrawingSettings(
                _unlitTag, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
            unlitList = renderGraph.CreateRendererList(
                new RendererListParams(renderingData.cullResults, unlitDraw, filterSettings));
        }

        private class PassData
        {
            public Material      material;
            public TextureHandle src;
        }

        private class ProtectPassData
        {
            public RendererListHandle backSpriteList; // 적·팬 아웃라인(먼저)
            public RendererListHandle backUnlitList;
            public RendererListHandle spriteList;     // 플레이어 등 나머지 보호 레이어
            public RendererListHandle unlitList;
        }
    }
}
