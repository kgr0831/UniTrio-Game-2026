Shader "Custom/SpriteLit2D"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.0
        _EmissionMap ("Emission Map", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,0)
        _AmbientLight ("Ambient Light", Range(0, 1)) = 0.15
        _LightSteps ("Light Steps", Range(2, 16)) = 6
        _PixelGridSize ("Pixel Grid Size (1/PPU)", Float) = 0.03125
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
            Name "SpriteLit2DForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _FORWARD_PLUS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _NormalStrength;
                half4 _EmissionColor;
                half _AmbientLight;
                half _LightSteps;
                float _PixelGridSize;
            CBUFFER_END

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);   SAMPLER(sampler_NormalMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 tangentWS   : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                half4  color       : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;

                float3 worldNormal = TransformObjectToWorldNormal(float3(0, 0, -1));
                float3 worldTangent = TransformObjectToWorldDir(float3(1, 0, 0));
                float3 worldBitangent = cross(worldNormal, worldTangent);

                OUT.normalWS = worldNormal;
                OUT.tangentWS = worldTangent;
                OUT.bitangentWS = worldBitangent;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 finalColor = texColor * _Color * IN.color;

                if (finalColor.a < 0.01)
                    discard;

                // Normal map
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv));
                normalTS.xy *= _NormalStrength;
                normalTS = normalize(normalTS);

                float3x3 TBN = float3x3(
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    normalize(IN.normalWS)
                );
                float3 normalWS = normalize(mul(normalTS, TBN));

                // Snap world position to pixel grid for pixelated light boundaries
                float3 snappedPosWS = floor(IN.positionWS / _PixelGridSize) * _PixelGridSize;

                // Lighting
                half3 lighting = _AmbientLight;

                // Main directional light
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                lighting += mainLight.color * NdotL * mainLight.distanceAttenuation;

                // Additional lights - use snapped position for pixelated attenuation
                float2 normalizedScreenUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                uint additionalLightCount = GetAdditionalLightsCount();

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalizedScreenSpaceUV = normalizedScreenUV;

                LIGHT_LOOP_BEGIN(additionalLightCount)
                    Light light = GetAdditionalLight(lightIndex, snappedPosWS);
                    half NdotL_add = saturate(dot(normalWS, light.direction));
                    lighting += light.color * NdotL_add * light.distanceAttenuation * light.shadowAttenuation;
                LIGHT_LOOP_END

                // Quantize brightness to steps
                lighting = floor(lighting * _LightSteps) / _LightSteps;
                finalColor.rgb *= lighting;

                // Emission
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv).rgb * _EmissionColor.rgb;
                finalColor.rgb += emission;

                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}