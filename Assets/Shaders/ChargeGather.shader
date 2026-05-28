Shader "Custom/ChargeGather"
{
    Properties
    {
        _ChargeColor    ("Charge Color", Color)      = (0.85, 0.7, 0.25, 1)
        _ChargeProgress ("Charge Progress", Range(0, 1)) = 0
        _GlowStrength   ("Glow Strength", Float)     = 3.0
        _CoreSize       ("Core Glow Size", Float)    = 0.15
        _Burst          ("Burst Multiplier", Float)  = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha One   // Additive blending
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 posOS : POSITION;
                float2 uv    : TEXCOORD0;
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
                float2 uv    : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4  _ChargeColor;
                half   _ChargeProgress;
                half   _GlowStrength;
                half   _CoreSize;
                half   _Burst;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.posCS = TransformObjectToHClip(IN.posOS.xyz);
                o.uv = IN.uv;
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half2 centeredUV = (IN.uv - 0.5) * 2.0;
                half dist = length(centeredUV);
                half angle = atan2(centeredUV.y, centeredUV.x);

                half progress = _ChargeProgress;
                half time = _Time.y;

                // ── Energy Flow (유기적이고 비대칭적인 흐름) ──
                
                // 기존보다 속도 대폭 감소 (너무 빠르지 않게)
                half slowTime = time * 0.4;
                half fastTime = time * 1.5;

                // 각도에 불규칙한 왜곡을 주어 완벽한 대칭성 파괴
                half angleDistort = sin(angle * 2.0 + slowTime) * 0.6 + sin(angle * 5.0 - slowTime * 1.2) * 0.3;
                half distortedAngle = angle + angleDistort;

                // 불규칙한 에너지 줄기 (Streaks)
                half streaks = sin(distortedAngle * 7.0) * sin(distortedAngle * 13.0 + fastTime * 0.5);
                streaks = pow(max(0.0, streaks), 2.0); // 날카롭게 깎아냄

                // 안으로 빨려가는 파동 (Flow)
                half flow = sin(dist * 10.0 + angleDistort * 3.0 + fastTime * 2.0);
                flow = max(0.0, flow); // 음수 영역 제거

                // 메인 줄기
                half energyStreaks = streaks * flow;
                energyStreaks *= lerp(0.3, 1.8, progress); // 진행도에 따른 강도
                energyStreaks *= smoothstep(1.0, 0.1, dist); // 외곽은 부드럽게 사라짐

                // ── Core Glow (중심부 발광) ──
                // 중심부도 살짝 일렁이게 (유기적인 느낌)
                half coreRadius = _CoreSize + (progress * 0.15) + (sin(time * 10.0) * 0.02);
                half coreGlow = smoothstep(coreRadius * 4.0, 0.0, dist) * lerp(0.5, 2.0, progress);
                half hotCore = smoothstep(coreRadius * 0.6, 0.0, dist) * (1.0 + progress);

                // ── Burst (폭발 연출) ──
                // 폭발이 쿼드 경계(네모)에 닿지 않도록 최대 반경을 0.85로 제한
                // 실제 시각적 크기는 스크립트에서 Quad 자체의 스케일을 키워 구현함
                half burstRadius = _Burst * 0.85;
                half burstWidth = 0.1 + _Burst * 0.3;
                
                // 폭발 외곽선 왜곡 (속성에 걸맞는 불규칙한 형태)
                half burstDistort = sin(angle * 4.0 + fastTime) * 0.3 + sin(angle * 9.0) * 0.15;
                half burstEdge = abs(dist - (burstRadius + burstDistort * _Burst)) / burstWidth;
                burstEdge = pow(max(0.0, 1.0 - burstEdge), 2.0); // 폭발 링

                // 폭발 내부 잔여 에너지 (가루/노이즈가 터져나가는 느낌)
                half innerNoise = sin(dist * 15.0 - fastTime * 3.0) * sin(angle * 8.0 + fastTime);
                half burstInner = smoothstep(burstRadius, 0.0, dist) * max(0.0, innerNoise);

                // 폭발은 시간이 지날수록 페이드아웃
                half burstFade = 1.0 - pow(saturate(_Burst), 1.5);
                half burstIntensity = (burstEdge + burstInner) * burstFade * 3.0;

                // ── 렌더링 합성 ──
                
                // 색상 결정: 속성 색상을 유지하되 중심으로 갈수록 하얗게
                half3 baseColor = lerp(_ChargeColor.rgb + half3(0.4, 0.4, 0.4), _ChargeColor.rgb, progress);
                half3 color = baseColor * _GlowStrength * lerp(0.8, 1.5, progress);

                // 코어 발광 추가 (플레이어를 너무 가리지 않도록 2.5 -> 1.5로 감소)
                color = lerp(color, half3(1, 1, 1) * _GlowStrength * 1.5, saturate(hotCore));

                // 폭발 시: 속성 색상과 흰색이 섞여 강렬하게 타오르는 색상
                half3 burstColor = lerp(_ChargeColor.rgb * 2.0, half3(1, 1, 1), 0.4) * _GlowStrength * 3.0;
                color = lerp(color, burstColor, saturate(burstIntensity));

                // 알파(투명도) 처리
                half masterAlpha = _ChargeColor.a;
                
                // 모이는 에너지의 투명도를 낮춤 (기존 0.7~1.0 -> 0.4~0.75)
                half normalAlpha = saturate(energyStreaks + coreGlow) * lerp(0.4, 0.75, progress) * (1.0 - saturate(_Burst));
                
                // 최종 투명도는 모이는 에너지 + 폭발 에너지
                half finalAlpha = saturate(normalAlpha + burstIntensity) * masterAlpha;

                // 쿼드 경계선을 완벽한 원형으로 잘라내어 네모로 짤리는 현상 방지
                finalAlpha *= smoothstep(1.0, 0.8, dist);

                return half4(color, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
