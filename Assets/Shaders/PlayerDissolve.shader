// ═══════════════════════════════════════════════════════════════════════════
// PlayerDissolve.shader  —  위→아래 픽셀 분해(Pixel Disintegration) 효과
//
// 동작:
//   스프라이트를 _PixelSize 크기의 사각형 블록으로 나눈 뒤,
//   _DissolveAmount(0→1)에 따라 위쪽 블록부터 순서대로 사라집니다.
//   블록 단위로 hard-clip 되므로 마치 픽셀들이 소멸하는 것처럼 보입니다.
//
// 파라미터:
//   _DissolveAmount  : 0=완전 표시, 1=완전 소멸  ← PlayerDeathEffect가 MPB로 제어
//   _PixelSize       : 각 픽셀 블록의 UV 크기 (클수록 큰 블록, 작을수록 세밀)
//   _Scatter         : 같은 Y열에서 블록마다 소멸 시점이 달라지는 산포도 (0=줄 단위, 1=완전 랜덤)
// ═══════════════════════════════════════════════════════════════════════════
Shader "Custom/PlayerDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Pixel Disintegration)]
        _DissolveAmount ("Dissolve Amount (0=full  1=gone)", Range(0, 1.05)) = 0
        _PixelSize      ("Pixel Block Size (UV)",             Range(0.002, 0.08)) = 0.018
        _Scatter        ("Scatter (0=row  1=random)",         Range(0, 1))        = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline"    = "UniversalPipeline"
        }

        Cull    Off
        Lighting Off
        ZWrite  Off
        Blend One OneMinusSrcAlpha   // Premultiplied Alpha (URP 스프라이트 기본)

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   2.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                float  _DissolveAmount;
                float  _PixelSize;
                float  _Scatter;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ─── 블록별 결정론적 해시 (같은 블록은 항상 같은 값) ────────────
            float _BlockHash(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv    = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // ── 원본 UV로 텍스처 샘플링 (스프라이트 품질 그대로 유지) ──
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                c *= input.color;
                clip(c.a - 0.01); // 원본 투명 영역 즉시 제거

                // ── 픽셀 블록 그리드 계산 ────────────────────────────────────
                // UV 공간을 _PixelSize 단위 격자로 분할.
                // 같은 격자 안의 모든 픽셀은 동일한 블록 인덱스(cell)를 가집니다.
                float2 cell         = floor(input.uv / _PixelSize);
                float2 blockCenterY = (cell.y + 0.5) * _PixelSize; // 블록의 Y 중심

                // ── 블록별 소멸 임계값 계산 ──────────────────────────────────
                // dissolveY: 0=위쪽, 1=아래쪽 → 위쪽 블록이 낮은 임계값 = 먼저 사라짐
                float dissolveY    = 1.0 - blockCenterY;

                // 블록마다 다른 해시 값(0-1)으로 산포도 적용
                // _Scatter=0 이면 Y열 단위로 일제 소멸, 1이면 완전 랜덤 패턴
                float blockRandom  = _BlockHash(cell);
                float threshold    = saturate(dissolveY + (blockRandom - 0.5) * _Scatter);

                // _DissolveAmount가 블록 임계값을 넘으면 이 블록 소멸
                clip(threshold - _DissolveAmount);

                // ── Premultiplied Alpha ───────────────────────────────────────
                c.rgb *= c.a;
                return c;
            }
            ENDHLSL
        }
    }
}
