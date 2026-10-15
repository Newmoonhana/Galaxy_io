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
        private static readonly int SweepAlphaId  = Shader.PropertyToID("_SweepAlpha");
        private static readonly int RadiusId      = Shader.PropertyToID("_Radius");
        private static readonly int GridSpacingId = Shader.PropertyToID("_GridSpacing");
        private static readonly int GridOffsetId  = Shader.PropertyToID("_GridOffset");

        [SerializeField]
        [Tooltip("UI-RadarScanner 머티리얼을 쓰는 이미지")]
        private Image scanner;

        [SerializeField]
        [Tooltip("UI-RadarBackground 머티리얼을 쓰는 이미지")]
        private Image background;

        [SerializeField]
        [Tooltip("로컬 플레이어의 월드 좌표를 띄우는 텍스트")]
        private TMP_Text coordinates;

        [SerializeField]
        [Tooltip("스캐너 빔이 투명해지기 시작하는 회전 속도(도/초)")]
        [Min(0.0F)]
        private float sweepFadeStartSpeed = 360.0F;

        [SerializeField]
        [Tooltip("스캐너 빔이 완전히 보이지 않게 되는 회전 속도(도/초)")]
        [Min(0.0F)]
        private float sweepFadeEndSpeed = 5000.0F;

        [SerializeField]
        [Tooltip("점이 사라지는 데 걸리는 시간(초). 스캐너가 기본 회전 속도일 때의 값.")]
        [Min(0.01F)]
        private float fadeOutTime = 2.0F;

        [SerializeField]
        [Tooltip("스캐너가 가장 빨리 돌 때 점이 사라지는 데 걸리는 시간(초)")]
        [Min(0.01F)]
        private float minFadeOutTime = 0.05F;

        [SerializeField]
        [Tooltip("점이 사라지는 시간이 최소가 되는 스캐너 회전 속도(도/초)")]
        [Min(0.0F)]
        private float fastestSweepSpeed = 7200.0F;

        [SerializeField]
        [Tooltip("배경 격자 한 칸의 크기(월드 유닛). 탐지 거리가 늘면 격자가 촘촘해짐.")]
        [Min(1.0F)]
        private float gridCellSize = 48.0F;

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

        private Material material;
        private Material backgroundMaterial;

        // 로컬 우주선의 레이더 기본값과 강화를 반영한 값. 우주선이 없으면 마지막 값을 유지함.
        // 스캐너 회전 속도(도/초)는 시계 방향.
        private float sweepSpeed;
        private float sweepBonus;
        private float range;
        private float response;
        private float fadeOut;

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
                ComponentType.ReadOnly<SpaceshipRadarComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            targetQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<SpaceshipSpecComponent, LocalTransform>()
                .WithNone<GhostOwnerIsLocal>()
                .Build(entityManager);

            // 각도, 점 목록은 이 뷰 전용이라 머티리얼을 복제함.
            material = new Material(scanner.material);
            scanner.material = material;

            backgroundMaterial = new Material(background.material);
            background.material = backgroundMaterial;

            fadeOut = fadeOutTime;
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }

            if (backgroundMaterial != null)
            {
                Destroy(backgroundMaterial);
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

            if (!playerQuery.IsEmpty)
            {
                using var players = playerQuery.ToEntityArray(Allocator.Temp);
                var radar = entityManager.GetComponentData<SpaceshipRadarComponent>(players[0]);

                sweepSpeed = radar.SweepSpeed;
                response = radar.ResponseTime;
                range = radar.Range;
                sweepBonus = 0.0F;

                // 강화가 없는 기체는 기본값 그대로.
                if (entityManager.HasBuffer<UpgradeParamElement>(players[0]))
                {
                    var parameters = entityManager.GetBuffer<UpgradeParamElement>(players[0], true);
                    var levels = entityManager.GetBuffer<UpgradeLevelElement>(players[0], true);

                    sweepBonus = Upgrade.Value(parameters, levels, UpgradeParam.RadarSweep);
                    range += Upgrade.Value(parameters, levels, UpgradeParam.RadarRange);
                    response = Mathf.Max(response - Upgrade.Value(parameters, levels, UpgradeParam.RadarResponse), 0.0F);
                }

                // 스캐너가 빨리 돌수록 점도 빨리 사라짐.
                fadeOut = Mathf.Lerp(
                    fadeOutTime,
                    minFadeOutTime,
                    Mathf.InverseLerp(sweepSpeed, fastestSweepSpeed, sweepSpeed + sweepBonus)
                );
            }

            previousAngle = angle;
            angle += (sweepSpeed + sweepBonus) * Time.fixedDeltaTime;

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

            // 빨리 돌수록 빔이 투명해짐.
            material.SetFloat(
                SweepAlphaId,
                1.0F - Mathf.InverseLerp(sweepFadeStartSpeed, sweepFadeEndSpeed, sweepSpeed + sweepBonus)
            );

            UpdateCoordinates();

            // 격자 간격과 위치를 레이더 좌표로 환산. 스캐너 반지름 = 탐지 거리.
            // 격자는 월드 좌표에 고정이라 우주선이 움직인 만큼 밀림. 우주선이 없으면 마지막 위치를 유지함.
            if (range > 0.0F && !playerQuery.IsEmpty)
            {
                using var players = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

                float scale = material.GetFloat(RadiusId) / range;
                float spacing = gridCellSize * scale;

                backgroundMaterial.SetFloat(GridSpacingId, spacing);
                backgroundMaterial.SetVector(GridOffsetId, new Vector4(
                    Mathf.Repeat(players[0].Position.x * scale, spacing),
                    Mathf.Repeat(players[0].Position.y * scale, spacing)
                ));
            }

            while (blips.Count > 0 && Time.time - blips.Peek().ShowTime >= fadeOut)
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
                    1.0F - elapsed / fadeOut,
                    blip.IsPlayer ? 1.0F : 0.0F
                );
            }

            material.SetVectorArray(BlipsId, blipBuffer);
            material.SetFloat(BlipCountId, count);
        }

        // from ~ to 각도 사이를 지나간 우주선을 점으로 예약함.
        private void Sweep(float from, float to)
        {
            if (playerQuery.IsEmpty)
            {
                return;
            }

            using var players = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var targets = targetQuery.ToEntityArray(Allocator.Temp);

            Vector2 center = players[0].Position.xy;
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
                    ShowTime = Time.time + response,
                    IsPlayer = entityManager.HasComponent<PlayerNameComponent>(target),
                });
            }
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