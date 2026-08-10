// 광원 아이콘 본체용 — Custom/UIHpIconBody와 같은 구조지만 판별 채널이 다르다.
// EnergyBar.png는 HpBar.png와 같은 순수 2색 픽셀아트지만 색상이 청록이라(실측 2026-08-11:
// 꺼진 칸=(9,88,95), 켜진 칸=(52,221,236)) HP가 쓰는 빨강 채널 문턱으로는 구분이 안 된다(둘 다
// R이 낮음). 대신 파랑 채널이 크게 갈린다(95/255≈0.37 vs 236/255≈0.93) — _UnlitBlueThreshold
// (기본 0.65)로 구분한다.
//
// _LitTint: 켜진 칸을 원래 청록 대신 이 색으로 완전히 덮어 그린다(HP의 _LitTint와 동일 원리) —
// 평소엔 원래 청록에 가깝게, 저에너지/폭주 경고 시엔 PlayerHudUI가 매 프레임 빨갛게 바꿔 먹인다.
// _UnlitTint: 알파가 0이면 꺼진 칸은 원본 색(어두운 청록) 그대로 두고, 알파가 있으면 그 색으로
// 덮어 그린다 — 폭주 중 "빈칸이 잘 안 보인다"는 사용자 피드백(2026-08-11)에 대응해, 켜진 칸(밝은
// 경고색)과 대비되는 어두운 색으로 꺼진 칸을 살짝 눌러줄 때 쓴다.
Shader "Custom/UIEnergyIconBody"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _UnlitBlueThreshold ("Unlit Blue Threshold", Range(0,1)) = 0.65
        _LitTint ("Lit Tint", Color) = (0.204, 0.867, 0.925, 1)
        _UnlitTint ("Unlit Tint Override (alpha=0 -> pass-through)", Color) = (0, 0, 0, 0)
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
            Name "UIEnergyIconBody"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _UnlitBlueThreshold;
                half4 _LitTint;
                half4 _UnlitTint;
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
                bool isLit = inSilhouette && tex.b > _UnlitBlueThreshold;

                if (isLit) return half4(_LitTint.rgb, tex.a * IN.color.a);
                if (inSilhouette)
                {
                    if (_UnlitTint.a > 0.01h) return half4(_UnlitTint.rgb, tex.a * IN.color.a);
                    return tex * IN.color;
                }
                return half4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
