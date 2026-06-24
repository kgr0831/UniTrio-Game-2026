// ═══════════════════════════════════════════════════════════════════════════
// GolemSpikeWall.shader  —  골렘 구역 가시 바위 벽 (절차적·픽셀)
//
// 평면 쿼드(피벗 하단 중앙) 위에 절차적으로 "가시 바위" 한 개를 그린다.
//   · 위로 갈수록 좁아지는 가시 실루엣 + 결정론적 노이즈로 들쭉날쭉한 바위 가장자리
//   · UV 양자화(_PixelSize)로 픽셀 느낌
//   · 골렘 돌 색 팔레트(_BaseColor/_ShadowColor/_HighlightColor) + 가장자리 발광(_GlowColor)
//   · _Reveal(0→1): base에서 위로 차오르며 솟음 / 1→0: 땅속으로 가라앉음 (Step 4가 애니메이트)
//   · _Seed: 가시별 난수(모양·노이즈 변주) — MaterialPropertyBlock으로 주입
//
// SpriteMask 호환: 선택적 Stencil 블록 포함. 기본은 Comp Always(마스킹 없음).
//   필요 시 _StencilComp=Equal + SpriteMask로 추가 클립 가능.
//
// 블렌딩은 RockShatter/AdditiveGlow와 동일한 URP 스프라이트 규약(Premultiplied Alpha).
// ═══════════════════════════════════════════════════════════════════════════
Shader "Custom/GolemSpikeWall"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Rock Palette)]
        _BaseColor      ("Base Color",      Color) = (0.42, 0.40, 0.38, 1)
        _ShadowColor    ("Shadow Color",    Color) = (0.20, 0.19, 0.18, 1)
        _HighlightColor ("Highlight Color", Color) = (0.62, 0.60, 0.57, 1)

        [Header(Glow)]
        [HDR] _GlowColor ("Edge Glow", Color) = (0.30, 0.70, 1.40, 1)
        _GlowIntensity   ("Glow Intensity", Range(0, 4)) = 0.6

        [Header(Shape)]
        _PixelSize      ("Pixel Size (grid)", Range(8, 128)) = 36
        _Taper          ("Taper (tip sharpness)", Range(0.3, 2.5)) = 0.5
        _EdgeRoughness  ("Edge Roughness", Range(0, 1)) = 0.7
        _ColorVariation ("Color Variation", Range(0, 1)) = 0.5
        _FacetStrength  ("Facet Strength (2.5D 입체)", Range(0, 1)) = 0.75
        _Seed           ("Seed (per spike)", Float) = 0

        [Header(Emerge)]
        _Reveal ("Reveal (0=in ground  1=full)", Range(0, 1)) = 1

        [Header(Stencil (optional SpriteMask))]
        _StencilRef  ("Stencil Ref", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 8 // Always
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

        Cull     Off
        Lighting Off
        ZWrite   Off
        Blend    One OneMinusSrcAlpha   // Premultiplied Alpha

        Stencil
        {
            Ref  [_StencilRef]
            Comp [_StencilComp]
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   2.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                half4  _BaseColor;
                half4  _ShadowColor;
                half4  _HighlightColor;
                half4  _GlowColor;
                float  _GlowIntensity;
                float  _PixelSize;
                float  _Taper;
                float  _EdgeRoughness;
                float  _ColorVariation;
                float  _FacetStrength;
                float  _Seed;
                float  _Reveal;
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

            // 결정론적 해시 (0~1)
            float hash11(float n) { return frac(sin(n) * 43758.5453123); }
            float hash21(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123); }

            // 1D value noise — 세로로 매끄럽게 변하는 바위 굴곡용(정전기 같은 너덜거림 방지)
            float vnoise(float x)
            {
                float i = floor(x);
                float f = frac(x);
                float a = hash11(i);
                float b = hash11(i + 1.0);
                return lerp(a, b, f * f * (3.0 - 2.0 * f));
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
                // 픽셀 느낌: UV를 그리드로 양자화
                float px = max(8.0, _PixelSize);
                float2 q = (floor(input.uv * px) + 0.5) / px;

                float yy = saturate(q.y);          // 0=base, 1=tip
                float dx = abs(q.x - 0.5);         // 중앙으로부터의 폭

                // 폭 프로파일: 위로 갈수록 좁아지되 끝까지 두툼하게(최소폭) → 위쪽이 비지 않음
                float taper    = pow(1.0 - yy, _Taper);          // 1(base)→0(tip)
                float baseHalf = lerp(0.18, 0.5, taper);         // 최소 0.18 폭 보장

                // 좌/우 서로 다른, 세로로 매끄럽게 변하는 바위 굴곡(저주파 value noise 2옥타브)
                float sideS = (q.x < 0.5) ? 0.0 : 1.0;
                float e1 = vnoise(yy * 3.5 + _Seed * 10.0 + sideS * 50.0);
                float e2 = vnoise(yy * 8.0 + _Seed * 4.0  + sideS * 13.0);
                float edge  = ((e1 * 0.7 + e2 * 0.3) - 0.5) * _EdgeRoughness * 0.6;
                float halfW = saturate(baseHalf + edge);

                // 끝 높이를 좌우 다르게(매끄럽게) — 평평한 윗변 제거
                float topCut = 1.0 - (0.06 + vnoise(_Seed * 7.0 + sideS * 30.0) * 0.18);

                float inside = step(dx, halfW) * step(yy, _Reveal) * step(yy, topCut);
                clip(inside - 0.5);

                // ── 불규칙 바위 음영 ──
                float n  = hash21(floor(q * px) + _Seed);          // 픽셀별 바위 노이즈
                float n2 = hash21(floor(q * px) * 1.7 + _Seed * 2.3);
                float3 col = lerp(_ShadowColor.rgb, _BaseColor.rgb, saturate(n * 0.7 + 0.3));
                col = lerp(col, _HighlightColor.rgb, saturate((yy - 0.5) * 1.8) * (0.6 + 0.4 * n2));
                col *= lerp(0.8, 1.12, yy);                        // 세로 그라데이션

                // ── 2.5D 패싯(각진 면) 입체 음영 ─────────────────────────────
                // 능선(ridge): 중앙을 기준으로 세로로 살짝 흔들리는 모서리 선
                float u     = (q.x - 0.5) / max(halfW, 0.001);     // -1(왼쪽 가장자리)~+1(오른쪽)
                float ridge = (vnoise(yy * 2.0 + _Seed * 3.0) - 0.5) * 0.45;
                // 좌상단 가상 광원: 능선 왼쪽 면은 밝고(빛 받음), 오른쪽 측면은 어둡다
                float facetSide = smoothstep(-0.14, 0.14, u - ridge);
                float faceLight = lerp(1.18, 0.58, facetSide);
                // 세로 크리스탈 패싯: 높이를 단으로 나눠 단·면마다 밝기 변주(각진 결정면)
                float bandId    = floor(yy * 4.0);
                float bandShade = 0.82 + 0.34 * hash11(bandId + _Seed * 5.0 + facetSide * 17.0);
                // 능선 모서리에 빛나는 하이라이트(빛 받는 각진 모서리)
                float ridgeHi   = smoothstep(0.12, 0.0, abs(u - ridge)) * 0.55;
                float facet     = lerp(1.0, faceLight * bandShade, _FacetStrength) + ridgeHi * _FacetStrength;
                col *= facet;

                // 색 불규칙: 픽셀별 밝기 + 채널 흔들림 + 가끔 어두운 균열 점
                col *= lerp(1.0 - _ColorVariation * 0.5, 1.0 + _ColorVariation * 0.35, n);
                col.r *= lerp(1.0 - _ColorVariation * 0.15, 1.0 + _ColorVariation * 0.15, n2);
                col.b *= lerp(1.0 - _ColorVariation * 0.12, 1.0 + _ColorVariation * 0.10, frac(n2 * 1.9));
                if (n < _ColorVariation * 0.18) col *= 0.5;        // 어두운 바위 균열

                // 윗면 빛 받음: 끝(위)으로 갈수록 상단 면이 밝게 — 탑다운 광원 느낌
                col += _HighlightColor.rgb * saturate((yy - 0.55) * 2.2) * 0.35 * _FacetStrength;

                // 가장자리 림 발광 (오른쪽 그림자 측면은 약하게)
                float rim = smoothstep(halfW * 0.5, halfW, dx);
                col += _GlowColor.rgb * _GlowIntensity * rim * lerp(1.0, 0.5, facetSide);

                // base(땅 근처)는 어둡게 — 땅에 박힌 접지 음영(AO)
                col *= lerp(0.45, 1.0, saturate(yy * 2.0));

                half4 c = half4(col, 1.0) * half4(input.color.rgb, 1.0);
                c.a = input.color.a;
                c.rgb *= c.a;   // Premultiplied Alpha
                return c;
            }
            ENDHLSL
        }
    }
}
