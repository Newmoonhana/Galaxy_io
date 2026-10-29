// UI-RadarBlip.shadergraph 의 Custom Function. 탐지 점 하나(점 + 헤일로). 색은 Image 색.
#ifndef GALAXYIO_UI_RADAR_BLIP_INCLUDED
#define GALAXYIO_UI_RADAR_BLIP_INCLUDED

// 이미지 중심이 점 중심. Extent: 이미지 가장자리까지의 거리(점 반지름 단위). 헤일로가 잘리지 않게 넉넉히 둠.
void RadarBlipMask_float(float2 UV, float Extent, out float Mask)
{
    // 점 반지름 1 기준 거리
    float d = length(UV * 2.0 - 1.0) * Extent;
    float aa = fwidth(d);

    float blipDot  = 1.0 - smoothstep(1.0 - aa, 1.0 + aa, d);
    float blipHalo = exp(-d) * 0.5;

    Mask = saturate(blipDot + blipHalo);
}

#endif
