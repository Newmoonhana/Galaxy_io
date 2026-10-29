using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 받은 <see cref="CarrierOrderRPC"/>를 검증해 보낸 사람 모선의 명령으로 바꾸는 서버 시스템.
    /// 명령 지점은 레이더 탐지 거리 안이어야 하고, 귀환을 뺀 명령은 재사용 대기시간이 있음.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(GhostSimulationSystemGroup))]
    [UpdateBefore(typeof(PredictedSimulationSystemGroup))]
    public sealed partial class CarrierOrderSystem : SystemBase
    {
        private EntityQuery requestQuery;
        private EntityQuery carrierQuery;
        private EntityQuery craftQuery;

        protected override void OnCreate()
        {
            requestQuery = SystemAPI.QueryBuilder().WithAll<ReceiveRpcCommandRequest, CarrierOrderRPC>().Build();
            carrierQuery = SystemAPI.QueryBuilder().WithAll<CarrierComponent, GhostOwner, PlayerNameComponent, LocalTransform>().Build();
            craftQuery = SystemAPI.QueryBuilder().WithAll<CarriedCraftComponent>().Build();

            RequireForUpdate(requestQuery);
            RequireForUpdate<NetworkTime>();
        }

        protected override void OnUpdate()
        {
            using NativeArray<ReceiveRpcCommandRequest> requests = requestQuery.ToComponentDataArray<ReceiveRpcCommandRequest>(Allocator.Temp);
            using NativeArray<CarrierOrderRPC> orders = requestQuery.ToComponentDataArray<CarrierOrderRPC>(Allocator.Temp);
            using NativeArray<Entity> carriers = carrierQuery.ToEntityArray(Allocator.Temp);
            using NativeArray<Entity> crafts = craftQuery.ToEntityArray(Allocator.Temp);

            EntityManager.DestroyEntity(requestQuery);

            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.SerializedData;

            for (int i = 0; i < orders.Length; i++)
            {
                // 입장 전이거나 보낸 뒤 접속이 끊긴 요청은 버림.
                Entity connection = requests[i].SourceConnection;

                if (!EntityManager.HasComponent<RelevancyRadiusComponent>(connection))
                {
                    continue;
                }

                int networkId = EntityManager.GetComponentData<NetworkId>(connection).Value;
                Entity carrierEntity = Entity.Null;

                foreach (Entity entity in carriers)
                {
                    if (EntityManager.GetComponentData<GhostOwner>(entity).NetworkId == networkId)
                    {
                        carrierEntity = entity;
                        break;
                    }
                }

                if (carrierEntity == Entity.Null)
                {
                    continue;
                }

                CarrierOrderRPC order = orders[i];
                CarrierComponent carrier = EntityManager.GetComponentData<CarrierComponent>(carrierEntity);
                float2 position = EntityManager.GetComponentData<LocalTransform>(carrierEntity).Position.xy;

                if (order.Order == CarrierOrder.Return)
                {
                    if (carrier.Order == CarrierOrder.None || carrier.Order == CarrierOrder.Return)
                    {
                        continue;
                    }

                    // 함재기마다 출발 시각을 다르게 해서 서로 덜 부딪힘.
                    var random = Random.CreateFromIndex(math.hash(new uint2(tick, (uint)networkId)) & 0x7FFFFFFFu);

                    foreach (Entity entity in crafts)
                    {
                        CarriedCraftComponent craft = EntityManager.GetComponentData<CarriedCraftComponent>(entity);

                        if (craft.Carrier == carrierEntity && !craft.Returning)
                        {
                            craft.ReturnRemaining = random.NextFloat(0.0F, carrier.ReturnDelay);
                            EntityManager.SetComponentData(entity, craft);
                        }
                    }

                    carrier.Order = CarrierOrder.Return;
                    carrier.Target = Entity.Null;
                    EntityManager.SetComponentData(carrierEntity, carrier);
                    continue;
                }

                if (carrier.OrderRemaining > 0.0F)
                {
                    continue;
                }

                switch (order.Order)
                {
                    case CarrierOrder.Attack:
                        // 아군, 같은 편은 공격하지 않음.
                        if (!EntityManager.HasComponent<SpaceshipSpecComponent>(order.Target)
                            || !EntityManager.HasComponent<LocalTransform>(order.Target)
                            || Faction.IsFriendly(
                                EntityManager.GetComponentData<FactionComponent>(carrierEntity),
                                EntityManager.GetComponentData<FactionComponent>(order.Target)))
                        {
                            continue;
                        }

                        order.Position = EntityManager.GetComponentData<LocalTransform>(order.Target).Position.xy;
                        break;

                    case CarrierOrder.SecureArea:
                        order.Target = Entity.Null;
                        break;

                    case CarrierOrder.Escort:
                        order.Target = Entity.Null;
                        order.Position = position;
                        break;

                    default:
                        continue;
                }

                // 명령 지점은 레이더 지도 안이어야 함.
                if (math.distancesq(position, order.Position) > carrier.OrderRange * carrier.OrderRange)
                {
                    continue;
                }

                // 귀환 중이던 함재기도 새 명령을 따름.
                foreach (Entity entity in crafts)
                {
                    CarriedCraftComponent craft = EntityManager.GetComponentData<CarriedCraftComponent>(entity);

                    if (craft.Carrier == carrierEntity && craft.Returning)
                    {
                        craft.Returning = false;
                        EntityManager.SetComponentData(entity, craft);
                    }
                }

                carrier.Order = order.Order;
                carrier.Target = order.Target;
                carrier.Position = order.Position;
                carrier.Reassign = true;
                carrier.OrderRemaining = carrier.OrderCooldown;
                EntityManager.SetComponentData(carrierEntity, carrier);
            }
        }
    }
}
