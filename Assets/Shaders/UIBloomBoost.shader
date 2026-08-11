// UGUI 그래픽(Text/Image)을 HDR로 살짝 밝게 내보내 블룸이 걸리게 하는 최소 셰이더.
//
// 왜 필요한가: URP에는 오브젝트별 블룸이 없고 HDR 출력만이 유일한 수단인데(PlayerBloomOverlay ·
// UIHpGlow와 같은 전제), UGUI의 정점 색은 Color32(0~255)라 흰색이 1.0에서 잘린다. Bloom threshold가
// 1.15라 흰 텍스트는 아무리 밝게 칠해도 절대 번지지 않는다. 그래서 셰이더에서 _Boost를 곱해 1을 넘긴다.
//
// ⚠️ 이 셰이더가 붙은 캔버스는 반드시 Screen Space - Camera여야 한다. Screen Space - Overlay 캔버스는
//    URP 포스트프로세싱을 아예 안 받아서(PlayerHudUI 주석 참고) HDR로 내보내도 블룸이 안 걸린다.
Shader "Custom/UIBloomBoost"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        // 획이 얇은 텍스트는 Bloom scatter에 에너지가 흩어져 3 정도로는 티가 안 난다(실측) — 상한을 크게 잡았다.
        _Boost ("Bloom Boost (HDR)", Range(1, 16)) = 6
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

        Blend SrcAlpha OneMinusSrcAlpha   // 평범한 알파 블렌드 — 색만 1을 넘겨 내보낸다
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "UIBloomBoost"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // ⚠️ UGUI가 폰트 아틀라스처럼 알파 전용 텍스처를 물릴 때 (1,1,1,0)을 넣어주는 값.
            //    이걸 안 더하면 rgb가 0이라 텍스트가 새까맣게 나온다(실측 — UI/Default도 같은 처리를 한다).
            half4 _TextureSampleAdd;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Boost;
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

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) + _TextureSampleAdd;
                half4 c = tex * IN.color * _Color;
                c.rgb *= _Boost;   // 알파는 그대로 — 페이드(CanvasGroup)가 정상 동작해야 한다
                return c;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
