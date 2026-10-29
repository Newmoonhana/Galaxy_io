// Particle-EnergyDust.shadergraph 의 Custom Function. 정점 위치만 다시 구하고 색은 그래프 노드.
// 파티클 렌더러 Render Mode: Billboard.
// Custom Vertex Streams: Position, Color, UV, Center (TEXCOORD0.zw|x), Velocity (TEXCOORD1.yzw), Size.x (TEXCOORD2.x)
#ifndef GALAXYIO_PARTICLE_ENERGY_DUST_INCLUDED
#define GALAXYIO_PARTICLE_ENERGY_DUST_INCLUDED

// 속도 방향으로 늘어난 선분 사각형의 꼭짓점. 길이 = MaxLength * Min(1, 속력 / MaxSpeed), 굵기 = Size.x.
// UV0 xy: UV, zw: Center.xy / UV1 x: Center.z, yzw: Velocity / UV2 x: Size.x
void EnergyDustPosition_float(float4 UV0, float4 UV1, float4 UV2, float MaxLength, float MaxSpeed, out float3 Position)
{
    float3 center = float3(UV0.zw, UV1.x);
    float2 velocity = UV1.yz;
    float speed = length(velocity);

    // 속도 방향(along)과 그 수직(across). 멈춘 가루는 위쪽을 향한 점.
    float2 along = speed > 1e-4 ? velocity / speed : float2(0.0, 1.0);
    float2 across = float2(-along.y, along.x);

    float width = UV2.x;
    float len = max(MaxLength * min(1.0, speed / max(MaxSpeed, 1e-4)), width);

    // 빌보드 꼭짓점을 UV 로 구분해 선분 사각형으로 다시 놓음. 머리(UV y 1)가 진행 방향.
    float2 corner = UV0.xy - 0.5;
    Position = center + float3(along * (corner.y * len) + across * (corner.x * width), 0.0);
}

#endif
