// 패링 성공 시 플레이어를 감싸는 구형 실드. 내부는 완전히 비어 있고 링(아웃라인)만 그린다.
// 적 공격을 1회 막으면 _Break(0→1)가 올라가며 링이 유리처럼 조각나 바깥으로 흩어진다.
//
// UV 규약(ParryShieldFx.cs가 만드는 쿼드): uv 0~1 → p=(uv-0.5)*2 ∈ [-1,1].
// 링은 |p|=0.5에 놓인다 — 즉 쿼드의 반너비는 반지름의 2배이고, 남은 0.5가 파편이 날아갈 여유 공간이다.
//
// URP 2D Renderer는 "Universal2D"/"SRPDefaultUnlit" 태그만 수집한다(UniversalForward는 통째로 스킵).
// 블룸은 오브젝트별로 켤 수 없어 _BloomBoost로 1.0 위 HDR을 출력해 임계값(1.15)을 넘긴다.
Shader "Custom/ParryShield"
{
    Properties
    {
        _Color ("Shield Color", Color) = (0.42, 0.86, 1.0, 1)
        _CoreColor ("Core Color", Color) = (0.92, 0.99, 1.0, 1)
        _BloomBoost ("Bloom Boost (HDR)", Range(1,8)) = 2.6
        _Alpha ("Global Alpha", Range(0,1)) = 0.72
        _Thickness ("Outline Thickness", Range(0.01,0.4)) = 0.1
        _Edge ("Edge Softness", Range(0.001,0.2)) = 0.025
        _Break ("Break Progress", Range(0,1)) = 0
        _ShardCount ("Shard Count", Range(4,16)) = 12
        _CrackGap ("Crack Gap (rad)", Range(0,0.4)) = 0.08
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
            Name "ParryShield"
            // ⚠️ "Universal2D"가 아니라 "SRPDefaultUnlit" — GrayscaleRendererFeature의 보호 레이어
            // 재그리기가 "Universal2D" 태그를 스프라이트로 간주해 머티리얼을 통째로 스프라이트용
            // 언릿로 강제 교체한다(GrayscaleRendererFeature.cs의 spriteDraw.overrideMaterial). 이 실드는
            // 텍스처 없는 순수 프로시저럴 쿼드라 그 교체를 거치면 링 대신 텅 빈/엉뚱한 텍스처가 나와
            // "잔상처럼 겹친 이상한 스프라이트"로 보였다(2026-08-06, 실드 있을 때만 재현되던 버그).
            // TMP 데미지 텍스트와 같은 이유로 SRPDefaultUnlit을 쓰면 머티리얼 교체 없이(자기 셰이더
            // 그대로) 보호 레이어에 포함된다 — URP 2D Renderer도 이 태그를 정상 수집한다(같은 파일 주석 참고).
            // ⚠️ "Universal2D"가 아니라 "SRPDefaultUnlit" — GrayscaleRendererFeature의 보호 레이어
            // 재그리기가 "Universal2D" 태그를 스프라이트로 간주해 머티리얼을 통째로 스프라이트용
            // 언릿로 강제 교체한다(GrayscaleRendererFeature.cs의 spriteDraw.overrideMaterial). 이 실드는
            // 텍스처 없는 순수 프로시저럴 쿼드라 그 교체를 거치면 링 대신 텅 빈/엉뚱한 텍스처가 나와
            // "잔상처럼 겹친 이상한 스프라이트"로 보였다(2026-08-06, 실드 있을 때만 재현되던 버그).
            // TMP 데미지 텍스트와 같은 이유로 SRPDefaultUnlit을 쓰면 머티리얼 교체 없이(자기 셰이더
            // 그대로) 보호 레이어에 포함된다 — URP 2D Renderer도 이 태그를 정상 수집한다(같은 파일 주석 참고).
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // TWO_PI는 URP Core(Macros.hlsl)에 이미 있으므로 다시 정의하지 않는다(매크로 재정의 경고).
            #define MAX_SHARDS 16
            #define RING_R 0.5

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CoreColor;
                float _BloomBoost;
                float _Alpha;
                float _Thickness;
                float _Edge;
                float _Break;
                float _ShardCount;
                float _CrackGap;
                float _Seed;
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

            // 반지름 RING_R 링의 소속도(0~1). 내부는 자동으로 0이라 "속이 빈 아웃라인"이 된다.
            float RingMask(float d)
            {
                float half_t = _Thickness * 0.5;
                return 1.0 - smoothstep(half_t - _Edge, half_t + _Edge, abs(d - RING_R));
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
                float2 p = (IN.uv - 0.5) * 2.0;
                float amt = 0.0;
                float ang = 0.0;

                if (_Break <= 0.0005)
                {
                    // 성한 상태 — 링 하나만 그리면 되므로 조각 루프를 통째로 건너뛴다.
                    amt = RingMask(length(p));
                    ang = atan2(p.y, p.x);
                }
                else
                {
                    // 조각 i = (링 ∩ 각도 구간 i)를 바깥으로 밀고 살짝 회전시킨 것.
                    // 픽셀마다 각 조각의 강체 변환을 "거꾸로" 풀어 원래 링 위의 점으로 되돌린 뒤,
                    // 그 점이 조각의 원래 각도 구간 안에 있는지로 소속을 판정한다(정확한 역변환).
                    int n = (int)_ShardCount;
                    float sector = TWO_PI / max(1.0, _ShardCount);

                    [loop] for (int i = 0; i < MAX_SHARDS; i++)
                    {
                        if (i >= n) break;

                        float a0 = i * sector;
                        float mid = a0 + sector * 0.5;
                        float2 dir = float2(cos(mid), sin(mid));
                        float2 h = Hash2(i + _Seed);

                        float push = _Break * lerp(0.20, 0.45, h.x);   // 조각마다 다른 거리로 흩어짐
                        float spin = _Break * (h.y - 0.5) * 4.0;       // 날아가며 제각각 회전

                        // 'centroid'는 HLSL 보간 한정자 예약어라 변수명으로 쓸 수 없다.
                        float2 pivot = dir * RING_R;
                        float2 rel = (p - dir * push) - pivot;
                        float cs = cos(-spin), sn = sin(-spin);
                        rel = float2(rel.x * cs - rel.y * sn, rel.x * sn + rel.y * cs);
                        float2 q = pivot + rel;

                        float qa = atan2(q.y, q.x);
                        qa = qa < 0.0 ? qa + TWO_PI : qa;
                        // 균열 간격(_CrackGap)만큼 구간 양끝을 깎아 조각 사이가 갈라져 보이게 한다.
                        if (qa < a0 + _CrackGap * 0.5 || qa > a0 + sector - _CrackGap * 0.5) continue;

                        float a = RingMask(length(q));
                        if (a > amt) { amt = a; ang = qa; }
                    }

                    amt *= saturate(1.0 - _Break); // 흩어지면서 함께 사라짐
                }

                if (amt < 0.01) discard;

                // 각도를 따라 도는 은은한 하이라이트 — 평평한 링이 아니라 유리 구면처럼 보이게 한다.
                float shimmer = 0.75 + 0.25 * sin(ang * 3.0 + _Time.y * 5.0);
                half3 rgb = lerp(_Color.rgb, _CoreColor.rgb, saturate(shimmer * shimmer * 0.6));
                return half4(rgb * _BloomBoost * shimmer, amt * _Alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
