// UI 스프라이트 실루엣 바깥으로 부드럽게 번지는 아우라(외곽 발광).
//
// UISilhouetteOutline과의 차이: 저쪽은 알파 경계 1픽셀만 칠하는 "테두리선"이라 아무리 밝게 해도
// 선처럼 보인다. 여기서는 여러 반경으로 알파를 훑어 거리 감쇠를 곱하므로 멀리까지 퍼지는 빛이 된다.
//
// ⚠️ 아우라는 스프라이트 바깥으로 나가야 하는데 UI 쿼드는 스프라이트 크기 그대로다. 그래서 쿼드를
//    _Expand 배로 키워 놓고, 셰이더가 UV를 거꾸로 축소해 스프라이트를 가운데에 원래 크기로 그린다.
//    남는 가장자리 여백이 아우라가 번질 자리다. (쿼드 크기와 _Expand는 반드시 같은 값이어야 한다)
//
// ⚠️ CanvasRenderer는 SpriteRenderer와 달리 _MainTex_TexelSize를 자동으로 안 채운다 —
//    쓰는 쪽에서 material.SetVector로 직접 넣어야 한다(PlayerHudUI와 같은 함정).
Shader "Custom/UIAuraGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _GlowColor ("Glow Color (HDR)", Color) = (0.35, 0.8, 3, 1)
        _Radius ("Aura Radius (texels)", Range(1, 128)) = 40
        _Expand ("Quad Expand (쿼드 배율과 일치)", Range(1, 3)) = 1.5
        _Falloff ("Falloff Power", Range(0.5, 4)) = 1.6
        _Strength ("Strength", Range(0, 4)) = 1
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

        Blend One One   // 가산 — 뒤쪽 화면 위에 빛만 더한다
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "UIAuraGlow"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            // ⚠️ 스프라이트가 픽셀아트라 텍스처 자체는 Point 필터다. 그 샘플러로 아우라를 뽑으면
            //    알파가 텍셀 단위로 계단처럼 끊겨 네모난 블록이 그대로 보인다. 아우라는 부드러워야
            //    하므로 인라인 선형 샘플러를 따로 쓴다(원본 스프라이트 설정은 건드리지 않는다).
            SAMPLER(sampler_linear_clamp);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                float _Radius;
                float _Expand;
                float _Falloff;
                float _Strength;
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

            // 스프라이트 바깥을 샘플하면 clamp 때문에 가장자리 픽셀이 번져 나간다 — 명시적으로 0을 준다.
            half SampleAlpha(float2 uv)
            {
                if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return 0;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_linear_clamp, uv).a;
            }

            // 원판을 고르게 덮는 황금각 나선(Vogel) 샘플. 링+단계 격자로 훑으면 방향 사이가 벌어져
            // 별빛 살이 생기고, max로 모으면 얼룩이 진다 — 나선 + 가중 평균이면 둘 다 안 생긴다.
            #define SAMPLES 64
            #define GOLDEN_ANGLE 2.39996323

            half4 frag(Varyings IN) : SV_Target
            {
                // 쿼드를 키운 만큼 UV를 되돌려 스프라이트를 가운데에 원래 크기로 앉힌다.
                float2 uv = (IN.uv - 0.5) * _Expand + 0.5;

                float2 texel = _MainTex_TexelSize.xy;
                half centerA = SampleAlpha(uv);

                // 알파를 원판 전체에 걸쳐 가중 평균한다 = 반경 _Radius짜리 블러.
                // max로 모으면 "가장 가까운 실루엣 하나"만 남아 얼룩덜룩해지지만, 평균은 실루엣을
                // 고르게 감싸는 매끈한 빛이 된다(레퍼런스 느낌).
                half sum = 0;
                float wsum = 0;
                for (int i = 0; i < SAMPLES; i++)
                {
                    float r = sqrt((i + 0.5) / SAMPLES);        // 원판을 고르게 채우는 반경 분포
                    float ang = i * GOLDEN_ANGLE;
                    float2 off = float2(cos(ang), sin(ang)) * r * _Radius;

                    float w = pow(1 - r, _Falloff);             // 멀수록 약하게
                    sum += SampleAlpha(uv + off * texel) * w;
                    wsum += w;
                }
                half glow = sum / max(wsum, 1e-4);

                // 실루엣 안쪽은 뺀다 — 빼지 않으면 그림 안쪽 빈 곳(눈구멍 같은)까지 빛이 차서
                // 뿌연 덩어리로 보인다. 빛은 실루엣 바깥으로만 번져야 한다.
                // (예전처럼 딱딱한 테두리가 되지 않는 이유: 위 나선 평균이 경계를 부드럽게 만든다)
                glow *= saturate(1 - centerA) * _Strength * IN.color.a;

                return half4(_GlowColor.rgb * glow, 0);   // 가산이라 알파는 쓰이지 않는다
            }
            ENDHLSL
        }
    }

    FallBack Off
}
