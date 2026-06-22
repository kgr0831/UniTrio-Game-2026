Shader "UniTrio/SpriteLitFlashWind"
{
    // ─────────────────────────────────────────────────────────────
    // SpriteLitFlash + 바람 흔들림(Wind Sway) 통합 버전 (나무 전용)
    //  - 기존 SpriteLitFlash의 모든 기능 유지
    //    (낮/밤 주변광, 추가 포인트 라이트, 피격 플래시, 이미시브)
    //  - 정점 단계에서 X축으로 흔들림 추가
    //    밑동(uv.y=0)은 고정, 꼭대기(uv.y=1)로 갈수록 크게 흔들림
    // ─────────────────────────────────────────────────────────────
    Properties
    {
        [MainTexture] _BaseMap("Sprite Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)

        _FlashColor("Flash Color", Color) = (1,1,1,1)
        _FlashAmount("Flash Amount", Range(0.0, 1.0)) = 0.0

        _EmissionMap("Emission Map", 2D) = "black" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,0)

        _AmbientLight("Fallback Ambient (No Manager)", Range(0, 1)) = 0.5
        _LightSteps("Point Light Steps", Range(2, 32)) = 8
        _PixelGridSize("Pixel Grid Size (1/PPU)", Float) = 0.03125

        [Header(Wind Settings)]
        _WindSpeed("Wind Speed", Float) = 3.0            // 흔들리는 속도
        _WindStrength("Wind Strength", Float) = 0.1      // 흔들리는 폭(오브젝트 공간)
        _WindTurbulence("Wind Turbulence", Float) = 1.0  // 높이에 따른 위상 불규칙성
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
            Name "SpriteLitFlashWindForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _FlashColor;
                half  _FlashAmount;
                half4 _EmissionColor;
                half  _AmbientLight;
                half  _LightSteps;
                float _PixelGridSize;
                float _WindSpeed;
                float _WindStrength;
                float _WindTurbulence;
            CBUFFER_END

            // DayNightManager가 Shader.SetGlobal*로 주입하는 전역 값
            half4 _DayNightAmbientColor;
            half  _DayNightEnabled;

            TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4  color      : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // --- 바람 흔들림: 밑동(uv.y=0) 고정, 꼭대기(uv.y=1)만 X축으로 흔들림 ---
                float sway = sin(_Time.y * _WindSpeed + IN.positionOS.y * _WindTurbulence)
                             * _WindStrength * IN.uv.y;
                IN.positionOS.x += sway;

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 finalColor = texColor * _BaseColor * IN.color;

                if (finalColor.a < 0.01)
                    discard;

                // 주변광: 매니저 활성 시 글로벌 색, 아니면 머티리얼 폴백(무채색)
                half3 ambient = lerp(_AmbientLight.xxx, _DayNightAmbientColor.rgb, saturate(_DayNightEnabled));

                // 태양광: 평면 스프라이트이므로 색·강도만 사용 (NdotL 없음)
                Light mainLight = GetMainLight();
                half3 sunLight = mainLight.color;

                // 주변광 + 태양광은 1.0에서 캡 → 매니저 없는 씬은 기존 언릿 룩과 동일
                half3 baseLight = min(ambient + sunLight, half3(1, 1, 1));

                // 포인트 라이트: 픽셀 그리드에 스냅한 위치로 감쇠 → 픽셀풍 경계
                float3 snappedPosWS = floor(IN.positionWS / _PixelGridSize) * _PixelGridSize;
                half3 pointLights = half3(0, 0, 0);

                float2 normalizedScreenUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                uint additionalLightCount = GetAdditionalLightsCount();

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalizedScreenSpaceUV = normalizedScreenUV;

                LIGHT_LOOP_BEGIN(additionalLightCount)
                    Light light = GetAdditionalLight(lightIndex, snappedPosWS);
                    pointLights += light.color * light.distanceAttenuation * light.shadowAttenuation;
                LIGHT_LOOP_END

                // 포인트 라이트만 광량 양자화 (태양광까지 양자화하면 시간 전환 시 화면 전체가 단계적으로 튐)
                pointLights = floor(pointLights * _LightSteps) / _LightSteps;

                finalColor.rgb *= baseLight + pointLights;

                // 자체 발광 (라이팅의 영향을 받지 않음)
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv).rgb * _EmissionColor.rgb;
                finalColor.rgb += emission;

                // 피격 플래시는 최종 색 위에 덮어씀 (밤에도 동일하게 보이도록)
                finalColor.rgb = lerp(finalColor.rgb, _FlashColor.rgb, _FlashAmount);

                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
