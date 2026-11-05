using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.Tools;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 우주선 선체 아래에 체력바를 그리는 클라이언트 전용 뷰.
    /// </summary>
    public sealed class SpaceshipHealthBars : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("씬에 배치된 체력바 템플릿")]
        private HealthBarView barTemplate;

        [SerializeField]
        [Tooltip("선체 아래쪽 끝과 체력바 사이 간격")]
        private float gap = 0.2F;

        private EntityManager entityManager;
        private EntityQuery spaceshipQuery;

        // 우주선별 체력바. 바가 다른 우주선으로 옮겨 다니지 않게 고정함.
        private readonly Dictionary<Entity, HealthBarView> active = new Dictionary<Entity, HealthBarView>();
        private readonly Stack<HealthBarView> pool = new Stack<HealthBarView>();
        private readonly List<Entity> released = new List<Entity>();

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        private void Awake()
        {
            barTemplate.gameObject.SetActive(false);
        }

        private void Start()
        {
            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            entityManager = clientWorld.EntityManager;
            spaceshipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SpaceshipSpecComponent>(),
                ComponentType.ReadOnly<ColliderComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            using var spaceships = spaceshipQuery.ToEntityArray(Allocator.Temp);

            int frame = Time.frameCount;

            foreach (Entity spaceship in spaceships)
            {
                var spec = entityManager.GetComponentData<SpaceshipSpecComponent>(spaceship);

                // 체력이 가득 찬 우주선은 표시하지 않음.
                if (spec.MaxHealth <= math.EPSILON || spec.Health >= spec.MaxHealth)
                {
                    continue;
                }

                var transformData = entityManager.GetComponentData<LocalTransform>(spaceship);

                // 이름표와 같이 회전한 선체의 아래쪽 끝에 붙임.
                Aabb bounds = ColliderBounds.ToAabb(
                    ColliderOverlap.ToWorld(entityManager.GetComponentData<ColliderComponent>(spaceship), transformData)
                );

                if (!active.TryGetValue(spaceship, out HealthBarView bar))
                {
                    bar = Rent();
                    active.Add(spaceship, bar);
                }

                bar.SeenFrame = frame;
                bar.transform.position = new Vector3(
                    transformData.Position.x,
                    bounds.Min.y - gap,
                    transformData.Position.z
                );

                bar.SetRatio(math.saturate(spec.Health / spec.MaxHealth));
            }

            // 이번 프레임에 쓰이지 않은 바는 반납함. 체력이 다 찼거나 우주선이 사라진 경우.
            released.Clear();

            foreach (KeyValuePair<Entity, HealthBarView> pair in active)
            {
                if (pair.Value.SeenFrame != frame) released.Add(pair.Key);
            }

            foreach (Entity entity in released)
            {
                HealthBarView bar = active[entity];
                bar.gameObject.SetActive(false);
                pool.Push(bar);
                active.Remove(entity);
            }
        }

        private HealthBarView Rent()
        {
            HealthBarView bar = pool.Count > 0 ? pool.Pop() : Instantiate(barTemplate, transform);
            bar.gameObject.SetActive(true);
            return bar;
        }
    }
}