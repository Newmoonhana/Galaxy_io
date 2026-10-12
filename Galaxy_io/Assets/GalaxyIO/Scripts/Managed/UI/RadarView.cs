using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Tools;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 스캐너가 훑고 지나간 주변 우주선을 점으로 표시하는 클라이언트 전용 레이더 뷰.
    /// </summary>
    public sealed class RadarView : MonoBehaviour
    {
        // UI-RadarScanner 셰이더의 MAX_BLIPS 와 같아야 함.
        private const int MaxBlips = 32;

        private static readonly int AngleId     = Shader.PropertyToID("_Angle");
        private static readonly int BlipsId     = Shader.PropertyToID("_Blips");
        private static readonly int BlipCountId = Shader.PropertyToID("_BlipCount");

        [SerializeField]
        [Tooltip("UI-RadarScanner 머티리얼을 쓰는 이미지")]
        private Image scanner;

        [SerializeField]
        [Tooltip("로컬 플레이어의 월드 좌표를 띄우는 텍스트")]
        private TMP_Text coordinates;

        [SerializeField]
        [Tooltip("스캐너 회전 속도(도/초). 시계 방향.")]
        [Min(0.0F)]
        private float sweepSpeed = 120.0F;

        [SerializeField]
        [Tooltip("스캐너가 지나간 뒤 점이 나타나기까지의 시간(초)")]
        [Min(0.0F)]
        private float responseTime = 0.1F;

        [SerializeField]
        [Tooltip("점이 사라지는 데 걸리는 시간(초)")]
        [Min(0.01F)]
        private float fadeOutTime = 2.0F;

        private struct Blip
        {
            // 탐지 시점의 레이더 좌표. 1 = 탐지 거리.
            public Vector2 Offset;

            public float ShowTime;

            public bool IsPlayer;
        }

        private EntityManager entityManager;
        private EntityQuery playerQuery;
        private EntityQuery targetQuery;
        private EntityQuery rangeQuery;

        private Material material;

        // 고정 프레임마다 갱신되는 스캐너 각도(도). 화면에는 둘 사이를 보간해 그림.
        private float previousAngle;
        private float angle;

        // 표시 중인 좌표. 바뀔 때만 문자열을 만들어 GC 할당을 막음.
        private Vector2Int shownCoordinates = new Vector2Int(int.MinValue, int.MinValue);

        // 표시 시각 순
        private readonly Queue<Blip> blips = new Queue<Blip>();
        private readonly Vector4[] blipBuffer = new Vector4[MaxBlips];

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        private void Start()
        {
            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            entityManager = clientWorld.EntityManager;
            playerQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<PlayerSpaceshipMovementComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            targetQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SpaceshipSpecComponent, LocalTransform>()
                .WithNone<GhostOwnerIsLocal>()
                .Build(entityManager);
            rangeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<RelevancyConfigComponent>());

            // 각도, 점 목록은 이 뷰 전용이라 머티리얼을 복제함.
            material = new Material(scanner.material);
            scanner.material = material;
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        private void FixedUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            previousAngle = angle;
            angle += sweepSpeed * UpgradeValue(UpgradeStat.RadarSweep, 1.0F) * Time.fixedDeltaTime;

            Sweep(previousAngle, angle);

            // 두 각도를 같이 되돌려야 보간이 끊기지 않음.
            if (angle >= 360.0F)
            {
                previousAngle -= 360.0F;
                angle -= 360.0F;
            }
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            // 마지막 고정 프레임 이후 흐른 비율만큼 보간.
            float t = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            material.SetFloat(AngleId, Mathf.Repeat(Mathf.Lerp(previousAngle, angle, t), 360.0F));

            UpdateCoordinates();

            while (blips.Count > 0 && Time.time - blips.Peek().ShowTime >= fadeOutTime)
            {
                blips.Dequeue();
            }

            int count = 0;

            foreach (Blip blip in blips)
            {
                float elapsed = Time.time - blip.ShowTime;

                // 반응 시간 대기 중. 뒤쪽은 더 늦게 뜨므로 중단.
                if (elapsed < 0.0F)
                {
                    break;
                }

                blipBuffer[count++] = new Vector4(
                    blip.Offset.x,
                    blip.Offset.y,
                    1.0F - elapsed / fadeOutTime,
                    blip.IsPlayer ? 1.0F : 0.0F
                );
            }

            material.SetVectorArray(BlipsId, blipBuffer);
            material.SetFloat(BlipCountId, count);
        }

        // from ~ to 각도 사이를 지나간 우주선을 점으로 예약함.
        private void Sweep(float from, float to)
        {
            if (playerQuery.IsEmpty || rangeQuery.IsEmpty)
            {
                return;
            }

            using var players = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var targets = targetQuery.ToEntityArray(Allocator.Temp);

            Vector2 center = players[0].Position.xy;
            float range = rangeQuery.GetSingleton<RelevancyConfigComponent>().Radius
                          + UpgradeValue(UpgradeStat.RadarRange, 0.0F);
            float swept = to - from;

            foreach (Entity target in targets)
            {
                var targetTransform = entityManager.GetComponentData<LocalTransform>(target);
                Vector2 offset = (Vector2)targetTransform.Position.xy - center;

                if (offset.sqrMagnitude > range * range)
                {
                    continue;
                }

                // 위쪽 0도, 시계 방향
                float bearing = Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg;

                if (Mathf.Repeat(bearing - from, 360.0F) >= swept)
                {
                    continue;
                }

                if (blips.Count >= MaxBlips)
                {
                    blips.Dequeue();
                }

                blips.Enqueue(new Blip
                {
                    Offset   = offset / range,
                    ShowTime = Time.time + responseTime,
                    IsPlayer = entityManager.HasComponent<PlayerNameComponent>(target),
                });
            }
        }

        // 로컬 우주선의 강화한 능력치 값. 우주선이 없거나 강화가 없는 기체면 fallback.
        private float UpgradeValue(UpgradeStat stat, float fallback)
        {
            if (playerQuery.IsEmpty)
            {
                return fallback;
            }

            using var players = playerQuery.ToEntityArray(Allocator.Temp);

            return entityManager.HasBuffer<UpgradeElement>(players[0])
                ? Upgrade.Value(
                    entityManager.GetBuffer<UpgradeElement>(players[0], true),
                    entityManager.GetBuffer<UpgradeLevelElement>(players[0], true),
                    stat)
                : fallback;
        }

        // 로컬 우주선이 없으면 마지막 좌표를 유지함.
        private void UpdateCoordinates()
        {
            if (playerQuery.IsEmpty)
            {
                return;
            }

            using var players = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            var rounded = new Vector2Int(
                Mathf.RoundToInt(players[0].Position.x),
                Mathf.RoundToInt(players[0].Position.y)
            );

            if (rounded != shownCoordinates)
            {
                shownCoordinates = rounded;
                coordinates.text = $"X {rounded.x}  Y {rounded.y}";
            }
        }
    }
}
