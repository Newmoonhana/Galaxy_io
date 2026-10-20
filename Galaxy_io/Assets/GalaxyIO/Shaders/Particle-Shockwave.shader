// 원 둘레를 따라 화면을 사방으로 일그러뜨리는 충격파 파티클 셰이더. Trail-Wake 의 원형판.
// 일그러뜨리는 방향은 파티클 중심에서 바깥쪽. 파티클 크기를 키우면 고리가 퍼짐(Size over Lifetime).
// 세기는 파티클 색의 알파를 곱함(Color over Lifetime 으로 사라지게).
// 화면은 2D 렌더러의 Camera Sorting Layer Texture 에서 읽음. 파티클은 그 경계보다 앞 레이어(Distortion)에 둘 것.
// 파티클 회전은 0 으로 둘 것. UV 가 화면 축과 나란하다고 봄.
Shader "GalaxyIO/Particle-Shockwave"
{
    Properties
    {
        _Strength ("Distortion Strength (Screen Height)", Range(0, 0.1)) = 0.02
        _RingWidth ("Ring Width (Radius Ratio)", Range(0.05, 1)) = 0.35
        _RippleCount ("Ripple Count", Float) = 2
        _RippleSpeed ("Ripple Speed", Float) = 6
        _Tint ("Tint (Alpha = Intensity)", Color) = (1, 1, 1, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        // 배경을 그대로 옮겨 그리므로 섞지 않음. 고리 바깥은 일그러짐이 0 이라 경계가 보이지 않음.
        Blend Off
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex ShockwaveVertex
            #pragma fragment ShockwaveFragment

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
                float4 screenPos  : TEXCOORD1;
            };

            TEXTURE2D(_CameraSortingLayerTexture);
            SAMPLER(sampler_CameraSortingLayerTexture);

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
                float _RingWidth;
                float _RippleCount;
                float _RippleSpeed;
                half4 _Tint;
            CBUFFER_END

            Varyings ShockwaveVertex(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.color = input.color;
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 ShockwaveFragment(Varyings input) : SV_Target
            {
                // 중심 0, 둘레 1.
                float2 local = input.uv * 2.0 - 1.0;
                float radius = length(local);
                float2 outward = radius > 1e-4 ? local / radius : float2(0.0, 0.0);

                // 둘레 안쪽 RingWidth 폭에 몰림. 고리 가운데가 가장 강하고 둘레와 안쪽 끝에서 0.
                float ring = saturate(1.0 - abs(radius - (1.0 - _RingWidth)) / _RingWidth);
                ring = smoothstep(0.0, 1.0, ring) * input.color.a;

                // 둘레와 나란한 물결이 바깥으로 퍼짐.
                float ripple = sin(TWO_PI * (radius * _RippleCount - _Time.y * _RippleSpeed));
                float amount = _Strength * ring * ripple;

                // 화면 높이 기준 길이를 화면 UV 로 바꿈.
                float2 toScreen = float2(_ScaledScreenParams.y / _ScaledScreenParams.x, _ProjectionParams.x);
                float2 screenUV = input.screenPos.xy / input.screenPos.w + outward * toScreen * amount;
                half3 scene = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, screenUV).rgb;

                half3 color = scene + _Tint.rgb * (_Tint.a * ring);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
