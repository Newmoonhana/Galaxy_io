// UI-RadarSweep.shadergraph 의 Custom Function. 빔/선 마스크만 구하고 색 합성은 그래프 노드.
#ifndef GALAXYIO_UI_RADAR_SWEEP_INCLUDED
#define GALAXYIO_UI_RADAR_SWEEP_INCLUDED

#include "Assets/GalaxyIO/Shaders/RadarCommon.hlsl"

// Sweep: 빔 몸통 + 잔상 부채꼴 밝기(SweepAlpha 포함).
// Lines: 빔 앞쪽 가장자리, 외곽 호(SweepAlpha 포함), 탐지 거리 링.
void RadarSweepMasks_float(
    float2 UV, float PixelResolution, float Angle, float Radius, float RangeRadius, float SweepAlpha,
    float BeamWidth, float BeamAlpha, float TrailLength, float TrailAlpha, float TrailFalloff, float EdgeWidth,
    out float Sweep, out float Lines)
{
    float2 p  = RadarPoint(UV, PixelResolution);
    float  r  = length(p);
    float  aa = RadarAA(UV, PixelResolution);

    float inside = 1.0 - smoothstep(Radius - aa, Radius + aa, r);

    // 빔 뒤쪽으로 떨어진 각도. 0 이면 빔 앞쪽 가장자리.
    float behind = fmod(radians(Angle) - Bearing(p) + TWO_PI * 2.0, TWO_PI);

    float beam  = radians(BeamWidth);
    float trail = radians(TrailLength);

    // 빔 몸통은 앞쪽이 가장 밝음. 잔상은 빔 뒤로 갈수록 옅어짐.
    float core = behind < beam ? lerp(1.0, 0.6, behind / beam) * BeamAlpha : 0.0;
    float fade = behind >= beam ? pow(saturate(1.0 - (behind - beam) / trail), TrailFalloff) * TrailAlpha : 0.0;

    // 바깥쪽일수록 밝게.
    Sweep = (core + fade) * lerp(0.35, 1.0, saturate(r / Radius)) * inside * SweepAlpha;

    float edge = Band(min(behind, TWO_PI - behind) * r, EdgeWidth, aa) * inside;
    float arc  = Band(r - Radius, EdgeWidth, aa) * step(behind, beam);

    // 탐지 거리 경계. 표시 거리보다 짧을 때만 보임.
    float range = ThinBand(r - RangeRadius, EdgeWidth * 0.5, aa) * step(RangeRadius, Radius - aa) * 0.5;

    // 빔과 함께 도는 선만 투명해짐.
    Lines = saturate((edge + arc) * SweepAlpha + range);
}

#endif
