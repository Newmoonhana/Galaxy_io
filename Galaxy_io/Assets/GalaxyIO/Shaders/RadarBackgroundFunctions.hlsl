// UI-RadarBackground.shadergraph 의 Custom Function. 도형 마스크만 구하고 색 합성은 그래프 노드.
#ifndef GALAXYIO_RADAR_BACKGROUND_FUNCTIONS_INCLUDED
#define GALAXYIO_RADAR_BACKGROUND_FUNCTIONS_INCLUDED

#include "Assets/GalaxyIO/Shaders/RadarCommon.hlsl"

// Disc: 원판 안쪽 1. Grid: 격자 + 눈금(원판 안). LineGlow: 격자의 약한 가짜 블룸.
// Rim: 외곽 링. Glow: 외곽 링 가짜 블룸(GlowIntensity 포함, 사각형 가장자리에서 0).
// RimBloom: 외곽 링 위 얇은 띠 x 시간에 따라 AlphaMin ~ AlphaMax 를 왕복하는 투명도. 그래프가 HDR 색을 곱해 BaseColor 에 더함(실제 블룸).
// Time 은 그래프 Time 노드에서 받음. RimBloomSpeed 는 초당 왕복 횟수.
void RadarBackgroundMasks_float(
    float2 UV, float PixelResolution, float GridSpacing, float2 GridOffset, float LineWidth,
    float TickCount, float TickInner, float TickOuter, float RimRadius, float RimWidth,
    float GlowWidth, float GlowIntensity, float LineGlowWidth,
    float RimBloomWidth, float RimBloomAlphaMin, float RimBloomAlphaMax, float RimBloomSpeed, float Time,
    out float Disc, out float Grid, out float LineGlow, out float Rim, out float Glow, out float RimBloom)
{
    float2 p  = RadarPoint(UV, PixelResolution);
    float  r  = length(p);
    float  aa = RadarAA(UV, PixelResolution);

    Disc = 1.0 - smoothstep(RimRadius - aa, RimRadius + aa, r);

    // 격자. 월드 좌표에 고정된 선이라 GridOffset 만큼 밀림. 눈금 안쪽까지, 눈금보다 흐리게.
    float2 cell = p + GridOffset;
    cell -= round(cell / max(GridSpacing, 1e-4)) * GridSpacing;
    float gridMask = step(1e-4, GridSpacing) * step(r, TickInner);
    float squares = max(Band(cell.x, LineWidth * 0.5, aa), Band(cell.y, LineWidth * 0.5, aa));
    squares *= gridMask * 0.45;

    // 눈금. 방위각을 호 길이로 바꿔 선 굵기를 반지름과 무관하게 맞춤.
    float tickStep = TWO_PI / TickCount;
    float tickArc = abs(Bearing(p) - round(Bearing(p) / tickStep) * tickStep) * r;
    float ticks = Band(tickArc, LineWidth, aa) * step(TickInner, r) * step(r, TickOuter);

    Grid = max(ticks, squares) * Disc;

    // 선에서 멀어질수록 옅어짐.
    LineGlow = exp(-min(abs(cell.x), abs(cell.y)) / LineGlowWidth) * gridMask;

    // 사각형 가장자리에서 잘리지 않게 r = 1 까지 0 으로 줄임.
    float rimDistance = r - RimRadius;
    Rim  = Band(rimDistance, RimWidth, aa);
    Glow = exp(-max(abs(rimDistance) - RimWidth, 0.0) / GlowWidth) * GlowIntensity;
    Glow *= saturate((1.0 - r) / max(1.0 - RimRadius - RimWidth, 1e-4));

    // 코사인 왕복이라 Min / Max 에서 부드럽게 멈췄다 돌아감.
    float pulse = 0.5 - 0.5 * cos(Time * RimBloomSpeed * TWO_PI);
    RimBloom = Band(rimDistance, RimBloomWidth, aa) * lerp(RimBloomAlphaMin, RimBloomAlphaMax, pulse);
}

#endif
