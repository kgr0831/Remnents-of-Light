// 일섬 차지 연출: 작은 픽셀들이 플레이어 주위에서 페이드 인 되며 생성되고, 바라보는 방향의
// 살짝 위쪽 한 점으로 감기듯 모여든다. 취소 시엔 왔던 방향으로 되튀며(유리 파편) 페이드 아웃.
//
// 픽셀 하나 = 메시에 미리 깔아둔 쿼드 1개. 위치·알파를 전부 버텍스 셰이더에서 계산하므로
// CPU는 _Progress 하나만 갱신하면 되고, 프래그먼트에서 파티클을 루프 도는 방식보다 훨씬 저렴하다.
// 메시는 IlseomChargeFx.cs가 런타임에 생성한다(쿼드당 corner=TEXCOORD0, 파티클 인덱스=TEXCOORD1).
//
// URP 2D Renderer는 "Universal2D"/"SRPDefaultUnlit" 태그만 수집한다("UniversalForward"는 통째로
// 스킵됨 — Assets/Scripts/VFX/HitVfxAutoReturn.cs 주석의 실측 기록 참고).
Shader "Custom/IlseomChargePixels"
{
    Properties
    {
        // #7ebfc6
        _Color ("Pixel Color", Color) = (0.4941, 0.7490, 0.7765, 1)
        // 출력 밝기를 1.0 위로 밀어올려 Bloom 임계값을 넘긴다(URP엔 오브젝트별 블룸이 없어서
        // "HDR로 밝게 쓰고 임계값으로 골라내는" 방식이 유일한 수단). #7ebfc6에 2.0을 곱하면
        // (0.99, 1.50, 1.55)가 되어 R은 1 아래로 남아 시안 색조가 흰색으로 날아가지 않는다.
        _BloomBoost ("Bloom Boost (HDR)", Range(1,6)) = 2.0
        _Progress ("Charge Progress", Range(0,1)) = 0
        _Alpha ("Global Alpha", Range(0,1)) = 1
        _Burst ("Burst (cancel)", Range(0,1)) = 0
        _BurstDistance ("Burst Distance", Float) = 0.55
        _Target ("Gather Offset (xy, local)", Vector) = (0.45, 0.55, 0, 0)
        _PixelSize ("Pixel Size (local units)", Float) = 0.0625
        // 링 각도를 균등 분할하기 위한 총 픽셀 수. IlseomChargeFx가 메시 정점 수와 맞춰서 넣어준다.
        _Count ("Pixel Count", Float) = 64
        _RadiusMin ("Spawn Radius Min", Float) = 0.75
        _RadiusMax ("Spawn Radius Max", Float) = 1.7
        // 차지 경과 시간(초). 픽셀이 링→수집점을 "계속" 흐르게 하는 시계. _Progress는 세기(참여 픽셀 수)만 담당.
        _Flow ("Flow Time (sec)", Float) = 0
        _FlowSpeed ("Flow Speed (cycles/sec)", Range(0.1,4)) = 0.85
        _FadeInFrac ("Fade-In Fraction", Range(0.01,1)) = 0.22
        // 수집점(플레이어)에 가까워질수록 사라지는 구간의 비율 — 흡수되듯 스며든다.
        _FadeOutFrac ("Fade-Out Fraction", Range(0.05,0.95)) = 0.4
        _Swirl ("Swirl (rad)", Float) = 1.1
        _Seed ("Seed", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "IlseomChargePixels"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                float4 _Target;
                float  _BloomBoost;
                float  _Progress;
                float  _Alpha;
                float  _Burst;
                float  _BurstDistance;
                float  _PixelSize;
                float  _Count;
                float  _RadiusMin;
                float  _RadiusMax;
                float  _Flow;
                float  _FlowSpeed;
                float  _FadeInFrac;
                float  _FadeOutFrac;
                float  _Swirl;
                float  _Seed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 corner     : TEXCOORD0; // 쿼드 네 꼭짓점의 -0.5~+0.5 오프셋
                float2 particle   : TEXCOORD1; // x = 파티클 인덱스
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half   alpha      : TEXCOORD0;
            };

            // sin 기반 해시(frac(sin(dot(p,k))*43758.5))는 인덱스가 정수라 sin의 인자가 커지면
            // GPU의 범위 축약 정밀도가 무너져 결과가 몇 개 값으로 뭉친다 — 실제로 각도가 0·π 근처로
            // 쏠려 링이 아니라 "가로로 눌린 덩어리"가 나오는 것을 스크린샷 픽셀 분포로 확인(2026-07-25,
            // x 71px vs y 27px). 곱셈·frac만 쓰는 해시로 교체해 정밀도 문제를 없앤다.
            // Source: Dave Hoskins, "Hash without Sine" (shadertoy.com/view/4djSRW) — hash21 변형
            float2 Hash2(float n)
            {
                float3 p3 = frac(float3(n, n, n) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float  id = IN.particle.x;
                float2 h  = Hash2(id + _Seed);
                float2 h2 = Hash2(id * 3.77 + _Seed + 19.7);

                // 픽셀은 링 → 수집점을 "계속" 순환한다(사용자 요청: 계속 모이면서 가까워질수록 페이드 아웃).
                // _Progress로 t를 직접 만들면 완충 시 모든 픽셀의 t가 1로 같아져 "한 번 모이고 끝"이 된다.
                // 그래서 시계(_Flow)와 세기(_Progress)를 분리하고, 픽셀마다 속도·위상을 달리한 주기 흐름으로 만든다.
                float speed = _FlowSpeed * lerp(0.75, 1.35, h2.y);
                float t = frac(_Flow * speed + h.y * 0.917);
                float e = t * t * (3.0 - 2.0 * t); // smoothstep — 처음엔 느리게, 끝에 빨려들듯

                // 차지가 오를수록 참여하는 픽셀이 늘어난다 — 초반엔 드문드문, 완충 직전엔 전부.
                float activeGate = step(h2.x, saturate(_Progress * 1.15 + 0.08));

                // 각도는 균등 분할 + 약간의 지터(stratified). 64개뿐이라 순수 난수로는 뭉치고 비는 구간이
                // 생기므로, 인덱스로 링을 고르게 나눈 뒤 칸 안에서만 흔든다 → 항상 고른 원형이 보장된다.
                // 반경은 각도와 독립적인 해시를 써야 특정 방향만 멀리 뜨는 편향이 안 생긴다.
                float  ang = (id + h.x * 0.85) / max(1.0, _Count) * 6.2831853;
                float  rad = lerp(_RadiusMin, _RadiusMax, frac(h.x * 7.13 + 0.31));

                // 궤도를 조금 틀어 직선으로 빨려들지 않고 감기며 모이게 한다.
                float  spun  = ang + _Swirl * (1.0 - e) * lerp(-1.0, 1.0, h2.y);
                float2 start = float2(cos(spun), sin(spun)) * rad;
                float2 pos   = lerp(start, _Target.xy, e);

                // 취소: 왔던 방향으로 되튀며 흩어진다(유리 파편).
                float2 outDir = float2(cos(ang), sin(ang));
                pos += outDir * (_Burst * _BurstDistance * lerp(0.5, 1.5, h2.x));

                // 픽셀 그리드에 스냅 — 도트가 반픽셀로 뭉개지지 않고 각지게 보이도록.
                pos = floor(pos / _PixelSize + 0.5) * _PixelSize;

                // 링에서 페이드 인 → 수집점(플레이어)에 가까워질수록 페이드 아웃(흡수되듯 스며든다).
                float fadeIn  = saturate(t / _FadeInFrac);
                float fadeOut = 1.0 - smoothstep(1.0 - _FadeOutFrac, 1.0, t);
                // 에너지 느낌의 미세 깜빡임 — 픽셀마다 위상이 다른 빠른 진동(사용자 요청 "에너지 느낌").
                float flicker = lerp(0.7, 1.0, 0.5 + 0.5 * sin(_Flow * 22.0 + h.x * 6.2831853));
                OUT.alpha = (half)(fadeIn * fadeOut * activeGate * flicker * _Alpha);

                float3 posOS = float3(pos + IN.corner * _PixelSize, IN.positionOS.z);
                OUT.positionCS = TransformObjectToHClip(posOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half a = _Color.a * IN.alpha;
                if (a < 0.004) discard;
                // Blend SrcAlpha OneMinusSrcAlpha라 최종 색은 rgb*a가 되므로, 페이드 인 중(a가 작을 때)엔
                // 임계값을 못 넘어 블룸이 없고 픽셀이 밝아질수록 자연히 번지기 시작한다.
                return half4(_Color.rgb * _BloomBoost, a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
