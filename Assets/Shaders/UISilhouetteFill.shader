// HP 아이콘의 실루엣(알파) 안쪽만, 위에서부터 _FillAmount만큼 단색(_Color)으로 칠하는 셰이더.
//
// 왜 필요한가: 기존엔 이 자리에 UGUI Image.Type.Filled + 흰색 사각형 스프라이트(WhiteSprite)를
// 썼다 — 옛 "칸 5개" 시절엔 칸 자체가 정사각형이라 문제가 없었지만, 지금은 아이콘이 꽃/바람개비
// 모양이라 사각형 Fill이 그 실루엣을 무시하고 네모난 회색 막대로 덮어버렸다(사용자 스크린샷
// 2026-08-10 "그냥 사각형으로 진행되는 문제"). 이 셰이더는 스프라이트의 알파를 실루엣 마스크로
// 쓰고, 그 안에서만 위→아래 Fill을 계산한다.
//
// ⚠️ 한 번 "덮기" 대신 "지우기"(Blend Zero)로 바꿨다가 되돌렸다(사용자 피드백 2026-08-11) —
// 이 오버레이는 화면 맨 뒤(Overlay 캔버스 맨 위)에 그려져서 "지운" 자리 뒤에 실제로 비칠 게
// 없다(이미 다 그려진 프레임의 RGB를 0으로 만들 뿐이라 그냥 검은색이 된다). 자아가 줄어드는
// 동안(1→0)의 회색 표시는 원래대로 "덮기"가 맞다 — 자아 0에서 HP가 깎이는 부분을 투명하게
// 하는 건 이 오버레이가 아니라 아이콘 본체(Base/Ghost)의 "꺼진 칸" 픽셀을 아예 그리지 않는
// 방식(Custom/UIHpIconBody, _HideUnlit)으로 따로 처리한다.
Shader "Custom/UISilhouetteFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Fill Color", Color) = (0.55, 0.56, 0.60, 1)
        _FillAmount ("Fill Amount (Top-down)", Range(0,1)) = 0
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
            Name "UISilhouetteFill"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _FillAmount;
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

            half4 frag(Varyings IN) : SV_Target
            {
                half silhouette = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                if (silhouette < _AlphaThreshold) return half4(0, 0, 0, 0); // 실루엣 밖은 항상 투명

                // uv.y는 위쪽이 1, 아래쪽이 0 — "위에서부터 채워진 비율"은 (1-uv.y) <= _FillAmount.
                half distFromTop = 1.0h - IN.uv.y;
                half filled = step(distFromTop, _FillAmount);

                return half4(_Color.rgb, _Color.a * filled * IN.color.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
