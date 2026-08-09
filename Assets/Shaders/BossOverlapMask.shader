// 보스와 겹치는 요소를 찾기 위한 "실루엣 마스크" 전용 오버라이드 셰이더.
// BossOverlapOutlineFeature가 대상 레이어의 렌더러를 이 머티리얼로 한 번 더 그려서
// 알파 = "여기에 요소가 있다"인 흑백 마스크 텍스처를 만든다(색은 쓰지 않는다).
//
// 정점 경로는 URP의 Sprite-Unlit-Default를 그대로 따른다 — SpriteRenderer의 flipX/flipY,
// 스프라이트 스키닝, GPU 인스턴싱까지 원본과 똑같이 처리해야 마스크가 실제 그림과 어긋나지 않는다.
Shader "Hidden/BossOverlapMask"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        // 프리멀티플라이드 블렌딩 — 커버리지를 알파가 아니라 "모든 채널"에 누적한다.
        // ⚠️ 실측(2026-08-09): 알파 채널에만 담으면 안 된다. 이 프로젝트는 HDR 32Bit라 URP의 색
        //    버퍼가 B10G11R11(알파 없음)이고, 마스크 텍스처를 어떤 포맷으로 잡든 .a에 의존하는 순간
        //    "전부 1"로 읽히는 사고가 났다. RGB에 담으면 포맷이 뭐든 안전하다(아웃라인 셰이더는 .r을 읽는다).
        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex MaskVertex
            #pragma fragment MaskFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings MaskVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 MaskFragment(Varyings input) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.color.a;
                // 커버리지를 RGB에 프리멀티플라이드로 담는다(위 Blend 주석 참고) — 알파는 블렌딩용.
                return half4(a, a, a, a);
            }
            ENDHLSL
        }
    }
}
