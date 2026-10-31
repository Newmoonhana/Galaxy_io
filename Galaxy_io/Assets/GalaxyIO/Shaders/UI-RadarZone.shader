// 레이더 원 안에서 전투 구역 밖을 빨갛게 칠하는 UI 셰이더. 경계선은 더 진하게 그림.
// 좌표는 레이더 셰이더와 같음. 이미지 중심이 원점, 가장자리가 1. RadarView 가 _Zone, _Radius 를 채움.
Shader "GalaxyIO/UI-RadarZone"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ZoneColor ("Zone Color", Color) = (1, 0.15, 0.15, 0.3)
        _EdgeColor ("Edge Color", Color) = (1, 0.25, 0.25, 0.9)
        _EdgeWidth ("Edge Width", Float) = 0.006

        // RadarView 가 채우는 값. 전투 구역 (최소 x, 최소 y, 최대 x, 최대 y)와 레이더 원 반지름.
        [HideInInspector] _Zone ("Zone", Vector) = (-10, -10, 10, 10)
        [HideInInspector] _Radius ("Radius", Float) = 0.9

        // UI 마스크용. 인스펙터에서는 숨김.
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex ZoneVertex
            #pragma fragment ZoneFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
                half4  _ZoneColor;
                half4  _EdgeColor;
                float  _EdgeWidth;
                float4 _Zone;
                float  _Radius;
            CBUFFER_END

            Varyings ZoneVertex(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.color = input.color;
                return o;
            }

            half4 ZoneFragment(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float aa = fwidth(p.x);

                float disc = 1.0 - smoothstep(_Radius - aa, _Radius + aa, length(p));

                // 구역 사각형까지의 거리. 바깥이 +.
                float2 gap = max(_Zone.xy - p, p - _Zone.zw);
                float outside = max(gap.x, gap.y);

                float zone = smoothstep(-aa, aa, outside);
                float edge = 1.0 - smoothstep(_EdgeWidth - aa, _EdgeWidth + aa, abs(outside));

                half4 color = lerp(_ZoneColor * zone, _EdgeColor, edge);
                color.a *= disc * input.color.a;
                color.rgb *= input.color.rgb;
                return color;
            }
            ENDHLSL
        }
    }
}
