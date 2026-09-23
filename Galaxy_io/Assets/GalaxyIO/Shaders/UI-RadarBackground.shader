// 레이더 배경. 원판, 동심원, 십자선, 대각선, 눈금, 외곽 링 글로우.
Shader "GalaxyIO/UI-RadarBackground"
{
    Properties
    {
        // Image 가 채우는 값. 인스펙터에서는 숨김.
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}

        _FillColor ("Fill Color", Color) = (0.13, 0.17, 0.23, 0.9)
        _LineColor ("Line Color", Color) = (0.1, 0.7, 0.8, 1)
        _RimColor ("Rim Color", Color) = (0.25, 0.95, 1, 1)

        _LineWidth ("Line Width", Float) = 0.006
        _RingCount ("Ring Count", Float) = 3
        _GridRadius ("Outermost Ring Radius", Float) = 0.72
        _CenterDot ("Center Dot Radius", Float) = 0.025

        _TickCount ("Tick Count", Float) = 48
        _TickInner ("Tick Inner Radius", Float) = 0.84
        _TickOuter ("Tick Outer Radius", Float) = 0.88

        _RimRadius ("Rim Radius", Float) = 0.93
        _RimWidth ("Rim Width", Float) = 0.02
        _GlowWidth ("Glow Width", Float) = 0.03
        _GlowIntensity ("Glow Intensity", Float) = 1.2

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
                float _RingCount;
                float _GridRadius;
                float _CenterDot;
                float _TickCount;
                float _TickInner;
                float _TickOuter;
                float _RimRadius;
                float _RimWidth;
                float _GlowWidth;
                float _GlowIntensity;
                float _PixelResolution;
            CBUFFER_END

            half4 BackgroundFragment(Varyings input) : SV_Target
            {
                float2 p  = RadarPoint(input.uv, _PixelResolution);
                float  r  = length(p);
                float  aa = RadarAA(input.uv, _PixelResolution);

                half disc = 1.0 - smoothstep(_RimRadius - aa, _RimRadius + aa, r);

                // 동심원. 가장 가까운 링 하나만 검사함.
                float spacing = _GridRadius / _RingCount;
                float ring = clamp(round(r / spacing), 1.0, _RingCount);
                half rings = Band(r - ring * spacing, _LineWidth, aa);

                half cross = max(Band(p.x, _LineWidth, aa), Band(p.y, _LineWidth, aa));

                // 대각선은 첫 링부터 눈금 바깥까지. 십자선보다 흐리게.
                half diagonals = max(Band((p.x - p.y) * 0.7071, _LineWidth * 0.7, aa),
                                     Band((p.x + p.y) * 0.7071, _LineWidth * 0.7, aa));
                diagonals *= step(spacing, r) * step(r, _TickOuter) * 0.6;

                // 눈금. 방위각을 호 길이로 바꿔 선 굵기를 반지름과 무관하게 맞춤.
                float tickStep = TWO_PI / _TickCount;
                float tickArc = abs(Bearing(p) - round(Bearing(p) / tickStep) * tickStep) * r;
                half ticks = Band(tickArc, _LineWidth, aa) * step(_TickInner, r) * step(r, _TickOuter);

                half center = 1.0 - smoothstep(_CenterDot - aa, _CenterDot + aa, r);

                half grid = saturate(max(max(rings, cross), max(diagonals, ticks)) + center) * disc;

                // 원판 바깥 픽셀은 글로우만 남으므로 외곽 링 색에서 시작함.
                half4 color = half4(lerp(_RimColor.rgb, _FillColor.rgb, disc), _FillColor.a * disc);
                color.rgb = lerp(color.rgb, _LineColor.rgb, grid);
                color.a   = max(color.a, grid * _LineColor.a);

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