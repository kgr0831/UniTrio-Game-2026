Shader "Custom/DebuffOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Debuff Activation)]
        _FireActive  ("Fire Active (0 or 1)",  Float) = 0
        _IceActive   ("Ice Active (0 or 1)",   Float) = 0
        _EarthActive ("Earth Active (0 or 1)", Float) = 0

        [Header(Fire Effect)]
        [HDR] _FireColor ("Fire Color", Color) = (1.5, 0.4, 0.05, 1)
        _FirePulseSpeed ("Fire Pulse Speed", Float) = 8.0
        _FireIntensity  ("Fire Intensity", Range(0, 5)) = 1.1

        [Header(Ice Effect)]
        [HDR] _IceColor ("Ice Color", Color) = (0.2, 1.2, 1.5, 1)
        _IceNoiseScale ("Ice Noise Scale", Float) = 5.0
        _IceIntensity  ("Ice Intensity", Range(0, 5)) = 1.0

        [Header(Earth Effect)]
        [HDR] _EarthColor ("Earth Color", Color) = (1.2, 1.0, 0.3, 1)
        _EarthPulseSpeed ("Earth Pulse Speed", Float) = 3.0
        _EarthIntensity  ("Earth Intensity", Range(0, 5)) = 0.9
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+2"
        }

        Blend SrcAlpha One
        Cull Off
        ZWrite Off

        Pass
        {
            Name "DebuffOverlayForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;

                half   _FireActive;
                half   _IceActive;
                half   _EarthActive;

                half4  _FireColor;
                float  _FirePulseSpeed;
                half   _FireIntensity;

                half4  _IceColor;
                float  _IceNoiseScale;
                half   _IceIntensity;

                half4  _EarthColor;
                float  _EarthPulseSpeed;
                half   _EarthIntensity;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // 2D hash for noise
            float2 _Hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453);
            }

            // Gradient noise 0~1
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

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color      = IN.color * _Color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 스프라이트 알파 마스크
                half spriteAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                spriteAlpha *= IN.color.a;
                if (spriteAlpha < 0.01)
                    discard;

                half3 effectColor = half3(0, 0, 0);
                half  effectAlpha = 0;

                // ──────── FIRE: 주황 점멸 ────────
                if (_FireActive > 0.5)
                {
                    float firePulse = sin(_Time.y * _FirePulseSpeed) * 0.5 + 0.5;
                    // 가장자리 강조: UV 기반 가장자리 감지
                    float edgeDist = min(min(IN.uv.x, 1.0 - IN.uv.x), min(IN.uv.y, 1.0 - IN.uv.y));
                    float edgeFactor = 1.0 - saturate(edgeDist * 3.0);
                    float fireEffect = saturate(firePulse * 0.7 + edgeFactor * 0.6);

                    effectColor += _FireColor.rgb * fireEffect * _FireIntensity;
                    effectAlpha += fireEffect * _FireIntensity * 0.8;
                }

                // ──────── ICE: 시안 결빙 노이즈 ────────
                if (_IceActive > 0.5)
                {
                    float2 iceUV = IN.uv * _IceNoiseScale;
                    
                    // 시간에 따른 UV 이동(스크롤)을 제거하여 완전히 정지된 얼음 질감을 유지합니다.
                    float iceNoise = _GradientNoise(iceUV);
                    
                    // 노이즈 대비 증가
                    iceNoise = smoothstep(0.3, 0.7, iceNoise);

                    // 점멸(펄스) 없이 일정하게 결빙 효과 유지
                    float iceEffect = saturate(iceNoise * 1.5 + 0.3); // 노이즈 + 기본 베이스 색상

                    effectColor += _IceColor.rgb * iceEffect * _IceIntensity;
                    effectAlpha += iceEffect * _IceIntensity * 0.7;
                }

                // ──────── EARTH: 황금 아우라 맥동 ────────
                if (_EarthActive > 0.5)
                {
                    float earthPulse = sin(_Time.y * _EarthPulseSpeed) * 0.4 + 0.6;
                    // 전체적으로 조금 더 덮이도록
                    float edgeDist2 = min(min(IN.uv.x, 1.0 - IN.uv.x), min(IN.uv.y, 1.0 - IN.uv.y));
                    float earthEdge = 1.0 - saturate(edgeDist2 * 2.0);
                    float earthEffect = saturate(earthPulse * 0.6 + earthEdge * 0.8);

                    effectColor += _EarthColor.rgb * earthEffect * _EarthIntensity;
                    effectAlpha += earthEffect * _EarthIntensity * 0.7;
                }

                // 최종 합성
                effectAlpha = saturate(effectAlpha) * spriteAlpha;
                half3 finalColor = min(effectColor * effectAlpha, half3(2.5, 2.5, 2.5));
                return half4(finalColor, effectAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
