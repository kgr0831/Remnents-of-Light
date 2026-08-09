// 폭주 시야 제한(B-2)의 아웃라인 전용 셰이더 — 적 실루엣 링/코어와 지형 라인 메시가 함께 쓴다.
//
// ⚠️ LightMode 태그를 일부러 넣지 않는다.
//    RampageVisionFeature의 보호 패스는 "Universal2D" 리스트를 Sprite-Unlit-Default로 머티리얼
//    오버라이드해서 그린다(2D 라이트 전역 바인딩 의존을 끊기 위해). 그 리스트에 들어가면 아래의
//    "실루엣 평탄화"가 통째로 사라져 아웃라인이 그냥 원본 스프라이트로 나온다.
//    태그를 비우면 SRPDefaultUnlit으로 수집되고, 그 리스트는 오버라이드 없이 그려지므로
//    어둠 위에 이 셰이더 그대로 덧그려진다. (URP 2D Renderer가 수집하는 태그도 이 둘뿐)
Shader "Custom/RampageOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Flat Color", Color) = (1.0, 0.14, 0.10, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        // 가산이 아니라 일반 알파 블렌딩 — 코어가 링을 "덮어야" 테두리만 남기 때문이다.
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "RampageOutline"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
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
                // ⚠️ 거의 투명한 픽셀(알파 1~2/255)은 버린다. 평소엔 안 보이지만 폭주처럼 화면이 완전
                //    암전이면 그 미세한 값도 눈에 띄어 "유령 도형"이 뜬다(실제로 플레이어 시트의 알파 1~2
                //    잔여물이 아웃라인에 왕관 모양으로 나타났다). 알파를 곱하는 것만으로는 부족해서 잘라낸다.
                clip(tex.a - 0.02);
                // 실루엣 평탄화: 원본 RGB를 버리고 알파만 쓴다.
                // 어두운 적 스프라이트도 통짜 색으로 나오므로 어둠 속에서 실루엣이 읽힌다.
                // (지형 라인 메시는 _MainTex가 기본 흰색이라 tex.a = 1 → 단색 선이 된다)
                return half4(_Color.rgb, tex.a * _Color.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
