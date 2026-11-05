using System.Collections.Generic;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Spaceship;
using GalaxyIO.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using Random = UnityEngine.Random;

namespace GalaxyIO.Scripts.Managed.Effects
{
    /// <summary>
    /// 우주선 추진, 충전 연출을 그리는 클라이언트 전용 뷰. 모든 우주선(플레이어, 봇)에 적용됨.
    /// <list type="bullet">
    /// <item>엔진 불빛: 기종별 노즐 자리(<see cref="SpaceshipEngineLayout"/>)에서 항상 빛남. 멈춤 / 호버링 / 가속으로 세기가 달라짐.</item>
    /// <item>가속 불꽃: 비행 속력(호버링 최고 속력 초과)일 때만 노즐 뒤로 뻗는 불꽃.</item>
    /// <item>잔상 불똥: 추진 세기에 비례해 노즐에서 뿌림. 기체 속도를 물려받아 뒤로 길게 남음.</item>
    /// <item>충전 빛: 충전 포탑(ReleaseFire)을 충전하는 동안 총구에서 커지고, 다 차면 떨리며 하얗게 빛남.</item>
    /// </list>
    /// 추진 상태는 복제되는 속도로 판정함(입력은 다른 클라이언트에 복제되지 않음).
    /// 모든 사각형을 메쉬 하나에 모아 한 번에 그림.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SpaceshipVfxView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("기종별 엔진 자리")]
        private SpaceshipEngineLayout layout;

        [SerializeField]
        [Tooltip("가산 이펙트 머티리얼(GalaxyIO/FX Additive). 텍스처는 실행 중 코드로 만든 아틀라스를 넣음.")]
        private Material material;

        [Header("추진")]

        [SerializeField]
        [Tooltip("엔진 불빛 색(HDR). 내 우주선과 다른 우주선 공통.")]
        [ColorUsage(true, true)]
        private Color engineColor = new Color(1.6F, 0.75F, 0.3F, 1.0F);

        [SerializeField]
        [Tooltip("가속 불꽃 색(HDR). 뿌리는 하얗고 끝은 이 색.")]
        [ColorUsage(true, true)]
        private Color boostColor = new Color(0.55F, 1.1F, 2.2F, 1.0F);

        [SerializeField]
        [Tooltip("불똥 색(HDR)")]
        [ColorUsage(true, true)]
        private Color emberColor = new Color(1.8F, 0.6F, 0.2F, 1.0F);

        [SerializeField]
        [Tooltip("추진 세기: 멈춤 / 호버링 이동 / 가속")]
        private Vector3 thrustLevels = new Vector3(0.08F, 0.45F, 1.0F);

        [SerializeField]
        [Tooltip("추진 세기가 바뀌는 빠르기(1/초)")]
        [Min(0.0F)]
        private float thrustResponse = 8.0F;

        [SerializeField]
        [Tooltip("엔진 불빛 크기: 노즐 반지름 x (기본 + 추진 x 이 값)")]
        private Vector2 glowScale = new Vector2(1.2F, 2.6F);

        [SerializeField]
        [Tooltip("가속 불꽃 길이: 노즐 반지름 x 이 범위(매 프레임 일렁임)")]
        private Vector2 flameLength = new Vector2(4.0F, 6.5F);

        [SerializeField]
        [Tooltip("추진 1 일 때 초당 불똥 수(노즐 하나)")]
        [Min(0.0F)]
        private float emberRate = 45.0F;

        [SerializeField]
        [Tooltip("불똥 수명 범위(초). 가속 중이면 1.4 배.")]
        private Vector2 emberLifetime = new Vector2(0.18F, 0.38F);

        [SerializeField]
        [Tooltip("불똥이 기체 속도를 물려받는 비율")]
        [Range(0.0F, 1.0F)]
        private float emberInherit = 0.6F;

        [SerializeField]
        [Tooltip("불똥이 뒤로 뿜어지는 속력 범위(월드 단위/초)")]
        private Vector2 emberSpeed = new Vector2(6.0F, 16.0F);

