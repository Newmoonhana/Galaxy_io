using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GalaxyIO
{
    /// <summary>
    /// Instantiates the single player box from the baked entity prefab, once.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MovementSystemGroup))]
    public partial struct PlayerSpawnSystem : ISystem
    {
        EntityQuery m_Players;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_Players = SystemAPI.QueryBuilder().WithAll<PlayerTag>().Build();
            state.RequireForUpdate<SimulationConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!m_Players.IsEmpty)
                return;

            var config = SystemAPI.GetSingleton<SimulationConfig>();
            if (config.PlayerPrefab == Entity.Null)
                return;

            var player = state.EntityManager.Instantiate(config.PlayerPrefab);
            state.EntityManager.SetComponentData(player, LocalTransform.FromPositionRotationScale(
                float3.zero, quaternion.identity, config.PlayerSize));
            state.EntityManager.SetComponentData(player, new BoxSize { Value = config.PlayerSize });
            state.EntityManager.SetComponentData(player, new Velocity { Value = float2.zero });
        }
    }

    /// <summary>
    /// Keeps the bot population equal to <see cref="SimulationConfig.MaxBotCount"/>:
    /// spawns when the HUD raises the cap or the player has destroyed some, and destroys
    /// the surplus when the cap is lowered. Spawning is budgeted per frame so that going
    /// from 1000 to 100000 does not stall a frame.
    ///
    /// Each new bot gets a direction drawn uniformly from the unit circle and a random
    /// speed. Neither is ever re-rolled: the direction is decided once, at spawn.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(MovementSystemGroup))]
    [UpdateAfter(typeof(PlayerSpawnSystem))]
    public partial struct BotSpawnSystem : ISystem
    {
        EntityQuery m_Bots;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_Bots = SystemAPI.QueryBuilder().WithAll<BotTag>().Build();
            state.RequireForUpdate<SimulationConfig>();
            state.RequireForUpdate<WorldBounds>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<SimulationConfig>();
            var bounds = SystemAPI.GetSingleton<WorldBounds>();

            // Copy the RNG by value: the EntityManager calls below are structural changes,
            // which would invalidate any RefRW obtained before them.
            var random = SystemAPI.GetSingleton<SimulationRandom>().Value;

            var alive = m_Bots.CalculateEntityCount();
            var deficit = config.MaxBotCount - alive;

            if (deficit > 0 && config.BotPrefab != Entity.Null)
            {
                var count = math.min(deficit, math.max(1, config.MaxSpawnsPerFrame));
                var spawned = state.EntityManager.Instantiate(config.BotPrefab, count, Allocator.Temp);

                var half = config.BotSize * 0.5f;
                var min = bounds.Min + half;
                var max = bounds.Max - half;

                for (var i = 0; i < spawned.Length; i++)
                {
                    var position = random.NextFloat2(min, max);
                    var angle = random.NextFloat(0f, 2f * math.PI);
                    var speed = random.NextFloat(config.BotSpeedMin, config.BotSpeedMax);
                    var direction = new float2(math.cos(angle), math.sin(angle));

                    state.EntityManager.SetComponentData(spawned[i], LocalTransform.FromPositionRotationScale(
                        new float3(position, 0f), quaternion.identity, config.BotSize));
                    state.EntityManager.SetComponentData(spawned[i], new Velocity { Value = direction * speed });
                    state.EntityManager.SetComponentData(spawned[i], new BoxSize { Value = config.BotSize });
                }

                alive += count;
                spawned.Dispose();
            }
            else if (deficit < 0)
            {
                var all = m_Bots.ToEntityArray(Allocator.Temp);
                var surplus = math.min(-deficit, all.Length);
                state.EntityManager.DestroyEntity(all.GetSubArray(all.Length - surplus, surplus));
                all.Dispose();
                alive -= surplus;
            }

            SystemAPI.SetSingleton(new SimulationRandom { Value = random });

            if (SystemAPI.HasSingleton<SimulationStats>())
            {
                var stats = SystemAPI.GetSingleton<SimulationStats>();
                stats.AliveBotCount = alive;
                SystemAPI.SetSingleton(stats);
            }
        }
    }
}