// 스프라이트에서 색이 바뀌는 텍셀 경계만 외곽선 색으로 그리는 UI 셰이더. 나머지는 투명.
// 선 굵기는 화면 픽셀 기준이라 이미지 크기와 상관없이 일정함.
Shader "GalaxyIO/UI-Outline"
{
    Properties
    {
        // Image 가 채우는 값. 인스펙터에서는 숨김.
        [HideInInspector] _MainTex ("Sprite Texture", 2D) = "white" {}

        _OutlineColor ("Outline Color", Color) = (0.3, 0.95, 1, 1)

        // 1 에 가까울수록 작은 색 차이도 외곽선으로 봄.
        _Sensitivity ("Sensitivity", Range(0, 1)) = 0.5

        _Thickness ("Thickness (Pixel)", Float) = 1.5

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

            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // RectMask2D 가 채우는 값
            float4 _ClipRect;

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                half _Sensitivity;
                float _Thickness;
            CBUFFER_END

            Varyings OutlineVertex(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.color = input.color;
                o.positionUI = input.positionOS.xy;
                return o;
            }

            // 투명한 픽셀끼리는 색이 달라도 같게 보도록 알파를 곱해 둠.
            half4 SamplePremultiplied(float2 uv)
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                return half4(c.rgb * c.a, c.a);
            }

            half Difference(half4 a, half4 b)
            {
                half4 d = abs(a - b);
                return max(max(d.r, d.g), max(d.b, d.a));
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                // 화면 픽셀 하나만큼의 UV
                float2 offset = fwidth(input.uv) * _Thickness;

                half4 center = SamplePremultiplied(input.uv);

                half difference = max(
                    max(Difference(center, SamplePremultiplied(input.uv + float2(offset.x, 0.0))),
                        Difference(center, SamplePremultiplied(input.uv - float2(offset.x, 0.0)))),
                    max(Difference(center, SamplePremultiplied(input.uv + float2(0.0, offset.y))),
                        Difference(center, SamplePremultiplied(input.uv - float2(0.0, offset.y))))
                );

                half edge = step(1.0 - _Sensitivity, difference) * step(1e-3, difference);
                half4 color = _OutlineColor * input.color;
                color.a *= edge;

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
