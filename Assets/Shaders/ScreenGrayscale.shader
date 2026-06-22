// URP 14+ 풀스크린 흑백(그레이스케일) 셰이더 (Blit.hlsl 기반)
// GrayscaleRendererFeature에서 사용. _Intensity(0-1)로 원본↔흑백 보간.
// 회피 저스트(위치타임) 슬로우모션 연출용.
Shader "Hidden/ScreenGrayscale"
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
            Name "Grayscale"
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma target 2.0

            // URP Core를 먼저 포함해야 TEXTURE2D_X 등이 정의됨
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float  _Intensity;
            float2 _Center;     // 흑백이 퍼져나가는 중심 (스크린 UV, 0~1). 보통 플레이어 위치.
            float  _Radius;     // 흑백 영역 반경 (UV 기준). 크게 잡으면 화면 전체.
            float  _Softness;   // 경계 부드러움
            float  _Aspect;     // 화면 종횡비(width/height) — 원형 보정용

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);

                // 중심에서의 거리(종횡비 보정 → 타원이 아닌 원)
                float2 d = input.texcoord - _Center;
                d.x *= _Aspect;
                float dist = length(d);

                // 반경 안쪽이면 1, 바깥이면 0 (플레이어 중앙에서 퍼져나가는 효과)
                float reveal = 1.0 - smoothstep(_Radius - _Softness, _Radius + _Softness, dist);
                half  amount = _Intensity * reveal;

                // 리니어 색공간 기준 휘도(Rec.709)로 채도 제거
                half  luma = dot(col.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half4 gray = half4(luma, luma, luma, col.a);

                return lerp(col, gray, amount);
            }
            ENDHLSL
        }
    }
}
