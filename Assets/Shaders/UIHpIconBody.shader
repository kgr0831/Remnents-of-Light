// HP 아이콘 본체(Base/Ghost)용 — 평소엔 "켜진 칸"(밝은 빨강 픽셀)을 _LitTint(더 밝고 연한 색)로
// 다시 칠해 대비를 준다. _HideUnlit이 켜지면(자아 0 붕괴 중) "꺼진 칸"(어두운 적갈색)은 아예
// 그리지 않고 투명하게 비우고, "켜진 칸"은 _GrayTint로 회색 처리한다.
//
// HpBar.png는 실측(2026-08-10)으로 순수 2색 픽셀아트다 — 밝은 빨강(237,0,0, 켜진 칸)과
// 어두운 적갈색(142,0,0, 꺼진 칸)뿐, 안티에일리어싱도 없다. 그래서 R 채널 하나만 봐도 두 색이
// 뚜렷이 갈린다(237 vs 142, /255 ≈ 0.93 vs 0.56) — _UnlitRedThreshold(기본 0.75)로 간단히 구분한다.
//
// 왜 필요한가: 사용자 지시(2026-08-11) "아직 차있는 HP를 좀 더 연하고 밝은 색으로 만들어서
// 대비가 되게" — 원본 아트의 밝은 빨강(237,0,0)이 꺼진 칸(142,0,0)과 명도 차이만 있어서 눈에
// 잘 안 갈렸다. 켜진 칸만 _LitTint로 다시 칠해 밝기·채도를 올린다. 꺼진 칸(어두운 적갈색)은
// "빨강→어두운 적갈색" 손실 언어를 유지해야 하므로 그대로 둔다.
// 자아 0 붕괴 중(_HideUnlit)엔 이미 깎인 칸은 투명, 아직 안 깎인 칸은 빨강이 아니라 회색
// (자아 고갈 경고색)으로 — 별도 사용자 지시(2026-08-11). 평소(자아 0이 아니거나 폭주가 아닐
// 때)는 _HideUnlit=0이라 이 분기는 안 타고 위 _LitTint 재채색만 적용된다.
Shader "Custom/UIHpIconBody"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _HideUnlit ("Hide Unlit Cells (0=off, 1=on)", Range(0,1)) = 0
        _UnlitRedThreshold ("Unlit Red Threshold", Range(0,1)) = 0.75
        _GrayTint ("Gray Tint (HideUnlit 중 켜진 칸 색)", Color) = (0.55, 0.56, 0.60, 1)
        _LitTint ("Lit Tint (평소 켜진 칸 색 — 더 밝고 연하게)", Color) = (1, 0.42, 0.38, 1)
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
            Name "UIHpIconBody"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _HideUnlit;
                float _UnlitRedThreshold;
                half4 _GrayTint;
                half4 _LitTint;
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
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                bool inSilhouette = tex.a > 0.01h;
                bool isUnlit = inSilhouette && tex.r < _UnlitRedThreshold;

                if (_HideUnlit > 0.5h)
                {
                    if (isUnlit) return half4(0, 0, 0, 0); // 이미 깎인 칸 — 투명
                    if (inSilhouette) return half4(_GrayTint.rgb, tex.a * IN.color.a); // 아직 남은 칸 — 회색
                    return half4(0, 0, 0, 0);
                }

                if (inSilhouette && !isUnlit) return half4(_LitTint.rgb, tex.a * IN.color.a); // 켜진 칸 — 밝고 연하게
                return tex * IN.color; // 꺼진 칸(어두운 적갈색) · 실루엣 밖 — 원래 그대로
            }
            ENDHLSL
        }
    }

    FallBack Off
}
