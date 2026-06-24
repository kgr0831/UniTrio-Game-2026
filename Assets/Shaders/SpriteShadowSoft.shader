Shader "UniTrio/SpriteShadowSoft"
{
    // SpriteShadow 전용 '투영형' 그림자 셰이더.
    //  - 버텍스 시어: 발 라인을 축으로 스프라이트를 바닥에 눕히고 광원 방향으로 기울임
    //    (회전 방식과 달리 발이 항상 바닥에 붙어 있고 그림자가 위로 향하지 않음)
    //  - 9탭 블러로 가장자리를 부드럽게
    //  - 발밑은 진하고 그림자 끝으로 갈수록 옅어지는 페이드
    // 셰이프 파라미터는 MaterialPropertyBlock으로 렌더러마다 주입됩니다.
    Properties
    {
        [MainTexture] _BaseMap("Sprite Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Shadow Color", Color) = (0,0,0,1)
        _BlurSize("Blur Size (texels)", Range(0, 4)) = 1.5
        _TipFade("Tip Fade", Range(0, 1)) = 0.45
        // x=시어(가로 기울기), y=스쿼시(세로 비율), z=발 로컬Y, w=미사용
        _ShadowParams("Shadow Params", Vector) = (0, 0.5, 0, 0)
        // 아틀라스 내 스프라이트 UV 영역 (xy=min, zw=size) — 끝 페이드 정규화용
        _UVRect("UV Rect", Vector) = (0, 0, 1, 1)
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
            Name "SpriteShadowSoft"

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
                half   _TipFade;
                float4 _ShadowParams;
                float4 _UVRect;
            CBUFFER_END

            // 주의: TexelSize는 엔진이 드로우마다 채우는 값이라 CBUFFER에 넣으면
            // SRP Batcher 경로에서 쓰레기값이 됨
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

                float shear  = _ShadowParams.x;
                float squash = _ShadowParams.y;
                float feetY  = _ShadowParams.z;

                // 발 라인 기준으로 바닥에 눕히기 (머리가 그림자 끝이 됨)
                float3 pos = IN.positionOS.xyz;
                float rel  = max(pos.y - feetY, 0.0);  // 발 위쪽 높이
                float drop = squash * rel;             // 그림자 진행 거리

                pos.y = feetY - drop;                  // 발 아래로 투영
                pos.x += shear * drop;                 // 광원 방향 시어

                OUT.positionCS = TransformObjectToHClip(pos);
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

                // 9탭 박스 블러 (알파만) → 부드러운 가장자리
                half a = SampleAlpha(IN.uv);
                a += SampleAlpha(IN.uv + float2(-o.x, -o.y));
                a += SampleAlpha(IN.uv + float2(   0, -o.y));
                a += SampleAlpha(IN.uv + float2( o.x, -o.y));
                a += SampleAlpha(IN.uv + float2(-o.x,    0));
                a += SampleAlpha(IN.uv + float2( o.x,    0));
                a += SampleAlpha(IN.uv + float2(-o.x,  o.y));
                a += SampleAlpha(IN.uv + float2(   0,  o.y));
                a += SampleAlpha(IN.uv + float2( o.x,  o.y));
                a /= 9.0;

                // 끝(머리 쪽 = 스프라이트 위쪽 UV)으로 갈수록 옅어짐
                float vNorm = saturate((IN.uv.y - _UVRect.y) / max(_UVRect.w, 1e-4));
                a *= 1.0 - _TipFade * vNorm;

                half4 col;
                col.rgb = IN.color.rgb;
                col.a   = a * IN.color.a;

                if (col.a < 0.004)
                    discard;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
