// 투명 픽셀 중 상하좌우에 불투명 픽셀이 있으면 외곽선 색으로 칠하는 스프라이트 셰이더.
Shader "GalaxyIO/Sprite-Outline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1, 0, 0, 1)
        _OutlineWidth ("Outline Width (Texel)", Float) = 1

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

            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings OutlineVertex(Attributes input)
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

            half SampleAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                if (color.a > 0.01)
                {
                    return color;
                }

                // 스프라이트 텍스처는 렌더러가 넣으므로 크기를 직접 읽음.
                float width, height;
                _MainTex.GetDimensions(width, height);

                float2 offset = float2(1.0 / width, 1.0 / height) * _OutlineWidth;

                half neighbor = max(
                    max(SampleAlpha(input.uv + float2(offset.x, 0.0)), SampleAlpha(input.uv - float2(offset.x, 0.0))),
                    max(SampleAlpha(input.uv + float2(0.0, offset.y)), SampleAlpha(input.uv - float2(0.0, offset.y)))
                );

                // 스프라이트가 투명해지면 외곽선도 같이 사라짐.
                return neighbor > 0.01
                    ? half4(_OutlineColor.rgb, _OutlineColor.a * input.color.a)
                    : half4(0.0, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }
    }
}