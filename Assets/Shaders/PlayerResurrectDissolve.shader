// ═══════════════════════════════════════════════════════════════════════════
// PlayerResurrectDissolve.shader  —  부활 역용해 (아래→위, 암적색 엣지)
//
// ▸ 동작 원리:
//     _Threshold를 1→0으로 낮추면 하단 픽셀부터 순서대로 나타납니다.
//     나타나는 경계선에서 암적색 엣지 빛이 생기며,
//     스프라이트 전체가 _TintColor로 물들다가 _TintAmount가 줄어들며
//     원래 색으로 복원됩니다.
//
// 파라미터:
//   _Threshold   : 1=완전 소멸(시작), 0=완전 표시(종료)
//   _EdgeColor   : 경계선 엣지 색 (기본: 암적색)
//   _EdgeBright  : 엣지 밝기 배율
//   _TintColor   : 스프라이트 전체 색조 (부활 시작: 암적색, 종료: 흰색)
//   _TintAmount  : 색조 적용 비율 (1=완전 색조, 0=원래 색)
//   _NoiseScale  : 노이즈 주파수
//   _FadeWidth   : 소프트 페이드 구간 폭
// ═══════════════════════════════════════════════════════════════════════════
Shader "Custom/PlayerResurrectDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Dissolve)]
        _Threshold  ("Threshold (1=gone  0=full)", Range(0, 1.1))     = 1
        _NoiseScale ("Noise Scale",                Range(5, 60))       = 28
        _FadeWidth  ("Fade Width",                 Range(0.02, 0.4))   = 0.18

        [Header(Edge Glow)]
        _EdgeColor  ("Edge Color (dark-red)",  Color)            = (0.6, 0.0, 0.0, 1)
        _EdgeBright ("Edge Brightness",        Range(0.5, 5))    = 1.8

        [Header(Sprite Tint)]
        _TintColor  ("Tint Color (dark-red)", Color)           = (0.35, 0.0, 0.0, 1)
        _TintAmount ("Tint Amount (1=tinted 0=normal)", Range(0, 1)) = 1
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
        Blend One OneMinusSrcAlpha

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
                float  _Threshold;
                float  _NoiseScale;
                float  _FadeWidth;
                half4  _EdgeColor;
                float  _EdgeBright;
                half4  _TintColor;
                float  _TintAmount;
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
                float2 rawUV      : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ─── FBM 노이즈 (PlayerDissolveV2와 동일) ────────────────────────
            float2 _GHash(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453123) * 2.0 - 1.0;
            }
            float _GradNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(dot(_GHash(i),               f),
                         dot(_GHash(i + float2(1,0)), f - float2(1,0)), u.x),
                    lerp(dot(_GHash(i + float2(0,1)), f - float2(0,1)),
                         dot(_GHash(i + float2(1,1)), f - float2(1,1)), u.x),
                    u.y);
            }
            float _FBM(float2 uv, float scale)
            {
                float2 p = uv * scale;
                float  n = _GradNoise(p)                            * 0.55
                         + _GradNoise(p * 2.2 + float2(1.7, 9.2))  * 0.30
                         + _GradNoise(p * 5.1 + float2(8.3, 2.8))  * 0.15;
                return n * 0.5 + 0.5;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv    = TRANSFORM_TEX(input.uv, _MainTex);
                output.rawUV = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                c *= input.color;
                clip(c.a - 0.01);

                // ── 역방향 Dissolve 마스크 ──────────────────────────────────
                // rawUV.y=0(하단)→yMask=1 → 하단이 먼저 나타남
                float yMask = 1.0 - input.rawUV.y;   // 죽음과 동일한 방향 마스크
                float noise  = _FBM(input.rawUV, _NoiseScale);
                float dissolveMask = saturate(yMask * 0.70 + noise * 0.30);

                // Threshold 1→0: 높은 mask 값(하단) 픽셀부터 clipDist > 0이 됨
                float clipDist = dissolveMask - _Threshold;
                clip(clipDist + _FadeWidth);

                float fadeAlpha = saturate(clipDist / max(_FadeWidth, 0.001));

                // ── 암적색 엣지 글로우 (나타나는 경계) ─────────────────────
                float edgeT = 1.0 - fadeAlpha;
                edgeT = edgeT * edgeT;
                c.rgb += _EdgeColor.rgb * _EdgeBright * edgeT;

                // ── 스프라이트 전체 암적색 색조 → 원래 색으로 복원 ──────────
                // TintAmount=1: 색조 강하게 적용, TintAmount=0: 원래 색 그대로
                c.rgb = lerp(c.rgb, c.rgb * _TintColor.rgb * 2.0h, _TintAmount);

                c.a   *= fadeAlpha;
                c.rgb *= c.a;
                return c;
            }
            ENDHLSL
        }
    }
}
