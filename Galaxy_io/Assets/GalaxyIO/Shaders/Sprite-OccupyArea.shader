// 점령전 위성의 점령 범위 원. 스프라이트 사각형 UV 로 원을 그림. 색은 SpriteRenderer.color(unity_SpriteColor).
// 안쪽은 옅게 채우고, 테두리는 _Glow 배만큼 밝혀 블룸(Bloom)이 걸리게 함.
// _RingWidth 는 반지름 대비 테두리 두께. 머티리얼(M_OccupyArea) 값을 그대로 씀. 반지름 35 에서 0.007 이 약 0.25 유닛.
Shader "GalaxyIO/Sprite-OccupyArea"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _RingWidth ("Ring Width (Radius Ratio)", Range(0.001, 0.2)) = 0.01
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.04
        _Glow ("Ring Glow (HDR Multiplier)", Float) = 3
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

            #pragma vertex AreaVertex
            #pragma fragment AreaFragment

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
                float _RingWidth;
                float _FillAlpha;
                float _Glow;
            CBUFFER_END

            Varyings AreaVertex(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                // SpriteRenderer.color 는 정점 색이 아니라 unity_SpriteColor 로 들어옴.
                o.color = input.color * unity_SpriteColor;
                return o;
            }

            half4 AreaFragment(Varyings input) : SV_Target
            {
                // 중심 0, 둘레 1. 화면 픽셀 하나만큼 가장자리를 부드럽게 끊음.
                float radius = length(input.uv * 2.0 - 1.0);
                float pixel = max(fwidth(radius), 1e-5);

                float inside = 1.0 - smoothstep(1.0 - pixel, 1.0, radius);
                float ring = smoothstep(1.0 - _RingWidth - pixel, 1.0 - _RingWidth, radius) * inside;

                half3 rgb = input.color.rgb * lerp(1.0, _Glow, ring);
                half alpha = lerp(_FillAlpha, 1.0, ring) * inside * input.color.a;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
