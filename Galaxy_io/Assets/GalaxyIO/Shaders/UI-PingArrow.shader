// 위를 향한 삼각형 화살표(또는 원)와 그 주변의 은은한 빛번짐을 그리는 UI 셰이더. 스프라이트를 쓰지 않음.
// 이미지 사각형 가장자리의 _Padding 만큼은 빛번짐 자리이고, 삼각형은 그 안쪽을 채움.
// 색은 Image 의 Color 를 따름.
Shader "GalaxyIO/UI-PingArrow"
{
    Properties
    {
        // Image 가 채우는 값. 인스펙터에서는 숨김.
        [HideInInspector] _MainTex ("Sprite Texture", 2D) = "white" {}

        // 켜면 삼각형 대신 원을 그림.
        [Toggle(_CIRCLE)] _Circle ("Circle", Float) = 0

        // 높이 대비 삼각형 밑변 비율. 작을수록 뾰족해져 방향이 잘 보임.
        _BaseWidth ("Base Width", Range(0.2, 1)) = 0.55

        // 원 / 삼각형의 불투명도
        _Opacity ("Opacity", Range(0, 1)) = 0.5

        // 사각형 한 변 대비 빛번짐 자리 비율. PingMarkerView 가 이 값으로 화살표 끝을 피벗에 맞춤.
        _Padding ("Glow Padding", Range(0, 0.45)) = 0.25

        // 빛번짐이 줄어드는 거리. 삼각형 크기 기준.
        _GlowWidth ("Glow Width", Range(0.01, 1)) = 0.12

        _GlowStrength ("Glow Strength", Range(0, 1)) = 0.35

        // Mask 컴포넌트용. 인스펙터에서는 숨김.
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        ColorMask [_ColorMask]

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex ArrowVertex
            #pragma fragment ArrowFragment
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma shader_feature_local _CIRCLE

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
                float2 positionUI : TEXCOORD1;
            };

            // RectMask2D 가 채우는 값
            float4 _ClipRect;

            CBUFFER_START(UnityPerMaterial)
                float _BaseWidth;
                half _Opacity;
                float _Padding;
                float _GlowWidth;
                half _GlowStrength;
            CBUFFER_END

            Varyings ArrowVertex(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.color = input.color;
                o.positionUI = input.positionOS.xy;
                return o;
            }

            half4 ArrowFragment(Varyings input) : SV_Target
            {
                // 삼각형 영역 기준 좌표. (0, 0) 좌하단, (1, 1) 우상단. 꼭짓점은 (0.5, 1).
                float2 p = (input.uv - _Padding) / (1.0 - 2.0 * _Padding);

                #ifdef _CIRCLE
                // 영역에 꽉 찬 원까지의 부호 있는 거리. 안쪽이 음수.
                float distance = length(p - 0.5) - 0.5;
                #else
                // 꼭짓점 기준, 아래쪽이 +y 인 좌표로 바꿔 이등변삼각형까지의 부호 있는 거리를 구함. 안쪽이 음수.
                float2 q = float2(_BaseWidth * 0.5, 1.0);
                float2 apex = float2(abs(p.x - 0.5), 1.0 - p.y);
                float2 a = apex - q * saturate(dot(apex, q) / dot(q, q));
                float2 b = apex - q * float2(saturate(apex.x / q.x), 1.0);
                float2 d = min(float2(dot(a, a), apex.y * q.x - apex.x * q.y), float2(dot(b, b), q.y - apex.y));
                float distance = sqrt(d.x) * sign(-d.y);
                #endif

                float aa = fwidth(distance);
                half fill = smoothstep(aa, -aa, distance);
                half glow = exp(-max(distance, 0.0) / _GlowWidth) * _GlowStrength;

                half4 color = input.color;
                color.a *= max(fill * _Opacity, glow);

                #ifdef UNITY_UI_CLIP_RECT
                float2 inside = step(_ClipRect.xy, input.positionUI) * step(input.positionUI, _ClipRect.zw);
                color.a *= inside.x * inside.y;
                #endif

                return color;
            }
            ENDHLSL
        }
    }
}
