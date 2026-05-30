// ═══════════════════════════════════════════════════════════════════════════
// RockShatter.shader  —  조각 비산(Shatter) 효과
//
// 동작:
//   세분화된 그리드 메시(셀마다 정점 분리)를 받아, 각 셀을 강체처럼
//   _Progress(0→1)에 따라 셀 중심 기준으로 회전시키고 바깥으로 밀어내며
//   중력을 적용한다. 동시에 알파를 페이드해 산산조각 후 사라지는 모습.
//
//   셀 식별: 정점의 TEXCOORD1(셀 중심, 오브젝트 공간)으로 셀별 해시를 만든다.
//   같은 셀의 4개 정점은 동일한 중심/난수를 공유하므로 강체로 움직인다.
//
// 파라미터:
//   _Progress        : 0=원형 그대로, 1=완전 비산/소멸  ← ShatterEffect가 MPB로 제어
//   _ScatterDistance : 셀이 바깥으로 밀려나는 최대 거리(오브젝트 공간)
//   _RotateAmount    : 셀 최대 회전량(라디안)
//   _Gravity         : 진행에 따라 아래로 끌리는 양(포물선 낙하)
// ═══════════════════════════════════════════════════════════════════════════
Shader "Custom/RockShatter"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Shatter)]
        _Progress        ("Progress (0=intact  1=gone)", Range(0, 1)) = 0
        _ScatterDistance ("Scatter Distance",            Range(0, 5)) = 1.2
        _RotateAmount    ("Rotate Amount (radians)",     Range(0, 12)) = 4.0
        _Gravity         ("Gravity",                     Range(0, 8)) = 1.5
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
                float  _Progress;
                float  _ScatterDistance;
                float  _RotateAmount;
                float  _Gravity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float2 cellCenter : TEXCOORD1; // 셀 중심(오브젝트 공간 XY)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ─── 셀별 결정론적 해시 (같은 셀은 항상 같은 값) ────────────
            float _Hash(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float2 center = input.cellCenter;
                float2 local  = input.positionOS.xy - center; // 셀 중심 기준 상대 위치

                // 셀별 난수: 흔들림, 회전 방향/세기
                float h1    = _Hash(center);
                float h2    = _Hash(center + 17.3);
                float spin  = (h2 - 0.5) * 2.0 * _RotateAmount; // 회전량(±)

                // 비산 방향: 바위 중심(오브젝트 원점)에서 바깥으로 방사 + 약간의 난수 흔들림
                // → 조각이 사방으로 고르게 퍼진다 (피벗이 스프라이트 중심일 때).
                float jitterA = h1 * 6.2831853;
                float2 jitter = float2(cos(jitterA), sin(jitterA)) * 0.35;
                float2 radial = (dot(center, center) > 1e-6) ? normalize(center) : jitter;
                float2 dir    = normalize(radial + jitter);

                // 셀을 중심 기준 회전(강체)
                float rot = spin * _Progress;
                float s, c;
                sincos(rot, s, c);
                float2 rotated = float2(local.x * c - local.y * s,
                                        local.x * s + local.y * c);

                // 중심을 바깥으로 밀고 중력으로 낙하(포물선)
                float2 offset = dir * (_ScatterDistance * (0.4 + 0.6 * h2)) * _Progress;
                offset.y -= _Gravity * _Progress * _Progress;

                float2 finalPos = center + rotated + offset;

                output.positionCS = TransformObjectToHClip(float3(finalPos, input.positionOS.z));
                output.uv    = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                c *= input.color;
                clip(c.a - 0.01); // 원본 투명 영역 제거

                // 진행에 따른 알파 페이드 (후반부에 빠르게 사라짐)
                c.a *= saturate(1.0 - _Progress * _Progress);

                c.rgb *= c.a; // Premultiplied Alpha
                return c;
            }
            ENDHLSL
        }
    }
}
