Shader "Custom/SpriteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        // 플래시 효과를 위한 전용 프로퍼티
        [HDR] _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0,1)) = 0
        
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
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
        Blend One OneMinusSrcAlpha // Premultiplied Alpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment CustomSpriteFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 _FlashColor;
            float _FlashAmount;

            fixed4 CustomSpriteFrag(v2f IN) : SV_Target
            {
                // 원본 스프라이트의 색상 (텍스처 * 틴트 * 페이드)
                fixed4 c = SampleSpriteTexture (IN.texcoord) * IN.color;
                
                // 빛남 효과(Flash) 블렌딩: 투명도(Alpha) 영역에는 흰색이 칠해지지 않게 곱해줍니다.
                c.rgb = lerp(c.rgb, _FlashColor.rgb * c.a, _FlashAmount);
                
                // UnitySprites.cginc 에서 Premultiply Alpha 처리를 한 후에 리턴
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
