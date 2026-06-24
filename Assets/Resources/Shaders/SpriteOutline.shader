Shader "Custom/SpriteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0, 10)) = 1
        
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
        Blend One OneMinusSrcAlpha 

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment outlineFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 _OutlineColor;
            float _OutlineWidth;
            float4 _MainTex_TexelSize; // Unity가 자동으로 텍스처 크기를 주입해줍니다.

            fixed4 outlineFrag(v2f IN) : SV_Target
            {
                // 원본 스프라이트 텍스처 픽셀 읽어오기
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;

                // 상하좌우를 탐색하기 위한 오프셋 (픽셀 크기 분의 1)
                float dx = _MainTex_TexelSize.x * _OutlineWidth;
                float dy = _MainTex_TexelSize.y * _OutlineWidth;

                // 주변 4방향의 알파(투명도) 값을 십자 꼴로 읽어옵니다.
                float aUp    = SampleSpriteTexture(IN.texcoord + float2(0, dy)).a;
                float aDown  = SampleSpriteTexture(IN.texcoord + float2(0, -dy)).a;
                float aLeft  = SampleSpriteTexture(IN.texcoord + float2(-dx, 0)).a;
                float aRight = SampleSpriteTexture(IN.texcoord + float2(dx, 0)).a;

                // 이 중 하나라도 값이 있다면 이곳은 이미지의 겉둘레(외곽선)가 될 가능성이 큽니다!
                float outlineAlpha = max(max(aUp, aDown), max(aLeft, aRight));
                
                // 만약 현재 픽셀이 거의 투명(알파 < 0.1)인데 주변에 색상이 있다면 이곳에 외곽선을 그립니다.
                if (c.a < 0.1 && outlineAlpha > 0.0)
                {
                    c = _OutlineColor;
                    c.a = outlineAlpha * _OutlineColor.a; // 가장자리가 부드럽게 스며들도록
                }

                // Unity API의 Premultiplied Alpha 규격을 맞춰서 리턴
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
