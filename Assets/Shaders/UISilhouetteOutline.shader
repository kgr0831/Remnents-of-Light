// UI 스프라이트의 실루엣(알파) 가장자리에 흰색 아웃라인을 실시간으로 그리는 셰이더.
//
// 왜 Outline 컴포넌트를 안 쓰나: UGUI의 Outline/Shadow는 같은 버텍스를 오프셋만큼 복제해서 다시
// 그리는 방식이라, 복제본도 원본 텍스처(=HP 아이콘의 빨강/적갈색 픽셀)를 그대로 샘플링한 뒤
// effectColor를 곱한다 — 흰색을 곱해도 원래 색이 그대로 비쳐서 "흰 테두리"가 아니라 살짝 번진
// 빨간/어두운 색 테두리가 된다(사용자 실측 2026-08-10). 이 셰이더는 원본 색을 전혀 안 쓰고
// 알파 경계만 검사해 그 자리를 순수 흰색으로 덮어씌운다.
//
// 원리: 현재 텍셀이 실루엣 밖(알파≈0)인데 4방향(상하좌우) 이웃 중 하나라도 실루엣 안(알파>임계값)
// 이면 그 자리를 _OutlineColor로 채운다. HP1~8/HP0 프레임마다 실루엣 모양이 조금씩 달라(실측 확인)
// 정적 마스크 하나로 재사용할 수 없어서, PlayerHudUI가 매 프레임 이 머티리얼에 물리는 스프라이트를
// Base와 동기화하는 방식으로 대응한다.
// ⚠️ 대각선 이웃까지 8방향으로 검사했더니 뾰족한 모서리마다 대각선 픽셀이 덧붙어 아웃라인이
// 두꺼워 보였다(사용자 피드백 2026-08-10 "너무 찐해요") — 4방향만 남겨 실루엣에 더 밀착시켰다.
Shader "Custom/UISilhouetteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        _AlphaThreshold ("Alpha Threshold", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "UISilhouetteOutline"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize; // Unity가 _MainTex에 자동으로 채워준다(x,y = 1/width,1/height)

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _AlphaThreshold;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half SampleAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half centerA = SampleAlpha(IN.uv);
                if (centerA > _AlphaThreshold) return half4(0, 0, 0, 0); // 실루엣 안쪽은 그리지 않는다(Base/Ghost가 그 위에 그려짐)

                float2 texel = _MainTex_TexelSize.xy;
                half maxNeighbor = 0;
                maxNeighbor = max(maxNeighbor, SampleAlpha(IN.uv + float2( texel.x, 0)));
                maxNeighbor = max(maxNeighbor, SampleAlpha(IN.uv + float2(-texel.x, 0)));
                maxNeighbor = max(maxNeighbor, SampleAlpha(IN.uv + float2(0,  texel.y)));
                maxNeighbor = max(maxNeighbor, SampleAlpha(IN.uv + float2(0, -texel.y)));

                half isEdge = step(_AlphaThreshold, maxNeighbor);
                return half4(_OutlineColor.rgb, _OutlineColor.a * isEdge * IN.color.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
