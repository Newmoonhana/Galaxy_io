// 배가 남기는 물결처럼 트레일 뒤 화면을 일그러뜨리는 셰이더. TrailRenderer 전용.
// 트레일 UV: x 는 머리(0) -> 꼬리(1), y 는 한쪽 가장자리(0) -> 반대쪽(1).
// 일그러뜨리는 방향은 트레일 진행 방향(Tangent)의 수직. TrailRenderer 의 Generate Lighting Data 를 켤 것.
// 화면은 2D 렌더러의 Camera Sorting Layer Texture 에서 읽음. 트레일은 그 경계보다 앞 레이어에 둘 것.
Shader "GalaxyIO/Trail-Wake"
{
    Properties
    {
        _Strength ("Distortion Strength (Screen Height)", Range(0, 0.05)) = 0.012
        _Falloff ("Tail Falloff", Range(0.1, 8)) = 1.5
        _EdgeSharpness ("Edge Sharpness", Range(0.5, 8)) = 2
        _RippleCount ("Ripple Count", Float) = 3
        _RippleSpeed ("Ripple Speed", Float) = 4
        _Tint ("Tint (Alpha = Intensity)", Color) = (1, 1, 1, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        // 물결 세기를 알파로 섞음. 트레일 전체를 덮어쓰면 겹친 다른 효과가 지워짐.
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex WakeVertex
            #pragma fragment WakeFragment

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                float4 screenPos  : TEXCOORD1;
                float2 across     : TEXCOORD2;
            };

            TEXTURE2D(_CameraSortingLayerTexture);
            SAMPLER(sampler_CameraSortingLayerTexture);

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
                float _Falloff;
                float _EdgeSharpness;
                float _RippleCount;
                float _RippleSpeed;
                half4 _Tint;
            CBUFFER_END

            Varyings WakeVertex(Attributes input)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                float3 tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);

                o.positionCS = TransformWorldToHClip(positionWS);
                o.uv = input.uv;
                o.color = input.color;
                o.screenPos = ComputeScreenPos(o.positionCS);

                // 트레일 진행 방향을 화면 픽셀 기준으로 구하고 수직으로 돌림.
                float4 tipCS = TransformWorldToHClip(positionWS + tangentWS);
                float2 along = (tipCS.xy / tipCS.w - o.positionCS.xy / o.positionCS.w) * _ScaledScreenParams.xy;
                float2 across = normalize(float2(-along.y, along.x));

                // 화면 높이 기준 길이를 화면 UV 로 바꿈.
                o.across = across * _ScaledScreenParams.y / _ScaledScreenParams.xy * float2(1.0, _ProjectionParams.x);
                return o;
            }

            half4 WakeFragment(Varyings input) : SV_Target
            {
                // 중심 0, 가장자리 ±1.
                float side = input.uv.y * 2.0 - 1.0;
                float across = abs(side);

                // 투사체(머리)에 가까울수록 강함.
                float fade = pow(saturate(1.0 - input.uv.x), _Falloff) * input.color.a;

                // 삼각형의 두 변(가장자리)에 몰리고, 테두리에서는 0 으로 이어지게 함.
                float edge = pow(across, _EdgeSharpness) * smoothstep(1.0, 0.85, across);

                // 가장자리와 나란한 물결이 바깥으로 퍼짐.
                float ripple = sin(TWO_PI * (across * _RippleCount - _Time.y * _RippleSpeed));
                float amount = _Strength * fade * edge * ripple;

                float2 screenUV = input.screenPos.xy / input.screenPos.w + input.across * (amount * sign(side));
                half3 scene = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, screenUV).rgb;

                half3 color = scene + _Tint.rgb * (_Tint.a * fade * edge);
                return half4(color, fade * edge);
            }
            ENDHLSL
        }
    }
}