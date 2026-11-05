using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Spaceship;
using GalaxyIO.DOTS.Systems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Bots
{
    /// <summary>
    /// 격추된 봇이 무리 드랍 목록대로 재료를 떨어뜨리게 하는 시스템. 서버만.
    /// 봇은 인벤토리가 없어 플레이어 사망 드랍이 없으므로 따로 떨어뜨림. 우주선이 지워지기 전에 돌아야 함.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(EventSystemGroup))]
    [UpdateAfter(typeof(DamageNotifySystem))]
    [UpdateAfter(typeof(SpaceshipCrashNotifySystem))]
    [UpdateBefore(typeof(SpaceshipDeathSystem))]
    public partial struct BotDropSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ItemSpawnMessage>();
            state.RequireForUpdate<PrefabRegistryComponent>();
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<BotComponent>().Build());
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity itemPrefab = SystemAPI.GetSingleton<PrefabRegistryComponent>().ItemEntity;

            if (itemPrefab == Entity.Null)
            {
                return;
            }

            ItemComponent item = SystemAPI.GetComponent<ItemComponent>(itemPrefab);
            DynamicBuffer<ItemSpawnMessage> spawns = SystemAPI.GetSingletonBuffer<ItemSpawnMessage>();
            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.SerializedData;

            foreach (var (botRef, spec, transform, entity) in
                     SystemAPI.Query<RefRW<BotComponent>, RefRO<SpaceshipSpecComponent>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                ref BotComponent bot = ref botRef.ValueRW;

                if (bot.Dropped || spec.ValueRO.Health > 0.0F || !SystemAPI.HasBuffer<BotDropElement>(bot.Group))
                {
                    continue;
                }

                bot.Dropped = true;

                var random = Random.CreateFromIndex(math.hash(new uint2((uint)entity.Index, tick)) & 0x7FFFFFFFu);

                foreach (BotDropElement drop in SystemAPI.GetBuffer<BotDropElement>(bot.Group))
                {
                    if (random.NextFloat() >= drop.Chance)
                    {
                        continue;
                    }

                    int count = random.NextInt(math.max(drop.Min, 0), math.max(drop.Max, drop.Min) + 1);

                    if (count <= 0)
                    {
                        continue;
                    }

                    spawns.Add(new ItemSpawnMessage
                    {
                        Prefab      = itemPrefab,
                        Id          = drop.Item,
                        Count       = count,
                        Position    = transform.ValueRO.Position,
                        Velocity    = random.NextFloat2Direction() * random.NextFloat(0.5F, 1.0F) * item.SlideSpeed,
                        PickupDelay = item.DropPickupDelay,
                    });
                }
            }
        }
    }

    /// <summary>
    /// 박치기 무리(CraftsRam) 모선의 함재기가 교전 대상에 그대로 들이받게 하는 시스템. 서버만.
    /// 함재기 조종(<see cref="CarriedCraftBehaviorSystem"/>)이 정한 속도를 대상 쪽 최고 속력으로 덮어씀.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(CarriedCraftBehaviorSystem))]
    public partial struct BotCraftRamSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<BotComponent>().Build());
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<CarriedCraftComponent>().Build());
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();

            float deltaTime = SystemAPI.Time.DeltaTime;
            ComponentLookup<LocalTransform> transforms = SystemAPI.GetComponentLookup<LocalTransform>(true);
            ComponentLookup<BotComponent> bots = SystemAPI.GetComponentLookup<BotComponent>(true);

            foreach (var (movement, craft, spec, entity) in
                     SystemAPI.Query<RefRW<MovementComponent>, RefRO<CarriedCraftComponent>, RefRW<CarriedCraftSpecComponent>>()
                         .WithAll<Simulate>()
                         .WithEntityAccess())
            {
                CarriedCraftComponent state1 = craft.ValueRO;

                if (!bots.HasComponent(state1.Carrier) || !bots[state1.Carrier].CraftsRam)
                {
                    continue;
                }

                // 박치기 함재기는 출격, 귀환까지 늘 지정한 최고 속력(나이트호크 최대 강화)으로 남.
                BotComponent carrierBot = bots[state1.Carrier];

                if (carrierBot.CraftSpeed > 0.0F)
                {
                    spec.ValueRW.Speed = math.max(spec.ValueRO.Speed, carrierBot.CraftSpeed);
                    spec.ValueRW.Acceleration = math.max(spec.ValueRO.Acceleration, carrierBot.CraftAcceleration);
                }

                if (state1.Returning || state1.Abandoned || state1.Target == Entity.Null
                    || !transforms.HasComponent(state1.Target) || !transforms.HasComponent(entity))
                {
                    continue;
                }

                LocalTransform self = transforms[entity];
                float2 position = self.Position.xy;
                float2 forward = math.normalizesafe(SpaceshipHeading.FromRotation(self.Rotation), new float2(0.0F, 1.0F));
                float2 toTarget = math.normalizesafe(transforms[state1.Target].Position.xy - position, forward);

                float speed = math.max(spec.ValueRO.Speed, math.length(movement.ValueRO.Velocity));
                movement.ValueRW.Velocity = toTarget * speed;
                movement.ValueRW.AngularVelocity = SpaceshipHeading.AngularVelocity(forward, toTarget, deltaTime);
            }
        }
    }
}
