// 사망 화면 전용 UI 노이즈 셰이더(사용자 지시 2026-08-10: "그냥 아예 패널로 다 안보이게").
// DeathScreenUI의 검은 배경 Image에 붙는다. ScreenGlitch.shader(카메라 렌더러 피처)와 같은 Hash
// 노이즈 공식을 UI(Screen Space - Overlay)에서도 쓸 수 있게 옮겨 왔다 — Overlay 캔버스는 카메라
// 렌더 패스 뒤에 완전히 분리되어 그려지므로(ScreenFadeUI와 동일 원리) 카메라·조명·다른 렌더러
// 피처가 뭘 그리든 이 패널이 물리적으로 전부 가린다. 그 대가로 카메라 쪽 ScreenGlitchFeature가
// 이 패널까지 건드릴 수 없어서, 노이즈를 이 셰이더 자체에 내장했다(_NoiseIntensity/_Seed를
// DeathScreenUI가 매 프레임 갱신).
Shader "Custom/UINoiseOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _NoiseIntensity ("Noise Intensity (0~1)", Range(0,1)) = 0
        _Seed ("Seed", Float) = 0
        _NoiseAmount ("Noise Amount", Range(0,1)) = 0.18
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _NoiseIntensity;
            float _Seed;
            float _NoiseAmount;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            // ⚠️ 실측 버그 수정 1(2026-08-10): ComputeScreenPos는 카메라 투영(원근분할) 기준이라 Screen
            // Space - Overlay 캔버스(연결된 카메라가 없음)에서는 screenPos.w가 기대한 대로 안 나와
            // 노이즈가 거의 안 보였다("UI에 노이즈가 안 뜬다" 리포트) — ComputeScreenPos 대신
            // IN.texcoord(0~1로 정규화된, 이 배경 이미지 자체의 UV — 전체 화면을 덮으므로 사실상
            // 정규화 스크린 좌표와 같다)를 쓴다.
            // ⚠️ 실측 버그 수정 2(같은 날): 1을 고치고 나니 이번엔 대각선 줄무늬(헤링본) 패턴이 나왔다 —
            // sin()에 큰 인자를 넣으면 GPU에서 정밀도가 깨져 반복 패턴이 생기는 전형적인 문제
            // (frac(sin(dot(p,...))*큰수) 해시 함수의 알려진 함정). 원인은 실제 픽셀 좌표(최대
            // 1920 안팎)를 그대로 곱해 넣었던 것 — 이미 검증된 ScreenGlitch.shader와 똑같이
            // **0~1 정규화 좌표 × 512**로 되돌려 sin() 인자를 작게 유지한다.
            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                float n = Hash(IN.texcoord * 512.0 + _Seed) - 0.5;
                c.rgb = saturate(c.rgb + n * _NoiseAmount * _NoiseIntensity);
                return c;
            }
            ENDHLSL
        }
    }
}
