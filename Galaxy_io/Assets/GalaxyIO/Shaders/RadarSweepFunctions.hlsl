// UI-RadarSweep.shadergraph 의 Custom Function. 빔/선 마스크만 구하고 색 합성은 그래프 노드.
#ifndef GALAXYIO_RADAR_SWEEP_FUNCTIONS_INCLUDED
#define GALAXYIO_RADAR_SWEEP_FUNCTIONS_INCLUDED

#include "Assets/GalaxyIO/Shaders/RadarCommon.hlsl"

// RadarView.MaxMembers 와 같아야 함.
#define RADAR_MAX_SHARES 16

// 레이더를 공유하는 초계함 파티원. xy: 레이더 좌표, z: 탐지 거리(레이더 좌표). RadarView 가 SetVectorArray 로 채움.
// 그래프 프로퍼티로는 배열을 못 써서 여기에 둠.
float4 _Shares[RADAR_MAX_SHARES];
float _ShareCount;

// Sweep: 빔 몸통 + 잔상 부채꼴 밝기(SweepAlpha 포함).
// Lines: 빔 앞쪽 가장자리, 외곽 호(SweepAlpha 포함), 탐지 거리 링, 공유 탐지 거리 링(ShareAlpha 포함).
void RadarSweepMasks_float(
    float2 UV, float PixelResolution, float Angle, float Radius, float RangeRadius, float SweepAlpha,
    float BeamWidth, float BeamAlpha, float TrailLength, float TrailAlpha, float TrailFalloff, float EdgeWidth,
    float ShareAlpha,
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

    // 초계함 파티원의 탐지 거리 경계. 다른 탐지 거리(자기 것 포함) 안에 겹친 부분은 그리지 않음.
    float shares = 0.0;
    float ownCover = 1.0 - smoothstep(RangeRadius - aa, RangeRadius + aa, r);
    float shareCover = 0.0;
    int shareCount = min((int)_ShareCount, RADAR_MAX_SHARES);

    [loop]
    for (int k = 0; k < shareCount; k++)
    {
        float d = length(p - _Shares[k].xy * Radius);
        float shareRadius = _Shares[k].z * Radius;
        float covered = ownCover;

        [loop]
        for (int j = 0; j < shareCount; j++)
        {
            if (j != k)
            {
                float other = _Shares[j].z * Radius;
                covered = max(covered, 1.0 - smoothstep(other - aa, other + aa, length(p - _Shares[j].xy * Radius)));
            }
        }

        shares = max(shares, ThinBand(d - shareRadius, EdgeWidth * 0.5, aa) * ShareAlpha * (1.0 - covered));
        shareCover = max(shareCover, 1.0 - smoothstep(shareRadius - aa, shareRadius + aa, d));
    }

    // 자기 탐지 거리 경계도 공유 탐지 거리 안에 들어간 부분은 지움.
    range *= 1.0 - shareCover;

    // 빔과 함께 도는 선만 투명해짐.
    Lines = saturate((edge + arc) * SweepAlpha + range + shares * inside);
}

#endif
