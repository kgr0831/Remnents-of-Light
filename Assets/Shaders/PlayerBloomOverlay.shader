// 플레이어 스프라이트 위에 같은 실루엣을 HDR로 한 겹 더 얹어 "그 오브젝트만" 블룸시킨다.
// (URP엔 오브젝트별 블룸이 없다 → 임계값 1.15 위로 출력하는 것이 유일한 방법. 원본 스프라이트는
//  건드리지 않고 가산 합성만 하므로 2D 라이팅·머티리얼 교체 복원 같은 부작용이 없다.)
//
// PlayerBloomFx.cs가 매 프레임 플레이어의 sprite/flipX를 복사한 SpriteRenderer에 이 셰이더를 물린다.
// _Intensity가 0이면 아무것도 더하지 않으므로 페이드 인/아웃은 이 값 하나로 제어한다.
Shader "Custom/PlayerBloomOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Glow Tint", Color) = (0.65, 0.92, 1.0, 1)
        _BloomBoost ("Bloom Boost (HDR)", Range(1,8)) = 3.0
        _Intensity ("Intensity", Range(0,1)) = 0
        // 0이면 스프라이트 색을 그대로 곱해 밝은 픽셀만 빛나고(어두운 캐릭터는 거의 안 빛남),
        // 1이면 알파 실루엣 전체가 균일하게 빛난다. 사무라이 스프라이트는 대부분 어두워서 기본값을 높게 둔다.
        _Flatten ("Silhouette Flatten", Range(0,1)) = 0.65
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
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // ⚠ _MainTex_ST를 여기 넣으면 안 된다 — 2D SRP Batcher가 _TexelSize/_ST 텍스처 프로퍼티를
            // 지원하지 않아 이 머티리얼을 쓰는 2D 렌더러 전체의 SRP 배칭이 꺼진다(에디터 경고로 확인).
            // SpriteRenderer가 만드는 메시의 UV는 이미 아틀라스 좌표라 타일링/오프셋이 필요 없다.
            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                float  _BloomBoost;
                float  _Intensity;
                float  _Flatten;
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
                // 가산 블렌드라 알파는 쓰이지 않는다 → 실루엣 모양은 알파를 rgb에 직접 곱해서 만든다.
                half3 glow = base * _Color.rgb * (tex.a * _Intensity * _BloomBoost);
                return half4(glow, 0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
