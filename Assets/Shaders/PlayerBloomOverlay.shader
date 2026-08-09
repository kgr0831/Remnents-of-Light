// 플레이어 스프라이트 위에 같은 실루엣을 HDR로 한 겹 더 얹어 "그 오브젝트만" 블룸시킨다.
// (URP엔 오브젝트별 블룸이 없다 → 임계값 1.15 위로 출력하는 것이 유일한 방법. 원본 스프라이트는
//  건드리지 않고 가산 합성만 하므로 2D 라이팅·머티리얼 교체 복원 같은 부작용이 없다.)
//
// PlayerBloomFx.cs가 매 프레임 플레이어의 sprite/flipX를 복사한 SpriteRenderer에 이 셰이더를 물린다.
// _Intensity가 0이면 아무것도 더하지 않으므로 페이드 인/아웃은 이 값 하나로 제어한다.
//
// C-4: _EmissionMask(흰=발광, 검=비발광)를 곱해 발광부만 빛나게 한다. 마스크 시트는 원본과 크기·
// 프레임 배치가 완전히 동일해서(부록A 실측) _MainTex와 같은 UV를 그대로 재사용 — 프레임별 배선이
// 필요 없다. 기본값이 "white"라 마스크를 안 물리면 이전과 동일하게 전체가 _Flatten대로 빛난다
// (PlayerBloomFx.cs가 마스크를 못 찾으면 명시적으로 Texture2D.whiteTexture를 넣어 같은 폴백을 보장).
Shader "Custom/PlayerBloomOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [NoScaleOffset] _EmissionMask ("Emission Mask (White=Glow)", 2D) = "white" {}
        _Color ("Glow Tint", Color) = (0.65, 0.92, 1.0, 1)
        _BloomBoost ("Bloom Boost (HDR)", Range(1,8)) = 3.0
        _Intensity ("Intensity", Range(0,1)) = 0
        // 0이면 스프라이트 색을 그대로 곱해 밝은 픽셀만 빛나고(어두운 캐릭터는 거의 안 빛남),
        // 1이면 알파 실루엣 전체가 균일하게 빛난다. 사무라이 스프라이트는 대부분 어두워서 기본값을 높게 둔다.
        _Flatten ("Silhouette Flatten", Range(0,1)) = 0.65
        // 마스크 밖(검은 부분)도 이 비율만큼은 빛나게 한다. 0이면 기존과 완전히 동일(마스크만 발광).
        // 폭주처럼 "캐릭터 전체가 붉게 타오르는" 느낌이 필요할 때만 올린다 — 마스크 부분은 여전히
        // 훨씬 밝아서 "마스크에 따른 발광"이라는 성격은 유지된다.
        _MaskFloor ("Mask Floor (0=마스크만)", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Blend One One   // 가산 — 원본 스프라이트 위에 빛만 더한다
        Cull Off
        ZWrite Off

        Pass
        {
            Name "PlayerBloomOverlay"
            // ⚠️ LightMode 태그를 일부러 두지 않는다(2026-08-01 실측으로 잡은 버그).
            //    RampageVisionFeature/GrayscaleRendererFeature의 보호 패스는 "Universal2D" 리스트를
            //    Sprite-Unlit-Default로 **머티리얼 오버라이드**해서 그린다. 이 오버레이가 그 리스트에
            //    들어가면 가산 HDR 합성이 통째로 사라지고 그냥 스프라이트가 한 겹 더 그려질 뿐이라,
            //    폭주 중 붉은 블룸이 전혀 안 보였다. 태그를 비우면 SRPDefaultUnlit으로 수집돼
            //    오버라이드 없이 이 셰이더 그대로 그려진다(URP 2D 렌더러가 수집하는 태그는 이 둘뿐).
            //    이 셰이더는 원래 2D 라이트를 참조하지 않으므로 라이팅 경로를 잃어도 결과가 같다.

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_EmissionMask); // _MainTex와 UV·샘플러를 공유 — 둘 다 같은 크기의 Point 필터 시트라 별도 샘플러가 필요 없다.

            // ⚠ _MainTex_ST를 여기 넣으면 안 된다 — 2D SRP Batcher가 _TexelSize/_ST 텍스처 프로퍼티를
            // 지원하지 않아 이 머티리얼을 쓰는 2D 렌더러 전체의 SRP 배칭이 꺼진다(에디터 경고로 확인).
            // SpriteRenderer가 만드는 메시의 UV는 이미 아틀라스 좌표라 타일링/오프셋이 필요 없다.
            // (같은 이유로 _EmissionMask_ST도 선언하지 않는다.)
            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                float  _BloomBoost;
                float  _Intensity;
                float  _Flatten;
                float  _MaskFloor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                // 어두운 픽셀도 빛나게 원본 색을 흰색 쪽으로 끌어올린다(_Flatten). 그러지 않으면
                // 대부분이 어두운 사무라이 스프라이트는 곱셈 결과가 0에 가까워 사실상 안 빛난다.
                half3 base = lerp(tex.rgb, half3(1.0, 1.0, 1.0), _Flatten);
                // C-4: 마스크의 R채널(흰=1/검=0)을 곱해 발광부만 남긴다. 마스크가 "white"(기본값/폴백)면
                // 1이라 기존 동작(실루엣 전체가 _Flatten대로 빛남)과 완전히 동일하다.
                half mask = SAMPLE_TEXTURE2D(_EmissionMask, sampler_MainTex, IN.uv).r;
                // 마스크 밖도 _MaskFloor만큼은 살려 둔다(기본 0 = 기존 동작). 발광원이 마스크 몇 픽셀
                // 뿐이면 블룸이 "점"으로만 보여서, 폭주처럼 전체가 타올라야 하는 구간에서 이 바닥값을 쓴다.
                mask = lerp(_MaskFloor, 1.0h, mask);
                // 가산 블렌드라 알파는 쓰이지 않는다 → 실루엣 모양은 알파를 rgb에 직접 곱해서 만든다.
                half3 glow = base * _Color.rgb * (tex.a * _Intensity * _BloomBoost * mask);
                return half4(glow, 0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
