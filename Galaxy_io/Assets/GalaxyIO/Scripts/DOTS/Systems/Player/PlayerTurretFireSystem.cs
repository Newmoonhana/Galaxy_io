using System;
using GalaxyIO.DOTS.Components;
using GalaxyIO.DOTS.Client.Components;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 클라이언트 Input을 매 프레임 캡쳐하는 <see cref="PlayerInputComponent"/>를
    /// 읽어들여 터렛의 투사체를 생성하는 시스템.
    ///
    /// PredictedSimulationSystemGroup—투사체는 클라이언트에서 먼저 스폰되고, 서버에서
    /// 이를 나중에 검증한다.
    /// </summary>
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
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

        // 플레이어 입력 > 우주선의 Forward, Velocity 수정
        public void OnUpdate(ref SystemState state)
        {
            foreach (
                var (transform, inputRequest, turrets) in
                    SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerInputComponent>, DynamicBuffer<TurretElement>>()
            ) {
                // 발사되었을때만 commandBuffer가 필요하므로 최적화
                var commandBuffer = inputRequest.ValueRO.TurretTriggered
                    ? SystemAPI
                        .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                        .CreateCommandBuffer(state.WorldUnmanaged)
                    : new EntityCommandBuffer();

                // ref참조를 위해 foreach가 아닌 for로 접근
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

                    if (inputRequest.ValueRO.TurretTriggered) Debug.Log(turret.FireCooldown);

                    // 발사 누를시에만 투사체를 발사함.
                    // FireCooldown때문에 Early-exit 불가.
                    if (!inputRequest.ValueRO.TurretTriggered)
                    {
                        continue;
                    }

                    // `TransformTransform`호출시 방어적 복사 발생: OK, 두 Transform 모두 원본을 수정하지
                    // 말아야 하며 여기서 필요한건 임시로 합성된 `muzzleTransform`.
                    LocalTransform muzzleTransform = transform.ValueRO.TransformTransform(turret.MuzzleLocation);

                    // 탄퍼짐
                    if (turret.Spread > 0.0F)
                    {
                        float halfSpread = turret.Spread * 0.5F;
                        muzzleTransform = muzzleTransform.RotateZ(random.NextFloat(-halfSpread, halfSpread));
                    }

                    Entity projectile = commandBuffer.Instantiate(turret.ProjectilePrefab);
                    commandBuffer.SetComponent(projectile, muzzleTransform);

                    turret.FireCooldown = turret.MaxFireCooldown;
                }
            }
        }
    }
}