        [SerializeField]
        [Tooltip("화면에 남길 불똥 최대 수")]
        [Min(0)]
        private int maxEmbers = 900;

        [Header("충전")]

        [SerializeField]
        [Tooltip("충전 빛 색(HDR). 다 차면 하얗게 겹침.")]
        [ColorUsage(true, true)]
        private Color chargeColor = new Color(0.8F, 0.6F, 2.4F, 1.0F);

        [SerializeField]
        [Tooltip("충전 빛 크기 범위(월드 단위). 0 -> 다 참.")]
        private Vector2 chargeSize = new Vector2(0.4F, 1.6F);

        [SerializeField]
        [Tooltip("내 우주선 충전이 다 찬 순간 한 번 재생. 비우면 소리 없음.")]
        private string chargeReadySound = "UI_Press";

        private struct Ember
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public float Size;
        }

        private readonly List<Ember> embers = new List<Ember>(1024);
        private readonly Dictionary<Entity, float> thrust = new Dictionary<Entity, float>();
        private readonly Dictionary<Entity, float> emberDebt = new Dictionary<Entity, float>();
        private readonly HashSet<Entity> seen = new HashSet<Entity>();
        private readonly List<Entity> stale = new List<Entity>();

        private FxQuadBatch batch;
        private MeshRenderer meshRenderer;
        private World clientWorld;
        private EntityQuery shipQuery;
        private EntityQuery registryQuery;
        private bool sortingSet;
        private bool localCharged;

        private void Awake()
        {
            // 서버에서 비활성화함
            if (Application.isBatchMode)
            {
                gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            batch = new FxQuadBatch("SpaceshipVfx");
            GetComponent<MeshFilter>().sharedMesh = batch.Mesh;
            meshRenderer = GetComponent<MeshRenderer>();

            if (material == null)
            {
                material = new Material(Shader.Find("GalaxyIO/FX Additive"));
            }
            else
            {
                material = new Material(material);
            }

            material.mainTexture = FxAtlas.Texture;
            meshRenderer.sharedMaterial = material;

            shipQuery = clientWorld.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SpaceshipKindComponent>(),
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<MovementComponent>(),
                ComponentType.ReadOnly<PlayerSpaceshipMovementComponent>(),
                ComponentType.ReadOnly<SpaceshipSpecComponent>()
            );
            registryQuery = clientWorld.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ItemRegistryElement>());
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                batch.Begin();
                batch.End();
                enabled = false;
                return;
            }

            float deltaTime = Time.deltaTime;
            EntityManager entityManager = clientWorld.EntityManager;
            using NativeArray<Entity> ships = shipQuery.ToEntityArray(Allocator.Temp);
            DynamicBuffer<ItemRegistryElement> registry = registryQuery.IsEmpty
                ? default
                : registryQuery.GetSingletonBuffer<ItemRegistryElement>(true);

            batch.Begin();
            seen.Clear();
            bool anyLocalCharged = false;

            foreach (Entity ship in ships)
            {
                seen.Add(ship);
                SpaceshipSpecComponent spec = entityManager.GetComponentData<SpaceshipSpecComponent>(ship);

                if (spec.Health <= 0.0F)
                {
                    continue;
                }

                LocalTransform transform = entityManager.GetComponentData<LocalTransform>(ship);
                Vector2 position = new Vector2(transform.Position.x, transform.Position.y);
                float2 heading = math.normalizesafe(SpaceshipHeading.FromRotation(transform.Rotation), new float2(0.0F, 1.0F));
                Vector2 forward = new Vector2(heading.x, heading.y);
                Vector2 right = new Vector2(forward.y, -forward.x);
                float2 rawVelocity = entityManager.GetComponentData<MovementComponent>(ship).Velocity;
                Vector2 velocity = new Vector2(rawVelocity.x, rawVelocity.y);
                PlayerSpaceshipMovementComponent movement = entityManager.GetComponentData<PlayerSpaceshipMovementComponent>(ship);

                // 정렬: 처음 본 우주선 스프라이트 바로 아래에 그림.
                if (!sortingSet && entityManager.HasComponent<CompanionComponent<SpriteRenderer>>(ship))
                {
                    SpriteRenderer body = entityManager.GetCompanion<SpriteRenderer>(ship);

                    if (body != null)
                    {
                        meshRenderer.sortingLayerID = body.sortingLayerID;
                        meshRenderer.sortingOrder = body.sortingOrder - 1;
                        sortingSet = true;
                    }
                }

                DrawThrusters(entityManager, ship, position, forward, right, velocity, movement, deltaTime);

                if (registry.IsCreated && DrawCharge(entityManager, ship, transform, registry))
                {
                    anyLocalCharged |= entityManager.HasComponent<GhostOwnerIsLocal>(ship);
                }
            }

            // 내 충전이 다 찬 순간 한 번 알림.
            if (anyLocalCharged && !localCharged && !string.IsNullOrEmpty(chargeReadySound)
                && Newmoonhana.Runtime.Core.SoundManager.Instance != null)
            {
                Newmoonhana.Runtime.Core.SoundManager.Instance.PlaySfx(chargeReadySound);
            }

            localCharged = anyLocalCharged;

            UpdateEmbers(deltaTime);

            // 사라진 우주선 정리
            stale.Clear();

            foreach (Entity ship in thrust.Keys)
            {
                if (!seen.Contains(ship))
                {
                    stale.Add(ship);
                }
            }

            foreach (Entity ship in stale)
            {
                thrust.Remove(ship);
                emberDebt.Remove(ship);
            }

            batch.End();
        }

        private void DrawThrusters(
            EntityManager entityManager,
            Entity ship,
            Vector2 position,
            Vector2 forward,
            Vector2 right,
            Vector2 velocity,
            in PlayerSpaceshipMovementComponent movement,
            float deltaTime)
        {
            if (layout == null
                || !layout.TryGet(entityManager.GetComponentData<SpaceshipKindComponent>(ship).Value, out List<SpaceshipEngineLayout.Engine> engines))
            {
                return;
            }

            // 비행 속력이면 가속, 움직이면 호버링, 아니면 멈춤.
            float speed = velocity.magnitude;
            bool boosting = speed > movement.HoveringSpeed * 1.05F + 0.1F;
            float target = boosting ? thrustLevels.z : speed > 0.3F ? thrustLevels.y : thrustLevels.x;

            thrust.TryGetValue(ship, out float current);
            current = Mathf.Lerp(current, target, 1.0F - Mathf.Exp(-thrustResponse * deltaTime));
            thrust[ship] = current;

            emberDebt.TryGetValue(ship, out float debt);
            debt += Mathf.Max(current - 0.25F, 0.0F) * emberRate * deltaTime * engines.Count;
            float flicker = 0.8F + 0.2F * Mathf.Sin(Time.time * 40.0F + ship.Index * 3.0F);

            foreach (SpaceshipEngineLayout.Engine engine in engines)
            {
                Vector2 nozzle = position + right * engine.Position.x + forward * engine.Position.y;
                float radius = Mathf.Max(engine.Radius, 0.05F);

                // 엔진 불빛
                float glow = radius * (glowScale.x + current * glowScale.y) * (flicker + Random.Range(-0.08F, 0.08F));
                Color glowColor = engineColor * (0.35F + current * 0.6F);
                batch.Add(nozzle, new Vector2(glow * 2.0F, glow * 2.0F), forward, FxAtlas.Glow, glowColor);

                // 가속 불꽃: 노즐에서 뒤로 뻗음
                if (boosting)
                {
                    float length = radius * Random.Range(flameLength.x, flameLength.y) * Mathf.Clamp01(current);
                    batch.Add(nozzle, new Vector2(radius * 1.4F, length), forward, FxAtlas.Flame, boostColor, 1.0F);
                }
            }

            // 불똥: 쌓인 개수만큼 노즐을 돌아가며 뿌림.
            int spawn = Mathf.FloorToInt(debt);
            debt -= spawn;
            emberDebt[ship] = debt;

            for (int i = 0; i < spawn && embers.Count < maxEmbers; i++)
            {
                SpaceshipEngineLayout.Engine engine = engines[Random.Range(0, engines.Count)];
                float radius = Mathf.Max(engine.Radius, 0.05F);
                Vector2 nozzle = position + right * (engine.Position.x + Random.Range(-radius, radius) * 0.3F) + forward * engine.Position.y;
                float angle = Random.Range(-0.25F, 0.25F);
                Vector2 back = -(forward * Mathf.Cos(angle) + right * Mathf.Sin(angle));
                float life = Random.Range(emberLifetime.x, emberLifetime.y) * (boosting ? 1.4F : 1.0F);

                embers.Add(new Ember
                {
                    Position = nozzle,
                    Velocity = velocity * emberInherit + back * Random.Range(emberSpeed.x, emberSpeed.y) * (0.5F + current),
                    Age = 0.0F,
                    Life = life,
                    Size = radius * Random.Range(0.8F, 1.3F),
                });
            }
        }

        /// <summary>
        /// 충전 포탑을 충전 중이면 총구마다 빛을 그림. 다 찼으면 true.
        /// </summary>
        private bool DrawCharge(EntityManager entityManager, Entity ship, in LocalTransform transform, DynamicBuffer<ItemRegistryElement> registry)
        {
            if (!entityManager.HasComponent<TurretSwitchComponent>(ship)
                || !entityManager.HasComponent<EquippedTurretComponent>(ship)
                || !entityManager.HasBuffer<TurretElement>(ship))
            {
                return false;
            }

            ItemRegistryElement turret = registry.Get(entityManager.GetComponentData<EquippedTurretComponent>(ship).Value);
            TurretSwitchComponent turretSwitch = entityManager.GetComponentData<TurretSwitchComponent>(ship);

            if (!turret.ReleaseFire || turretSwitch.State != TurretSwitchState.SpinningUp || turret.SwitchTime <= 0.0F)
            {
                return false;
            }

            float ratio = Mathf.Clamp01(1.0F - turretSwitch.Remaining / turret.SwitchTime);
            bool full = ratio >= 0.999F;
            float size = Mathf.Lerp(chargeSize.x, chargeSize.y, ratio) + (full ? Mathf.Sin(Time.time * 40.0F) * 0.15F : 0.0F);
            Color color = chargeColor * (0.5F + ratio * 0.5F);

            foreach (TurretElement muzzle in entityManager.GetBuffer<TurretElement>(ship, true))
            {
                float3 world = transform.TransformPoint(muzzle.MuzzleLocation.Position);
                Vector2 point = new Vector2(world.x, world.y);
                batch.Add(point, new Vector2(size, size), Vector2.up, FxAtlas.Glow, color);

                if (full)
                {
                    batch.Add(point, new Vector2(size * 0.6F, size * 0.6F), Vector2.up, FxAtlas.Glow, Color.white * 1.5F);
                }
            }

            return full;
        }

        private void UpdateEmbers(float deltaTime)
        {
            for (int i = embers.Count - 1; i >= 0; i--)
            {
                Ember ember = embers[i];
                ember.Age += deltaTime;

                if (ember.Age >= ember.Life)
                {
                    embers[i] = embers[embers.Count - 1];
                    embers.RemoveAt(embers.Count - 1);
                    continue;
                }

                ember.Position += ember.Velocity * deltaTime;
                embers[i] = ember;

                float t = ember.Age / ember.Life;
                float size = ember.Size * (1.0F - t * 0.6F);
                batch.Add(ember.Position, new Vector2(size, size), Vector2.up, FxAtlas.Dot, emberColor * (1.0F - t));
            }
        }
    }
}
