using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

/// <summary>
/// URP Renderer Feature: 화면에서 보스(픽셀 디스플레이)와 겹치는 모든 요소의 테두리를 흰색으로 그린다.
/// 사용자 지시(2026-08-09): "보스와 화면상에서 겹치는 모든 요소의 아웃라인을 흰색으로 표시".
///
/// 동작:
///  1) OutlineLayers에 속한 렌더러를 Hidden/BossOverlapMask로 한 번 더 그려 "요소 실루엣" 마스크를 만든다.
///  2) 그 마스크의 안쪽 가장자리(=테두리)를 뽑아, 보스 RT의 알파가 있는 픽셀에서만 흰색으로 덧그린다.
///
/// 보스 실루엣(RenderTexture)은 런타임 생성이라 에셋 참조로 연결할 수 없어
/// BossPixelResolutionController가 <see cref="BossTexture"/>에 직접 넣어 준다(보스가 없는 씬에선 null → 패스 전체 스킵).
/// </summary>
[System.Serializable]
public class BossOverlapOutlineFeature : ScriptableRendererFeature
{
    /// <summary>보스 픽셀 카메라의 RenderTexture. BossPixelResolutionController가 매 재생성 때 갱신한다.</summary>
    public static Texture BossTexture;

    [Header("아웃라인 대상 (보스 본체 Quad는 renderingLayerMask로 자동 제외)")]
    // 기본값 = Player(8) + Ground(9) + Wall(10) + Enemy(11) — 보스 앞에 서는 실제 요소들.
    // ⚠️ Default(0)를 넣으면 안 된다(실측으로 잡은 함정, 2026-08-09): 배경 하늘 타일맵(Grid (4)/d)이
    //    레이어 0이고 화면을 100% 덮기 때문에, 마스크가 전면 1이 되어 "가장자리"가 아예 없어진다
    //    (선이 한 픽셀도 안 나옴). 하늘은 보스보다 뒤라 애초에 "겹치는 요소"도 아니다.
    //    UI(5)·PixelBoss(16)도 제외 — HUD와 보스 본체.
    public LayerMask OutlineLayers = (1 << 8) | (1 << 9) | (1 << 10) | (1 << 11);

    public Color OutlineColor = Color.white;

    // ⚠️ 정수 픽셀만 의미가 있다(사용자 리포트 "선이 깨진다", 2026-08-09): 마스크를 포인트
    //    샘플링하므로 1.5픽셀처럼 반 픽셀 오프셋을 주면 프래그먼트의 서브픽셀 위치에 따라 이웃이
    //    1픽셀이 됐다 2픽셀이 됐다 하면서 가장자리 판정이 튀어 선이 점선처럼 끊긴다.
    [Tooltip("선 두께(화면 픽셀) — 정수로 반올림해서 쓴다. 1이 가장 얇다")]
    [Range(1f, 4f)] public float Thickness = 1f;

    [Tooltip("보스 RT 알파가 이 값 이상인 픽셀만 '보스와 겹친다'로 본다")]
    [Range(0.01f, 1f)] public float BossAlphaCutoff = 0.08f;

    OutlinePass _pass;
    Material _maskMaterial;
    Material _outlineMaterial;

