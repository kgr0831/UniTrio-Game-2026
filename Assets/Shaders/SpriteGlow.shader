Shader "Custom/SpriteGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Glow)]
        [NoScaleOffset] _EmissionTex ("Emission Texture (Secondary)", 2D) = "black" {}
        [HDR] _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
        _GlowIntensity ("Glow Intensity", Float) = 1.0
        
        // 셰이더 베리언트를 더 확실한 방식으로 변경 (shader_feature -> multi_compile)
        [Toggle(_USE_MAIN_ALPHA_AS_GLOW)] _UseMainAlphaAsGlow ("Use Whole Sprite Glow", Float) = 0

        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            
            // 더 안전한 multi_compile 방식을 사용하여 모든 상황에서 기능을 보장합니다.
            #pragma multi_compile_local _ _USE_MAIN_ALPHA_AS_GLOW
            
            #include "UnitySprites.cginc"

            // SRP Batcher 호환성 유지
            CBUFFER_START(UnityPerMaterial)
                fixed4 _GlowColor;
                float _GlowIntensity;
            CBUFFER_END

            sampler2D _EmissionTex;

            struct appdata_glow
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f_glow
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f_glow vert(appdata_glow IN)
            {
                v2f_glow OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap (OUT.vertex);
                #endif

                return OUT;
            }

            fixed4 frag(v2f_glow IN) : SV_Target
            {
                // 1. 기본 색상 샘플링 (Base)
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                
                // 2. 글로우 마스크 계산
                float mask = 0;

                #if defined(_USE_MAIN_ALPHA_AS_GLOW)
                    // 전체 발광 키워드가 있을 때
                    mask = c.a;
                #else
                    // 특정 마스크 텍스처를 사용할 때
                    fixed4 e = tex2D(_EmissionTex, IN.texcoord);
                    mask = e.r * e.a;
                #endif
                
                // 3. 글로우 연산 (더 직관적인 가산 혼합)
                float3 glow = _GlowColor.rgb * _GlowIntensity * mask;
                
                // 4. 알파 프리멀티플라이 (배경 투명도 적용)
                c.rgb *= c.a;
                
                // 5. 글로우 더하기
                c.rgb += glow;
                
                // 6. 투명도 보정 (글로우가 있다면 그 부분은 보이게 함)
                c.a = saturate(c.a + (mask * _GlowIntensity * 0.1));

                return c;
            }
            ENDCG
        }
    }
}
