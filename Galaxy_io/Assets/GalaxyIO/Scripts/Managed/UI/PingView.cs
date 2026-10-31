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
    /// 목표 지정 핑을 찍고, 받은 핑을 그리는 클라이언트 전용 뷰. 레이더를 누르면 그 자리를 목적지로 찍음.
    /// 화면 안의 핑은 그 자리에 원으로, 화면 밖의 핑은 화면 가장자리에서 그쪽을 가리키는 화살표로 그림.
    /// 받은 핑은 일정 시간 뒤 사라짐. 팀 모드에서는 우리 대장선과 위성을 늘 따라다니는 핑을 그림.
    /// </summary>
    public sealed class PingView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("씬에 배치된 핑 마커 템플릿. CarrierOrderView 와 공용")]
        private PingMarkerView markerTemplate;

        [SerializeField]
        [Tooltip("핑 찍기 (마우스 오른쪽)")]
        private InputActionReference pingAction;

        [SerializeField]
        [Tooltip("누르면 그 자리를 목적지로 찍는 레이더")]
        private RadarView radar;

        [SerializeField]
        private Color color = new Color(0.35F, 1.0F, 0.5F);

        [SerializeField]
        [Tooltip("적대 대상을 가리킨 핑의 색")]
        private Color hostileColor = new Color(1.0F, 0.3F, 0.3F);

        [SerializeField]
        [Tooltip("빨강 팀 위성 핑의 색")]
        private Color redColor = new Color(1.0F, 0.3F, 0.3F);

        [SerializeField]
        [Tooltip("파랑 팀 위성 핑의 색")]
        private Color blueColor = new Color(0.3F, 0.6F, 1.0F);

        [SerializeField]
        [Tooltip("아무도 점령하지 않은 위성 핑의 색")]
        private Color neutralColor = Color.white;

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

        [SerializeField]
        [Tooltip("받은 핑이 사라지기까지의 시간(초). 대장선과 위성 핑은 사라지지 않음.")]
        [Min(1.0F)]
        private float duration = 60.0F;

        // 받은 핑 하나. 대상 이름은 받을 때 번역해 둠. 대상이 사라져도 이름은 남음.
        private sealed class Ping
        {
            public Vector2 Position;
            public string Sender;
            public string Target;
            public bool Hostile;

            // 따라다니는 핑의 색. 받은 핑은 Hostile 로 색을 고름.
            public Color Color;

            // 받은 핑이 사라지는 시각(unscaledTime)
            public float ExpireAt;

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
        private EntityQuery modeStateQuery;
        private EntityQuery kingQuery;
        private EntityQuery satelliteQuery;

        // 보낸 플레이어 NetworkId -> 핑
        private readonly Dictionary<int, Ping> pings = new Dictionary<int, Ping>();

        // 시간이 다 된 핑. 순회가 끝난 뒤에 지움.
        private readonly List<int> expired = new List<int>();

        // 우리 대장선과 위성을 따라다니는 핑. 매 프레임 앞에서부터 다시 씀.
        private readonly List<Ping> objectives = new List<Ping>();

        // 핑 수만큼 늘어나는 표시 풀
        private readonly List<PingMarkerView> markers = new List<PingMarkerView>();

        // 표시마다 보여주는 글자와 색. 바뀔 때만 다시 씀.
        private readonly List<string> shownTexts = new List<string>();
        private readonly List<Color> shownColors = new List<Color>();

        private Canvas canvas;

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        private void Awake()
        {
            markerTemplate.gameObject.SetActive(false);
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
            pingQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PingRPC>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>()
            );
            localShipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<SpaceshipKindComponent>(),
                ComponentType.ReadOnly<FactionComponent>(),
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
            modeStateQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameModeStateComponent>());
            kingQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwner>(),
                ComponentType.ReadOnly<FactionComponent>(),
                ComponentType.ReadOnly<CarrierComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            satelliteQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SatelliteComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );

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

                    // 음수는 대장선 함재기가 찾은 적 대장선.
                    pings[rpc.SenderNetworkId] = new Ping
                    {
                        Position = new Vector2(rpc.Position.x, rpc.Position.y),
                        Sender   = rpc.SenderNetworkId < 0 ? L10n.Get("Ping/EnemyKing") : rpc.Sender.ToString(),
                        Hostile  = rpc.Hostile,
                        ExpireAt = Time.unscaledTime + duration,
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
                && localShip != Entity.Null && Mouse.current != null)
            {
                Vector2 cursor = Mouse.current.position.ReadValue();

                // 자신의 핑을 다시 누르면 지움.
                if (own != null && Vector2.Distance(cursor, PingMarkerView.Anchor(mainCamera, own.Position, edgeMargin * scale, out _)) <= clickRadius * scale)
                {
                    pings.Remove(localId);
                    SendPing(new PingRPC { Remove = true });
                }
                // 레이더에서는 대상을 고르지 않고 그 자리를 목적지로 찍음.
                else if (radar != null && radar.TryGetWorldPoint(cursor, out Vector2 radarPoint))
                {
                    SendPing(new PingRPC { Position = radarPoint, Target = PingTarget.Destination });
                }
                else
                {
                    Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(cursor.x, cursor.y, -mainCamera.transform.position.z));
                    var point = new float2(world.x, world.y);

                    // 커서 주변 반경에 걸린 대상 중 중심이 가장 가까운 것. 로컬 우주선은 뺌. 없으면 그 자리를 목적지로 찍음.
                    var ping = new PingRPC { Position = point, Target = PingTarget.Destination };
                    FactionComponent localFaction = entityManager.GetComponentData<FactionComponent>(localShip);

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
                            ping.Hostile = false;
                            continue;
                        }

                        ping.Hostile = !Faction.IsFriendly(localFaction, entityManager.GetComponentData<FactionComponent>(entity));

                        if (entityManager.HasComponent<EnemyNameComponent>(entity))
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

            // 거리는 로컬 우주선 기준. 우주선이 없으면 카메라 기준.
            Vector2 origin = localShip != Entity.Null
                ? (Vector2)entityManager.GetComponentData<LocalTransform>(localShip).Position.xy
                : (Vector2)mainCamera.transform.position;

            foreach (var (sender, ping) in pings)
            {
                if (Time.unscaledTime >= ping.ExpireAt)
                {
                    expired.Add(sender);
                }
            }

            foreach (int sender in expired)
            {
                pings.Remove(sender);
            }

            expired.Clear();

            int shown = 0;

            foreach (Ping ping in pings.Values)
            {
                Show(ping, ping.Hostile ? hostileColor : color, shown++, origin, mainCamera, scale);
            }

            int objectiveCount = CollectObjectives();

            for (int i = 0; i < objectiveCount; i++)
            {
                Show(objectives[i], objectives[i].Color, shown++, origin, mainCamera, scale);
            }

            // 이번 프레임에 쓰이지 않은 표시는 감춤.
            for (int i = shown; i < markers.Count; i++)
            {
                markers[i].gameObject.SetActive(false);
            }
        }

        // 핑 하나를 index 번째 표시에 놓음. 대상이 없는 핑(따라다니는 핑)은 이름과 거리만 씀.
        private void Show(Ping ping, Color tint, int index, Vector2 origin, UnityEngine.Camera mainCamera, float scale)
        {
            int distance = Mathf.RoundToInt(Vector2.Distance(origin, ping.Position));

            if (ping.Distance != distance)
            {
                ping.Distance = distance;
                ping.Text = ping.Target == null
                    ? $"{ping.Sender}({distance}m)"
                    : $"{ping.Sender}({distance}m)\n{ping.Target}";
            }

            PingMarkerView marker = Rent(index);

            Vector2 anchor = PingMarkerView.Anchor(mainCamera, ping.Position, edgeMargin * scale, out Vector2 direction);
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)transform, anchor, canvas.worldCamera, out Vector3 position);

            // 글자는 화면 가운데 쪽 옆에 붙임. 위쪽 절반이면 아래로, 아래쪽 절반이면 위로 늘어남.
            Vector2 safeCenter = Screen.safeArea.center;
            var side = new Vector2(anchor.x < safeCenter.x ? 1.0F : -1.0F, anchor.y > safeCenter.y ? 1.0F : -1.0F);

            marker.transform.position = position;
            marker.Place(direction, side);

            if (!ReferenceEquals(shownTexts[index], ping.Text) || shownColors[index] != tint)
            {
                shownTexts[index] = ping.Text;
                shownColors[index] = tint;
                marker.Set(ping.Text, tint);
            }
        }

        // 우리 대장선과 위성 핑을 objectives 앞에서부터 채우고 개수를 돌려줌. 팀이 없으면 0.
        private int CollectObjectives()
        {
            Team team = modeStateQuery.GetSingleton<GameModeStateComponent>().Team;

            if (team == Team.None)
            {
                return 0;
            }

            int count = 0;

            using (NativeArray<Entity> kings = kingQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity king in kings)
                {
                    uint party = entityManager.GetComponentData<FactionComponent>(king).Party;

                    if (!Teams.IsKing(entityManager.GetComponentData<GhostOwner>(king).NetworkId, party, true)
                        || Teams.FromParty(party) != team)
                    {
                        continue;
                    }

                    Place(RentObjective(count++), L10n.Get("Ping/King"), king, color);
                }
            }

            using (NativeArray<Entity> satellites = satelliteQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity entity in satellites)
                {
                    // 점령되는 중이면 두 팀 색을 번갈아 보여 줌.
                    SatelliteComponent satellite = entityManager.GetComponentData<SatelliteComponent>(entity);
                    Team owner = satellite.ShownTeam(Time.unscaledTimeAsDouble);
                    Color tint = owner == Team.Red ? redColor : owner == Team.Blue ? blueColor : neutralColor;

                    Place(RentObjective(count++), SatelliteComponent.Name(satellite.Index), entity, tint);
                }
            }

            return count;
        }

        // 따라다니는 핑을 대상 위치로 옮김. 이름이 바뀌면 글자를 다시 만듦.
        private void Place(Ping ping, string name, Entity target, Color tint)
        {
            ping.Position = entityManager.GetComponentData<LocalTransform>(target).Position.xy;
            ping.Color = tint;

            if (ping.Sender != name)
            {
                ping.Sender = name;
                ping.Distance = -1;
            }
        }

        private Ping RentObjective(int index)
        {
            while (objectives.Count <= index)
            {
                objectives.Add(new Ping());
            }

            return objectives[index];
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
                markers.Add(Instantiate(markerTemplate, transform));
                shownTexts.Add(null);
                shownColors.Add(default);
            }

            markers[index].gameObject.SetActive(true);
            return markers[index];
        }
    }
}