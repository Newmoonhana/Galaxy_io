using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 자기 우주선의 자동 포탑 투사체를 예측된 우주선 위치에 맞춰 옮겨 그리는 시스템.
    /// 자동 포탑 투사체는 보간 틱에 그려져 예측 틱에 그려지는 자기 우주선보다 뒤처짐.
    /// 처음 보일 때 두 틱 사이에 우주선이 움직이는 거리만큼 옮기고 그 거리를 계속 유지함.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ProjectileHitPresentationSystem))]
    [UpdateBefore(typeof(ProjectileVisualSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct AutoTurretProjectileShiftSystem : ISystem
    {
        private EntityQuery localShipQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            localShipQuery = SystemAPI.QueryBuilder()
                .WithAll<GhostOwnerIsLocal, AutoTurretElement, SpaceshipMovementComponent>()
                .Build();

            state.RequireForUpdate(localShipQuery);
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<ClientServerTickRate>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 기종을 바꾸는 순간 잠깐 두 대가 보일 수 있음. 그 프레임은 건너뜀.
            NativeArray<Entity> ships = localShipQuery.ToEntityArray(Allocator.Temp);

            if (ships.Length != 1)
            {
                return;
            }

            Entity ship = ships[0];
            float2 velocity = SystemAPI.GetComponent<SpaceshipMovementComponent>(ship).DeltaMovement;
            NetworkTime networkTime = SystemAPI.GetSingleton<NetworkTime>();

            if (!networkTime.ServerTick.IsValid || !networkTime.InterpolationTick.IsValid)
            {
                return;
            }

            float lag = math.max(networkTime.ServerTick.TicksSince(networkTime.InterpolationTick), 0)
                        / (float)SystemAPI.GetSingleton<ClientServerTickRate>().SimulationTickRate;

            // 조종 포탑 투사체는 예측 스폰하므로 제외함.
            foreach (
                var (transform, shared) in
                SystemAPI.Query<RefRW<LocalTransform>, RefRW<SharedProjectileComponent>>()
                    .WithAll<GhostInstance>()
                    .WithNone<PredictedGhost>()
            ) {
                if (shared.ValueRO.Owner != ship)
                {
                    continue;
                }

                if (!shared.ValueRO.Shifted)
                {
                    shared.ValueRW.VisualOffset = velocity * lag;
                    shared.ValueRW.Shifted = true;
                }

                float2 position = transform.ValueRO.Position.xy;

                if (position.Equals(shared.ValueRO.ShiftedPosition))
                {
                    continue;
                }

                position += shared.ValueRO.VisualOffset;
                transform.ValueRW.Position = new float3(position, transform.ValueRO.Position.z);
                shared.ValueRW.ShiftedPosition = position;
            }
        }
    }
}
