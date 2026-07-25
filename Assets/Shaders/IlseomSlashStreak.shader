// 일섬 이동 궤적에 남는 섬광. 출발점→도착점을 잇는 쿼드 하나에 그려지며,
// _Progress(0→1)가 선두(head)를 궤적 위로 훑고 지나가고 그 뒤로 꼬리가 감쇠하며 남는다.
// 중심의 밝은 코어 + 레인별로 길이·밝기가 다른 파선(스피드 라인)으로 "빠르게 지나갔다"를 표현한다.
//
// UV 규약(IlseomSlashFx.cs가 만드는 메시): uv.x = 0(출발)~1(도착), uv.y = 0~1(0.5가 궤적 중심선).
// 픽셀 아트 톤을 유지하려고 UV를 픽셀 그리드에 스냅하는데, 쿼드가 가로로 길어 축마다 UV당 픽셀 크기가
// 다르므로 스크립트가 _PixelStep.xy로 각각 넣어준다.
//
// URP 2D Renderer는 "Universal2D"/"SRPDefaultUnlit" 태그만 수집한다.
// 블룸은 오브젝트별로 켤 수 없어 _BloomBoost로 1.0 위 HDR로 출력해 임계값(1.15)을 넘긴다.
Shader "Custom/IlseomSlashStreak"
{
    Properties
    {
        // #7ebfc6 — 차지 픽셀과 같은 색
        _Color ("Streak Color", Color) = (0.4941, 0.7490, 0.7765, 1)
        _CoreColor ("Core Color", Color) = (0.88, 0.99, 1.0, 1)
        _BloomBoost ("Bloom Boost (HDR)", Range(1,8)) = 3.0
        _Progress ("Sweep Progress", Range(0,1)) = 0
        _Alpha ("Global Alpha", Range(0,1)) = 1
        // 레인은 "더 많이"(스펙 6 확장)로 상향(18→28, Density 0.55→0.78).
        // 꼬리는 0.85까지 늘렸다가 "너무 느리다"는 피드백으로 0.55로 되돌림 — 꼬리가 짧으면
        // 같은 스윕 시간에도 "빠르게 지나간" 느낌이 난다(사용자 확인 2026-07-25).
        _TailLength ("Tail Length (uv.x)", Range(0.02,1)) = 0.55
        _CoreThickness ("Core Thickness", Range(0.01,1)) = 0.16
        _LaneCount ("Speed Line Lanes", Range(2,48)) = 28
        _LaneDensity ("Speed Line Density", Range(0,1)) = 0.78
        _DashScale ("Dash Frequency", Range(2,60)) = 18
        _PixelStep ("Pixel Step (uv per pixel, xy)", Vector) = (0.01, 0.08, 0, 0)
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
            Name "IlseomSlashStreak"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                half4  _CoreColor;
                float4 _PixelStep;
                float  _BloomBoost;
                float  _Progress;
                float  _Alpha;
                float  _TailLength;
                float  _CoreThickness;
                float  _LaneCount;
                float  _LaneDensity;
                float  _DashScale;
                float  _Seed;
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

            // 곱셈·frac만 쓰는 해시 — sin 기반은 정수 입력에서 GPU 정밀도가 무너진다.
            // Source: Dave Hoskins, "Hash without Sine" (shadertoy.com/view/4djSRW)
            float2 Hash2(float n)
            {
                float3 p3 = frac(float3(n, n, n) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 도트가 반픽셀로 뭉개지지 않게 UV를 픽셀 그리드 중앙으로 스냅
                float2 step2 = max(_PixelStep.xy, 1e-5);
                float2 uv = floor(IN.uv / step2) * step2 + step2 * 0.5;

                // 선두가 아직 지나가지 않은 구간은 비어 있다 → "훑고 지나가는" 느낌
                float behind = _Progress - uv.x;
                if (behind < 0.0) discard;

                float tail = saturate(1.0 - behind / max(1e-4, _TailLength));
                tail *= tail; // 꼬리 쪽을 더 빨리 어둡게

                // 중심 코어: 궤적 중심선에 얇고 밝게
                float dy = abs(uv.y - 0.5) * 2.0;
                float core = 1.0 - smoothstep(0.0, _CoreThickness, dy);

                // 스피드 라인: y를 레인으로 나눠 레인마다 다른 길이·주기의 파선
                float  lane = floor(uv.y * _LaneCount);
                float2 h = Hash2(lane + _Seed);
                float  laneOn = step(h.x, _LaneDensity);
                float  laneTail = saturate(1.0 - behind / max(1e-4, _TailLength * lerp(0.35, 1.15, h.y)));
                float  dash = step(0.55, frac(uv.x * lerp(_DashScale * 0.6, _DashScale * 1.8, h.y) + h.x * 7.0));
                float  lines = laneOn * laneTail * dash;

                float amt = saturate(core * tail * 1.4 + lines * tail * 0.75);
                if (amt < 0.02) discard;

                half3 rgb = lerp(_Color.rgb, _CoreColor.rgb, core * core);
                return half4(rgb * _BloomBoost, amt * _Alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
