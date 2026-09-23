using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 우주선끼리 부딪힌 사실을 부딪힌 플레이어 본인에게만 알리는 시스템.
    /// AI 끼리의 충돌은 알리지 않음.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(CollisionDetectionSystemGroup))]
    [UpdateAfter(typeof(SpaceshipCollisionSystem))]
    public partial struct SpaceshipCrashNotifySystem : ISystem
    {
        // 한 번 울린 우주선이 다시 충돌음을 내기까지의 시간(초)
        private const float CrashSoundCooldown = 0.5F;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(
                SystemAPI.QueryBuilder()
                    .WithAll<SpaceshipCollisionElement, CrashSoundCooldownComponent, LocalTransform>()
                    .Build()
            );
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var cooldown in SystemAPI.Query<RefRW<CrashSoundCooldownComponent>>())
            {
                cooldown.ValueRW.Remaining -= deltaTime;
            }

            // NetworkId -> 접속
            var connections = new NativeHashMap<int, Entity>(4, Allocator.Temp);

            foreach (
                var (networkId, connection) in
                SystemAPI.Query<RefRO<NetworkId>>()
                    .WithAll<NetworkStreamInGame>()
                    .WithEntityAccess()
            ) {
                connections[networkId.ValueRO.Value] = connection;
            }

            ComponentLookup<GhostOwner> owners = SystemAPI.GetComponentLookup<GhostOwner>(true);
            ComponentLookup<CrashSoundCooldownComponent> cooldowns =
                SystemAPI.GetComponentLookup<CrashSoundCooldownComponent>();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (
                var (messages, transform, owner, entity) in
                SystemAPI.Query<DynamicBuffer<SpaceshipCollisionElement>, RefRO<LocalTransform>, RefRO<GhostOwner>>()
                    .WithAll<CrashSoundCooldownComponent>()
                    .WithEntityAccess()
            ) {
                for (int i = 0; i < messages.Length; i++)
                {
                    CollisionMessage message = messages[i].Message;

                    // 양쪽 모두 메시지를 받으므로 한 쌍에 한 번만 처리함.
                    if (entity.Index > message.Other.Index
                        || !cooldowns.HasComponent(message.Other)
                        || !owners.TryGetComponent(message.Other, out GhostOwner otherOwner))
                    {
                        continue;
                    }

                    // 플레이어가 아닌 쪽(AI 소유자 -1)은 받을 접속이 없음.
                    if (!connections.TryGetValue(owner.ValueRO.NetworkId, out Entity selfConnection))
                    {
                        selfConnection = Entity.Null;
                    }

                    if (!connections.TryGetValue(otherOwner.NetworkId, out Entity otherConnection))
                    {
                        otherConnection = Entity.Null;
                    }

                    // AI 끼리의 충돌.
                    if (selfConnection == Entity.Null && otherConnection == Entity.Null)
                    {
                        continue;
                    }

                    RefRW<CrashSoundCooldownComponent> self = cooldowns.GetRefRW(entity);
                    RefRW<CrashSoundCooldownComponent> other = cooldowns.GetRefRW(message.Other);

                    // 한쪽이라도 새로 부딪힌 것이면 울림.
                    if (self.ValueRO.Remaining > 0.0F && other.ValueRO.Remaining > 0.0F)
                    {
                        continue;
                    }

                    self.ValueRW.Remaining = CrashSoundCooldown;
                    other.ValueRW.Remaining = CrashSoundCooldown;

                    var crash = new SpaceshipCrashedRPC
                    {
                        Position = (transform.ValueRO.Position.xy + message.OtherCenter) * 0.5F,
                    };

                    Send(ecb, crash, selfConnection);
                    Send(ecb, crash, otherConnection);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            connections.Dispose();
        }

        // Entity.Null 이면 보내지 않음. 전체 전송 방지.
        private static void Send(EntityCommandBuffer ecb, in SpaceshipCrashedRPC message, Entity target)
        {
            if (target == Entity.Null)
            {
                return;
            }

            Entity rpcEntity = ecb.CreateEntity();

            ecb.AddComponent(rpcEntity, message);
            ecb.AddComponent(rpcEntity, new SendRpcCommandRequest { TargetConnection = target });
        }
    }
}
