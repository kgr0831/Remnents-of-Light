// URP 17(Unity 6) 풀스크린 글리치 셰이더 — ScreenGlitchFeature 전용(자아 고갈 연출, 사용자 지시 2026-08-01).
// ScreenGrayscale/ScreenDarkness와 같은 Blit 구조. 보호 레이어가 없다 — 화면을 숨기는 게 아니라
// 전체(플레이어·아웃라인 포함)에 잡음을 더하는 효과라 "덧그려 복원할" 대상이 없다.
// 느낌은 미세 노이즈 + 스캔라인 떨림(사용자 선택) — 옛 아날로그 신호 불안정에 가까운 은은한 글리치다.
Shader "Hidden/ScreenGlitch"
{
    Properties {}
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Glitch"
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Intensity;      // 0~1 전체 세기(진입·해제 페이드)
            float _Seed;           // ScreenGlitchFx가 초당 20회 계단식으로 갱신 — 끊기는 아날로그 느낌
            float _ScanlineDensity; // 화면을 몇 개의 가로줄로 쪼갤지
            float _ScanlineJitter;  // 튀는 줄의 최대 좌우 이동량(UV 기준)
            float _NoiseAmount;     // 픽셀별 밝기 잔떨림 세기

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                // 스캔라인 떨림: 일부 가로줄만 골라 좌우로 살짝 밀어낸다(전부 밀면 그냥 화면이 흔들리는 것처럼
                // 보여 "일부만 깨진" 디지털 느낌이 안 난다).
                float lineId = floor(uv.y * _ScanlineDensity);
                float lineRand = Hash(float2(lineId, _Seed));
                float active = step(0.85, lineRand); // 상위 15% 줄만 튄다
                float jitter = (Hash(float2(lineId, _Seed + 91.7)) - 0.5) * _ScanlineJitter * active;
                uv.x = saturate(uv.x + jitter * _Intensity);

                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                // 미세 노이즈: 픽셀별 밝기 잔떨림(신호 불안정)
                float n = Hash(uv * 512.0 + _Seed) - 0.5;
                col.rgb += n * _NoiseAmount * _Intensity;

                return col;
            }
            ENDHLSL
        }
    }
}
