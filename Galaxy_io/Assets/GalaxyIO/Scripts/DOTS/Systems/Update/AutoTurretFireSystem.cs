using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace GalaxyIO.DOTS.Systems
{
    /// <summary>
    /// 자동 포탑(<see cref="AutoTurretElement"/>)을 돌리고 투사체 스폰 메시지를 남기는 시스템.
    /// 시야각, 탐지 거리 안의 적을 향해 돌고, 적이 없으면 시야각 끝에서 끝으로 훑음.
    /// 포탑 RPS 위에 포탑마다 사격/휴식 주기를 얹음. 열, 피해 강화는 적용하지 않음.
    /// 투사체는 AI 의 것처럼 모든 클라이언트에서 보간함.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(UpdateSystemGroup))]
    [UpdateAfter(typeof(PlayerMovementSystem))]
    public partial struct AutoTurretFireSystem : ISystem
    {
        private EntityQuery hullQuery;

        public void OnCreate(ref SystemState state)
        {
            hullQuery = SystemAPI.QueryBuilder()
                .WithAll<SpaceshipSpecComponent, ColliderComponent, FactionComponent, LocalTransform>()
                .Build();

            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate<AutoTurretElement>();
            state.RequireForUpdate<ItemRegistryElement>();
            state.RequireForUpdate<ItemConfigComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity registryEntity = SystemAPI.GetSingletonEntity<ItemRegistryElement>();
            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetBuffer<ItemRegistryElement>(registryEntity);

            ItemConfigComponent config = SystemAPI.GetSingleton<ItemConfigComponent>();
            float deltaTime = SystemAPI.Time.DeltaTime;
            uint tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick.SerializedData;

            NativeArray<Entity> hulls = hullQuery.ToEntityArray(Allocator.Temp);
            NativeArray<SpaceshipSpecComponent> hullSpecs = hullQuery.ToComponentDataArray<SpaceshipSpecComponent>(Allocator.Temp);
            NativeArray<ColliderComponent> hullColliders = hullQuery.ToComponentDataArray<ColliderComponent>(Allocator.Temp);
            NativeArray<FactionComponent> hullFactions = hullQuery.ToComponentDataArray<FactionComponent>(Allocator.Temp);
            NativeArray<LocalTransform> hullTransforms = hullQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            DynamicBuffer<ProjectileSpawnMessage> spawns = SystemAPI.GetSingletonBuffer<ProjectileSpawnMessage>();

            // 포탑마다 비워서 다시 씀.
            var shots = new NativeList<TurretShotRequest>(8, Allocator.Temp);

            // TurretCycle 에 넘길 총구 하나의 쿨다운. 포탑마다 값을 옮겨 씀.
            var cooldown = new NativeArray<float>(1, Allocator.Temp);

            foreach (
                var (transform, faction, autoTurrets, mounts, spaceship) in
                    SystemAPI.Query<
                        RefRO<LocalTransform>,
                        RefRO<FactionComponent>,
                        DynamicBuffer<AutoTurretElement>,
                        DynamicBuffer<AutoTurretMountElement>
                    >().WithAll<Simulate>().WithEntityAccess()
            ) {
                for (int k = 0; k < autoTurrets.Length; k++)
                {
                    AutoTurretElement autoTurret = autoTurrets[k];
                    AutoTurretMountElement mount = mounts[k];
                    ItemRegistryElement turret = registry.Get(autoTurret.Turret);
                    TurretSpecData spec = turret.Turret;
                    AutoTurretSpecData auto = turret.AutoTurret;

                    // 빈 칸이나 쏠 수 없는 포탑은 멈춰 있음.
                    if (spec.ProjectilePrefab == Entity.Null)
                    {
                        autoTurret.Firing = false;
                        autoTurrets.ElementAt(k) = autoTurret;
                        continue;
                    }

                    // 채널링 포탑(e.g. Pyron, Veylance)은 사격이 끊기는 즉시 휴식함.
                    bool channeling = turret.HeatMode == TurretHeatMode.PerSecond;
                    bool wasFiring = autoTurret.Firing;
                    var random = Random.CreateFromIndex(math.hash(new uint3((uint)spaceship.Index, (uint)k, tick)) & 0x7FFFFFFFu);

                    float2 pivot = transform.ValueRO.TransformPoint(new float3(mount.Pivot, 0.0F)).xy;
                    float2 center = math.mul(transform.ValueRO.Rotation, new float3(math.normalizesafe(mount.Barrel), 0.0F)).xy;

                    // 시야각, 탐지 거리(대상 선체 바깥까지) 안의 가장 가까운 적. 조준 중인 대상이 남아 있으면 계속 노림.
                    int target = -1;
                    float targetAngle = 0.0F;
                    float nearest = float.MaxValue;

                    for (int i = 0; i < hulls.Length; i++)
                    {
                        if (hulls[i] == spaceship
                            || hullSpecs[i].Health <= 0.0F
                            || hullFactions[i].Value == FactionID.None
                            || Faction.IsFriendly(faction.ValueRO, hullFactions[i]))
                        {
                            continue;
                        }

                        float2 toHull = ColliderOverlap.ToWorld(hullColliders[i], hullTransforms[i]).Center - pivot;
                        float range = auto.DetectRange + hullSpecs[i].Thickness * 0.5F;
                        float distanceSq = math.lengthsq(toHull);

                        // 가운데 방향 기준 대상 방향 각도. 반시계가 +.
                        float angle = math.atan2(
                            center.x * toHull.y - center.y * toHull.x,
                            math.dot(center, toHull)
                        );

                        if (distanceSq > range * range || math.abs(angle) > auto.HalfSightAngle)
                        {
                            continue;
                        }

                        if (hulls[i] == mount.Target)
                        {
                            target = i;
                            targetAngle = angle;
                            break;
                        }

                        if (distanceSq < nearest)
                        {
                            target = i;
                            targetAngle = angle;
                            nearest = distanceSq;
                        }
                    }

                    if (target >= 0)
                    {
                        mount.Target = hulls[target];

                        float step = auto.TurningSpeed * deltaTime;
                        autoTurret.Angle += math.clamp(targetAngle - autoTurret.Angle, -step, step);

                        mount.CycleRemaining -= deltaTime;

                        if (mount.CycleRemaining <= 0.0F)
                        {
                            mount.Resting = !mount.Resting;
                            mount.CycleRemaining = mount.Resting
                                ? random.NextFloat(auto.RestDurationRange.x, auto.RestDurationRange.y)
                                : auto.FireDuration;
                        }

                        // 대상을 향했을 때만 사격.
                        autoTurret.Firing = !mount.Resting
                                            && math.abs(targetAngle - autoTurret.Angle) <= config.AutoTurretAimAngle;
                    }
                    else
                    {
                        mount.Target = Entity.Null;
                        autoTurret.Firing = false;

                        // 채널링 포탑은 대상이 없어도 휴식을 마저 셈. 나머지는 다음 교전을 사격부터 시작함.
                        if (channeling && mount.Resting)
                        {
                            mount.CycleRemaining -= deltaTime;

                            if (mount.CycleRemaining <= 0.0F)
                            {
                                mount.Resting = false;
                                mount.CycleRemaining = auto.FireDuration;
                            }
                        }
                        else
                        {
                            mount.Resting = false;
                            mount.CycleRemaining = auto.FireDuration;
                        }

                        if (mount.SweepWait > 0.0F)
                        {
                            mount.SweepWait -= deltaTime;
                        }
                        else
                        {
                            autoTurret.Angle += mount.SweepDirection * config.AutoTurretSweepSpeed * deltaTime;

                            // 끝에 닿으면 기다렸다가 반대쪽으로 훑음.
                            if (math.abs(autoTurret.Angle) >= auto.HalfSightAngle)
                            {
                                autoTurret.Angle = math.clamp(autoTurret.Angle, -auto.HalfSightAngle, auto.HalfSightAngle);
                                mount.SweepDirection = autoTurret.Angle > 0.0F ? -1.0F : 1.0F;
                                mount.SweepWait = config.AutoTurretSweepWait;
                            }
                        }
                    }

                    // 남은 사격 시간을 다음 교전으로 넘기지 않음.
                    if (channeling && wasFiring && !autoTurret.Firing && !mount.Resting)
                    {
                        mount.Resting = true;
                        mount.CycleRemaining = random.NextFloat(auto.RestDurationRange.x, auto.RestDurationRange.y);
                    }

                    shots.Clear();
                    cooldown[0] = mount.Cooldown;
                    TurretCycle.Advance(cooldown, spec, autoTurret.Firing, deltaTime, ref shots);
                    mount.Cooldown = cooldown[0];

                    autoTurrets.ElementAt(k) = autoTurret;
                    mounts.ElementAt(k) = mount;

                    if (shots.IsEmpty)
                    {
                        continue;
                    }

                    LocalTransform muzzle = AutoTurret.MuzzleLocation(mount, autoTurret.Angle);

                    float speed = SystemAPI.HasComponent<StraightforwardProjectileComponent>(spec.ProjectilePrefab)
                        ? SystemAPI.GetComponent<StraightforwardProjectileComponent>(spec.ProjectilePrefab).Speed
                        : 0.0F;

                    foreach (TurretShotRequest request in shots)
                    {
                        for (uint j = 0; j < spec.ProjectileCount; j++)
                        {
                            // 조종 포탑 총구 번호와 겹치지 않도록 64 부터 셈.
                            uint seed = math.hash(new uint4((uint)spaceship.Index, tick, 64u + (uint)k, request.Shot * 64 + j));

                            spawns.Add(new ProjectileSpawnMessage
                            {
                                Prefab             = spec.ProjectilePrefab,
                                Spawner            = spaceship,
                                Muzzle             = TurretShot.Aim(muzzle, spec, seed, out float speedScale),

                                // 주인 없음(-1). 주인 클라이언트도 예측하지 않고 보간함.
                                // 예측하면 서버가 쏜 틱부터 따라잡느라 총구에서 멀리 떨어져 나타남.
                                // 처치 기록은 쏜 우주선의 주인으로 남음. (ProjectileDamageSystem)
                                NetworkId          = -1,
                                SpawnId            = seed,
                                Turret             = autoTurret.Turret,
                                Damage             = spec.Damage,

                                // 서버가 직접 쏘므로 되감을 보간 지연이 없음.
                                InterpolationDelay = 0,
                                Speed              = speed * speedScale,

                                // 총구 방향으로 나아가는 우주선 속도 성분만 물려줌.
                                InheritAxis        = muzzle.Up().xy,
                            });
                        }
                    }
                }
            }
        }
    }
}