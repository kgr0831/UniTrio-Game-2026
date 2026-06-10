Shader "UniTrio/SpriteShadowBlur"
{
    // SpriteShadow 실루엣 전용 셰이더.
    // 9탭 박스 블러로 알파 가장자리를 부드럽게 만들어
    // 픽셀아트 실루엣이 너무 선명하게 보이는 것을 완화합니다.
    // 색은 버텍스 컬러(SpriteRenderer.color)를 그대로 사용합니다.
    Properties
    {
        [MainTexture] _BaseMap("Sprite Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _BlurSize("Blur Size (texels)", Range(0, 4)) = 1.5
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
            Name "SpriteShadowBlur"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _BlurSize;
            CBUFFER_END

            // 주의: TexelSize는 엔진이 드로우마다 채우는 값이라 CBUFFER에 넣으면
            // SRP Batcher 경로에서 쓰레기값이 됨 (블러가 깨져 그림자가 사라짐)
            float4 _BaseMap_TexelSize;

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
                half4  color      : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color * _BaseColor;
                return OUT;
            }

            half SampleAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 o = _BaseMap_TexelSize.xy * _BlurSize;

                // 9탭 박스 블러 (알파만)
                half a = SampleAlpha(IN.uv);
                a += SampleAlpha(IN.uv + float2(-o.x, -o.y));
                a += SampleAlpha(IN.uv + float2(   0, -o.y));
                a += SampleAlpha(IN.uv + float2( o.x, -o.y));
                a += SampleAlpha(IN.uv + float2(-o.x,    0));
                a += SampleAlpha(IN.uv + float2( o.x,    0));
                a += SampleAlpha(IN.uv + float2(-o.x,  o.y));
                a += SampleAlpha(IN.uv + float2(   0,  o.y));
                a += SampleAlpha(IN.uv + float2( o.x,  o.y));
                a /= 9.0h;

                half4 col;
                col.rgb = IN.color.rgb;
                col.a   = a * IN.color.a;

                if (col.a < 0.005)
                    discard;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
