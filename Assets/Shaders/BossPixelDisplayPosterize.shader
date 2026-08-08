Shader "Custom/2D/BossPixelDisplayPosterize"
{
    Properties
    {
        _MainTex("RT", 2D) = "white" {}
        [HideInInspector] _White("Tint", Color) = (1,1,1,1)
        // 채널당 색상 단계 수 — 낮을수록 색이 확 나뉘어 "픽셀아트"에 가까워지고,
        // 높을수록 원본(모자이크에 가까운 연속 색)에 가까워진다.
        _PosterizeLevels("Posterize Levels", Range(2, 32)) = 6
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
        // Off로 둬야 정렬 순서(Sorting Order)만으로 앞뒤가 정해진다 — On이면 화면을 꽉 채우는 이
        // Quad가 깊이버퍼에 자기 알파와 무관하게 깊이를 써버려서, 투명한 부분까지 뒤의 배경/플레이어를
        // 가려버리는 문제가 있었다(전체 화면이 이 오브젝트에 가려짐).
        ZWrite Off

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
            float _PosterizeLevels;

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

                half levels = max(_PosterizeLevels, 2.0);
                half3 posterized = floor(tex.rgb * levels) / (levels - 1.0);
                posterized = saturate(posterized);

                return half4(posterized, tex.a);
            }
            ENDHLSL
        }
    }
}
