// Sprite-BeamGlow.shadergraph 의 Custom Function. 번짐 모양만 구하고 색 합성은 그래프 노드.
#ifndef GALAXYIO_BEAM_GLOW_FUNCTIONS_INCLUDED
#define GALAXYIO_BEAM_GLOW_FUNCTIONS_INCLUDED

// 총구 쪽 구, 몸통, 끝 쪽 구를 합친 모양까지의 거리로 번짐(1 ~ 0)을 구함.
// 세 모양의 거리 중 가장 가까운 것만 쓰므로 번짐끼리 겹쳐 두 번 밝아지지 않음.
void BeamGlowShape_float(float2 UV, float2 Size, float Length, float StartRadius, float EndRadius, float HalfWidth, float BlurScale, out float Glow)
{
    // 사각형 중심 기준 좌표(unit). 총구 쪽 구는 -y, 끝 쪽 구는 +y.
    float2 p = (UV - 0.5) * Size;
    float halfLength = Length * 0.5;

    float toStart = length(p - float2(0.0, -halfLength)) - StartRadius;
    float toEnd = length(p - float2(0.0, halfLength)) - EndRadius;

    float2 q = abs(p) - float2(HalfWidth, halfLength);
    float toBody = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);

    // 합친 모양까지의 거리. 안쪽은 0 이하.
    float distance = min(min(toStart, toEnd), toBody);
    Glow = 1.0 - saturate(distance / max(BlurScale, 1e-4));
}

#endif
