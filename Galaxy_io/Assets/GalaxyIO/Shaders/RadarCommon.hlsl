// 레이더 UI 셰이더 공용 정점 함수와 도형 함수.
#ifndef GALAXYIO_RADAR_COMMON_INCLUDED
#define GALAXYIO_RADAR_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

Varyings RadarVertex(Attributes input)
{
    Varyings o;
    o.positionCS = TransformObjectToHClip(input.positionOS);
    o.uv = input.uv;
    o.color = input.color;
    return o;
}

// UV(0 ~ 1)를 중심 원점 좌표(-1 ~ 1)로 변환. resolution > 0 이면 픽셀 단위로 끊음.
float2 RadarPoint(float2 uv, float resolution)
{
    if (resolution > 0.0)
    {
        uv = (floor(uv * resolution) + 0.5) / resolution;
    }

    return uv * 2.0 - 1.0;
}

// 경계를 부드럽게 할 폭. 픽셀화하면 선이 픽셀 사이로 빠지지 않게 반 픽셀 이상으로 둠.
float RadarAA(float2 uv, float resolution)
{
    float aa = fwidth(uv.x) * 2.0;
    return resolution > 0.0 ? max(aa, 1.0 / resolution) : aa;
}

// |d| 가 width 안이면 1. 경계는 aa 폭으로 부드럽게.
half Band(float d, float width, float aa)
{
    return 1.0 - smoothstep(width - aa, width + aa, abs(d));
}

// 위쪽 0, 시계 방향 방위각. 0 ~ 2π.
float Bearing(float2 p)
{
    float a = atan2(p.x, p.y);
    return a < 0.0 ? a + TWO_PI : a;
}

#endif
