// ═══════════════════════════════════════════════════════════════════════════
// BossDeathCharge.shader  —  사망 직전 "균열 + 발광 차오름" 효과
//
// 절차적 보로노이(셀룰러) 균열을 스프라이트 전체에 갈래지게 생성하고,
// _Progress(0→1)에 따라 균열이 점점 두꺼워지며 _GlowColor(푸른 에너지)가
// 균열 틈에서 강하게 새어나오고 맥동한다. _Progress=1 직전이 가장 밝다.
//
// 스프라이트가 시트(아틀라스)일 수 있으므로 _SpriteRect(xy=UV min, zw=UV size)로
// 균열 좌표를 0~1 로컬 UV로 정규화한다(코드에서 현재 프레임 기준 주입).
// ═══════════════════════════════════════════════════════════════════════════
Shader "Custom/BossDeathCharge"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Death Charge)]
        _Progress    ("Progress (0=intact 1=overheat)", Range(0,1)) = 0
        [HDR] _GlowColor ("Glow Color", Color) = (0.3, 0.7, 1.4, 1)
        _SpriteRect  ("Sprite UV Rect (xy=min zw=size)", Vector) = (0,0,1,1)
        _CrackScale  ("Crack Density", Range(2,16)) = 6
        _CrackThickness ("Crack Max Thickness", Range(0.02,0.5)) = 0.22
        _PulseSpeed  ("Pulse Speed", Range(0,40)) = 20
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline"    = "UniversalPipeline"
        }

        Cull    Off
        Lighting Off
        ZWrite  Off
        Blend One OneMinusSrcAlpha   // Premultiplied Alpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                float  _Progress;
                half4  _GlowColor;
                float4 _SpriteRect;
                float  _CrackScale;
                float  _CrackThickness;
                float  _PulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // 보로노이 셀 경계까지의 거리(=균열 중심에서 0). IQ의 셀룰러 노이즈 응용.
            float voronoiEdge(float2 uv)
            {
                float2 n = floor(uv);
                float2 f = frac(uv);

                // 1차: 가장 가까운 특징점
                float2 mr = 0; float2 mg = 0; float md = 8.0;
                for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    float2 g = float2(i, j);
                    float2 o = hash2(n + g);
                    float2 r = g + o - f;
                    float d = dot(r, r);
                    if (d < md) { md = d; mr = r; mg = g; }
                }

                // 2차: 인접 셀과의 경계(이등분선)까지 최소 거리 → 균열망
                md = 8.0;
                for (int j2 = -2; j2 <= 2; j2++)
                for (int i2 = -2; i2 <= 2; i2++)
                {
                    float2 g = mg + float2(i2, j2);
                    float2 o = hash2(n + g);
                    float2 r = g + o - f;
                    float2 diff = r - mr;
                    float dd = dot(diff, diff);
                    if (dd > 1e-4)
                        md = min(md, dot(0.5 * (mr + r), normalize(diff)));
                }
                return md;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv    = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                c *= input.color;
                clip(c.a - 0.01);

                // 시트 UV → 스프라이트 로컬 0~1 (균열 스케일을 스프라이트 전체에 균일 적용)
                float2 luv = (input.uv - _SpriteRect.xy) / max(_SpriteRect.zw, 1e-5);

                // 절차적 보로노이 균열 (스프라이트 전체에 갈래짐)
                float edge = voronoiEdge(luv * _CrackScale);
                // 두께: 진행에 따라 0 → 최대. 후반에 두꺼워짐
                float thickness = _CrackThickness * (0.15 + 0.85 * _Progress);
                float crack = 1.0 - smoothstep(0.0, thickness, edge);          // 균열 선 = 1
                float core  = 1.0 - smoothstep(0.0, thickness * 0.35, edge);   // 균열 중심 코어(더 강한 빛)

                float pulse = 0.75 + 0.25 * sin(_Time.y * _PulseSpeed);

                // 균열 틈에서 새어나오는 강한 빛 (코어는 흰빛에 가깝게 과포화)
                half3 emission =
                      _GlowColor.rgb * crack * (2.0 + 7.0 * _Progress) * pulse
                    + _GlowColor.rgb * core  * (3.0 + 9.0 * _Progress) * pulse
                    + half3(1,1,1)   * core  * _Progress * 2.0;            // 코어 화이트닝

                // 폭발 직전 스프라이트 전체가 푸르게 과열
                emission += _GlowColor.rgb * (_Progress * _Progress) * 2.0 * pulse;

                c.rgb += emission;
                c.rgb *= c.a; // Premultiplied Alpha
                return c;
            }
            ENDHLSL
        }
    }
}
