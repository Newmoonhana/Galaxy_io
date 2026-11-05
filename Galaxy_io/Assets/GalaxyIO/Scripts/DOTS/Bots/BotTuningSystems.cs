using GalaxyIO.DOTS.Components.Actor;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Bots
{
    /// <summary>
    /// 강화 배율을 이미 반영한 우주선 표시. 서버 전용, 복제하지 않음.
    /// </summary>
    public struct ServerTunedTag : IComponentData
    {
    }

    /// <summary>
    /// 데디케이티드 서버 설정(Enemies.HealthScale)으로 생성된 AI 적의 최대 체력을 줄임. 서버만.
    /// 최대 체력은 서버가 복제하므로 기존 클라이언트도 그대로 보임.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct EnemyHealthTuningSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(
                SystemAPI.QueryBuilder()
                    .WithAll<EnemyPerceptionComponent, SpaceshipSpecComponent>()
                    .WithNone<CarriedCraftComponent, ServerTunedTag>()
                    .Build()
            );
        }

        public void OnUpdate(ref SystemState state)
        {
            Networking.DedicatedServerConfig.EnemySettings settings = Networking.DedicatedServerConfig.Current?.Enemies;

            if (settings == null || settings.HealthScale <= 0.0F || math.abs(settings.HealthScale - 1.0F) < 0.001F)
            {
                state.Enabled = false;
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (spec, entity) in
                     SystemAPI.Query<RefRW<SpaceshipSpecComponent>>()
                         .WithAll<EnemyPerceptionComponent>()
                         .WithNone<CarriedCraftComponent, ServerTunedTag>()
                         .WithEntityAccess())
            {
                spec.ValueRW.MaxHealth *= settings.HealthScale;
                spec.ValueRW.Health = math.min(spec.ValueRO.Health * settings.HealthScale, spec.ValueRO.MaxHealth);
                ecb.AddComponent<ServerTunedTag>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// 자유 모드 봇 서버에서 플레이어 우주선을 조금 강하게 함. 서버만.
    /// 최대 체력(PlayerHealthScale), 강화 한 단계마다 오르는 피해, 체력, 수리량(PlayerUpgradeGain).
    /// 투사체 피해 배율(PlayerDamageScale)은 ProjectileDamageSystem 이 곱함.
    /// 이동 능력치는 클라이언트 예측과 맞아야 하므로 바꾸지 않음. 피해, 최대 체력은 서버가 계산함.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PlayerTuningSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(
                SystemAPI.QueryBuilder()
                    .WithAll<PlayerSpaceshipMovementComponent, UpgradeParamElement, GhostOwner>()
                    .WithNone<BotComponent, ServerTunedTag>()
                    .Build()
            );
        }

        public void OnUpdate(ref SystemState state)
        {
            BotSettings settings = Networking.DedicatedServerConfig.Current?.Bots;

            // 봇 디렉터가 켜진 자유 모드 서버에서만.
            if (settings == null || !BotDamage.Table.Data.Enabled)
            {
                return;
            }

            float health = settings.PlayerHealthScale > 0.0F ? settings.PlayerHealthScale : 1.0F;
            float gain = settings.PlayerUpgradeGain > 0.0F ? settings.PlayerUpgradeGain : 1.0F;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (owner, spec, entity) in
                     SystemAPI.Query<RefRO<GhostOwner>, RefRW<SpaceshipSpecComponent>>()
                         .WithAll<PlayerSpaceshipMovementComponent, UpgradeParamElement>()
                         .WithNone<BotComponent, ServerTunedTag>()
                         .WithEntityAccess())
            {
                ecb.AddComponent<ServerTunedTag>(entity);

                if (owner.ValueRO.NetworkId <= 0)
                {
                    continue;
                }

                DynamicBuffer<UpgradeParamElement> parameters = SystemAPI.GetBuffer<UpgradeParamElement>(entity);

                if (parameters.Length <= (int)UpgradeParam.Repair)
                {
                    continue;
                }

                ref UpgradeParamElement damage = ref parameters.ElementAt((int)UpgradeParam.Damage);
                damage.Amount *= gain;

                ref UpgradeParamElement maxHealth = ref parameters.ElementAt((int)UpgradeParam.MaxHealth);
                maxHealth.Base *= health;
                maxHealth.Amount *= health * gain;

                ref UpgradeParamElement repair = ref parameters.ElementAt((int)UpgradeParam.Repair);
                repair.Amount *= gain;

                // 새 최대 체력으로 가득 채움(막 생성된 우주선).
                bool full = spec.ValueRO.Health >= spec.ValueRO.MaxHealth - 0.01F;
                float max = Upgrade.Value(parameters, SystemAPI.GetBuffer<UpgradeLevelElement>(entity), UpgradeParam.MaxHealth);
                spec.ValueRW.MaxHealth = max;
                spec.ValueRW.Health = full ? max : math.min(spec.ValueRO.Health, max);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
