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
        _OutlineWidth ("Outline Width (pixels)", Float) = 2.0
        
        [Toggle(_USE_MAIN_ALPHA_AS_GLOW)] _UseMainAlphaAsGlow ("Use Whole Sprite Glow", Float) = 0
        [Toggle(_USE_OUTLINE_GLOW)] _UseOutlineGlow ("Use Outline Glow Only", Float) = 0

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
            
            #pragma multi_compile_local _ _USE_MAIN_ALPHA_AS_GLOW
            #pragma multi_compile_local _ _USE_OUTLINE_GLOW
            
            #include "UnitySprites.cginc"

            CBUFFER_START(UnityPerMaterial)
                fixed4 _GlowColor;
                float _GlowIntensity;
                float _OutlineWidth;
            CBUFFER_END

            sampler2D _EmissionTex;
            float4 _MainTex_TexelSize; // Unity가 자동 제공 (1/width, 1/height, width, height)

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

                #if defined(_USE_OUTLINE_GLOW)
                    // 외곽선 전용 모드: 주변 픽셀 알파를 샘플링하여 가장자리만 감지
                    float2 texelSize = _MainTex_TexelSize.xy * _OutlineWidth;
                    float centerAlpha = SampleSpriteTexture(IN.texcoord).a;
                    
                    // 주변 8방향 샘플링
                    float surroundAlpha = 0;
                    surroundAlpha += SampleSpriteTexture(IN.texcoord + float2( texelSize.x, 0)).a;
                    surroundAlpha += SampleSpriteTexture(IN.texcoord + float2(-texelSize.x, 0)).a;
                    surroundAlpha += SampleSpriteTexture(IN.texcoord + float2(0,  texelSize.y)).a;
                    surroundAlpha += SampleSpriteTexture(IN.texcoord + float2(0, -texelSize.y)).a;
                    surroundAlpha += SampleSpriteTexture(IN.texcoord + float2( texelSize.x,  texelSize.y)).a;
                    surroundAlpha += SampleSpriteTexture(IN.texcoord + float2(-texelSize.x,  texelSize.y)).a;
                    surroundAlpha += SampleSpriteTexture(IN.texcoord + float2( texelSize.x, -texelSize.y)).a;
                    surroundAlpha += SampleSpriteTexture(IN.texcoord + float2(-texelSize.x, -texelSize.y)).a;
                    surroundAlpha /= 8.0;
                    
                    // 가장자리 = 주변에 투명 픽셀이 있는 불투명 영역, 또는 주변에 불투명 픽셀이 있는 투명 영역
                    float edge = abs(centerAlpha - surroundAlpha);
                    // 내부(centerAlpha 높고 surroundAlpha도 높음)는 edge가 0에 가까움
                    // 외곽선(centerAlpha와 surroundAlpha 차이 큼)만 mask가 높아짐
                    mask = saturate(edge * 4.0);
                    
                #elif defined(_USE_MAIN_ALPHA_AS_GLOW)
                    // 전체 발광 모드
                    mask = c.a;
                #else
                    // 특정 마스크 텍스처를 사용할 때
                    fixed4 e = tex2D(_EmissionTex, IN.texcoord);
                    mask = e.r * e.a;
                #endif

                // 렌더러 알파가 0이면 글로우도 사라지도록 (공격 후 투명화 대응)
                mask *= IN.color.a;
                
                // 3. 글로우 연산
                float3 glow = _GlowColor.rgb * _GlowIntensity * mask;
                
                // 4. 알파 프리멀티플라이
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
