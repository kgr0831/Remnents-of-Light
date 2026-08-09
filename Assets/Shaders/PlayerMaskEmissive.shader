// 발광 마스크가 흰색인 부위만 "아예 그 색으로 칠하고" HDR로 빛나게 하는 오버레이.
//
// PlayerBloomOverlay(가산 Blend One One)와 다른 점:
//   가산은 원본 픽셀 위에 빛을 "더하기"만 하므로, 원본이 청록이면 청록+빨강=분홍이 되고
//   세기를 올리면 캐릭터 전체가 물드는 것처럼 보인다(사용자 피드백 2026-08-01).
//   여기선 알파 블렌딩으로 마스크 부위를 **덮어써서** 그 부위만 정확히 지정한 색이 되게 한다.
//   RGB를 _BloomBoost배로 내보내므로 씬 Bloom(threshold 1.15)이 그 부위만 발광시킨다.
//   마스크가 0인 곳은 알파 0이라 원본이 그대로 남는다 — 몸 전체가 물들지 않는다.
//
// 프로퍼티 이름은 PlayerBloomOverlay와 동일하게 맞춰 PlayerBloomFx가 그대로 구동할 수 있게 했다.
// ⚠️ LightMode 태그를 두지 않는다 — 폭주/흑백 피처의 보호 패스가 Universal2D 리스트를
//    Sprite-Unlit-Default로 머티리얼 오버라이드해 버리기 때문(PlayerBloomOverlay와 같은 이유).
Shader "Custom/PlayerMaskEmissive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [NoScaleOffset] _EmissionMask ("Emission Mask (White=Glow)", 2D) = "white" {}
        _Color ("Emissive Color", Color) = (1.0, 0.08, 0.05, 1)
        _BloomBoost ("Bloom Boost (HDR)", Range(1,8)) = 4.0
        _Intensity ("Intensity", Range(0,1)) = 0
        // PlayerBloomFx가 공용으로 세팅하는 값들 — 이 셰이더에선 쓰지 않지만 선언해 둬야
        // SetFloat 호출이 조용히 무시되지 않고, 인스펙터에서도 혼선이 없다.
        _Flatten ("(미사용)", Range(0,1)) = 0
        _MaskFloor ("마스크 밖 발광 바닥값", Range(0,1)) = 0
        // 마스크와 별개로, 스프라이트에서 "원래 밝은" 픽셀(칼날 같은 흰 부위)도 빛나게 한다.
        // 0이면 꺼짐(기본). 마스크 부위는 _Color, 밝은 부위는 _BrightColor로 각각 다른 색이 된다.
        _BrightWeight ("밝은 부위 발광 (0=끔)", Range(0,1)) = 0
        _BrightColor ("밝은 부위 색", Color) = (1.0, 0.85, 0.78, 1)
        _BrightThreshold ("밝은 부위 임계", Range(0,1)) = 0.82
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha // 덮어쓰기 — 마스크 부위를 지정 색으로 "치환"한다
        Cull Off
        ZWrite Off

        Pass
        {
            Name "PlayerMaskEmissive"

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
                float  _Flatten;
                float  _MaskFloor;
                float  _BrightWeight;
                half4  _BrightColor;
                float  _BrightThreshold;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half  mask = SAMPLE_TEXTURE2D(_EmissionMask, sampler_MainTex, IN.uv).r;
                mask = lerp(_MaskFloor, 1.0h, mask);

                // 칼날처럼 원래 흰 부위도 빛나게 한다(마스크와 별개 경로, _BrightWeight=0이면 꺼짐).
                half lum = max(tex.r, max(tex.g, tex.b));
                half bright = smoothstep(_BrightThreshold, _BrightThreshold + 0.08h, lum) * _BrightWeight;

                half m = max(mask, bright);
                // 스프라이트 실루엣 안 + (마스크 or 밝은 부위)만 칠한다.
                half a = tex.a * m * _Intensity;
                clip(a - 0.01h);

                // 마스크 쪽이 더 강하면 _Color(붉은), 밝은 부위가 더 강하면 _BrightColor(흰 계열).
                half3 tint = lerp(_BrightColor.rgb, _Color.rgb, step(bright, mask));

                // RGB를 HDR로 내보내 그 부위만 블룸이 잡는다(주위를 비추지는 않는다).
                return half4(tint * _BloomBoost, a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
