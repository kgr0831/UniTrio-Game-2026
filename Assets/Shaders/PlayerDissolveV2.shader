// ═══════════════════════════════════════════════════════════════════════════
// PlayerDissolveV2.shader  —  소프트 페이드 Dissolve + 금색 먼지 변환
//
// ▸ 핵심 아이디어 — "금색 먼지가 되어가는 과정":
//     clip() 하드 컷 대신 소프트 알파 페이드를 사용합니다.
//     픽셀은 세 단계를 거칩니다:
//       1. 불투명 원색 (dissolve 경계에서 멀 때)
//       2. 금색으로 물들며 반투명해짐 (경계선 근처 _FadeWidth 범위)
//       3. 완전히 투명 (경계를 지남)
//
// ▸ 블록 단위로 사라지던 원인:
//     이전 버전: clip(dissolveMask - _Threshold) → 픽셀이 한 번에 사라짐
//     현재 버전: 소프트 fadeAlpha로 서서히 투명해짐 + 고주파 노이즈로 미세 입자 질감
//
// 파라미터:
//   _Threshold   : 0=완전 표시, 1=완전 소멸  ← PlayerDeathEffect MPB 제어
//   _NoiseScale  : 노이즈 주파수. 높을수록 더 가늘고 미세한 먼지 질감 (기본 28)
//   _FadeWidth   : 소프트 페이드 구간 폭. 넓을수록 더 넓은 범위에서 서서히 변함 (기본 0.18)
//   _GoldColor   : 먼지로 변하는 순간의 금색
//   _GoldBright  : 금색 밝기 배율 (Bloom 없으면 1.0, Bloom 있으면 2-3)
// ═══════════════════════════════════════════════════════════════════════════
Shader "Custom/PlayerDissolveV2"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Dissolve)]
        _Threshold  ("Threshold (0=full  1=gone)",  Range(0, 1.1))  = 0
        _NoiseScale ("Noise Scale (higher=finer dust)", Range(5, 60)) = 28
        _FadeWidth  ("Fade Width (soft dissolve zone)", Range(0.02, 0.4)) = 0.18

        [Header(Gold Dust Color)]
        _GoldColor  ("Gold Color",    Color)       = (1, 0.75, 0.05, 1)
        _GoldBright ("Gold Brightness (x1=LDR, x3=Bloom)", Range(0.5, 5)) = 1.6
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
                half4  _GoldColor;
                float  _GoldBright;
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
                float2 uv         : TEXCOORD0;   // 아틀라스 변환 후 (텍스처 샘플링)
                float2 rawUV      : TEXCOORD1;   // 변환 전 [0,1] (dissolve 방향 계산)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ─── 그래디언트 노이즈 ───────────────────────────────────────────
            float2 _GHash(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453123) * 2.0 - 1.0;
            }

            float _GradNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(dot(_GHash(i),               f),
                         dot(_GHash(i + float2(1,0)), f - float2(1,0)), u.x),
                    lerp(dot(_GHash(i + float2(0,1)), f - float2(0,1)),
                         dot(_GHash(i + float2(1,1)), f - float2(1,1)), u.x),
                    u.y);
            }

            // 3-octave FBM — 더 미세한 먼지 질감
            float _FBM(float2 uv, float scale)
            {
                float2 p  = uv * scale;
                float  n  = _GradNoise(p)                             * 0.55;
                       n += _GradNoise(p * 2.2 + float2(1.7,  9.2))  * 0.30;
                       n += _GradNoise(p * 5.1 + float2(8.3, 2.8))   * 0.15;
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
                // ── 원본 스프라이트 샘플링 ──────────────────────────────────
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                c *= input.color;
                clip(c.a - 0.01);   // 완전 투명 픽셀 제거

                // ── Dissolve 마스크 (rawUV 기준 — 아틀라스 독립) ───────────
                // rawUV.y=1(상단)=먼저 사라짐, rawUV.y=0(하단)=나중에 사라짐
                float yMask = 1.0 - input.rawUV.y;
                float noise = _FBM(input.rawUV, _NoiseScale);

                // Y-방향 70% + 노이즈 30% → 방향성 유지 + 불규칙 먼지 경계
                float dissolveMask = saturate(yMask * 0.70 + noise * 0.30);

                // ── 소프트 페이드 (핵심 변경) ────────────────────────────────
                // clipDist > 0        : 아직 살아있는 픽셀
                // clipDist ∈ (-FW, 0) : 서서히 금색으로 변하며 투명해지는 구간
                // clipDist < -FW      : 완전히 소멸
                float clipDist = dissolveMask - _Threshold;

                // _FadeWidth 바깥(완전 소멸 영역)은 하드 컷으로 비용 절감
                clip(clipDist + _FadeWidth);

                // fadeAlpha: 1=완전 불투명, 0=완전 투명
                // 경계(clipDist=0)에서 0, 멀어질수록(clipDist→FW) 1로 수렴
                float fadeAlpha = saturate(clipDist / max(_FadeWidth, 0.001));

                // ── 금색 먼지 변환 ─────────────────────────────────────────
                // goldT: fadeAlpha=0(경계)에서 1, fadeAlpha=1(멀리)에서 0
                // 픽셀이 사라지기 직전에 가장 강하게 금색이 됨
                float goldT = 1.0 - fadeAlpha;
                goldT = goldT * goldT;   // 2차 감쇠: 경계 근처에 집중

                half3 goldRGB = _GoldColor.rgb * _GoldBright;

                // Additive 오버레이: 원색을 유지하면서 금색 빛 추가
                // (lerp 대신 add → Bloom 없이도 금색이 보임)
                c.rgb += goldRGB * goldT;

                // ── 소프트 알파 적용 (서서히 사라지는 핵심) ─────────────────
                // 원본 알파에 fadeAlpha를 곱해 경계선에서 서서히 투명해짐
                c.a *= fadeAlpha;

                // ── Premultiplied Alpha ────────────────────────────────────
                c.rgb *= c.a;
                return c;
            }
            ENDHLSL
        }
    }
}
