// 에너지 구 스프라이트 셰이더. 정사각형 스프라이트 안에 원을 그림. 안쪽은 InnerColor, 테두리는 EdgeColor.
// 크기는 Transform 크기로 바꿈. 색과 테두리 두께는 머티리얼 설정값.
Shader "GalaxyIO/Sprite-EnergyBall"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _InnerColor ("Inner Color", Color) = (0.94, 0.78, 0.98, 1)
        _EdgeColor ("Edge Color", Color) = (0.65, 0.29, 0.66, 1)
        _EdgeWidth ("Edge Width (Radius Ratio)", Range(0, 1)) = 0.3
        _Smoothness ("Smoothness (Radius Ratio)", Range(0.001, 0.2)) = 0.03

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

            #pragma vertex BallVertex
            #pragma fragment BallFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _InnerColor;
                half4 _EdgeColor;
                float _EdgeWidth;
                float _Smoothness;
            CBUFFER_END

            Varyings BallVertex(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 BallFragment(Varyings input) : SV_Target
            {
                // 중심 0, 원 둘레 1.
                float radius = length(input.uv * 2.0 - 1.0);

                half inner = smoothstep(1.0 - _EdgeWidth + _Smoothness, 1.0 - _EdgeWidth - _Smoothness, radius);
                half4 color = lerp(_EdgeColor, _InnerColor, inner);
                color.a *= smoothstep(1.0, 1.0 - _Smoothness * 2.0, radius);

                return color * input.color;
            }
            ENDHLSL
        }
    }
}
