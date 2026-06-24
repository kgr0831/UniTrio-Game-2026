// ═══════════════════════════════════════════════════════════════════════════
// AdditiveGlow.shader  —  가산 블렌딩 글로우(후광) 스프라이트
//
// 부드러운 방사형 그라데이션 텍스처를 가산(Blend One One)으로 그려, 보스 실루엣
// 바깥 주변까지 빛이 새어나오는 후광을 표현한다. _Color(HDR)로 세기/색을 제어.
// GolemEntity 사망 연출에서 스케일·_Color를 애니메이트해 "빛이 커지다 펑" 연출.
// ═══════════════════════════════════════════════════════════════════════════
Shader "Custom/AdditiveGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (0.3, 0.7, 1.4, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent"
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType"    = "Plane"
        }

        Cull     Off
        Lighting Off
        ZWrite   Off
        Blend    One One   // Additive

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   2.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                half3 rgb = _Color.rgb * a * _Color.a;
                return half4(rgb, 1); // 가산 블렌딩이므로 alpha는 무시(rgb만 더해짐)
            }
            ENDHLSL
        }
    }
}