    public override void Create()
    {
        _maskMaterial = CoreUtils.CreateEngineMaterial("Hidden/BossOverlapMask");
        _outlineMaterial = CoreUtils.CreateEngineMaterial("Hidden/BossOverlapOutline");
        _pass = new OutlinePass(_maskMaterial, _outlineMaterial);
        // 포스트 프로세싱 직전 — 그레이스케일/폭주 비네트 같은 기존 피처보다 뒤에 등록하면
        // 그 결과 위에 그려져 아웃라인이 항상 흰색으로 남는다(피처 리스트 순서 = 실행 순서).
        _pass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_maskMaterial);
        CoreUtils.Destroy(_outlineMaterial);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_maskMaterial == null || _outlineMaterial == null) return;
        if (BossTexture == null) return;                       // 보스가 없는 씬 — 아무 비용도 들이지 않는다
        if (OutlineLayers == 0) return;

        var cameraData = renderingData.cameraData;
        // 게임 화면에만 적용한다. 씬 뷰는 보스 RT와 화면 좌표가 안 맞아(다른 카메라) 엉뚱한 곳에
        // 선이 생긴다.
        if (cameraData.cameraType != CameraType.Game) return;
        if (cameraData.renderType == CameraRenderType.Overlay) return;
        // 보스 픽셀 카메라 자신에게 걸리면 자기 RT에 선을 그려 되먹임이 생긴다 — 그 카메라만 뺀다.
        // ⚠️ "targetTexture != null이면 전부 스킵"으로 짰다가 스크린샷 캡처(카메라를 RT에 렌더)에서
        //    패스가 통째로 빠져 "아웃라인이 안 나온다"고 오진했다(2026-08-09 실측).
        if (cameraData.camera.targetTexture != null && cameraData.camera.targetTexture == BossTexture) return;

        _pass.Setup(OutlineLayers, OutlineColor, Thickness, BossAlphaCutoff);
        renderer.EnqueuePass(_pass);
    }

    class OutlinePass : ScriptableRenderPass
    {
        static readonly ShaderTagId Sprite2DTag = new ShaderTagId("Universal2D");
        // LightMode 태그가 없는 패스(예: TMP)는 SRPDefaultUnlit으로 수집된다 — URP 2D 렌더러가
        // 수집하는 태그도 이 둘뿐이다(DrawRenderer2DPass.k_ShaderTags).
        static readonly ShaderTagId UnlitTag = new ShaderTagId("SRPDefaultUnlit");

        static readonly int IdOutlineColor = Shader.PropertyToID("_OutlineColor");
        static readonly int IdOutlineTexel = Shader.PropertyToID("_OutlineTexel");
        static readonly int IdBossTexel = Shader.PropertyToID("_BossTexel");
        static readonly int IdBossCutoff = Shader.PropertyToID("_BossCutoff");
        static readonly int IdBossTex = Shader.PropertyToID("_BossTex");

        readonly Material _maskMat;
        readonly Material _outlineMat;

        int _layerMask;
        float _thickness;

        public OutlinePass(Material maskMat, Material outlineMat)
        {
            _maskMat = maskMat;
            _outlineMat = outlineMat;
            profilingSampler = new ProfilingSampler("BossOverlapOutline");
        }

        public void Setup(int layerMask, Color color, float thickness, float cutoff)
        {
            _layerMask = layerMask;
            // 정수 픽셀로 스냅 — 반 픽셀 오프셋은 선을 끊어 놓는다(위 Thickness 주석 참고).
            _thickness = Mathf.Max(1f, Mathf.Round(thickness));
            _outlineMat.SetColor(IdOutlineColor, color);
            _outlineMat.SetFloat(IdBossCutoff, cutoff);
            _outlineMat.SetTexture(IdBossTex, BossTexture);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle src = resourceData.activeColorTexture;

            TextureDesc desc = renderGraph.GetTextureDesc(src);
            desc.depthBufferBits = 0;
            desc.msaaSamples = MSAASamples.None;
            desc.clearBuffer = true;
            desc.clearColor = Color.clear;
            desc.name = "BossOverlapMask";
            // ⚠️ 실측으로 잡은 버그(2026-08-09): 카메라 색 버퍼 포맷을 그대로 물려받으면 안 된다.
            //    이 프로젝트는 HDR + 32Bit 정밀도라 URP가 B10G11R11_UFloatPack32를 쓰는데, 이 포맷엔
            //    **알파 채널이 없다**. 그러면 마스크의 .a가 항상 1로 읽혀 "안쪽/바깥" 구분이 사라지고
            //    (c==mn==1) 테두리가 한 픽셀도 안 나온다. 알파가 있는 8비트 포맷으로 못박는다.
            desc.format = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm;
            TextureHandle mask = renderGraph.CreateTexture(desc);

            // ── 1) 요소 실루엣 마스크 ──────────────────────────────────────────
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            // renderingLayerMask 비트0만 그린다 → 보스 디스플레이 Quad(비트1)는 마스크에서 빠진다.
            // (자기 자신이 마스크에 들어가면 화면 테두리를 따라 흰 사각형이 생긴다)
            var filter = new FilteringSettings(RenderQueueRange.all, _layerMask, 1u);

            var spriteDraw = RenderingUtils.CreateDrawingSettings(
                Sprite2DTag, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
            spriteDraw.overrideMaterial = _maskMat;
            spriteDraw.overrideMaterialPassIndex = 0;
            RendererListHandle spriteList = renderGraph.CreateRendererList(
                new RendererListParams(renderingData.cullResults, spriteDraw, filter));

            var unlitDraw = RenderingUtils.CreateDrawingSettings(
                UnlitTag, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
            unlitDraw.overrideMaterial = _maskMat;
            unlitDraw.overrideMaterialPassIndex = 0;
            RendererListHandle unlitList = renderGraph.CreateRendererList(
                new RendererListParams(renderingData.cullResults, unlitDraw, filter));

            using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>("BossOverlapMask", out var data, profilingSampler))
            {
                data.spriteList = spriteList;
                data.unlitList = unlitList;
                builder.UseRendererList(spriteList);
                builder.UseRendererList(unlitList);
                builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                builder.SetRenderFunc((MaskPassData d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.DrawRendererList(d.spriteList);
                    ctx.cmd.DrawRendererList(d.unlitList);
                });
            }

            // ── 2) 테두리 합성 ────────────────────────────────────────────────
            float w = Mathf.Max(1, desc.width);
            float h = Mathf.Max(1, desc.height);
            _outlineMat.SetVector(IdOutlineTexel, new Vector4(_thickness / w, _thickness / h, 0f, 0f));
            // 보스 RT는 화면보다 훨씬 저해상도(예: 272x153)라 자기 텍셀 크기로 실루엣을 부풀려야 한다.
            float bw = BossTexture != null ? Mathf.Max(1, BossTexture.width) : w;
            float bh = BossTexture != null ? Mathf.Max(1, BossTexture.height) : h;
            _outlineMat.SetVector(IdBossTexel, new Vector4(1f / bw, 1f / bh, 0f, 0f));

            using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>("BossOverlapOutlineComposite", out var data, profilingSampler))
            {
                data.mask = mask;
                data.material = _outlineMat;
                builder.UseTexture(mask, AccessFlags.Read);
                // ReadWrite: 이미 그려진 화면을 유지한 채 그 위에 알파 블렌딩으로 선만 얹는다.
                builder.SetRenderAttachment(src, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc((CompositePassData d, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, d.mask, new Vector4(1f, 1f, 0f, 0f), d.material, 0);
                });
            }
        }

        class MaskPassData
        {
            public RendererListHandle spriteList;
            public RendererListHandle unlitList;
        }

        class CompositePassData
        {
            public TextureHandle mask;
            public Material material;
        }
    }
}
