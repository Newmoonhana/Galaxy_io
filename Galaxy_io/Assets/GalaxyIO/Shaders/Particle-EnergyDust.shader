// 에너지 가루 파티클 셰이더. 속도 방향으로 늘어난 선분을 그림. 길이 = MaxLength * Min(1, 속력 / MaxSpeed).
// 속도는 파티클마다 바뀌는 값이라 Velocity 스트림으로 받음. 굵기는 파티클 크기(Size.x).
// 파티클 렌더러 Render Mode: Billboard.
// Custom Vertex Streams: Position, Color, UV, Center (TEXCOORD0.zw|x), Velocity (TEXCOORD1.yzw), Size.x (TEXCOORD2.x)
// EmissionIntensity 를 색에 곱함. 1 을 넘으면 블룸이 생김.
Shader "GalaxyIO/Particle-EnergyDust"
{
    Properties
    {
        _Color ("Color", Color) = (0.91, 0.75, 0.94, 1)
        _MaxLength ("Max Length", Float) = 0.6
        _MaxSpeed ("Max Speed", Float) = 15
        _EmissionIntensity ("Emission Intensity", Float) = 1
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

            #pragma vertex DustVertex
            #pragma fragment DustFragment

            struct Attributes
            {
                half4  color : COLOR;
                // xy: UV, zw: Center.xy
                float4 uv0   : TEXCOORD0;
                // x: Center.z, yzw: Velocity
                float4 uv1   : TEXCOORD1;
                // x: Size.x
                float  uv2   : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _MaxLength;
                float _MaxSpeed;
                half _EmissionIntensity;
            CBUFFER_END

            Varyings DustVertex(Attributes input)
            {
                float3 center = float3(input.uv0.zw, input.uv1.x);
                float2 velocity = input.uv1.yz;
                float speed = length(velocity);

                // 속도 방향(along)과 그 수직(across). 멈춘 가루는 위쪽을 향한 점.
                float2 along = speed > 1e-4 ? velocity / speed : float2(0.0, 1.0);
                float2 across = float2(-along.y, along.x);

                float width = input.uv2;
                float len = max(_MaxLength * min(1.0, speed / max(_MaxSpeed, 1e-4)), width);

                // 빌보드 꼭짓점을 UV 로 구분해 선분 사각형으로 다시 놓음. 머리(UV y 1)가 진행 방향.
                float2 corner = input.uv0.xy - 0.5;
                float3 position = center + float3(along * (corner.y * len) + across * (corner.x * width), 0.0);

                Varyings o;
                o.positionCS = TransformObjectToHClip(position);
                o.color = input.color * _Color;
                return o;
            }

            half4 DustFragment(Varyings input) : SV_Target
            {
                return half4(input.color.rgb * _EmissionIntensity, input.color.a);
            }
            ENDHLSL
        }
    }
}
