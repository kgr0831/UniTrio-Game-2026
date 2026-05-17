Shader "Custom/ElementalOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Element)]
        _ElementType ("Element (0=Earth 1=Fire 2=Ice)", Float) = 0
        _EffectIntensity ("Effect Intensity", Range(0, 3)) = 1.0

        [Header(Pixel Snap)]
        _PixelRes ("Pixel Resolution", Float) = 32.0

        [Header(Fire)]
        _ScrollSpeed ("Fire Scroll Speed", Float) = 1.5
        _NoiseScale  ("Fire Noise Scale", Float) = 4.0
        _VoronoiScale ("Fire Voronoi Scale", Float) = 6.0
        [HDR] _FireColor1 ("Fire Hot",  Color) = (1.0, 0.9, 0.2, 1)
        [HDR] _FireColor2 ("Fire Mid",  Color) = (1.0, 0.5, 0.0, 1)
        [HDR] _FireColor3 ("Fire Cool", Color) = (0.8, 0.1, 0.0, 1)

        [Header(Ice)]
        _IceVoronoiScale ("Ice Voronoi Scale", Float) = 24.0
        _EdgeThreshold ("Ice Edge Threshold", Range(0, 0.5)) = 0.2
        [HDR] _IceColor ("Ice Color", Color) = (0.4, 0.9, 1.0, 3)

        [Header(Earth)]
        _DitherScale ("Earth Dither Scale", Float) = 3.0
        _PulseSpeed  ("Earth Pulse Speed",  Float) = 2.5
        [HDR] _EarthColor ("Earth Color", Color) = (0.7, 0.55, 0.15, 2)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+1"
        }

        Blend SrcAlpha One
        Cull Off
        ZWrite Off

        Pass
        {
            Name "ElementalOverlayForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ──────────────────────────────────────────────
            //  CBUFFER
            // ──────────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                half   _ElementType;
                half   _EffectIntensity;
                float  _PixelRes;

                float  _ScrollSpeed;
                float  _NoiseScale;
                float  _VoronoiScale;
                half4  _FireColor1;
                half4  _FireColor2;
                half4  _FireColor3;

                float  _IceVoronoiScale;
                half   _EdgeThreshold;
                half4  _IceColor;

                float  _DitherScale;
                float  _PulseSpeed;
                half4  _EarthColor;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            // ──────────────────────────────────────────────
            //  Vertex I/O
            // ──────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 screenPos   : TEXCOORD1;
                half4  color       : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ──────────────────────────────────────────────
            //  Procedural math helpers
            // ──────────────────────────────────────────────

            // 2D → 2D hash (gradient noise용)
            float2 _Hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453);
            }

            // Gradient noise (Perlin-like), 0~1 range
            float _GradientNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = dot(_Hash22(i + float2(0, 0)), f - float2(0, 0));
                float b = dot(_Hash22(i + float2(1, 0)), f - float2(1, 0));
                float c = dot(_Hash22(i + float2(0, 1)), f - float2(0, 1));
                float d = dot(_Hash22(i + float2(1, 1)), f - float2(1, 1));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 0.5 + 0.5;
            }

            // Voronoi (cell distance), returns min distance 0~1
            float _Voronoi(float2 uv, float angleOffset)
            {
                float2 g = floor(uv);
                float2 f = frac(uv);
                float md = 1.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 nb = float2(x, y);
                        float2 h  = float2(
                            dot(g + nb, float2(127.1, 311.7)),
                            dot(g + nb, float2(269.5, 183.3)));
                        float2 pt = frac(sin(h) * 43758.5453);
                        pt = 0.5 + 0.5 * sin(angleOffset + 6.2831853 * pt);

                        float2 diff = nb + pt - f;
                        md = min(md, dot(diff, diff));
                    }
                }
                return sqrt(md);
            }

            // Bayer 4×4 ordered dithering (screen-space)
            float _Bayer4x4(float2 pos)
            {
                // Bayer 매트릭스 값을 하드코딩
                int2 idx = int2(fmod(abs(pos), 4.0));
                // row-major 4x4 matrix flattened
                const float bayer[16] = {
                     0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
                    12.0 / 16.0,  4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
                     3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
                    15.0 / 16.0,  7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
                };
                return bayer[idx.y * 4 + idx.x];
            }

            // ──────────────────────────────────────────────
            //  Vertex shader
            // ──────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.screenPos  = ComputeScreenPos(OUT.positionCS);
                OUT.color      = IN.color * _Color;
                return OUT;
            }

            // ──────────────────────────────────────────────
            //  Fragment shader
            // ──────────────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                // ── 1. 스프라이트 알파 마스크 ──
                half spriteAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                spriteAlpha *= IN.color.a;
                if (spriteAlpha < 0.01)
                    discard;

                // ── 2. UV 양자화 (픽셀 스냅) ──
                float2 pixUV = floor(IN.uv * _PixelRes + 0.5) / _PixelRes;

                // ── 3. 속성 분기 ──
                int elem = (int)round(_ElementType);
                half3 effectColor = half3(0, 0, 0);
                half  effectAlpha = 0;

                // ──────────── FIRE ────────────
                if (elem == 1)
                {
                    float2 scrollUV = pixUV;
                    scrollUV.y += _Time.y * _ScrollSpeed;

                    // Gradient noise + Voronoi 혼합
                    float noise  = _GradientNoise(scrollUV * _NoiseScale) * 0.7;
                    float voro   = _Voronoi(scrollUV * _VoronoiScale, _Time.y * 2.0) * 0.3;
                    float combined = saturate(noise + voro);

                    // 3단 색상 밴딩 (Posterize)
                    if (combined > 0.66)
                        effectColor = _FireColor1.rgb;
                    else if (combined > 0.33)
                        effectColor = _FireColor2.rgb;
                    else
                        effectColor = _FireColor3.rgb;

                    effectAlpha = saturate(combined * 1.5) * _EffectIntensity;
                }
                // ──────────── ICE ─────────────
                else if (elem == 2)
                {
                    // 고밀도 Voronoi (Scale 24~30 → 촘촘한 얼음 파편)
                    float dist1 = _Voronoi(pixUV * _IceVoronoiScale, 0.01);
                    // 두 번째 레이어: 더 미세한 서리 결정
                    float dist2 = _Voronoi(pixUV * _IceVoronoiScale * 1.7 + 3.7, 0.03);

                    // 테두리 추출 (두 레이어 합산)
                    float edge1 = 1.0 - smoothstep(_EdgeThreshold - 0.08, _EdgeThreshold, dist1);
                    float edge2 = 1.0 - smoothstep(_EdgeThreshold - 0.04, _EdgeThreshold * 0.8, dist2);
                    float edge = saturate(edge1 + edge2 * 0.6);

                    // 시간 기반 반짝임 (서리 미세 깜빡임)
                    float shimmer = _GradientNoise(pixUV * 20.0 + _Time.y * 0.8);
                    edge += shimmer * 0.2;

                    // 내부 서리 텍스처 (약한 노이즈 → 반투명 얼음 질감)
                    float frost = _GradientNoise(pixUV * _IceVoronoiScale * 0.5 + _Time.y * 0.1);
                    edge = saturate(edge + frost * 0.15);

                    effectColor = _IceColor.rgb;
                    effectAlpha = saturate(edge) * _EffectIntensity;
                }
                // ──────────── EARTH ───────────
                else
                {
                    // ── 픽셀아트 해상도 정합 (UV 기반) ──
                    // 이미 위에서 계산된 pixUV를 사용해 계단 현상이 있는 노이즈를 만듭니다.
                    
                    // ── UV 기반 돌/결정 패턴 (Voronoi) ──
                    float rockDist = _Voronoi(pixUV * 8.0, _Time.y * 1.5);
                    float rockEdge = 1.0 - smoothstep(0.05, 0.2, rockDist);

                    // ── 유기적 노이즈 마스크 (두 옥타브) ──
                    float noise1 = _GradientNoise(pixUV * 5.0 + _Time.y * 0.15);
                    float noise2 = _GradientNoise(pixUV * 12.0 + 3.0);
                    float noiseMask = saturate(noise1 * 0.6 + noise2 * 0.4);

                    // ── 황금 토파즈 ↔ 에메랄드 녹색 그라데이션 ──
                    half3 goldenAmber  = _EarthColor.rgb;  // HDR 황금빛
                    half3 emeraldGreen = half3(0.15, 0.5, 0.2) * 2.0;
                    
                    // 노이즈와 Voronoi를 곱해 마법 암석 파편 같은 질감 생성
                    half3 earthBlend = lerp(emeraldGreen, goldenAmber, noiseMask);
                    earthBlend += rockEdge * goldenAmber * 0.5; // 결정 테두리를 더 밝게

                    effectColor = earthBlend;
                    effectAlpha = _EffectIntensity; // 알파 파편화를 제거하고 온전한 형태로 렌더링
                }

                // ── 4. 최종 합성 ──
                effectAlpha *= spriteAlpha;

                // ── 최대 밝기 제한 (Clamp) ──
                // Additive 블렌딩 시 겹쳐도 너무 하얗게 타지 않도록 최대치 제한
                half3 finalColor = min(effectColor * effectAlpha, half3(2.5, 2.5, 2.5));
                return half4(finalColor, effectAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
