// 레이더 UI 셰이더 그래프 공용 도형 함수. Custom Function 파일(UI-Radar*.hlsl)이 포함함.
#ifndef GALAXYIO_RADAR_COMMON_INCLUDED
#define GALAXYIO_RADAR_COMMON_INCLUDED

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
float Band(float d, float width, float aa)
{
    return 1.0 - smoothstep(width - aa, width + aa, abs(d));
}

// 해상도와 무관하게 매끈한 가는 선. aa 는 1 픽셀.
// 1 픽셀보다 가는 선은 픽셀 중심에 걸칠 때와 사이에 걸칠 때 밝기가 달라 계단·점선이 생김.
// 반폭을 1 픽셀 이상으로 넓혀 부드러운 단면으로 그리고, 대신 어둡게 해서 최소 1 픽셀 굵기만큼의 밝기만 남김.
float ThinBand(float d, float width, float aa)
{
    float drawn = max(width, aa);
    return Band(d, drawn, aa) * saturate(max(width * 2.0, aa) / (drawn * 2.0));
}

// 위쪽 0, 시계 방향 방위각. 0 ~ 2π.
float Bearing(float2 p)
{
    float a = atan2(p.x, p.y);
    return a < 0.0 ? a + TWO_PI : a;
}

// @HACK_AI: Canvas 타깃 블렌드가 프리멀티플라이드 고정. 알파 1/1024, 색 ×1024 로 가산 근사.
// Canvas 셰이더는 rgb 에 알파를 곱한 뒤 Blend One OneMinusSrcAlpha 로 섞으므로 Color 가 그대로 더해지고 뒤 배경은 0.1% 만 어두워짐.
#define RADAR_ADDITIVE_SCALE 1024.0

// 스윕/점 그래프의 마지막 단계. 결과를 Base Color, Alpha 블록에 연결.
void RadarAdditive_float(float3 Color, out float3 BaseColor, out float Alpha)
{
    BaseColor = Color * RADAR_ADDITIVE_SCALE;
    Alpha = 1.0 / RADAR_ADDITIVE_SCALE;
}

#endif
