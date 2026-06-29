Shader "Custom/SpriteSilhouette"
{
    // 스프라이트의 알파 채널 모양만 사용해 단색으로 채우는 실루엣 셰이더.
    // _Color 머티리얼 프로퍼티로 색상을 지정합니다 (버텍스 컬러 미사용).
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color   ("Color",         Color) = (1, 0.08, 0.08, 1)
    }
    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull   Off
        ZWrite Off
        ZTest  LEqual

        Pass
        {
            Name "SpriteSilhouette"

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 텍스처 알파만 사용해 스프라이트 형태를 만들고,
                // RGB는 _Color 프로퍼티로 완전히 대체합니다.
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).a;
                return half4(_Color.rgb, alpha * _Color.a);
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
