// Sprite-EnergyBall.shadergraph 의 Custom Function. 원 마스크만 구하고 색 합성은 그래프 노드.
#ifndef GALAXYIO_ENERGY_BALL_FUNCTIONS_INCLUDED
#define GALAXYIO_ENERGY_BALL_FUNCTIONS_INCLUDED

// Inner: 안쪽 색 비율(테두리 0, 안쪽 1). Coverage: 원 안쪽 1, 바깥 0.
void EnergyBallMasks_float(float2 UV, float EdgeWidth, float Smoothness, out float Inner, out float Coverage)
{
    // 중심 0, 원 둘레 1.
    float radius = length(UV * 2.0 - 1.0);

    Inner = smoothstep(1.0 - EdgeWidth + Smoothness, 1.0 - EdgeWidth - Smoothness, radius);
    Coverage = smoothstep(1.0, 1.0 - Smoothness * 2.0, radius);
}

#endif
