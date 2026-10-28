using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.DOTS.Client.Systems
{
    /// <summary>
    /// 광선 투사체(<see cref="ProjectileBeamComponent"/>)를 쏘는 총구에 광선 연출을 붙이는 시스템.
    /// 사격 전환 상태를 따라 단계를 나눔.
    /// - 발사/충전(SpinningUp): 총구에 에너지 구가 생겨 커짐.
    /// - 폭발(Firing 진입): 고리와 가루가 터지고 구와 광선이 잠깐 부풀었다 돌아옴.
    /// - 사격(Firing): 광선이 기수 방향으로 늘어나고 양 끝의 구에서 가루가 퍼짐.
    /// - 중지: 광선과 구가 가늘어지다 사라짐.
    /// 충전 포탑은 몸통 없는 광선 프리팹을 써서 충전 구만 그림. 쏘거나 취소하면 구가 줄어들며 사라짐.
    /// 충전 중에는 구가 떨리고 둘레의 가루가 총구로 빨려듦.
    /// 광선은 사거리까지 늘어나고 처음 닿는 적 우주선에서 멈춤(클라이언트 판정). 데미지는 서버의 투사체가 줌.
    /// 자동 포탑은 충전 없이 복제된 사격 여부(<see cref="AutoTurretElement.Firing"/>)를 따름.
    /// 자동 포탑의 광선은 총구 번호를 조종 포탑 총구 뒤에 이어 붙임.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct ProjectileBeamSystem : ISystem
    {
        // 번짐 셰이더(Sprite-BeamGlow)에 광선마다 넘기는 모양 값.
        private static readonly MaterialPropertyBlock GlowProperties = new MaterialPropertyBlock();
        private static readonly int BlurScaleId = Shader.PropertyToID("_BlurScale");
        private static readonly int SizeId = Shader.PropertyToID("_Size");
        private static readonly int LengthId = Shader.PropertyToID("_Length");
        private static readonly int StartRadiusId = Shader.PropertyToID("_StartRadius");
        private static readonly int EndRadiusId = Shader.PropertyToID("_EndRadius");
        private static readonly int HalfWidthId = Shader.PropertyToID("_HalfWidth");

        private EntityQuery beamQuery;
        private EntityQuery hullQuery;

        public void OnCreate(ref SystemState state)
        {
            beamQuery = SystemAPI.QueryBuilder().WithAll<BeamComponent>().Build();

            hullQuery = SystemAPI.QueryBuilder()
                .WithAll<SpaceshipSpecComponent, ColliderComponent, FactionComponent, LocalTransform>()
                .Build();

            state.RequireForUpdate<ItemRegistryElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            EntityManager entityManager = state.EntityManager;
            float deltaTime = SystemAPI.Time.DeltaTime;

            Entity registryEntity = SystemAPI.GetSingletonEntity<ItemRegistryElement>();
            DynamicBuffer<ItemRegistryElement> registry = SystemAPI.GetBuffer<ItemRegistryElement>(registryEntity);

            // 발사했는데 광선이 없는 총구. 스폰은 구조 변경이라 순회 뒤에 처리함.
            NativeArray<BeamComponent> beams = beamQuery.ToComponentDataArray<BeamComponent>(Allocator.Temp);
            var pending = new NativeList<BeamComponent>(Allocator.Temp);

            foreach (
                var (turretSwitch, equipped, muzzles, autoTurrets, spaceship) in
                SystemAPI.Query<
                    RefRO<TurretSwitchComponent>,
                    RefRO<EquippedTurretComponent>,
                    DynamicBuffer<TurretElement>,
                    DynamicBuffer<AutoTurretElement>
                >().WithEntityAccess()
            ) {
                Entity projectile = registry.Get(equipped.ValueRO.Value).Turret.ProjectilePrefab;

                // 총구마다 광선 하나.
                if ((turretSwitch.ValueRO.State == TurretSwitchState.SpinningUp
                     || turretSwitch.ValueRO.State == TurretSwitchState.Firing)
                    && projectile != Entity.Null
                    && SystemAPI.HasComponent<ProjectileBeamComponent>(projectile))
                {
                    for (int i = 0; i < muzzles.Length; i++)
                    {
                        Request(beams, pending, spaceship, i, projectile);
                    }
                }

                for (int k = 0; k < autoTurrets.Length; k++)
                {
                    Entity autoProjectile = registry.Get(autoTurrets[k].Turret).Turret.ProjectilePrefab;

                    if (autoTurrets[k].Firing
                        && autoProjectile != Entity.Null
                        && SystemAPI.HasComponent<ProjectileBeamComponent>(autoProjectile))
                    {
                        Request(beams, pending, spaceship, muzzles.Length + k, autoProjectile);
                    }
                }
            }

            beams.Dispose();

            foreach (BeamComponent spawn in pending)
            {
                Entity instance = entityManager.Instantiate(entityManager.GetComponentData<ProjectileBeamComponent>(spawn.Projectile).Prefab);

                BeamComponent beam = entityManager.GetComponentData<BeamComponent>(instance);
                beam.Owner = spawn.Owner;
                beam.Muzzle = spawn.Muzzle;
                beam.Projectile = spawn.Projectile;
                entityManager.SetComponentData(instance, beam);
            }

            pending.Dispose();

            NativeArray<Entity> hulls = hullQuery.ToEntityArray(Allocator.Temp);
            NativeArray<LocalTransform> hullTransforms = hullQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            NativeArray<ColliderComponent> hullColliders = hullQuery.ToComponentDataArray<ColliderComponent>(Allocator.Temp);
            NativeArray<FactionComponent> hullFactions = hullQuery.ToComponentDataArray<FactionComponent>(Allocator.Temp);

            NativeArray<Entity> instances = beamQuery.ToEntityArray(Allocator.Temp);
            var finished = new NativeList<Entity>(Allocator.Temp);

            foreach (Entity instance in instances)
            {
                BeamComponent beam = entityManager.GetComponentData<BeamComponent>(instance);

                // 쏘는 우주선이 사라졌거나 복제 범위를 벗어남.
                if (!entityManager.Exists(beam.Owner) || !entityManager.HasComponent<TurretSwitchComponent>(beam.Owner))
                {
                    finished.Add(instance);
                    continue;
                }

                LocalTransform ownerTransform = entityManager.GetComponentData<LocalTransform>(beam.Owner);
                DynamicBuffer<TurretElement> muzzles = entityManager.GetBuffer<TurretElement>(beam.Owner, true);
                int auto = beam.Muzzle - muzzles.Length;
                LocalTransform muzzle;
                bool charging;
                bool firing;

                // 포탑을 바꿔 끼우면 사격을 멈춘 것으로 봄.
                if (auto >= 0)
                {
                    AutoTurretElement autoTurret = entityManager.GetBuffer<AutoTurretElement>(beam.Owner, true)[auto];
                    AutoTurretMountElement mount = entityManager.GetBuffer<AutoTurretMountElement>(beam.Owner, true)[auto];

                    muzzle = ownerTransform.TransformTransform(AutoTurret.MuzzleLocation(mount, mount.VisualAngle));
                    charging = false;
                    firing = autoTurret.Firing && registry.Get(autoTurret.Turret).Turret.ProjectilePrefab == beam.Projectile;
                }
                else
                {
                    ItemId turret = entityManager.GetComponentData<EquippedTurretComponent>(beam.Owner).Value;
                    TurretSwitchComponent turretSwitch = entityManager.GetComponentData<TurretSwitchComponent>(beam.Owner);
                    bool equipped = registry.Get(turret).Turret.ProjectilePrefab == beam.Projectile;

                    muzzle = ownerTransform.TransformTransform(muzzles[beam.Muzzle].MuzzleLocation);
                    charging = equipped && turretSwitch.State == TurretSwitchState.SpinningUp;
                    firing = equipped && turretSwitch.State == TurretSwitchState.Firing;

                    if (charging)
                    {
                        // 사라지는 중에 다시 쏘면 충전부터 다시 함.
                        float switchTime = registry.Get(turret).SwitchTime;
                        beam.Charge = switchTime > 0.0F ? math.saturate(1.0F - turretSwitch.Remaining / switchTime) : 1.0F;
                        beam.Fired = false;
                        beam.Length = 0.0F;
                    }
                }

                if (firing && !beam.Fired)
                {
                    beam.Fired = true;
                    beam.BurstRemaining = beam.BurstTime;

                    // 스폰한 프레임에는 아직 프리팹 위치에 있으므로 총구로 옮기고 재생함.
                    if (beam.BurstRing != Entity.Null)
                    {
                        ParticleSystem ring = entityManager.GetCompanion<ParticleSystem>(beam.BurstRing);
                        ring.transform.SetPositionAndRotation(muzzle.Position, muzzle.Rotation);
                        ring.Play();
                    }

                    if (beam.BurstDust != Entity.Null)
                    {
                        ParticleSystem dust = entityManager.GetCompanion<ParticleSystem>(beam.BurstDust);
                        dust.transform.SetPositionAndRotation(muzzle.Position, muzzle.Rotation);
                        dust.Play();
                    }
                }

                beam.Thickness = charging || firing
                    ? 1.0F
                    : math.saturate(beam.Thickness - deltaTime / math.max(beam.NarrowTime, math.EPSILON));

                if (beam.Thickness <= 0.0F)
                {
                    finished.Add(instance);
                    continue;
                }

                // 폭발 순간 BurstScale 배로 부풀었다가 BurstTime 동안 줄어듦.
                beam.BurstRemaining = math.max(beam.BurstRemaining - deltaTime, 0.0F);
                float burst = beam.BurstTime > 0.0F ? beam.BurstRemaining / beam.BurstTime : 0.0F;
                float swell = 1.0F + (beam.BurstScale - 1.0F) * burst * burst;

                // 사거리 안에서 처음 닿는 적 우주선까지. 투사체 충돌체 폭으로 훑음.
                float2 origin = muzzle.Position.xy;
                float2 direction = muzzle.Up().xy;
                float range = entityManager.GetComponentData<ProjectileBeamComponent>(beam.Projectile).Range;
                float halfWidth = entityManager.HasComponent<ColliderComponent>(beam.Projectile)
                    ? entityManager.GetComponentData<ColliderComponent>(beam.Projectile).Extents.x
                    : 0.0F;

                var path = new ColliderWorld
                {
                    Shape   = ColliderShape.Obb,
                    Center  = origin + direction * (range * 0.5F),
                    AxisX   = new float2(direction.y, -direction.x),
                    AxisY   = direction,
                    Extents = new float2(halfWidth, range * 0.5F),
                };

                FactionComponent faction = entityManager.GetComponentData<FactionComponent>(beam.Owner);
                float reach = range;

                for (int i = 0; i < hulls.Length; i++)
                {
                    if (hulls[i] == beam.Owner || Faction.IsFriendly(faction, hullFactions[i]))
                    {
                        continue;
                    }

                    ColliderWorld hull = ColliderOverlap.ToWorld(hullColliders[i], hullTransforms[i]);

                    // 서버 명중 지점처럼 선체 중심을 광선에 내린 점에서 멈춤.
                    if (ColliderOverlap.Test(path, hull, out _))
                    {
                        reach = math.min(reach, math.max(math.dot(hull.Center - origin, direction), 0.0F));
                    }
                }

                // 사격 중에만 늘어남. 막히면 바로 줄어듦.
                beam.Length = firing
                    ? math.min(beam.Length + beam.GrowSpeed * deltaTime, reach)
                    : math.min(beam.Length, reach);

                entityManager.SetComponentData(instance, beam);

                // 프리팹의 크기는 그대로 쓰고 위치/회전만 총구를 따름.
                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(instance);
                transform.Position = muzzle.Position;
                transform.Rotation = muzzle.Rotation;
                entityManager.SetComponentData(instance, transform);

                // 충전 단계는 총구 쪽 구만 보임.
                float startSize = (beam.Fired ? beam.BallSize * swell : math.lerp(beam.LaunchSize, beam.WindupSize, beam.Charge))
                                  * beam.Thickness;
                float endSize = beam.Fired ? beam.BallSize * swell * beam.Thickness : 0.0F;

                // 충전 중 떨림. 매 프레임 크기와 위치를 지름의 ChargeJitter 배 안에서 흔듦.
                float3 jitter = float3.zero;

                if (charging && beam.ChargeJitter > 0.0F)
                {
                    float amount = beam.ChargeJitter * startSize;
                    Vector2 offset = UnityEngine.Random.insideUnitCircle * amount;
                    jitter = new float3(offset.x, offset.y, 0.0F);
                    startSize += UnityEngine.Random.Range(-amount, amount);
                }

                float bodyHalfWidth = 0.0F;

                if (beam.Body != Entity.Null)
                {
                    SpriteRenderer body = entityManager.GetCompanion<SpriteRenderer>(beam.Body);
                    float bodyWidth = beam.Width * swell * beam.Thickness;
                    body.enabled = beam.Fired;
                    body.size = new Vector2(bodyWidth, beam.Length);

                    // 양옆 투명 테두리를 뺀 보이는 폭. 폭이 테두리보다 좁으면 Sliced 가 테두리째 줄임.
                    Sprite sprite = body.sprite;
                    float border = (sprite.border.x + sprite.border.z) / sprite.pixelsPerUnit;
                    float visible = bodyWidth >= border
                        ? bodyWidth - border
                        : bodyWidth * (1.0F - border * sprite.pixelsPerUnit / sprite.rect.width);
                    bodyHalfWidth = beam.Fired ? visible * 0.5F : 0.0F;
                }

                if (beam.Glow != Entity.Null)
                {
                    // 몸통과 양 끝 구를 합친 모양에 번짐 폭을 더한 만큼 덮음. 모양 값은 셰이더가 거리 계산에 씀.
                    SpriteRenderer glow = entityManager.GetCompanion<SpriteRenderer>(beam.Glow);
                    float blur = glow.sharedMaterial.GetFloat(BlurScaleId);
                    float extent = math.max(math.max(startSize, endSize) * 0.5F, bodyHalfWidth) + blur;
                    var size = new Vector2(extent * 2.0F, beam.Length + extent * 2.0F);

                    glow.enabled = startSize > 0.0F;
                    glow.size = size;
                    entityManager.SetComponentData(beam.Glow, LocalTransform.FromPosition(jitter + new float3(0.0F, beam.Length * 0.5F, 0.0F)));

                    glow.GetPropertyBlock(GlowProperties);
                    GlowProperties.SetVector(SizeId, size);
                    GlowProperties.SetFloat(LengthId, beam.Length);
                    GlowProperties.SetFloat(StartRadiusId, startSize * 0.5F);
                    GlowProperties.SetFloat(EndRadiusId, endSize * 0.5F);
                    GlowProperties.SetFloat(HalfWidthId, bodyHalfWidth);
                    glow.SetPropertyBlock(GlowProperties);
                }

                // 크기 0 인 Transform 은 회전을 잃으므로 그리지 않고 아주 작게 둠.
                if (beam.StartBall != Entity.Null)
                {
                    entityManager.GetCompanion<SpriteRenderer>(beam.StartBall).enabled = startSize > 0.0F;
                    entityManager.SetComponentData(beam.StartBall, LocalTransform.FromPositionRotationScale(
                        jitter,
                        quaternion.identity,
                        math.max(startSize, 0.001F)
                    ));
                }

                if (beam.EndBall != Entity.Null)
                {
                    entityManager.GetCompanion<SpriteRenderer>(beam.EndBall).enabled = endSize > 0.0F;
                    entityManager.SetComponentData(beam.EndBall, LocalTransform.FromPositionRotationScale(
                        new float3(0.0F, beam.Length, 0.0F),
                        quaternion.identity,
                        math.max(endSize, 0.001F)
                    ));
                }

                if (beam.StartDust != Entity.Null)
                {
                    ParticleSystem.EmissionModule emission = entityManager.GetCompanion<ParticleSystem>(beam.StartDust).emission;
                    emission.enabled = firing;
                }

                if (beam.EndDust != Entity.Null)
                {
                    entityManager.SetComponentData(beam.EndDust, LocalTransform.FromPosition(0.0F, beam.Length, 0.0F));

                    ParticleSystem.EmissionModule emission = entityManager.GetCompanion<ParticleSystem>(beam.EndDust).emission;
                    emission.enabled = firing;
                }

                if (beam.ChargeDust != Entity.Null)
                {
                    // 다 차면 멈춤.
                    ParticleSystem.EmissionModule emission = entityManager.GetCompanion<ParticleSystem>(beam.ChargeDust).emission;
                    emission.enabled = charging && beam.Charge < 1.0F;
                }

                if (beam.Flow != Entity.Null)
                {
                    // 파티클이 로컬 공간에서 흐르도록 Entity 는 그대로 두고 방출 범위만 광선에 맞춤.
                    ParticleSystem flow = entityManager.GetCompanion<ParticleSystem>(beam.Flow);

                    ParticleSystem.ShapeModule shape = flow.shape;
                    shape.position = new Vector3(0.0F, beam.Length * 0.5F, 0.0F);
                    shape.scale = new Vector3(shape.scale.x, beam.Length, shape.scale.z);

                    ParticleSystem.EmissionModule emission = flow.emission;
                    emission.enabled = firing;

                    // 광선 끝(로컬 +Y 의 Length)을 넘은 가루는 지움. 광선이 막혀 짧아져도 넘어 흐르지 않음.
                    var particles = new NativeArray<ParticleSystem.Particle>(flow.particleCount, Allocator.Temp);
                    int count = flow.GetParticles(particles);

                    for (int i = 0; i < count; i++)
                    {
                        if (particles[i].position.y > beam.Length)
                        {
                            ParticleSystem.Particle particle = particles[i];
                            particle.remainingLifetime = 0.0F;
                            particles[i] = particle;
                        }
                    }

                    flow.SetParticles(particles, count);
                    particles.Dispose();
                }
            }

            entityManager.DestroyEntity(finished.AsArray());

            instances.Dispose();
            finished.Dispose();
            hulls.Dispose();
            hullTransforms.Dispose();
            hullColliders.Dispose();
            hullFactions.Dispose();
        }

        // 이 총구에 광선이 없으면 스폰 목록에 넣음.
        private static void Request(
            NativeArray<BeamComponent> beams,
            NativeList<BeamComponent> pending,
            Entity owner,
            int muzzle,
            Entity projectile)
        {
            foreach (BeamComponent beam in beams)
            {
                if (beam.Owner == owner && beam.Muzzle == muzzle)
                {
                    return;
                }
            }

            pending.Add(new BeamComponent { Owner = owner, Muzzle = muzzle, Projectile = projectile });
        }
    }
}