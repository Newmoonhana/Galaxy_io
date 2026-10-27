// 스프라이트와 그 외곽선을 그리는 셰이더. 투명 픽셀 중 상하좌우에 불투명 픽셀이 있으면 외곽선 색으로 칠함.
// 같은 머티리얼을 쓴 스프라이트(선체, 포탑)가 그린 자리에는 외곽선을 그리지 않아 합친 모습의 바깥에만 외곽선이 남음.
// 발광은 Sprite-Emissive 와 같음. 보조 텍스처 _EmissionMap 이 칠해진 곳을 원래 색 * EmissionIntensity 로 칠함.
Shader "GalaxyIO/Sprite-Outline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1, 0, 0, 1)
        _OutlineWidth ("Outline Width (Texel)", Float) = 1
        _EmissionMap ("Emission Mask", 2D) = "black" {}
        _EmissionIntensity ("Emission Intensity", Float) = 1

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

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

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

        TEXTURE2D(_EmissionMap);
        SAMPLER(sampler_EmissionMap);

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _OutlineColor;
            float _OutlineWidth;
            half _EmissionIntensity;
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
        ENDHLSL

        // 스프라이트. 그린 픽셀에 스텐실 비트(128)를 남김.
        Pass
        {
            Stencil
            {
                Ref 128
                WriteMask 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment SpriteFragment
            #pragma multi_compile_instancing

            half4 SpriteFragment(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                clip(color.a - 0.01);

                half4 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv);
                color.rgb *= lerp(1.0, _EmissionIntensity, Max3(emission.r, emission.g, emission.b) * emission.a);
                return color;
            }
            ENDHLSL
        }

        // 외곽선. 스텐실 비트가 없는 자리에만 그림.
        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            Stencil
            {
                Ref 128
                ReadMask 128
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma multi_compile_instancing

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;

                if (color.a > 0.01)
                {
                    discard;
                }

                // 스프라이트 텍스처는 렌더러가 넣으므로 크기를 직접 읽음.
                float width, height;
                _MainTex.GetDimensions(width, height);

                float2 offset = float2(1.0 / width, 1.0 / height) * _OutlineWidth;

                half neighbor = max(
                    max(SampleAlpha(input.uv + float2(offset.x, 0.0)), SampleAlpha(input.uv - float2(offset.x, 0.0))),
                    max(SampleAlpha(input.uv + float2(0.0, offset.y)), SampleAlpha(input.uv - float2(0.0, offset.y)))
                );

                if (neighbor <= 0.01)
                {
                    discard;
                }

                // 스프라이트가 투명해지면 외곽선도 같이 사라짐.
                return half4(_OutlineColor.rgb, _OutlineColor.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
