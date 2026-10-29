// Particle-EnergyRing.shadergraph 의 Custom Function. 고리 마스크만 구하고 색 합성은 그래프 노드.
// 파티클 렌더러 Custom Vertex Streams: Position, Color, UV, Custom1.x (TEXCOORD0.z)
#ifndef GALAXYIO_PARTICLE_ENERGY_RING_INCLUDED
#define GALAXYIO_PARTICLE_ENERGY_RING_INCLUDED

// UV0 xy: UV, z: HoleScale (Custom1.x).
// Inner: 안쪽 색 비율(두 둘레 0, 그 사이 1). Coverage: 고리 안 1, 고리 밖과 구멍 0.
void EnergyRingMasks_float(float4 UV0, float EdgeWidth, float Smoothness, out float Inner, out float Coverage)
{
    // 중심 0, 바깥 둘레 1. 구멍 둘레는 HoleScale.
    // 바깥 둘레의 번짐까지 사각형 안에 들도록 고리를 번짐만큼 작게 그림.
    float radius = length(UV0.xy * 2.0 - 1.0) * (1.0 + Smoothness);
    float hole = saturate(UV0.z);

    // 두 둘레 중 가까운 쪽까지의 거리로 테두리를 칠함.
    float toEdge = min(1.0 - radius, radius - hole);
    Inner = smoothstep(EdgeWidth - Smoothness, EdgeWidth + Smoothness, toEdge);

    // 고리 밖과 구멍은 투명. 구멍이 둘레까지 커지면 사라짐.
    Coverage = smoothstep(-Smoothness, Smoothness, toEdge) * step(hole, 0.999);
}

#endif
