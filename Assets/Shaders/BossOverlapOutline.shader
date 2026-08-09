// 화면에서 보스(픽셀 디스플레이 RT의 실루엣)와 겹치는 요소의 테두리를 흰색으로 그린다.
// _BlitTexture = BossOverlapMask 패스가 만든 요소 실루엣 마스크(알파)
// _BossTex     = 보스 픽셀 카메라의 RenderTexture(알파 = 보스 실루엣)
//
// ⚠️ UV 정렬 전제: BossPixelResolutionController가 픽셀 카메라의 ortho/aspect를 메인 카메라와
//    똑같이 맞춰 두기 때문에 _BossTex는 화면(카메라 뷰포트)과 1:1로 대응한다. 그래서 별도의
//    좌표 변환 없이 같은 uv로 두 텍스처를 함께 샘플링할 수 있다.
Shader "Hidden/BossOverlapOutline"
{
    Properties {}
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "BossOverlapOutline"
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_BossTex);
            SAMPLER(sampler_BossTex);

            float4 _OutlineColor;
            float2 _OutlineTexel;  // 한 스텝(=선 두께)에 해당하는 UV 크기 — 항상 정수 픽셀 단위다
            float2 _BossTexel;     // 보스 RT 1텍셀의 UV 크기(실루엣 판정을 부풀리는 데 쓴다)
            float  _BossCutoff;    // 이 알파 이상이면 "보스가 있는 픽셀"

            // 마스크는 커버리지를 RGB에 담는다(BossOverlapMask.shader 주석 참고) — .a는 못 믿는다.
            half MaskAt(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).r;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                // ⚠️ 조기 return으로 분기하지 않는다 — 텍스처 샘플이 암시적 밉(미분값)을 쓰기 때문에
                //    발산 분기 안에서 샘플하면 컴파일러/플랫폼에 따라 경고·오류가 난다. 전부 계산한
                //    뒤 곱해서 걸러낸다(연산량 차이는 무시할 수준의 풀스크린 패스다).

                // 보스가 없는 픽셀에서는 결과가 0이 된다 — "겹치는 부분만" 이라는 조건.
                // ⚠️ 보스 RT는 272x153로 아주 낮은 해상도라, 중심 한 점만 보면 실루엣 가장자리에서
                //    한 텍셀 단위로 판정이 튀어 선이 점선처럼 끊긴다(사용자 리포트 "선이 깨진다").
                //    이웃까지 max로 훑어 실루엣을 한 텍셀 부풀려서 판정을 안정시킨다.
                float2 bs = _BossTexel;
                half boss = SAMPLE_TEXTURE2D(_BossTex, sampler_BossTex, uv).a;
                boss = max(boss, SAMPLE_TEXTURE2D(_BossTex, sampler_BossTex, uv + float2( bs.x, 0)).a);
                boss = max(boss, SAMPLE_TEXTURE2D(_BossTex, sampler_BossTex, uv + float2(-bs.x, 0)).a);
                boss = max(boss, SAMPLE_TEXTURE2D(_BossTex, sampler_BossTex, uv + float2(0,  bs.y)).a);
                boss = max(boss, SAMPLE_TEXTURE2D(_BossTex, sampler_BossTex, uv + float2(0, -bs.y)).a);
                half overlap = step(_BossCutoff, boss);

                // 선은 요소 "안쪽" 가장자리에 그린다(밖으로 번지면 보스 몸통에 선이 뜬다).
                half c = MaskAt(uv);
                half inside = step(0.5h, c);

                // 4방향 이웃 중 하나라도 요소 밖이면 여기가 실루엣 경계다.
                float2 s = _OutlineTexel;
                half mn = MaskAt(uv + float2( s.x, 0));
                mn = min(mn, MaskAt(uv + float2(-s.x, 0)));
                mn = min(mn, MaskAt(uv + float2(0,  s.y)));
                mn = min(mn, MaskAt(uv + float2(0, -s.y)));
                // 대각선까지 봐야 45도 경사(지형 슬로프)에서 선이 끊기지 않는다.
                mn = min(mn, MaskAt(uv + float2( s.x,  s.y)));
                mn = min(mn, MaskAt(uv + float2(-s.x,  s.y)));
                mn = min(mn, MaskAt(uv + float2( s.x, -s.y)));
                mn = min(mn, MaskAt(uv + float2(-s.x, -s.y)));

                half edge = saturate((c - mn) * 2.0h) * inside * overlap;
                return half4(_OutlineColor.rgb, edge * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
