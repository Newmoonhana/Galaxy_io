// 광선의 번짐 셰이더. 총구 쪽 구, 몸통, 끝 쪽 구를 합친 하나의 모양 주변을 Color 로 번지게 칠함.
// 세 모양의 거리 중 가장 가까운 것만 쓰므로 번짐끼리 겹쳐 두 번 밝아지지 않음.
// 정사각형 스프라이트를 Sliced 로 늘려 씀. 모양 값은 광선마다 다르므로 렌더러별 값(MaterialPropertyBlock)으로 받음.
// EmissionIntensity 를 색에 곱함. 1 을 넘으면 블룸이 생김.
Shader "GalaxyIO/Sprite-BeamGlow"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _GlowColor ("Color", Color) = (0.7, 0.3, 0.85, 0.6)
        _BlurScale ("Blur Scale", Float) = 0.4
        _EmissionIntensity ("Emission Intensity", Float) = 1

        // 광선 시스템이 렌더러마다 채우는 값. 인스펙터에서는 숨김.
        [HideInInspector] _Size ("Quad Size", Vector) = (1, 1, 0, 0)
        [HideInInspector] _Length ("Length", Float) = 0
        [HideInInspector] _StartRadius ("Start Radius", Float) = 0
        [HideInInspector] _EndRadius ("End Radius", Float) = 0
        [HideInInspector] _HalfWidth ("Half Width", Float) = 0

        // SpriteRenderer 가 채우는 값. 인스펙터에서는 숨김.
        [HideInInspector] _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
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
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex GlowVertex
            #pragma fragment GlowFragment

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _GlowColor;
                float _BlurScale;
                float2 _Size;
                float _Length;
                float _StartRadius;
                float _EndRadius;
                float _HalfWidth;
                half _EmissionIntensity;
            CBUFFER_END

            Varyings GlowVertex(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.color = input.color * _Color;
                return o;
            }

            half4 GlowFragment(Varyings input) : SV_Target
            {
                // 사각형 중심 기준 좌표(unit). 총구 쪽 구는 -y, 끝 쪽 구는 +y.
                float2 p = (input.uv - 0.5) * _Size;
                float halfLength = _Length * 0.5;

                float toStart = length(p - float2(0.0, -halfLength)) - _StartRadius;
                float toEnd = length(p - float2(0.0, halfLength)) - _EndRadius;

                float2 q = abs(p) - float2(_HalfWidth, halfLength);
                float toBody = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);

                // 합친 모양까지의 거리. 안쪽은 0 이하.
                float distance = min(min(toStart, toEnd), toBody);
                half glow = 1.0 - saturate(distance / max(_BlurScale, 1e-4));

                return half4(_GlowColor.rgb * _EmissionIntensity, _GlowColor.a * glow * glow) * input.color;
            }
            ENDHLSL
        }
    }
}
