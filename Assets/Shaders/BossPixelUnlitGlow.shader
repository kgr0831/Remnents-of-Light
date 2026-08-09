Shader "Custom/2D/BossPixelUnlitGlow"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        [HideInInspector] _White("Tint", Color) = (1,1,1,1)
        _BodyDarkness("Body Darkness", Range(0,1)) = 0.2
        _EmissiveBoost("Emissive Boost", Float) = 3.0
        _RedMaskSharpness("Red Mask Sharpness", Float) = 6.0
        // 사용자 지시(2026-08-08): "주변 색상이랑 크게 차이나지 않아야 자연스러움" — 몸통(렌즈
        // 제외) 색을 주변 배경색 쪽으로 일정 비율 당겨서 이질감을 줄인다. 렌즈(emissive)는 안 섞임.
        _EnvironmentColor("Environment Color (주변 배경색)", Color) = (0.75, 0.35, 0.4, 1)
        _EnvironmentBlend("Environment Blend", Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Stencil
        {
            Ref 128
            Comp always
            Pass replace
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Back
        ZWrite On

        Pass
        {
            Stencil
            {
                Ref 128
                Comp always
                Pass replace
            }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            #pragma multi_compile_instancing

            struct Attributes
            {
                COMMON_2D_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
            };

            float4 _White;
            float _BodyDarkness;
            float _EmissiveBoost;
            float _RedMaskSharpness;
            float4 _EnvironmentColor;
            float _EnvironmentBlend;

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings UnlitVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _White;

                half dominance = tex.r - max(tex.g, tex.b);
                half redMask = saturate(dominance * _RedMaskSharpness);

                half3 darkBody = tex.rgb * _BodyDarkness;
                // 렌즈(redMask)는 자체발광이라 안 섞고, 몸통만 주변색 쪽으로 당겨서 배경과
                // 이질감을 줄인다 — redMask가 1에 가까울수록(렌즈일수록) 블렌드가 자동으로 빠짐.
                half3 blendedBody = lerp(darkBody, darkBody * _EnvironmentColor.rgb, _EnvironmentBlend * (1.0 - redMask));
                half3 emissive = tex.rgb * redMask * _EmissiveBoost;

                return half4(blendedBody + emissive, tex.a);
            }
            ENDHLSL
        }
    }
}
