using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Tools;
using Newmoonhana.Runtime.Core;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 모선의 함재기 명령을 내리고, 지금 명령을 핑으로 그리는 클라이언트 전용 뷰. 살아 있는 함재기 수도 띄움.
    /// 적을 누르면 공격, 빈 곳이나 레이더는 지역 확보, 자기 모선은 호위(호위 중이거나 두 번 누르면 귀환).
    /// </summary>
    public sealed class CarrierOrderView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("PingView 와 같은 씬 템플릿. 템플릿을 끄는 것은 PingView 가 담당")]
        private PingMarkerView markerTemplate;

        [SerializeField]
        [Tooltip("명령 (마우스 왼쪽). 포탑과 같은 입력.")]
        private InputActionReference orderAction;

        [SerializeField]
        [Tooltip("누르면 그 자리를 지키는 명령을 내리는 레이더")]
        private RadarView radar;

        [SerializeField]
        [Tooltip("함재기 아이콘. 모선을 탔을 때만 보임. 아직 차지 않은 부분.")]
        private Image craftIcon;

        [SerializeField]
        [Tooltip("craftIcon 위에 겹친 아이콘. 다음 함재기 생산 진행률만큼 시계 방향으로 채움.")]
        private Image craftIconFill;

        [SerializeField]
        [Tooltip("살아 있는 함재기 수. 모선 안과 밖을 합침.")]
        private TMP_Text craftCount;

        [SerializeField]
        private Color color = new Color(1.0F, 0.75F, 0.3F);

        [SerializeField]
        [Tooltip("화면 밖 명령 지점을 그릴 때 화면 가장자리에서 띄우는 거리(캔버스 단위)")]
        [Min(0.0F)]
        private float edgeMargin = 12.0F;

        [SerializeField]
        [Tooltip("커서 주변에서 대상을 찾는 반경(캔버스 단위). 확대 배율과 관계없이 화면에서 같은 크기.")]
        [Min(0.0F)]
        private float pickRadius = 24.0F;

        [SerializeField]
        [Tooltip("이 시간(초) 안에 자기 모선을 두 번 누르면 귀환")]
        [Min(0.0F)]
        private float doubleClickTime = 0.35F;

        [SerializeField]
        [Tooltip("명령을 내리면 재생")]
        private string orderSound = "Ping";

        private EntityManager entityManager;
        private EntityQuery localShipQuery;
        private EntityQuery pickQuery;
        private EntityQuery stateQuery;
        private EntityQuery spaceshipRegistryQuery;

        private Canvas canvas;
        private PingMarkerView marker;

        // 다음 명령을 보낼 수 있는 시각. 귀환은 제외.
        private float nextOrderTime;

        // 자기 모선을 마지막으로 누른 시각
        private float lastSelfClickTime = float.NegativeInfinity;

        // 표시 중인 명령과 대상. 바뀔 때만 대상 이름과 글자를 다시 만들어 GC 할당을 막음.
        private CarrierOrder shownOrder;
        private Entity shownTarget;
        private string targetName;
        private int shownDistance = -1;
        private int shownTotal = -1;
        private SpaceshipId shownKind;

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
            localShipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<CarrierComponent>(),
                ComponentType.ReadOnly<SpaceshipKindComponent>(),
                ComponentType.ReadOnly<FactionComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );

            // 명령으로 가리킬 수 있는 우주선
            pickQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SpaceshipSpecComponent>(),
                ComponentType.ReadOnly<ColliderComponent>(),
                ComponentType.ReadOnly<FactionComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            stateQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerStateComponent>());
            spaceshipRegistryQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpaceshipRegistryAssetComponent>());

            canvas = GetComponentInParent<Canvas>();
            marker = Instantiate(markerTemplate, transform);
            marker.gameObject.SetActive(false);
            SetCraftCountVisible(false);
        }

        private void OnEnable()
        {
            orderAction?.action.Enable();
        }

        private void OnDisable()
        {
            orderAction?.action.Disable();
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;

            if (localShipQuery.IsEmpty || mainCamera == null)
            {
                marker.gameObject.SetActive(false);
                SetCraftCountVisible(false);
                return;
            }

            using var ships = localShipQuery.ToEntityArray(Allocator.Temp);
            Entity localShip = ships[0];
            CarrierComponent carrier = entityManager.GetComponentData<CarrierComponent>(localShip);
            SpaceshipId kind = entityManager.GetComponentData<SpaceshipKindComponent>(localShip).Value;
            Vector2 shipPosition = (Vector2)entityManager.GetComponentData<LocalTransform>(localShip).Position.xy;
            float scale = canvas.scaleFactor;

            // 메뉴, 채팅 입력창이 열려 있거나 커서가 채팅 탭 위에 있으면 명령하지 않음.
            PlayerStateComponent state = stateQuery.GetSingleton<PlayerStateComponent>();
            bool blocked = state.GameMenuOpened || state.InventoryOpened || state.ChatFocused || state.ChatHovered || state.Dead;

            if (orderAction.action.WasPressedThisFrame() && Application.isFocused && !blocked && Mouse.current != null)
            {
                Vector2 cursor = Mouse.current.position.ReadValue();
                var order = new CarrierOrderRPC();

                if (radar != null && radar.TryGetWorldPoint(cursor, out Vector2 radarPoint))
                {
                    order.Order = CarrierOrder.SecureArea;
                    order.Position = radarPoint;
                }
                else
                {
                    Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(cursor.x, cursor.y, -mainCamera.transform.position.z));
                    var point = new float2(world.x, world.y);

                    // 커서 주변 반경에 걸린 우주선 중 중심이 가장 가까운 것. 없으면 그 지역을 확보함.
                    order.Order = CarrierOrder.SecureArea;
                    order.Position = point;

                    // 캔버스 단위 반경을 월드 유닛으로 바꿈.
                    float radius = pickRadius * scale * mainCamera.orthographicSize * 2.0F / Screen.height;
                    var probe = new ColliderWorld { Shape = ColliderShape.Circle, Center = point, Extents = new float2(radius, 0.0F) };
                    float nearest = float.MaxValue;

                    using var entities = pickQuery.ToEntityArray(Allocator.Temp);

                    foreach (Entity entity in entities)
                    {
                        ColliderWorld collider = ColliderOverlap.ToWorld(
                            entityManager.GetComponentData<ColliderComponent>(entity),
                            entityManager.GetComponentData<LocalTransform>(entity)
                        );
                        float distanceSq = math.distancesq(collider.Center, point);

                        if (distanceSq >= nearest || !ColliderOverlap.Test(probe, collider, out _))
                        {
                            continue;
                        }

                        nearest = distanceSq;
                        order.Target = entity;
                    }

                    if (order.Target == localShip)
                    {
                        // 호위 중이거나 두 번 누르면 귀환, 아니면 호위.
                        bool doubleClick = Time.unscaledTime - lastSelfClickTime <= doubleClickTime;
                        lastSelfClickTime = doubleClick ? float.NegativeInfinity : Time.unscaledTime;

                        order.Order = doubleClick || carrier.Order == CarrierOrder.Escort ? CarrierOrder.Return : CarrierOrder.Escort;
                        order.Target = Entity.Null;
                    }
                    else if (order.Target != Entity.Null)
                    {
                        // 아군, 같은 편은 공격하지 않음.
                        order.Order = Faction.IsFriendly(
                            entityManager.GetComponentData<FactionComponent>(localShip),
                            entityManager.GetComponentData<FactionComponent>(order.Target))
                            ? CarrierOrder.None
                            : CarrierOrder.Attack;
                    }
                }

                // 명령 지점은 레이더 지도 안이어야 함.
                bool inRange = radar == null || order.Order == CarrierOrder.Attack
                               || Vector2.Distance(shipPosition, order.Position) <= radar.ViewRange;

                if (order.Order == CarrierOrder.Return
                    || (order.Order != CarrierOrder.None && inRange && Time.unscaledTime >= nextOrderTime))
                {
                    if (order.Order != CarrierOrder.Return)
                    {
                        nextOrderTime = Time.unscaledTime + carrier.OrderCooldown;
                    }

                    Entity rpcEntity = entityManager.CreateEntity(typeof(CarrierOrderRPC), typeof(SendRpcCommandRequest));
                    entityManager.SetComponentData(rpcEntity, order);

                    if (SoundManager.Instance != null)
                    {
                        SoundManager.Instance.PlaySfx(orderSound);
                    }
                }
            }

            UpdateCraftCount(carrier, kind);

            bool showing = carrier.Order == CarrierOrder.Attack
                           || carrier.Order == CarrierOrder.SecureArea
                           || carrier.Order == CarrierOrder.Escort;

            marker.gameObject.SetActive(showing);

            if (!showing)
            {
                shownOrder = CarrierOrder.None;
                return;
            }

            // 공격 지점은 대상을 실시간으로 따라감. 대상이 복제 범위 밖이면 서버가 보낸 위치.
            bool targetVisible = carrier.Target != Entity.Null && entityManager.HasComponent<LocalTransform>(carrier.Target);
            Vector2 position = carrier.Order == CarrierOrder.Escort
                ? shipPosition
                : targetVisible
                    ? (Vector2)entityManager.GetComponentData<LocalTransform>(carrier.Target).Position.xy
                    : (Vector2)carrier.Position;

            // 공격 대상이 늦게 복제되면 그때 이름을 채움.
            bool changed = carrier.Order != shownOrder || carrier.Target != shownTarget
                           || (targetName == null && carrier.Order == CarrierOrder.Attack && targetVisible);

            if (changed)
            {
                shownOrder = carrier.Order;
                shownTarget = carrier.Target;
                targetName = carrier.Order == CarrierOrder.Escort ? L10n.Get($"Spaceship/{kind}")
                    : carrier.Order == CarrierOrder.Attack && targetVisible ? TargetName(carrier.Target)
                    : null;
            }

            int distance = Mathf.RoundToInt(Vector2.Distance(shipPosition, position));

            if (changed || distance != shownDistance)
            {
                shownDistance = distance;

                string text = $"{L10n.Get($"Order/{carrier.Order}")}({distance}m)";
                marker.Set(targetName != null ? $"{text}\n{targetName}" : text, color);
            }

            Vector2 anchor = PingMarkerView.Anchor(mainCamera, position, edgeMargin * scale, out Vector2 direction);
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)transform, anchor, canvas.worldCamera, out Vector3 markerPosition);

            // 글자는 화면 가운데 쪽 옆에 붙임. 위쪽 절반이면 아래로, 아래쪽 절반이면 위로 늘어남.
            Vector2 safeCenter = Screen.safeArea.center;
            var side = new Vector2(anchor.x < safeCenter.x ? 1.0F : -1.0F, anchor.y > safeCenter.y ? 1.0F : -1.0F);

            marker.transform.position = markerPosition;
            marker.Place(direction, side);
        }

        // 공격 대상의 표시 이름. AI 는 이름, 플레이어는 기종, 함재기는 함재기.
        private string TargetName(Entity target)
        {
            if (entityManager.HasComponent<EnemyNameComponent>(target))
            {
                return entityManager.GetComponentData<EnemyNameComponent>(target).Value.ToString();
            }

            if (entityManager.HasComponent<CarriedCraftComponent>(target))
            {
                return L10n.Get("Order/CarriedCraft");
            }

            return L10n.Get($"Spaceship/{entityManager.GetComponentData<SpaceshipKindComponent>(target).Value}");
        }

        // 살아 있는 함재기 수, 기종의 함재기 아이콘과 생산 진행률. 수와 아이콘은 바뀔 때만 다시 씀.
        private void UpdateCraftCount(in CarrierComponent carrier, SpaceshipId kind)
        {
            SetCraftCountVisible(true);

            if (craftIcon != null && kind != shownKind && !spaceshipRegistryQuery.IsEmpty)
            {
                shownKind = kind;

                SpaceshipAsset spaceship = spaceshipRegistryQuery.GetSingleton<SpaceshipRegistryAssetComponent>().Value.Find(kind);
                CarrierAuthoring carrierAuthoring = spaceship != null && spaceship.Prefab != null
                    ? spaceship.Prefab.GetComponent<CarrierAuthoring>()
                    : null;
                SpriteRenderer craftSprite = carrierAuthoring != null && carrierAuthoring.CarriedCraft != null
                    ? carrierAuthoring.CarriedCraft.GetComponent<SpriteRenderer>()
                    : null;

                craftIcon.sprite = craftSprite != null ? craftSprite.sprite : null;

                if (craftIconFill != null)
                {
                    craftIconFill.sprite = craftIcon.sprite;
                }
            }

            if (craftIconFill != null)
            {
                craftIconFill.fillAmount = carrier.AssemblyProgress;
            }

            if (craftCount != null && carrier.Total != shownTotal)
            {
                shownTotal = carrier.Total;
                craftCount.text = $"x{carrier.Total}";
            }
        }

        private void SetCraftCountVisible(bool visible)
        {
            if (craftIcon != null && craftIcon.gameObject.activeSelf != visible)
            {
                craftIcon.gameObject.SetActive(visible);
            }

            if (craftCount != null && craftCount.gameObject.activeSelf != visible)
            {
                craftCount.gameObject.SetActive(visible);
            }
        }
    }
}