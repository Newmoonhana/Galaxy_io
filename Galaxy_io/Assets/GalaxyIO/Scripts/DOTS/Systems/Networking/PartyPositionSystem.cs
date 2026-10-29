using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 플레이어 우주선 위치를 같은 파티원에게 주기적으로 보내는 서버 시스템.
    /// 초계함은 레이더에 잡힌 우주선도 함께 보냄.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PartyPositionSystem : ISystem
    {
        // 보내는 간격(초)
        private const double Interval = 0.25;

        // 초계함 하나가 한 번에 공유하는 점 수 상한
        private const int MaxSharedBlips = 24;

        private struct Ship
        {
            public int NetworkId;
            public uint Party;
            public float2 Position;

            // 레이더 탐지 거리(강화 포함)
            public float Range;

            // 레이더 스캐너 기본 회전 속도, 강화로 더한 값, 반응 시간(강화 포함)
            public float SweepSpeed;
            public float SweepBonus;
            public float ResponseTime;

            // 레이더를 파티원에게 공유하는 초계함
            public bool Shares;
        }

        private struct Target
        {
            public float2 Position;
            public uint Party;

            // 플레이어 우주선이나 함재기
            public bool Player;

            public float Thickness;
        }

        private double nextSendTime;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkStreamInGame>();
        }

        public void OnUpdate(ref SystemState state)
        {
            double time = SystemAPI.Time.ElapsedTime;

            if (time < nextSendTime)
            {
                return;
            }

            nextSendTime = time + Interval;

            SystemAPI.ManagedAPI.TryGetSingleton(out SpaceshipRegistryAssetComponent registry);

            var ships = new NativeList<Ship>(Allocator.Temp);
            bool sharing = false;

            foreach (var (owner, faction, transform, radar, kind, entity) in
                     SystemAPI.Query<RefRO<GhostOwner>, RefRO<FactionComponent>, RefRO<LocalTransform>, RefRO<SpaceshipRadarComponent>, RefRO<SpaceshipKindComponent>>()
                         .WithAll<PlayerSpaceshipMovementComponent>()
                         .WithEntityAccess())
            {
                // 파티가 없는 플레이어는 혼자만의 번호라 받을 사람이 없음.
                if (faction.ValueRO.Party == Faction.SoloParty(owner.ValueRO.NetworkId))
                {
                    continue;
                }

                float range = radar.ValueRO.Range;
                float sweepBonus = 0.0F;
                float response = radar.ValueRO.ResponseTime;

                // RadarView 와 같은 강화 계산
                if (SystemAPI.HasBuffer<UpgradeParamElement>(entity))
                {
                    DynamicBuffer<UpgradeParamElement> parameters = SystemAPI.GetBuffer<UpgradeParamElement>(entity);
                    DynamicBuffer<UpgradeLevelElement> levels = SystemAPI.GetBuffer<UpgradeLevelElement>(entity);

                    range += Upgrade.Value(parameters, levels, UpgradeParam.RadarRange);
                    sweepBonus = Upgrade.Value(parameters, levels, UpgradeParam.RadarSweep);
                    response = math.max(response - Upgrade.Value(parameters, levels, UpgradeParam.RadarResponse), 0.0F);
                }

                SpaceshipAsset asset = registry?.Value != null ? registry.Value.Find(kind.ValueRO.Value) : null;
                bool shares = asset != null && asset.Category == SpaceshipCategory.Corvette;

                sharing |= shares;

                ships.Add(new Ship
                {
                    NetworkId = owner.ValueRO.NetworkId,
                    Party     = faction.ValueRO.Party,
                    Position  = transform.ValueRO.Position.xy,
                    Range        = range,
                    SweepSpeed   = radar.ValueRO.SweepSpeed,
                    SweepBonus   = sweepBonus,
                    ResponseTime = response,
                    Shares       = shares,
                });
            }

            // 초계함이 있을 때만 공유할 우주선을 모음.
            var targets = new NativeList<Target>(Allocator.Temp);

            if (sharing)
            {
                foreach (var (spec, faction, transform, entity) in
                         SystemAPI.Query<RefRO<SpaceshipSpecComponent>, RefRO<FactionComponent>, RefRO<LocalTransform>>().WithEntityAccess())
                {
                    targets.Add(new Target
                    {
                        Position  = transform.ValueRO.Position.xy,
                        Party     = faction.ValueRO.Party,
                        Player    = SystemAPI.HasComponent<PlayerNameComponent>(entity) || SystemAPI.HasComponent<CarriedCraftComponent>(entity),
                        Thickness = spec.ValueRO.Thickness,
                    });
                }
            }

            if (ships.Length > 1)
            {
                var commands = new EntityCommandBuffer(Allocator.Temp);

                foreach (var (networkId, connection) in
                         SystemAPI.Query<RefRO<NetworkId>>().WithAll<NetworkStreamInGame>().WithEntityAccess())
                {
                    int receiver = -1;

                    for (int i = 0; i < ships.Length; i++)
                    {
                        if (ships[i].NetworkId == networkId.ValueRO.Value)
                        {
                            receiver = i;
                            break;
                        }
                    }

                    if (receiver < 0)
                    {
                        continue;
                    }

                    Ship self = ships[receiver];

                    foreach (Ship ship in ships)
                    {
                        if (ship.Party != self.Party || ship.NetworkId == self.NetworkId)
                        {
                            continue;
                        }

                        // 공유 점보다 먼저 보내야 클라이언트가 이전 점을 비우고 새 점을 받음.
                        Entity rpc = commands.CreateEntity();
                        commands.AddComponent(rpc, new PartyMemberPositionRPC
                        {
                            NetworkId    = ship.NetworkId,
                            Position     = ship.Position,
                            Range        = ship.Shares ? ship.Range : 0.0F,
                            SweepSpeed   = ship.Shares ? ship.SweepSpeed : 0.0F,
                            SweepBonus   = ship.Shares ? ship.SweepBonus : 0.0F,
                            ResponseTime = ship.Shares ? ship.ResponseTime : 0.0F,
                        });
                        commands.AddComponent(rpc, new SendRpcCommandRequest { TargetConnection = connection });

                        if (!ship.Shares)
                        {
                            continue;
                        }

                        int shared = 0;

                        foreach (Target target in targets)
                        {
                            if (shared >= MaxSharedBlips)
                            {
                                break;
                            }

                            // 파티원 우주선과 같은 편 함재기는 따로 그림. 받는 쪽 레이더 안은 직접 스캔함.
                            if ((target.Player && Faction.IsSameParty(self.Party, target.Party))
                                || math.distancesq(target.Position, ship.Position) > ship.Range * ship.Range
                                || math.distancesq(target.Position, self.Position) <= self.Range * self.Range)
                            {
                                continue;
                            }

                            Entity blip = commands.CreateEntity();
                            commands.AddComponent(blip, new PartyRadarBlipRPC
                            {
                                NetworkId = ship.NetworkId,
                                Position  = target.Position,
                                Kind      = (byte)(Faction.IsSameParty(self.Party, target.Party) ? 2 : target.Player ? 1 : 0),
                                Thickness = target.Thickness,
                            });
                            commands.AddComponent(blip, new SendRpcCommandRequest { TargetConnection = connection });
                            shared++;
                        }
                    }
                }

                commands.Playback(state.EntityManager);
                commands.Dispose();
            }

            ships.Dispose();
            targets.Dispose();
        }
    }
}
