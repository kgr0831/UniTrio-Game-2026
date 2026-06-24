Shader "Custom/URP_Triplanar"
{
    Properties
    {
        [MainTexture] _MainTex ("Texture", 2D) = "white" {}
        [MainColor] _Color ("Tint Color", Color) = (1,1,1,1)
        _Tiling ("Texture Tiling (Scale)", Float) = 1.0
        _BlendSharpness ("Blend Sharpness", Range(1, 20)) = 5.0
        [Toggle] _UseWorldSpace ("Use World Space (체크 해제 시 이동해도 텍스처 고정)", Float) = 0
    }
    SubShader
    {
        Tags { 
            "RenderType"="Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue"="Geometry" 
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 positionOS   : TEXCOORD2;
                float3 normalOS     : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Tiling;
                float _BlendSharpness;
                float _UseWorldSpace;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalOS = IN.normalOS;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 오브젝트의 트랜스폼 스케일(크기)을 매트릭스에서 추출
                float3 objScale = float3(
                    length(GetObjectToWorldMatrix()[0].xyz),
                    length(GetObjectToWorldMatrix()[1].xyz),
                    length(GetObjectToWorldMatrix()[2].xyz)
                );

                // _UseWorldSpace 값(0 또는 1)에 따라 로컬 좌표계와 월드 좌표계 혼합
                // 로컬을 쓰면(0) 이동/회전 시 텍스처가 큐브에 고정되지만 크기를 키우면 타일링됨
                float3 triplanarPos = lerp(IN.positionOS * objScale, IN.positionWS, _UseWorldSpace);
                float3 triplanarNorm = lerp(normalize(IN.normalOS), normalize(IN.normalWS), _UseWorldSpace);

                triplanarPos *= _Tiling;
                
                float3 blendWeights = abs(triplanarNorm);
                blendWeights = pow(blendWeights, _BlendSharpness);
                blendWeights /= (blendWeights.x + blendWeights.y + blendWeights.z); 

                float2 uvX = triplanarPos.zy;
                float2 uvY = triplanarPos.xz;
                float2 uvZ = triplanarPos.xy;

                half4 colX = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvX);
                half4 colY = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvY);
                half4 colZ = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvZ);

                half4 finalColor = colX * blendWeights.x + colY * blendWeights.y + colZ * blendWeights.z;

                return finalColor * _Color;
            }
            ENDHLSL
        }
    }
}
