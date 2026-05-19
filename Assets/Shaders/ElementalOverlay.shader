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
                // ──────────── ICE (Frost Aura · 냉기 안개) ─────────────
                else if (elem == 2)
                {
                    // 두 레이어의 Gradient Noise를 서로 다른 방향/속도로 느리게 스크롤
                    // → 차가운 안개가 무기를 감싸며 일렁이는 효과
                    float2 frostUV1 = pixUV;
                    frostUV1.x += _Time.y * 0.25;
                    frostUV1.y -= _Time.y * 0.15;

                    float2 frostUV2 = pixUV;
                    frostUV2.x -= _Time.y * 0.18;
                    frostUV2.y += _Time.y * 0.22;

                    // 부드러운 노이즈 혼합 (서로 다른 스케일로 겹쳐 안개 질감 생성)
                    float frost1 = _GradientNoise(frostUV1 * 4.0);
                    float frost2 = _GradientNoise(frostUV2 * 6.5 + 3.7);
                    float combined = saturate(frost1 * 0.55 + frost2 * 0.45);

                    // 쨍한 시안 ↔ 깊은 아이스 블루 HDR 그라데이션
                    half3 sharpCyan = _IceColor.rgb * 2.5;              // 시안 HDR (인스펙터 색상 기반)
                    half3 iceBlue   = half3(0.2, 0.45, 0.95) * 2.0;     // 깊은 아이스 블루 HDR
                    effectColor = lerp(iceBlue, sharpCyan, combined);

                    // 느린 맥동: 차가운 기운이 숨쉬듯 부드럽게 일렁임
                    float pulse = sin(_Time.y * 1.5 + frost1 * 3.0) * 0.12 + 0.88;
                    effectAlpha = saturate(combined * 1.4 * pulse) * _EffectIntensity;
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
