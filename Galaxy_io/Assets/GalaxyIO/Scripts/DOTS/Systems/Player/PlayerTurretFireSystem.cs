using System;
using GalaxyIO.DOTS.Client.Components;
using GalaxyIO.DOTS.Components;
using GalaxyIO.DOTS.Components.Projectiles;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// <see cref="PlayerInputComponent"/>를 읽어들여 터렛의 투사체를 생성하는 시스템.
    /// 투사체는 서버에서만 생성되고 클라이언트는 복제된 Ghost를 받음.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(SpaceshipMovementSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct PlayerTurretFireSystem : ISystem
    {
        private float heat;
        private float maxHeat;

        // 탄 퍼짐에 사용할 난수 생성기.
        private Random random;

        public void OnCreate(ref SystemState state)
        {
            // Random의 시드는 0이 될 수 없음.
            random = Random.CreateFromIndex((uint)DateTime.Now.Ticks & 0x7FFFFFFFu);

            state.RequireForUpdate<PlayerInputComponent>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        // 플레이어 입력 > 터렛 투사체 생성
        public void OnUpdate(ref SystemState state)
        {
            foreach (
                var (transform, inputRequest, movement, turrets) in
                    SystemAPI.Query<
                        RefRO<LocalTransform>,
                        RefRO<PlayerInputComponent>,
                        RefRO<SpaceshipMovementComponent>,
                        DynamicBuffer<TurretElement>
                    >()
            ) {
                // 발사할 때만 commandBuffer 생성.
                var commandBuffer = inputRequest.ValueRO.TurretTriggered
                    ? SystemAPI
                        .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                        .CreateCommandBuffer(state.WorldUnmanaged)
                    : new EntityCommandBuffer();

                // ref 참조를 위해 foreach가 아닌 for로 접근
                for (int i = 0; i < turrets.Length; i++)
                {
                    ref TurretElement turret = ref turrets.ElementAt(i);

                    // 투사체가 없는 터렛
                    if (turret.ProjectilePrefab == Entity.Null)
                    {
                        continue;
                    }

                    // 발사 재사용 대기중인 터렛
                    if (turret.FireCooldown > 0.0F)
                    {
                        turret.FireCooldown -= SystemAPI.Time.DeltaTime;
                        continue;
                    }

                    // 발사 누를시에만 투사체를 발사함.
                    // FireCooldown때문에 Early-exit 불가.
                    if (!inputRequest.ValueRO.TurretTriggered)
                    {
                        continue;
                    }

                    // 터렛의 로컬 좌표를 월드 좌표로 합성. 원본 Transform은 수정하지 않음.
                    LocalTransform muzzleTransform = transform.ValueRO.TransformTransform(turret.MuzzleLocation);

                    // 탄퍼짐
                    if (turret.Spread > 0.0F)
                    {
                        float halfSpread = turret.Spread * 0.5F;
                        muzzleTransform = muzzleTransform.RotateZ(random.NextFloat(-halfSpread, halfSpread));
                    }

                    Entity projectile = commandBuffer.Instantiate(turret.ProjectilePrefab);
                    commandBuffer.SetComponent(projectile, muzzleTransform);

                    // 기수 방향으로 나가는 직진 투사체는 우주선의 속력을 물려받음.
                    if (SystemAPI.HasComponent<StraightforwardProjectileComponent>(turret.ProjectilePrefab))
                    {
                        var straightforward =
                            SystemAPI.GetComponent<StraightforwardProjectileComponent>(turret.ProjectilePrefab);

                        if (straightforward.ReflectOwnerVelocity)
                        {
                            straightforward.Speed += movement.ValueRO.Speed;
                            commandBuffer.SetComponent(projectile, straightforward);
                        }
                    }

                    turret.FireCooldown = turret.MaxFireCooldown;
                }
            }
        }
    }
}