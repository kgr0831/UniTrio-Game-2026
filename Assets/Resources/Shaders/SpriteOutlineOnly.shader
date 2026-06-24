// 내부는 완전 투명, 스프라이트 실루엣 가장자리에만 색(테두리)을 그리는 외곽선 전용 셰이더.
// 회피 저스트: 흑백 화면 위(오버레이 카메라)에서 적의 빨간 외곽선만 컬러로 표시하는 데 사용.
Shader "Custom/SpriteOutlineOnly"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _OutlineColor ("Outline Color", Color) = (1, 0.12, 0.12, 1)
        _OutlineWidth ("Outline Width (texels)", Range(0, 12)) = 3

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
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 _OutlineColor;
            float  _OutlineWidth;
            float4 _MainTex_TexelSize;

            fixed4 frag(v2f IN) : SV_Target
            {
                float a = SampleSpriteTexture(IN.texcoord).a;

                float dx = _MainTex_TexelSize.x * _OutlineWidth;
                float dy = _MainTex_TexelSize.y * _OutlineWidth;

                // 8방향 이웃의 알파 (실루엣 인접 여부)
                float aU  = SampleSpriteTexture(IN.texcoord + float2(0,  dy)).a;
                float aD  = SampleSpriteTexture(IN.texcoord + float2(0, -dy)).a;
                float aL  = SampleSpriteTexture(IN.texcoord + float2(-dx, 0)).a;
                float aR  = SampleSpriteTexture(IN.texcoord + float2( dx, 0)).a;
                float aUL = SampleSpriteTexture(IN.texcoord + float2(-dx,  dy)).a;
                float aUR = SampleSpriteTexture(IN.texcoord + float2( dx,  dy)).a;
                float aDL = SampleSpriteTexture(IN.texcoord + float2(-dx, -dy)).a;
                float aDR = SampleSpriteTexture(IN.texcoord + float2( dx, -dy)).a;
                float nb  = max(max(max(aU, aD), max(aL, aR)),
                                max(max(aUL, aUR), max(aDL, aDR)));

                // 현재 픽셀이 (거의) 투명이고 주변에 실루엣이 있으면 = 가장자리 링.
                // 내부(불투명)와 먼 바깥(주변도 투명)은 완전 투명 → 그레이된 적이 그대로 비친다.
                float edge = step(a, 0.1) * nb;

                fixed4 c = _OutlineColor;
                c.a    = saturate(edge) * _OutlineColor.a;
                c.rgb *= c.a; // Premultiplied
                return c;
            }
            ENDCG
        }
    }
}
