using GalaxyIO.DOTS.Authoring;
using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Collision;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Message;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Tools;
using Newmoonhana.Runtime.Core;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 초계함의 목표 지정 핑을 찍고, 받은 핑을 그리는 클라이언트 전용 뷰.
    /// 화면 안의 핑은 그 자리에 원으로, 화면 밖의 핑은 화면 가장자리에서 그쪽을 가리키는 화살표로 그림.
    /// </summary>
    public sealed class PingView : MonoBehaviour
    {
        [SerializeField]
        private PingMarkerView markerPrefab;

        [SerializeField]
        [Tooltip("핑 찍기 (마우스 오른쪽)")]
        private InputActionReference pingAction;

        [SerializeField]
        private Color color = new Color(0.35F, 1.0F, 0.5F);

        [SerializeField]
        [Tooltip("화면 밖 핑을 그릴 때 화면 가장자리에서 띄우는 거리(캔버스 단위)")]
        [Min(0.0F)]
        private float edgeMargin = 12.0F;

        [SerializeField]
        [Tooltip("자신의 핑을 눌러 지울 수 있는 화살표 끝 주변 반경(캔버스 단위)")]
        [Min(0.0F)]
        private float clickRadius = 24.0F;

        [SerializeField]
        [Tooltip("커서 주변에서 대상을 찾는 반경(캔버스 단위). 확대 배율과 관계없이 화면에서 같은 크기.")]
        [Min(0.0F)]
        private float pickRadius = 24.0F;

        [SerializeField]
        [Tooltip("핑을 받으면 재생")]
        private string pingSound = "Ping";

        // 받은 핑 하나. 대상 이름은 받을 때 번역해 둠. 대상이 사라져도 이름은 남음.
        private sealed class Ping
        {
            public Vector2 Position;
            public string Sender;
            public string Target;

            // Text 를 만든 거리. 거리가 바뀔 때만 문자열을 만들어 GC 할당을 막음.
            public int Distance = -1;
            public string Text;
        }

        private EntityManager entityManager;
        private EntityQuery pingQuery;
        private EntityQuery localShipQuery;
        private EntityQuery pickQuery;
        private EntityQuery stateQuery;
        private EntityQuery connectionQuery;
        private EntityQuery spaceshipRegistryQuery;

        // 보낸 플레이어 NetworkId -> 핑
        private readonly Dictionary<int, Ping> pings = new Dictionary<int, Ping>();

        // 핑 수만큼 늘어나는 표시 풀
        private readonly List<PingMarkerView> markers = new List<PingMarkerView>();

        // 표시마다 보여주는 글자. 바뀔 때만 다시 씀.
        private readonly List<string> shownTexts = new List<string>();

        private Canvas canvas;

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
            pingQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PingRPC>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>()
            );
            localShipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<SpaceshipKindComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );

            // 핑으로 가리킬 수 있는 대상: 플레이어 우주선, AI 우주선, 떨어진 아이템
            pickQuery = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<ColliderComponent>(), ComponentType.ReadOnly<LocalTransform>() },
                Any = new[]
                {
                    ComponentType.ReadOnly<PlayerNameComponent>(),
                    ComponentType.ReadOnly<EnemyNameComponent>(),
                    ComponentType.ReadOnly<ItemComponent>(),
                },
            });
            stateQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerStateComponent>());
            connectionQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
            spaceshipRegistryQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpaceshipRegistryAssetComponent>());

            canvas = GetComponentInParent<Canvas>();
        }

        private void OnEnable()
        {
            pingAction?.action.Enable();
        }

        private void OnDisable()
        {
            pingAction?.action.Disable();
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            if (connectionQuery.IsEmpty)
            {
                return;
            }

            int localId = connectionQuery.GetSingleton<NetworkId>().Value;

            if (!pingQuery.IsEmpty)
            {
                using var received = pingQuery.ToComponentDataArray<PingRPC>(Allocator.Temp);

                entityManager.DestroyEntity(pingQuery);

                foreach (PingRPC rpc in received)
                {
                    if (rpc.Remove)
                    {
                        pings.Remove(rpc.SenderNetworkId);
                        continue;
                    }

                    pings[rpc.SenderNetworkId] = new Ping
                    {
                        Position = new Vector2(rpc.Position.x, rpc.Position.y),
                        Sender   = rpc.Sender.ToString(),
                        Target   = rpc.Target switch
                        {
                            PingTarget.Spaceship => L10n.Get($"Spaceship/{(SpaceshipId)rpc.TargetId}"),
                            PingTarget.Item      => L10n.Get($"Item/{(ItemId)rpc.TargetId}"),
                            PingTarget.Enemy     => rpc.TargetName.ToString(),
                            _                    => L10n.Get("Ping/Destination"),
                        },
                    };

                    if (SoundManager.Instance != null)
                    {
                        SoundManager.Instance.PlaySfx(pingSound);
                    }
                }
            }

            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;

            if (mainCamera == null)
            {
                return;
            }

            Entity localShip = Entity.Null;

            if (!localShipQuery.IsEmpty)
            {
                using var ships = localShipQuery.ToEntityArray(Allocator.Temp);
                localShip = ships[0];
            }

            float scale = canvas.scaleFactor;
            pings.TryGetValue(localId, out Ping own);

            // 메뉴, 채팅 입력창이 열려 있거나 커서가 채팅 탭 위에 있으면 찍지 않음.
            PlayerStateComponent state = stateQuery.GetSingleton<PlayerStateComponent>();
            bool blocked = state.GameMenuOpened || state.InventoryOpened || state.ChatFocused || state.ChatHovered || state.Dead;

            if (pingAction.action.WasPressedThisFrame() && Application.isFocused && !blocked
                && localShip != Entity.Null && Mouse.current != null && !spaceshipRegistryQuery.IsEmpty)
            {
                // 초계함만 핑을 찍을 수 있음. 지우기는 기종과 관계없음.
                SpaceshipAsset spaceship = spaceshipRegistryQuery.GetSingleton<SpaceshipRegistryAssetComponent>().Value
                    .Find(entityManager.GetComponentData<SpaceshipKindComponent>(localShip).Value);

                Vector2 cursor = Mouse.current.position.ReadValue();

                // 자신의 핑을 다시 누르면 지움.
                if (own != null && Vector2.Distance(cursor, MarkerAnchor(mainCamera, own.Position, scale, out _)) <= clickRadius * scale)
                {
                    pings.Remove(localId);
                    own = null;
                    SendPing(new PingRPC { Remove = true });
                }
                else if (spaceship != null && spaceship.Category == SpaceshipCategory.Corvette)
                {
                    Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(cursor.x, cursor.y, -mainCamera.transform.position.z));
                    var point = new float2(world.x, world.y);

                    // 커서 주변 반경에 걸린 대상 중 중심이 가장 가까운 것. 로컬 우주선은 뺌. 없으면 그 자리를 목적지로 찍음.
                    var ping = new PingRPC { Position = point, Target = PingTarget.Destination };
                    // 캔버스 단위 반경을 월드 유닛으로 바꿈.
                    float radius = pickRadius * scale * mainCamera.orthographicSize * 2.0F / Screen.height;
                    var probe = new ColliderWorld { Shape = ColliderShape.Circle, Center = point, Extents = new float2(radius, 0.0F) };
                    float nearest = float.MaxValue;

                    using var entities = pickQuery.ToEntityArray(Allocator.Temp);

                    foreach (Entity entity in entities)
                    {
                        if (entity == localShip)
                        {
                            continue;
                        }

                        ColliderWorld collider = ColliderOverlap.ToWorld(
                            entityManager.GetComponentData<ColliderComponent>(entity),
                            entityManager.GetComponentData<LocalTransform>(entity)
                        );
                        float distance = math.distancesq(collider.Center, point);

                        if (distance >= nearest || !ColliderOverlap.Test(probe, collider, out _))
                        {
                            continue;
                        }

                        nearest = distance;
                        ping.Position = collider.Center;

                        if (entityManager.HasComponent<ItemComponent>(entity))
                        {
                            ping.Target = PingTarget.Item;
                            ping.TargetId = (ushort)entityManager.GetComponentData<ItemComponent>(entity).Id;
                        }
                        else if (entityManager.HasComponent<EnemyNameComponent>(entity))
                        {
                            ping.Target = PingTarget.Enemy;
                            ping.TargetName = entityManager.GetComponentData<EnemyNameComponent>(entity).Value;
                        }
                        else
                        {
                            ping.Target = PingTarget.Spaceship;
                            ping.TargetId = (ushort)entityManager.GetComponentData<SpaceshipKindComponent>(entity).Value;
                        }
                    }

                    SendPing(ping);
                }
            }

            // 자신의 핑은 자신의 화면 안에 있는 동안만 유지. 화면을 벗어나면 지움.
            if (own != null)
            {
                Vector3 viewport = mainCamera.WorldToViewportPoint(own.Position);

                if (viewport.x < 0.0F || viewport.x > 1.0F || viewport.y < 0.0F || viewport.y > 1.0F)
                {
                    pings.Remove(localId);
                    SendPing(new PingRPC { Remove = true });
                }
            }

            // 거리는 로컬 우주선 기준. 우주선이 없으면 카메라 기준.
            Vector2 origin = localShip != Entity.Null
                ? (Vector2)entityManager.GetComponentData<LocalTransform>(localShip).Position.xy
                : (Vector2)mainCamera.transform.position;

            int shown = 0;

            foreach (Ping ping in pings.Values)
            {
                int distance = Mathf.RoundToInt(Vector2.Distance(origin, ping.Position));

                if (ping.Distance != distance)
                {
                    ping.Distance = distance;
                    ping.Text = $"{ping.Sender}({distance}m)\n{ping.Target}";
                }

                int index = shown++;
                PingMarkerView marker = Rent(index);

                Vector2 anchor = MarkerAnchor(mainCamera, ping.Position, scale, out Vector2 direction);
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    (RectTransform)transform, anchor, canvas.worldCamera, out Vector3 position);

                // 글자는 화면 가운데 쪽 옆에 붙임. 위쪽 절반이면 아래로, 아래쪽 절반이면 위로 늘어남.
                var side = new Vector2(anchor.x < Screen.width * 0.5F ? 1.0F : -1.0F, anchor.y > Screen.height * 0.5F ? 1.0F : -1.0F);

                marker.transform.position = position;
                marker.Place(direction, side);

                if (!ReferenceEquals(shownTexts[index], ping.Text))
                {
                    shownTexts[index] = ping.Text;
                    marker.Set(ping.Text, color);
                }
            }

            // 이번 프레임에 쓰이지 않은 표시는 감춤.
            for (int i = shown; i < markers.Count; i++)
            {
                markers[i].gameObject.SetActive(false);
            }
        }

        // 표시 기준점의 화면 좌표와 화살표 방향. 가장자리 여백 안이면 핑 자리(원의 중심)이고 방향은 0,
        // 밖이면 여백 경계에 붙인 화살표 끝과 핑 쪽 방향.
        private Vector2 MarkerAnchor(UnityEngine.Camera mainCamera, Vector2 worldPosition, float scale, out Vector2 direction)
        {
            Vector2 screen = mainCamera.WorldToScreenPoint(worldPosition);
            Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5F;
            Vector2 half = Vector2.Max(center - Vector2.one * (edgeMargin * scale), Vector2.one);
            Vector2 offset = screen - center;

            if (Mathf.Abs(offset.x) <= half.x && Mathf.Abs(offset.y) <= half.y)
            {
                direction = Vector2.zero;
                return screen;
            }

            direction = offset.normalized;
            return center + offset * Mathf.Min(half.x / Mathf.Abs(offset.x), half.y / Mathf.Abs(offset.y));
        }

        private void SendPing(in PingRPC ping)
        {
            Entity rpcEntity = entityManager.CreateEntity(typeof(PingRPC), typeof(SendRpcCommandRequest));
            entityManager.SetComponentData(rpcEntity, ping);
        }

        private PingMarkerView Rent(int index)
        {
            while (markers.Count <= index)
            {
                markers.Add(Instantiate(markerPrefab, transform));
                shownTexts.Add(null);
            }

            markers[index].gameObject.SetActive(true);
            return markers[index];
        }
    }
}
