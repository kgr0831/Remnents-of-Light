// HP 아이콘의 "빨간 칸"만 블룸시키는 UI 전용 가산 오버레이.
// PlayerBloomOverlay.shader와 같은 전제(URP는 오브젝트별 블룸이 없다 → HDR 출력만이 유일한 방법)를
// UGUI Image에 맞게 옮겼다 — _EmissionMask(흰=빨간 칸, 검=어두운 칸)를 곱해 빨간 칸 픽셀만 HDR로 내보낸다.
// _MainTex와 _EmissionMask는 크기·프레임 배치가 완전히 동일한 시트라(HpBarGlowMask는 HpBar.png의
// 픽셀 단위 복제본) 같은 UV를 그대로 재사용한다 — 프레임(HP1~8)별 배선이 필요 없다.
//
// 전용 Screen Space - Camera 캔버스(HPBloom 레이어, 전용 Overlay 카메라)에서만 그려지므로
// UI 마스크/스텐실 클리핑(_ClipRect)은 필요 없다 — 그 캔버스 아래엔 RectMask2D 등으로 잘리는
// 자식이 없다.
Shader "Custom/UIHpGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [NoScaleOffset] _EmissionMask ("Emission Mask (White=Glow)", 2D) = "black" {}
        _Color ("Glow Tint", Color) = (1.0, 0.15, 0.08, 1)
        _BloomBoost ("Bloom Boost (HDR)", Range(1,8)) = 4.0
        _Intensity ("Intensity", Range(0,1)) = 1.0
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

        Blend One One   // 가산 — 뒤쪽 화면(베이스 카메라 결과) 위에 빛만 더한다
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "UIHpGlow"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_EmissionMask);

            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                float  _BloomBoost;
                float  _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color; // Image.color(알파)를 그대로 받아 페이드에 쓴다
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half  mask = SAMPLE_TEXTURE2D(_EmissionMask, sampler_MainTex, IN.uv).r;

                half3 glow = _Color.rgb * (tex.a * IN.color.a * _Intensity * _BloomBoost * mask);
                return half4(glow, 0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
