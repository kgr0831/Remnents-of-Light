// 적에게 붉은 블룸 글로우를 입히는 가산 오버레이 셰이더.
// PlayerBloomOverlay.shader와 동일한 구조 — 원본 SpriteRenderer를 건드리지 않고
// 자식 SpriteRenderer에 이 셰이더를 물려 HDR 가산 합성으로 "그 오브젝트만" 블룸시킨다.
// EnemyExecutionGlowFx.cs가 매 프레임 적의 sprite/flipX를 복사한다.
Shader "Custom/EnemyExecutionGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Glow Tint", Color) = (1.0, 0.125, 0.125, 1)
        _BloomBoost ("Bloom Boost (HDR)", Range(1,8)) = 4.0
        _Intensity ("Intensity", Range(0,1)) = 0
        _Flatten ("Silhouette Flatten", Range(0,1)) = 0.75
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Blend One One   // 가산 — 원본 스프라이트 위에 빛만 더한다
        Cull Off
        ZWrite Off

        Pass
        {
            Name "EnemyExecutionGlow"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                float  _BloomBoost;
                float  _Intensity;
                float  _Flatten;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half3 base = lerp(tex.rgb, half3(1.0, 1.0, 1.0), _Flatten);
                half3 glow = base * _Color.rgb * (tex.a * _Intensity * _BloomBoost);
                return half4(glow, 0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
