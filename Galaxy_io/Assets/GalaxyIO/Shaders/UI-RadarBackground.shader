// 레이더 배경. 원판, 격자, 눈금, 외곽 링 글로우.
Shader "GalaxyIO/UI-RadarBackground"
{
    Properties
    {
        // Image 가 채우는 값. 인스펙터에서는 숨김.
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}

        // RadarView 가 채우는 값. 인스펙터 값은 미리보기용.
        _GridSpacing ("Grid Spacing (0 = Off)", Float) = 0.22
        _GridOffset ("Grid Offset", Vector) = (0, 0, 0, 0)

        _FillColor ("Fill Color", Color) = (0.13, 0.17, 0.23, 0.9)
        _LineColor ("Line Color", Color) = (0.1, 0.7, 0.8, 1)
        _RimColor ("Rim Color", Color) = (0.25, 0.95, 1, 1)

        _LineWidth ("Line Width", Float) = 0.006

        _TickCount ("Tick Count", Float) = 48
        _TickInner ("Tick Inner Radius", Float) = 0.84
        _TickOuter ("Tick Outer Radius", Float) = 0.88

        _RimRadius ("Rim Radius", Float) = 0.93
        _RimWidth ("Rim Width", Float) = 0.02
        _GlowWidth ("Glow Width", Float) = 0.03
        _GlowIntensity ("Glow Intensity", Float) = 1.2

        _LineGlowWidth ("Line Glow Width", Float) = 0.015
        _LineGlowIntensity ("Line Glow Intensity", Float) = 0.3

        _PixelResolution ("Pixel Resolution (0 = Off)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]

        Pass
        {
            HLSLPROGRAM
            #include "RadarCommon.hlsl"

            #pragma vertex RadarVertex
            #pragma fragment BackgroundFragment

            CBUFFER_START(UnityPerMaterial)
                half4 _FillColor;
                half4 _LineColor;
                half4 _RimColor;
                float _LineWidth;
                float _TickCount;
                float _TickInner;
                float _TickOuter;
                float _RimRadius;
                float _RimWidth;
                float _GlowWidth;
                float _GlowIntensity;
                float _PixelResolution;
                float _GridSpacing;
                float4 _GridOffset;
                float _LineGlowWidth;
                float _LineGlowIntensity;
            CBUFFER_END

            half4 BackgroundFragment(Varyings input) : SV_Target
            {
                float2 p  = RadarPoint(input.uv, _PixelResolution);
                float  r  = length(p);
                float  aa = RadarAA(input.uv, _PixelResolution);

                half disc = 1.0 - smoothstep(_RimRadius - aa, _RimRadius + aa, r);

                // 격자. 월드 좌표에 고정된 선이라 _GridOffset 만큼 밀림. 눈금 안쪽까지, 눈금보다 흐리게.
                float2 cell = p + _GridOffset.xy;
                cell -= round(cell / max(_GridSpacing, 1e-4)) * _GridSpacing;
                half gridMask = step(1e-4, _GridSpacing) * step(r, _TickInner);
                half squares = max(Band(cell.x, _LineWidth * 0.5, aa), Band(cell.y, _LineWidth * 0.5, aa));
                squares *= gridMask * 0.45;

                // 눈금. 방위각을 호 길이로 바꿔 선 굵기를 반지름과 무관하게 맞춤.
                float tickStep = TWO_PI / _TickCount;
                float tickArc = abs(Bearing(p) - round(Bearing(p) / tickStep) * tickStep) * r;
                half ticks = Band(tickArc, _LineWidth, aa) * step(_TickInner, r) * step(r, _TickOuter);

                half grid = max(ticks, squares) * disc;

                // 원판 바깥 픽셀은 글로우만 남으므로 외곽 링 색에서 시작함.
                half4 color = half4(lerp(_RimColor.rgb, _FillColor.rgb, disc), _FillColor.a * disc);
                color.rgb = lerp(color.rgb, _LineColor.rgb, grid);
                color.a   = max(color.a, grid * _LineColor.a);

                // 격자의 약한 가짜 블룸. 선에서 멀어질수록 옅어짐.
                half lineGlow = exp(-min(abs(cell.x), abs(cell.y)) / _LineGlowWidth) * gridMask;
                color.rgb += _LineColor.rgb * lineGlow * _LineGlowIntensity * disc;

                // 외곽 링 + 가짜 블룸. 사각형 가장자리에서 잘리지 않게 r = 1 까지 0 으로 줄임.
                float rimDistance = r - _RimRadius;
                half rim  = Band(rimDistance, _RimWidth, aa);
                half glow = exp(-max(abs(rimDistance) - _RimWidth, 0.0) / _GlowWidth) * _GlowIntensity;
                glow *= saturate((1.0 - r) / max(1.0 - _RimRadius - _RimWidth, 1e-4));

                color.rgb = lerp(color.rgb, _RimColor.rgb, rim) + _RimColor.rgb * glow * disc * 0.5;
                color.a   = max(color.a, saturate(max(rim, glow)) * _RimColor.a);

                return color * input.color;
            }
            ENDHLSL
        }
    }
}