Shader "Custom/DebuffIconWipe"
{
    Properties
    {
        [PerRendererData] _MainTex ("Icon Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Progress ("Wipe Progress (0=full, 1=gone)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "DebuffIconWipeForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                half   _Progress;
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
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                texColor *= IN.color;

                if (texColor.a < 0.01)
                    discard;

                // 시계방향 radial wipe:
                // UV 중심(0.5, 0.5)에서 극좌표 각도 계산
                // 12시(상단) 시작, 시계방향으로 진행
                float2 centeredUV = IN.uv - float2(0.5, 0.5);
                
                // atan2: 12시 방향 = 상단(y+) 시작
                // 시계방향: 12시→3시→6시→9시→12시
                float angle = atan2(-centeredUV.x, centeredUV.y); // -x, +y로 12시 시작 시계방향
                
                // angle: -PI ~ PI → 0 ~ 1 정규화
                float normalizedAngle = (angle + 3.14159265) / (2.0 * 3.14159265);

                // Progress에 따라 알파를 점진적으로 감소
                // Progress=0: 전부 표시, Progress=1: 전부 투명
                float wipeAlpha = step(normalizedAngle, 1.0 - _Progress);

                // 경계에 부드러운 전환 (안티앨리어싱)
                float edgeSoftness = 0.02;
                float threshold = 1.0 - _Progress;
                wipeAlpha = smoothstep(threshold - edgeSoftness, threshold + edgeSoftness, normalizedAngle);
                wipeAlpha = 1.0 - wipeAlpha;

                // 사라지는 영역은 반투명 처리 (완전히 투명이 아닌 30% 잔상)
                float finalAlpha = lerp(texColor.a * 0.25, texColor.a, wipeAlpha);

                return half4(texColor.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
