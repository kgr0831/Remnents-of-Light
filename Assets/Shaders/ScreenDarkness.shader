// URP 17(Unity 6) 풀스크린 암전 셰이더 — RampageVisionFeature 전용.
// ScreenGrayscale.shader와 완전히 같은 Blit 구조이고, 차이는 딱 하나다:
// 그레이스케일은 "반경 안"에 효과를 넣지만(reveal), 여기선 그걸 뒤집어 "반경 밖"만 어둡게 만든다.
// = 폭주 중 플레이어 아주 근처만 보이고 나머지는 암흑(이성의 붕괴).
Shader "Hidden/ScreenDarkness"
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
            Name "Darkness"
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target 2.0

            // URP Core를 먼저 포함해야 TEXTURE2D_X 등이 정의됨
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float  _Intensity;  // 0~1 전체 세기(진입·해제 페이드에 쓴다)
            float2 _Center;     // 가시 영역 중심(스크린 UV, 0~1) — 플레이어 위치
            float  _Radius;     // 가시 반경(UV 기준, y축 스케일). 월드 반경을 카메라가 환산해 넣어 준다
            float  _Softness;   // 경계 부드러움
            float  _Aspect;     // 화면 종횡비(width/height) — 타원이 아닌 원으로 보이게 보정
            float  _Darkness;   // 반경 밖에서 빼앗을 밝기 비율(0.92면 8%만 남는다 — 완전 검정은 피한다)

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);

                // 중심에서의 거리(종횡비 보정 → 타원이 아닌 원)
                float2 d = input.texcoord - _Center;
                d.x *= _Aspect;
                float dist = length(d);

                // 반경 밖이면 1, 안쪽이면 0 — ScreenGrayscale의 reveal과 정확히 반대다.
                float hidden = smoothstep(_Radius - _Softness, _Radius + _Softness, dist);
                float amount = saturate(_Intensity) * hidden * saturate(_Darkness);

                col.rgb *= (1.0 - amount);

                // 어둠 위에 남아야 하는 것(적 아웃라인 · 지형 아웃라인 · 플레이어)은 셰이더가 아니라
                // RampageVisionFeature의 "RampageVisionProtectLayer" 패스가 이 결과 위에 덧그려 처리한다.
                return col;
            }
            ENDHLSL
        }
    }
}
