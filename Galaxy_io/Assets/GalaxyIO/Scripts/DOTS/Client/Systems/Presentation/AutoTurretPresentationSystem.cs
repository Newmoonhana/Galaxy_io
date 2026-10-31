using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 자동 포탑 Entity 를 복제된 각도(<see cref="AutoTurretElement.Angle"/>)로 돌려 그리는 시스템.
    /// 포탑을 장착하지 않은 칸은 포탑 그림을 감춤.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(LocalProjectileSystem))]
    [UpdateBefore(typeof(ProjectileBeamSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct AutoTurretPresentationSystem : ISystem
    {
        // 그리기 각도가 복제 각도를 따라가는 빠르기. 클수록 바로 붙음.
        private const float FollowRate = 20.0F;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AutoTurretMountElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityManager entityManager = state.EntityManager;
            float follow = 1.0F - math.exp(-FollowRate * SystemAPI.Time.DeltaTime);

            // Lookup 을 메인 스레드에서 직접 읽고 씀. LocalTransform 을 쓰는 예약된 Job 을 먼저 끝냄.
            state.CompleteDependency();

            ComponentLookup<LocalTransform> transforms = SystemAPI.GetComponentLookup<LocalTransform>();

            foreach (
                var (autoTurrets, mounts) in
                SystemAPI.Query<DynamicBuffer<AutoTurretElement>, DynamicBuffer<AutoTurretMountElement>>()
            ) {
                for (int i = 0; i < mounts.Length; i++)
                {
                    ref AutoTurretMountElement mount = ref mounts.ElementAt(i);
                    // 시야각 제한이 없는 포탑은 ±π 를 넘나듦. 가까운 쪽으로 따라감.
                    mount.VisualAngle = AutoTurret.Wrap(mount.VisualAngle + AutoTurret.Wrap(autoTurrets[i].Angle - mount.VisualAngle) * follow);

                    if (entityManager.HasComponent<CompanionComponent<SpriteRenderer>>(mount.Sprite))
                    {
                        entityManager.GetCompanion<SpriteRenderer>(mount.Sprite).enabled =
                            autoTurrets[i].Turret != ItemId.None;
                    }

                    if (!transforms.HasComponent(mount.Turret))
                    {
                        continue;
                    }

                    LocalTransform transform = transforms[mount.Turret];
                    transform.Rotation = math.mul(mount.Rest, quaternion.RotateZ(mount.VisualAngle));
                    transforms[mount.Turret] = transform;
                }
            }
        }
    }
}
