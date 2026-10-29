using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Spaceship;
using GalaxyIO.Tools;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 스캐너가 훑고 지나간 주변 우주선을 점으로 표시하는 클라이언트 전용 레이더 뷰.
    /// 레이더 위에서 마우스 휠로 표시 거리를 바꿈. 파티원과 같은 편 함재기는 스캔과 관계없이 실시간으로 그림.
    /// </summary>
    public sealed class RadarView : MonoBehaviour
    {
        // 한 번에 띄우는 탐지 점, 표시 거리 밖 파티원 호의 상한
        private const int MaxBlips = 32;
        private const int MaxMembers = 16;

        private static readonly int AngleId       = Shader.PropertyToID("_Angle");
        private static readonly int RangeRadiusId = Shader.PropertyToID("_RangeRadius");
        private static readonly int SweepAlphaId  = Shader.PropertyToID("_SweepAlpha");
        private static readonly int RadiusId      = Shader.PropertyToID("_Radius");
        private static readonly int GridSpacingId = Shader.PropertyToID("_GridSpacing");
        private static readonly int GridOffsetId  = Shader.PropertyToID("_GridOffset");

        [SerializeField]
        [Tooltip("빔, 잔상, 테두리를 그리는 스윕 이미지. UI-RadarSweep 머티리얼")]
        [FormerlySerializedAs("scanner")]
        private Image sweep;

        [SerializeField]
        [Tooltip("씬에 배치된 탐지 점 템플릿. 스윕 이미지와 중심이 같은 부모 아래")]
        private Image blipTemplate;

        [SerializeField]
        [Tooltip("씬에 배치된 표시 거리 밖 파티원 호 템플릿. 피벗이 레이더 중심")]
        private Image memberTemplate;

        [SerializeField]
        [Tooltip("기수 방향 화살표")]
        private RectTransform heading;

        [SerializeField]
        [Tooltip("UI-RadarBackground 머티리얼을 쓰는 이미지")]
        private Image background;

        [SerializeField]
        [Tooltip("로컬 플레이어의 월드 좌표를 띄우는 텍스트")]
        private TMP_Text coordinates;

        [SerializeField]
        [Tooltip("스캐너 빔의 기본 알파")]
        [Range(0.0F, 1.0F)]
        private float sweepAlpha = 0.5F;

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
        [Tooltip("배경 격자 한 칸의 크기(월드 유닛). 표시 거리가 늘면 격자가 촘촘해짐.")]
        [Min(1.0F)]
        private float gridCellSize = 48.0F;

        [Header("View Range")]

        [SerializeField]
        [Tooltip("레이더 원 반지름이 나타내는 거리(월드 유닛)의 최솟값")]
        [Min(1.0F)]
        private float minViewRange = 50.0F;

        [SerializeField]
        [Tooltip("레이더 원 반지름이 나타내는 거리(월드 유닛)의 최댓값")]
        [Min(1.0F)]
        private float maxViewRange = 250.0F;

        [SerializeField]
        [Tooltip("휠 한 칸당 표시 거리 변화 비율")]
        [Range(0.01F, 0.5F)]
        private float zoomStep = 0.15F;

        [Header("Blip Size")]

        [SerializeField]
        [Tooltip("점 크기가 기본(1배)인 선체 두께")]
        [Min(0.01F)]
        private float referenceThickness = 1.2F;

        [SerializeField]
        [Tooltip("점 크기 배율 범위. 두께의 제곱근을 따름.")]
        private Vector2 blipScaleRange = new Vector2(0.8F, 3.0F);

        [Header("Blip Color")]

        [SerializeField]
        [Tooltip("AI 점 색")]
        private Color blipColor = new Color(0.2F, 1.0F, 1.0F, 1.0F);

        [SerializeField]
        [Tooltip("다른 플레이어와 그 함재기 점 색")]
        private Color playerBlipColor = new Color(1.0F, 0.6F, 0.2F, 1.0F);

        [SerializeField]
        [Tooltip("같은 편(파티원, 함재기) 점 색")]
        private Color friendlyBlipColor = new Color(0.35F, 1.0F, 0.5F, 1.0F);

        [Header("Party")]

        [SerializeField]
        [Tooltip("파티원 위치를 이 시간(초) 동안 못 받으면 지움")]
        [Min(0.1F)]
        private float memberTimeout = 1.5F;

        private struct Blip
        {
            // 탐지 시점의 로컬 우주선 기준 월드 좌표 차이
            public Vector2 Offset;

            public float ShowTime;

            // 점 종류. 0: AI, 1: 다른 플레이어와 그 함재기, 2: 같은 편(파티원, 함재기)
            public float Kind;

            // 점 크기 배율
            public float Scale;
        }

        private struct Member
        {
            public Vector2 Position;

            // 마지막으로 위치를 받은 시각
            public float Time;

            // 점 크기 배율. Ghost 를 본 적이 없으면 1.
            public float Scale;
        }

        private EntityManager entityManager;
        private EntityQuery playerQuery;
        private EntityQuery targetQuery;
        private EntityQuery memberQuery;
        private EntityQuery partyQuery;
        private EntityQuery craftQuery;
        private EntityQuery stateQuery;

        private Material material;
        private Material backgroundMaterial;

        // 레이더가 속한 HUD 캔버스. 게임에 들어가기 전에는 꺼져 있음.
        private Canvas canvas;

        // 로컬 우주선의 레이더 기본값과 강화를 반영한 값. 우주선이 없으면 마지막 값을 유지함.
        // 스캐너 회전 속도(도/초)는 시계 방향.
        private float sweepSpeed;
        private float sweepBonus;
        private float range;
        private float response;
        private float fadeOut;

        // 레이더 원 반지름이 나타내는 거리. 처음 탄 우주선의 탐지 거리로 시작함.
        private float viewRange;

        // 고정 프레임마다 갱신되는 스캐너 각도(도). 화면에는 둘 사이를 보간해 그림.
        private float previousAngle;
        private float angle;

        // 표시 중인 좌표. 바뀔 때만 문자열을 만들어 GC 할당을 막음.
        private Vector2Int shownCoordinates = new Vector2Int(int.MinValue, int.MinValue);

        // 표시 시각 순
        private readonly Queue<Blip> blips = new Queue<Blip>();
        private readonly Vector4[] blipBuffer = new Vector4[MaxBlips];
        private readonly float[] blipScaleBuffer = new float[MaxBlips];

        // 파티원 NetworkId -> 위치. 복제 범위 안이면 Ghost, 밖이면 서버가 보낸 위치.
        private readonly Dictionary<int, Member> members = new Dictionary<int, Member>();
        private readonly List<int> expiredMembers = new List<int>();
        private readonly Vector4[] memberBuffer = new Vector4[MaxMembers];

        // 템플릿 복제본. 앞에서부터 쓰고 남는 것은 끔.
        private readonly List<Image> blipImages = new List<Image>();
        private readonly List<Image> memberImages = new List<Image>();

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        /// <summary>
        /// 레이더 지도가 덮는 거리(현재 표시 거리).
        /// </summary>
        public float ViewRange => viewRange;

        /// <summary>
        /// 화면 좌표가 레이더 원 안이면 그 자리의 월드 좌표를 줌.
        /// </summary>
        public bool TryGetWorldPoint(Vector2 screen, out Vector2 world)
        {
            world = Vector2.zero;

            if (!ClientWorldScope.IsAlive(clientWorld) || playerQuery.IsEmpty || viewRange <= 0.0F
                || !TryGetRadarPoint(screen, out Vector2 point))
            {
                return false;
            }

            using var players = playerQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            world = (Vector2)players[0].Position.xy + point / material.GetFloat(RadiusId) * viewRange;

            return true;
        }

        // 셰이더와 같은 좌표. 이미지 중심이 원점, 가장자리가 1. 레이더 원 반지름은 _Radius. 원 밖이면 false.
        private bool TryGetRadarPoint(Vector2 screen, out Vector2 point)
        {
            point = Vector2.zero;

            if (canvas == null || !canvas.enabled)
            {
                return false;
            }

            UnityEngine.Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(sweep.rectTransform, screen, uiCamera, out Vector2 local))
            {
                return false;
            }

            point = Rect.PointToNormalized(sweep.rectTransform.rect, local) * 2.0F - Vector2.one;

            return point.magnitude <= material.GetFloat(RadiusId);
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
            memberQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PartyMemberPositionRPC>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>()
            );
            partyQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<PlayerNameComponent, GhostOwner, FactionComponent, SpaceshipSpecComponent, LocalTransform>()
                .WithNone<GhostOwnerIsLocal>()
                .Build(entityManager);
            craftQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CarriedCraftComponent>(),
                ComponentType.ReadOnly<FactionComponent>(),
                ComponentType.ReadOnly<SpaceshipSpecComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            stateQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<PlayerStateComponent>());

            canvas = sweep.GetComponentInParent<Canvas>();

            // 각도, 탐지 거리는 이 뷰 전용이라 머티리얼을 복제함.
            material = new Material(sweep.material);
            sweep.material = material;

            // 템플릿은 디자인 원본. 화면에는 복제본만 띄움.
            if (blipTemplate != null)
            {
                blipTemplate.gameObject.SetActive(false);
            }

            if (memberTemplate != null)
            {
                memberTemplate.gameObject.SetActive(false);
            }

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

                if (viewRange <= 0.0F)
                {
                    viewRange = Mathf.Clamp(range, minViewRange, maxViewRange);
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

            if (!memberQuery.IsEmpty)
            {
                using var received = memberQuery.ToComponentDataArray<PartyMemberPositionRPC>(Allocator.Temp);

                foreach (PartyMemberPositionRPC rpc in received)
                {
                    members[rpc.NetworkId] = new Member
                    {
                        Position = new Vector2(rpc.Position.x, rpc.Position.y),
                        Time     = Time.time,
                        Scale    = members.TryGetValue(rpc.NetworkId, out Member old) ? old.Scale : 1.0F,
                    };
                }

                entityManager.DestroyEntity(memberQuery);
            }

            // 레이더 위에서 휠을 굴리면 표시 거리를 바꿈. 휠 위 = 가까이. 카메라 확대는 막음.
            RefRW<PlayerStateComponent> state = stateQuery.GetSingletonRW<PlayerStateComponent>();
            bool blocked = state.ValueRO.GameMenuOpened || state.ValueRO.InventoryOpened || state.ValueRO.ChatFocused;
            bool hovered = Mouse.current != null && Application.isFocused && !blocked
                           && TryGetRadarPoint(Mouse.current.position.ReadValue(), out _);

            if (hovered && viewRange > 0.0F)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;

                if (scroll != 0.0F)
                {
                    viewRange = Mathf.Clamp(viewRange * (1.0F - Mathf.Sign(scroll) * zoomStep), minViewRange, maxViewRange);
                }
            }

            state.ValueRW.RadarHovered = hovered;

            // 마지막 고정 프레임 이후 흐른 비율만큼 보간.
            float t = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            material.SetFloat(AngleId, Mathf.Repeat(Mathf.Lerp(previousAngle, angle, t), 360.0F));

            // 빨리 돌수록 빔이 투명해짐.
            material.SetFloat(
                SweepAlphaId,
                sweepAlpha * (1.0F - Mathf.InverseLerp(sweepFadeStartSpeed, sweepFadeEndSpeed, sweepSpeed + sweepBonus))
            );

            UpdateCoordinates();

            float radius = material.GetFloat(RadiusId);

            // 파티원, 같은 편 함재기 점이 먼저 자리를 차지하고, 스캔된 점이 뒤를 채움.
            int count = 0;

            // 우주선이 없으면 격자, 화살표, 파티원 표시는 마지막 상태를 유지함.
            if (viewRange > 0.0F && !playerQuery.IsEmpty)
            {
                using var players = playerQuery.ToEntityArray(Allocator.Temp);
                LocalTransform ship = entityManager.GetComponentData<LocalTransform>(players[0]);
                Vector2 center = ship.Position.xy;
                uint party = entityManager.GetComponentData<FactionComponent>(players[0]).Party;

                // 격자 간격과 위치를 레이더 좌표로 환산. 스캐너 반지름 = 표시 거리.
                // 격자는 월드 좌표에 고정이라 우주선이 움직인 만큼 밀림.
                float scale = radius / viewRange;
                float spacing = gridCellSize * scale;

                backgroundMaterial.SetFloat(GridSpacingId, spacing);
                backgroundMaterial.SetVector(GridOffsetId, new Vector4(
                    Mathf.Repeat(center.x * scale, spacing),
                    Mathf.Repeat(center.y * scale, spacing)
                ));

                material.SetFloat(RangeRadiusId, range * scale);

                // 위쪽 0도, 시계 방향. UI 회전은 반시계가 양수.
                if (heading != null)
                {
                    float2 forward = SpaceshipHeading.FromRotation(ship.Rotation);
                    heading.localEulerAngles = new Vector3(0.0F, 0.0F, -Mathf.Atan2(forward.x, forward.y) * Mathf.Rad2Deg);
                }

                // 복제 범위 안의 파티원은 Ghost 위치로 덮어씀.
                using var ghosts = partyQuery.ToEntityArray(Allocator.Temp);

                foreach (Entity ghost in ghosts)
                {
                    if (Faction.IsSameParty(party, entityManager.GetComponentData<FactionComponent>(ghost).Party))
                    {
                        members[entityManager.GetComponentData<GhostOwner>(ghost).NetworkId] = new Member
                        {
                            Position = entityManager.GetComponentData<LocalTransform>(ghost).Position.xy,
                            Time     = Time.time,
                            Scale    = BlipScale(entityManager.GetComponentData<SpaceshipSpecComponent>(ghost).Thickness),
                        };
                    }
                }

                // 표시 거리 안의 파티원은 점으로, 밖은 테두리에 방향만 그림.
                int memberCount = 0;

                foreach (KeyValuePair<int, Member> member in members)
                {
                    if (Time.time - member.Value.Time > memberTimeout)
                    {
                        expiredMembers.Add(member.Key);
                        continue;
                    }

                    Vector2 offset = member.Value.Position - center;

                    if (offset.sqrMagnitude <= viewRange * viewRange)
                    {
                        if (count < MaxBlips)
                        {
                            blipScaleBuffer[count] = member.Value.Scale;
                            blipBuffer[count++] = new Vector4(offset.x / viewRange, offset.y / viewRange, 1.0F, 2.0F);
                        }
                    }
                    else if (memberCount < MaxMembers)
                    {
                        memberBuffer[memberCount++] = new Vector4(Mathf.Atan2(offset.x, offset.y), 0.0F);
                    }
                }

                foreach (int expired in expiredMembers)
                {
                    members.Remove(expired);
                }

                expiredMembers.Clear();

                ApplyMembers(memberCount);

                // 같은 편 함재기는 표시 거리 안이면 실시간 점으로. 복제 범위 밖은 위치를 모르므로 그리지 않음.
                using var crafts = craftQuery.ToEntityArray(Allocator.Temp);

                foreach (Entity craft in crafts)
                {
                    if (count >= MaxBlips)
                    {
                        break;
                    }

                    if (!Faction.IsSameParty(party, entityManager.GetComponentData<FactionComponent>(craft).Party))
                    {
                        continue;
                    }

                    Vector2 offset = (Vector2)entityManager.GetComponentData<LocalTransform>(craft).Position.xy - center;

                    if (offset.sqrMagnitude <= viewRange * viewRange)
                    {
                        blipScaleBuffer[count] = BlipScale(entityManager.GetComponentData<SpaceshipSpecComponent>(craft).Thickness);
                        blipBuffer[count++] = new Vector4(offset.x / viewRange, offset.y / viewRange, 1.0F, 2.0F);
                    }
                }
            }

            while (blips.Count > 0 && Time.time - blips.Peek().ShowTime >= fadeOut)
            {
                blips.Dequeue();
            }

            foreach (Blip blip in blips)
            {
                if (count >= MaxBlips)
                {
                    break;
                }

                float elapsed = Time.time - blip.ShowTime;

                // 반응 시간 대기 중. 뒤쪽은 더 늦게 뜨므로 중단.
                if (elapsed < 0.0F)
                {
                    break;
                }

                // 표시 거리 밖은 그리지 않음.
                Vector2 offset = blip.Offset / viewRange;

                if (offset.sqrMagnitude > 1.0F)
                {
                    continue;
                }

                blipScaleBuffer[count] = blip.Scale;
                blipBuffer[count++] = new Vector4(
                    offset.x,
                    offset.y,
                    1.0F - elapsed / fadeOut,
                    blip.Kind
                );
            }

            ApplyBlips(count, radius);
        }

        // 버퍼의 점을 템플릿 복제본으로 배치. xy: 레이더 좌표(1 = 표시 거리), z: 알파, w: 종류
        private void ApplyBlips(int count, float radius)
        {
            if (blipTemplate == null)
            {
                return;
            }

            // 스윕 이미지 반지름(px) × 셰이더 스캔 반지름
            float pixels = sweep.rectTransform.rect.width * 0.5F * radius;

            for (int i = 0; i < count; i++)
            {
                Image image = Rent(blipImages, blipTemplate, i);
                Vector4 blip = blipBuffer[i];
                Color color = blip.w > 1.5F ? friendlyBlipColor : blip.w > 0.5F ? playerBlipColor : blipColor;

                color.a *= blip.z;
                image.color = color;
                image.rectTransform.anchoredPosition = new Vector2(blip.x, blip.y) * pixels;
                image.rectTransform.localScale = Vector3.one * blipScaleBuffer[i];
            }

            Hide(blipImages, count);
        }

        // 표시 거리 밖 파티원을 테두리 호로 배치. x: 방위각(라디안, 위쪽 0, 시계 방향)
        private void ApplyMembers(int count)
        {
            if (memberTemplate == null)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Image image = Rent(memberImages, memberTemplate, i);
                image.rectTransform.localEulerAngles = new Vector3(0.0F, 0.0F, -memberBuffer[i].x * Mathf.Rad2Deg);
            }

            Hide(memberImages, count);
        }

        // 풀에서 index 번째 복제본을 켜서 줌. 모자라면 템플릿을 복제함.
        private static Image Rent(List<Image> pool, Image template, int index)
        {
            while (pool.Count <= index)
            {
                pool.Add(Instantiate(template, template.transform.parent));
            }

            pool[index].gameObject.SetActive(true);
            return pool[index];
        }

        private static void Hide(List<Image> pool, int shown)
        {
            for (int i = shown; i < pool.Count; i++)
            {
                pool[i].gameObject.SetActive(false);
            }
        }

        // from ~ to 각도 사이를 지나간 우주선을 점으로 예약함.
        private void Sweep(float from, float to)
        {
            if (playerQuery.IsEmpty)
            {
                return;
            }

            using var players = playerQuery.ToEntityArray(Allocator.Temp);
            using var targets = targetQuery.ToEntityArray(Allocator.Temp);

            Vector2 center = entityManager.GetComponentData<LocalTransform>(players[0]).Position.xy;
            uint party = entityManager.GetComponentData<FactionComponent>(players[0]).Party;
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

                FactionComponent faction = entityManager.GetComponentData<FactionComponent>(target);

                // 파티원 우주선과 같은 편 함재기는 스캔과 관계없이 실시간으로 그림.
                if (Faction.IsSameParty(party, faction.Party)
                    && (entityManager.HasComponent<PlayerNameComponent>(target) || entityManager.HasComponent<CarriedCraftComponent>(target)))
                {
                    continue;
                }

                if (blips.Count >= MaxBlips)
                {
                    blips.Dequeue();
                }

                blips.Enqueue(new Blip
                {
                    Offset   = offset,
                    ShowTime = Time.time + response,
                    Kind     = Faction.IsSameParty(party, faction.Party) ? 2.0F
                        : entityManager.HasComponent<PlayerNameComponent>(target)
                          || entityManager.HasComponent<CarriedCraftComponent>(target) ? 1.0F
                        : 0.0F,
                    Scale    = BlipScale(entityManager.GetComponentData<SpaceshipSpecComponent>(target).Thickness),
                });
            }
        }

        // 선체 두께의 제곱근을 따르는 점 크기 배율
        private float BlipScale(float thickness)
        {
            return Mathf.Clamp(Mathf.Sqrt(thickness / referenceThickness), blipScaleRange.x, blipScaleRange.y);
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