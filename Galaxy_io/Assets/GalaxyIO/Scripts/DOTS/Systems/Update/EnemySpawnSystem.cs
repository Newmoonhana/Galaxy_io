using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Spaceship;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 일정 주기로 플레이어 레이더 바깥의 스폰 영역에 적 우주선 스폰 메시지를 남기는 시스템.
    /// 스폰 영역은 레이더 탐지 거리 x <see cref="EnemySpawnConfigComponent.SpawnAreaScale"/> 원에서
    /// 모든 레이더 원을 뺀 곳. 바깥 원은 속력만큼 진행 방향으로 치우침.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(UpdateSystemGroup))]
    public partial struct EnemySpawnSystem : ISystem
    {
        // 스폰 위치 추첨 횟수. 모두 레이더 안이면 그 한 대는 건너뜀.
        private const int SpawnAttempts = 8;

        private EntityQuery enemyQuery;

        private double nextSpawnTime;

        public void OnCreate(ref SystemState state)
        {
            // 함재기도 AI 인지 컴포넌트를 쓰지만 적이 아님.
            enemyQuery = SystemAPI.QueryBuilder().WithAll<EnemyPerceptionComponent>().WithNone<CarriedCraftComponent>().Build();

            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<PrefabRegistryComponent>();
            state.RequireForUpdate<EnemySpawnConfigComponent>();

            // 살아 있는 플레이어가 없으면 스폰하지 않음.
            state.RequireForUpdate(
                SystemAPI.QueryBuilder().WithAll<PlayerSpaceshipMovementComponent, LocalTransform>().WithNone<Bots.BotComponent>().Build()
            );
        }

        public void OnUpdate(ref SystemState state)
        {
            double elapsed = SystemAPI.Time.ElapsedTime;
            EnemySpawnConfigComponent config = SystemAPI.GetSingleton<EnemySpawnConfigComponent>();

            // 데디케이티드 서버 설정(Enemies)이 있으면 생성 주기와 수를 덮어씀. 전체 상한은 아래에서 플레이어 수에 비례하게 정함.
            Networking.DedicatedServerConfig.EnemySettings tuning = Networking.DedicatedServerConfig.Current?.Enemies;

            if (tuning != null)
            {
                config.SpawnInterval = tuning.SpawnInterval;
                config.SpawnPerRadar = tuning.SpawnPerRadar;
                config.RadarCap = tuning.RadarCap;
            }

            if (elapsed < nextSpawnTime)
            {
                return;
            }

            nextSpawnTime = elapsed + config.SpawnInterval;

            Entity enemyPrefab = SystemAPI.GetSingleton<PrefabRegistryComponent>().EnemyStrafingShip;

            // 레지스트리에 적이 없으면 베이킹 설정 문제. 스폰할 것이 없음.
            if (enemyPrefab == Entity.Null)
            {
                return;
            }

            int enemyCount = enemyQuery.CalculateEntityCount();

            // 설정이 없으면 원래 고정 상한. 있으면 아래에서 플레이어 수로 다시 정함.
            if (tuning == null && config.Cap - enemyCount <= 0)
            {
                return;
            }

            // (레이더 중심, 탐지 거리). 죽은 플레이어는 마지막 위치와 거리를 씀.
            var radars = new NativeList<float3>(4, Allocator.Temp);

            foreach (
                var (position, radius) in
                SystemAPI.Query<RefRO<GhostConnectionPosition>, RefRO<RelevancyRadiusComponent>>()
                    .WithAll<NetworkStreamInGame>()
                    .WithNone<Spectator.SpectatorTag>() // 관찰자의 넓은 복제 반경은 스폰 영역 계산에서 뺌.
            ) {
                radars.Add(new float3(position.ValueRO.Position.xy, radius.ValueRO.Value));
            }

            // 살아 있는 우주선마다 (바깥 원 중심, 바깥 원 반지름)
            var areas = new NativeList<float3>(4, Allocator.Temp);

            foreach (
                var (transform, radar, movement, playerMovement, ship) in
                SystemAPI.Query<RefRO<LocalTransform>, RefRO<SpaceshipRadarComponent>, RefRO<MovementComponent>, RefRO<PlayerSpaceshipMovementComponent>>()
                    .WithNone<Bots.BotComponent>() // 서버 봇은 플레이어가 아님. 봇 주변에 적을 만들면 플레이어 몫이 줄어듦.
                    .WithEntityAccess()
            ) {
                float range = radar.ValueRO.Range;

                if (SystemAPI.HasBuffer<UpgradeParamElement>(ship))
                {
                    range += Upgrade.Value(
                        SystemAPI.GetBuffer<UpgradeParamElement>(ship),
                        SystemAPI.GetBuffer<UpgradeLevelElement>(ship),
                        UpgradeParam.RadarRange
                    );
                }

                float2 velocity = movement.ValueRO.Velocity;
                float maxSpeed = playerMovement.ValueRO.MaxSpeed;
                float speedRatio = maxSpeed > math.EPSILON ? math.saturate(math.length(velocity) / maxSpeed) : 0.0F;

                // 최대 속력이면 레이더 원이 바깥 원 뒤쪽 끝에 맞닿음. 레이더 원은 바깥 원을 넘지 않음.
                float2 offset = math.normalizesafe(velocity) * ((config.SpawnAreaScale - 1.0F) * range * speedRatio);

                areas.Add(new float3(transform.ValueRO.Position.xy + offset, range * config.SpawnAreaScale));
            }

            // 전체 상한: 설정이 있으면 기본 + 살아 있는 플레이어 수 x 플레이어당 상한.
            int cap = tuning != null ? tuning.CapBase + tuning.CapPerPlayer * areas.Length : config.Cap;
            int remaining = cap - enemyCount;

            if (remaining <= 0)
            {
                radars.Dispose();
                areas.Dispose();
                return;
            }

            // 바깥 원마다 안에 있는 적 수
            var counts = new NativeArray<int>(areas.Length, Allocator.Temp);

            foreach (
                var transform in
                SystemAPI.Query<RefRO<LocalTransform>>().WithAll<EnemyPerceptionComponent>().WithNone<CarriedCraftComponent>()
            ) {
                float2 xy = transform.ValueRO.Position.xy;

                for (int i = 0; i < areas.Length; i++)
                {
                    if (math.distancesq(areas[i].xy, xy) <= areas[i].z * areas[i].z)
                    {
                        counts[i]++;
                    }
                }
            }

            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.SerializedData;
            var random = Random.CreateFromIndex(tick & 0x7FFFFFFFu);
            DynamicBuffer<SpaceshipSpawnMessage> spawns = SystemAPI.GetSingletonBuffer<SpaceshipSpawnMessage>();

            for (int i = 0; i < areas.Length && remaining > 0; i++)
            {
                int spawnCount = math.min(config.SpawnPerRadar, config.RadarCap - counts[i]);

                for (int n = 0; n < spawnCount && remaining > 0; n++)
                {
                    if (!TryPickPosition(areas[i], radars, ref random, out float2 position))
                    {
                        continue;
                    }

                    // 적 소유자는 -1. 플레이어 NetworkId 는 1부터라 겹치지 않음.
                    spawns.Add(new SpaceshipSpawnMessage
                    {
                        Prefab    = enemyPrefab,
                        Position  = new float3(position, 0.0F),
                        Rotation  = SpaceshipHeading.ToRotation(random.NextFloat2Direction()),
                        NetworkId = -1,
                    });
                    remaining--;
                }
            }

            radars.Dispose();
            areas.Dispose();
            counts.Dispose();
        }

        /// <summary>
        /// 바깥 원(xy 중심, z 반지름) 안, 모든 레이더 원 밖의 점을 고름.
        /// </summary>
        private static bool TryPickPosition(
            float3 area,
            NativeList<float3> radars,
            ref Random random,
            out float2 position)
        {
            for (int attempt = 0; attempt < SpawnAttempts; attempt++)
            {
                // 면적 기준 균등 분포. 거리를 그대로 뽑으면 안쪽에 몰림.
                position = area.xy + random.NextFloat2Direction() * (math.sqrt(random.NextFloat()) * area.z);

                if (!IsInAnyRadar(position, radars))
                {
                    return true;
                }
            }

            position = float2.zero;

            return false;
        }

        private static bool IsInAnyRadar(float2 position, NativeList<float3> radars)
        {
            for (int i = 0; i < radars.Length; i++)
            {
                if (math.distancesq(position, radars[i].xy) <= radars[i].z * radars[i].z)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
