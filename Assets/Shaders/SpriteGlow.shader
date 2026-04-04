Shader "Custom/SpriteGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Glow)]
        [NoScaleOffset] _EmissionTex ("Emission Texture (Secondary)", 2D) = "black" {}
        [HDR] _GlowColor ("Glow Color", Color) = (1, 1, 1, 1) // Default White
        _GlowIntensity ("Glow Intensity", Float) = 1.0
        [MaterialToggle] _UseMainAlphaAsGlow ("Use Whole Sprite Glow", Float) = 0

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
            #include "UnitySprites.cginc"

            sampler2D _EmissionTex;
            fixed4 _GlowColor;
            float _GlowIntensity;
            float _UseMainAlphaAsGlow;

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
                // 1. 기본 색상 샘플링 (Base Color from Main Texture)
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                
                // 2. 글로우 마스크 설정
                float mask = 0;
                if (_UseMainAlphaAsGlow > 0.5)
                {
                    // [전체 발광] 토글이 켜져 있으면 스프라이트 전체(알파 채널 기준)가 마스크가 됨
                    mask = c.a;
                }
                else
                {
                    // [부분 발광] 토글이 꺼져 있으면 Emission 텍스처(R 채널)를 마스크로 사용
                    fixed4 e = tex2D(_EmissionTex, IN.texcoord);
                    mask = e.r * e.a;
                }
                
                // 3. 글로우 강도 계산
                float3 glow = _GlowColor.rgb * _GlowIntensity * mask;
                
                // 4. 알파 프리멀티플라이 (메인 스프라이트 투명도 적용)
                c.rgb *= c.a;
                
                // 5. 글로우 더하기
                c.rgb += glow;
                
                // 6. 투명도 보정 (글로우 강도가 높을수록 더 불투명하게 보임)
                c.a = saturate(c.a + (mask * _GlowIntensity * 0.2));

                return c;
            }
            ENDCG
        }
    }
}
