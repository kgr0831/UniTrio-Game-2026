Shader "Custom/AdditiveSpriteVFX"
{
    // 검은 배경 + 흰 모양(파티클용 Default 텍스처)을 순수 가산으로 더한다.
    // 알파 채널에 의존하지 않으므로(Blend One One) Sprite 타입이 아닌 텍스처도 안전하게 발광 VFX로 사용 가능.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _Color ("Tint (HDR)", Color) = (1,1,1,1)

        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
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
        Blend One One // 순수 가산

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment fragAdd
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 fragAdd(v2f IN) : SV_Target
            {
                // IN.color = vertexColor * _Color(HDR) * _RendererColor(SpriteRenderer.color)
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                // RGB를 그대로 더하되, 페이드는 RendererColor의 알파(IN.color.a)로 제어
                return fixed4(c.rgb * IN.color.a, 0);
            }
            ENDCG
        }
    }
}
