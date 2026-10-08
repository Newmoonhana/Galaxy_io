// 에너지 폭발 고리 파티클 셰이더. 가운데 구멍이 뚫린 원. 바깥 둘레와 구멍 둘레는 EdgeColor, 그 사이는 InnerColor.
// 구멍 크기(HoleScale)는 파티클마다 바뀌는 값이라 Custom Data(Custom1.x)로 받음.
// 파티클 렌더러 Custom Vertex Streams: Position, Color, UV, Custom1.x (TEXCOORD0.z)
Shader "GalaxyIO/Particle-EnergyRing"
{
    Properties
    {
        _InnerColor ("Inner Color", Color) = (0.91, 0.75, 0.94, 1)
        _EdgeColor ("Edge Color", Color) = (0.65, 0.29, 0.66, 1)
        _EdgeWidth ("Edge Width (Radius Ratio)", Range(0, 0.5)) = 0.04
        _Smoothness ("Smoothness (Radius Ratio)", Range(0.001, 0.2)) = 0.02
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex RingVertex
            #pragma fragment RingFragment

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4  color      : COLOR;
                // xy: UV, z: HoleScale (Custom1.x)
                float3 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float3 uv         : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _InnerColor;
                half4 _EdgeColor;
                float _EdgeWidth;
                float _Smoothness;
            CBUFFER_END

            Varyings RingVertex(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.color = input.color;
                o.uv = input.uv;
                return o;
            }

            half4 RingFragment(Varyings input) : SV_Target
            {
                // 중심 0, 바깥 둘레 1. 구멍 둘레는 HoleScale.
                // 바깥 둘레의 번짐까지 사각형 안에 들도록 고리를 번짐만큼 작게 그림.
                float radius = length(input.uv.xy * 2.0 - 1.0) * (1.0 + _Smoothness);
                float hole = saturate(input.uv.z);

                // 두 둘레 중 가까운 쪽까지의 거리로 테두리를 칠함.
                float toEdge = min(1.0 - radius, radius - hole);
                half inner = smoothstep(_EdgeWidth - _Smoothness, _EdgeWidth + _Smoothness, toEdge);
                half4 color = lerp(_EdgeColor, _InnerColor, inner);

                // 고리 밖과 구멍은 투명. 구멍이 둘레까지 커지면 사라짐.
                color.a *= smoothstep(-_Smoothness, _Smoothness, toEdge) * step(hole, 0.999);

                return color * input.color;
            }
            ENDHLSL
        }
    }
}
