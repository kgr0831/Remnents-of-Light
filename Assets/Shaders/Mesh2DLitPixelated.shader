Shader "Custom/2D/Mesh2D-Lit-Pixelated"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [HideInInspector] _White("Tint", Color) = (1,1,1,1)
        _PixelDensity("Pixel Density (UV steps)", Float) = 64
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Back
        ZWrite On

        Stencil
        {
            Ref 128
            Comp always
            Pass replace
        }

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY

            struct Attributes
            {
                COMMON_2D_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
            };

            float4 _White;
            float _PixelDensity;

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            Varyings LitVertex(Attributes input)
            {
                return CommonLitVertex(input);
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                Varyings pixelated = input;
                pixelated.uv = floor(input.uv * _PixelDensity) / _PixelDensity;
                return CommonLitFragment(pixelated, _White);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            #pragma multi_compile_instancing

            struct Attributes
            {
                COMMON_2D_NORMALS_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
            };

            float4 _White;
            float _PixelDensity;

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"

            Varyings NormalsRenderingVertex(Attributes input)
            {
               return CommonNormalsVertex(input);
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                Varyings pixelated = input;
                pixelated.uv = floor(input.uv * _PixelDensity) / _PixelDensity;
                return CommonNormalsFragment(pixelated, _White);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

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
            float _PixelDensity;

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            Varyings UnlitVertex(Attributes input)
            {
                return CommonUnlitVertex(input);
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                Varyings pixelated = input;
                pixelated.uv = floor(input.uv * _PixelDensity) / _PixelDensity;
                return CommonUnlitFragment(pixelated, _White);
            }
            ENDHLSL
        }
    }
}
