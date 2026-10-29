// UI-PingArrow.shadergraph 의 Custom Function. 채우기와 빛번짐 합성은 그래프 노드.
#ifndef GALAXYIO_UI_PING_ARROW_INCLUDED
#define GALAXYIO_UI_PING_ARROW_INCLUDED

// 삼각형 영역 기준 좌표. (0, 0) 좌하단, (1, 1) 우상단. 가장자리 Padding 만큼은 빛번짐 자리.
float2 PingArrowPoint(float2 uv, float padding)
{
    return (uv - padding) / (1.0 - 2.0 * padding);
}

// 위를 향한 이등변삼각형까지의 부호 있는 거리. 안쪽이 음수. 꼭짓점은 (0.5, 1).
void PingTriangleDistance_float(float2 UV, float Padding, float BaseWidth, out float Distance)
{
    float2 p = PingArrowPoint(UV, Padding);

    // 꼭짓점 기준, 아래쪽이 +y 인 좌표로 바꿔 거리를 구함.
    float2 q = float2(BaseWidth * 0.5, 1.0);
    float2 apex = float2(abs(p.x - 0.5), 1.0 - p.y);
    float2 a = apex - q * saturate(dot(apex, q) / dot(q, q));
    float2 b = apex - q * float2(saturate(apex.x / q.x), 1.0);
    float2 d = min(float2(dot(a, a), apex.y * q.x - apex.x * q.y), float2(dot(b, b), q.y - apex.y));
    Distance = sqrt(d.x) * sign(-d.y);
}

// 영역에 꽉 찬 원까지의 부호 있는 거리. 안쪽이 음수.
void PingCircleDistance_float(float2 UV, float Padding, out float Distance)
{
    Distance = length(PingArrowPoint(UV, Padding) - 0.5) - 0.5;
}

#endif
