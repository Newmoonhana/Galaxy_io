using GalaxyIO.DOTS.Components.Actor;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 발사대(<see cref="TurretRackComponent"/>)의 투사체 그림을 복제된 장전 상태대로 그리는 시스템.
    /// 장전되면 그림마다 Delay 만큼 기다렸다가 숨은 자리에서 밀어내며 선명하게 하고, 쏘면 바로 감춤.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct TurretRackPresentationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TurretRackElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityManager entityManager = state.EntityManager;
            float deltaTime = SystemAPI.Time.DeltaTime;
            ComponentLookup<LocalTransform> transforms = SystemAPI.GetComponentLookup<LocalTransform>();

            foreach (
                var (rack, rounds) in
                SystemAPI.Query<RefRO<TurretRackComponent>, DynamicBuffer<TurretRackElement>>()
            ) {
                for (int i = 0; i < rounds.Length; i++)
                {
                    ref TurretRackElement round = ref rounds.ElementAt(i);
                    bool loaded = (rack.ValueRO.Loaded & (1 << round.Muzzle)) != 0;

                    round.Progress = loaded
                        ? math.min(round.Progress + deltaTime / rack.ValueRO.SlideTime, 1.0F)
                        : -round.Delay;
                    float shown = math.smoothstep(0.0F, 1.0F, round.Progress);

                    // 나오면서 선명해짐.
                    if (entityManager.HasComponent<SpriteRenderer>(round.Sprite))
                    {
                        SpriteRenderer sprite = entityManager.GetComponentObject<SpriteRenderer>(round.Sprite);
                        sprite.enabled = round.Progress > 0.0F;

                        Color color = sprite.color;
                        color.a = shown;
                        sprite.color = color;
                    }

                    if (!transforms.HasComponent(round.Sprite))
                    {
                        continue;
                    }

                    LocalTransform transform = transforms[round.Sprite];
                    transform.Position = math.lerp(round.Hidden, round.Shown, shown);
                    transforms[round.Sprite] = transform;
                }
            }
        }
    }
}
