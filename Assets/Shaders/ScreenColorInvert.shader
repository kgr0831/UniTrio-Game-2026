// URP 14+ 풀스크린 색상 반전 셰이더 (Blit.hlsl 기반)
// ColorInvertRendererFeature에서 사용. _Intensity(0-1)로 원본↔반전 보간.
Shader "Hidden/ScreenColorInvert"
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
            Name "ColorInvert"
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target 2.0

            // URP Core를 먼저 포함해야 TEXTURE2D_X 등이 정의됨
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Intensity;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 col      = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                half4 inverted = half4(1.0h - col.rgb, col.a);
                return lerp(col, inverted, _Intensity);
            }
            ENDHLSL
        }
    }
}
