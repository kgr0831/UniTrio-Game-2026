// URP 17+ 전용 화면 전체 붉은 잔상(Radial Blur Afterimage) 셰이더.
// RedScreenAfterimageRendererFeature에서 사용. _Intensity(0-1)로 블러 강도와 붉은 틴트 보간.
Shader "Hidden/RedScreenAfterimage"
{
    Properties {}
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "RedAfterimage"
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Intensity;
            
            // 샘플링 개수 (밸런스 중시)
            #define SAMPLE_COUNT 8

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                float2 uv = input.texcoord;
                float time = _Time.y;
                
                // [Natural Wavy Distortion] 다중 파형 중첩하여 유기적인 일렁임 구현
                float distortionStrength = _Intensity * 0.04;
                
                // 가로/세로 복합 왜곡 (속도와 주파수가 다른 파형들을 섞어 인위적인 느낌 제거)
                float2 wavyOffset;
                wavyOffset.x = sin(uv.y * 10.0 + time * 6.0) * 0.6 + 
                               sin(uv.y * 22.0 + time * 14.0) * 0.4;
                wavyOffset.y = cos(uv.x * 11.0 + time * 7.0) * 0.6 + 
                               cos(uv.x * 25.0 + time * 15.0) * 0.4;
                
                float2 distortedUV = uv + wavyOffset * distortionStrength;
                
                // [RGB Split / Chromatic Aberration] 왜곡된 영역의 색상 분리 (자연스러운 잔상 느낌)
                float splitStrength = _Intensity * 0.012;
                float2 redUV   = distortedUV + float2(splitStrength, 0);
                float2 blueUV  = distortedUV - float2(splitStrength, 0);
                
                half r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, redUV).r;
                half g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distortedUV).g;
                half b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, blueUV).b;
                half a = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distortedUV).a;
                
                half4 blurredCol = half4(r, g, b, a);
                
                // [Radial Blur Overlay] 기존 방사형 잔상 로직을 왜곡된 UV 위에서 추가 적용
                float2 center = float2(0.5, 0.5);
                float2 dir = center - distortedUV;
                float radialStrength = _Intensity * 0.05;
                
                half4 blurAccum = blurredCol;
                for(int i = 1; i < SAMPLE_COUNT; i++)
                {
                    float2 offsetUV = distortedUV + dir * (float(i) / (SAMPLE_COUNT - 1.0)) * radialStrength;
                    blurAccum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, offsetUV);
                }
                blurAccum /= SAMPLE_COUNT;

                // [Visual Polish] 붉은 기운 보강 및 원본과 합성
                half3 redGlow = half3(0.5, 0.0, 0.0);
                half3 finalRGB = blurAccum.rgb + (redGlow * _Intensity);
                
                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                return lerp(original, half4(finalRGB, original.a), _Intensity);
            }
            ENDHLSL
        }
    }
}
