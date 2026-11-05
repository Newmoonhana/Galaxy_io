using System.Collections.Generic;
using DG.Tweening;
using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Tools;
using Unity.Cinemachine;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.Camera
{
    /// <summary>
    /// 인게임 이벤트(스폰, 격침, 폭발, 킬, 위성 점령, 대장선 격침, 판 종료, 과열)에 맞춰 카메라 연출을 재생하는 컴포넌트.
    /// </summary>
    [RequireComponent(typeof(FollowPlayerEntity))]
    public sealed class CameraEventDirector : MonoBehaviour
    {
        private enum EventKind
        {
            None,
            Death,
            King,
            Result,
        }

        [Header("Event Camera")]

        [SerializeField]
        [Tooltip("대상들을 함께 비추는 카메라. 켜지면 다른 카메라보다 우선함.")]
        private CinemachineCamera eventCamera;

        [SerializeField]
        [Tooltip("eventCamera 가 따라가는 대상 묶음. 멤버는 런타임에 채움.")]
        private CinemachineTargetGroup eventGroup;

        [SerializeField]
        [Tooltip("대상 하나가 화면에서 차지하는 반경(월드 단위)")]
        [Min(0.0F)]
        private float targetRadius = 6.0F;

        [Header("Impulse")]

        [SerializeField]
        [Tooltip("위치에서 퍼지는 임펄스 (폭발, 위성 점령, 대장선 격침)")]
        private CinemachineImpulseSource explosionImpulse;

        [SerializeField]
        [Tooltip("과열 떨림 임펄스")]
        private CinemachineImpulseSource overheatImpulse;

        [Header("D1 Spawn")]

        [SerializeField]
        [Tooltip("스폰 순간 늘리는 시야 비율")]
        private float spawnZoom = 0.3F;

        [SerializeField]
        [Tooltip("스폰 줌인 시간(초)")]
        [Min(0.0F)]
        private float spawnZoomDuration = 0.6F;

        [Header("D2 Death")]

        [SerializeField]
        [Tooltip("격침 후 잔해와 킬러를 비추는 시간(초)")]
        [Min(0.0F)]
        private float deathFrameDuration = 1.5F;

        [Header("D3 Explosion")]

        [SerializeField]
        [Tooltip("우주선 폭발 임펄스 세기")]
        [Min(0.0F)]
        private float shipExplosionForce = 0.5F;

        [SerializeField]
        [Tooltip("투사체 피해 반경 1 당 임펄스 세기")]
        [Min(0.0F)]
        private float projectileExplosionForcePerRadius = 0.1F;

        [Header("D4 Kill")]

        [SerializeField]
        [Tooltip("킬 순간 줄이는 시야 비율")]
        private float killPunch = 0.03F;

        [SerializeField]
        [Tooltip("킬 펀치 줌 시간(초)")]
        [Min(0.0F)]
        private float killPunchDuration = 0.2F;

        [Header("D5 Capture")]

        [SerializeField]
        [Tooltip("위성 점령 임펄스 세기")]
        [Min(0.0F)]
        private float captureForce = 0.3F;

        [Header("D6 King")]

        [SerializeField]
        [Tooltip("대장선 격침 임펄스 세기")]
        [Min(0.0F)]
        private float kingExplosionForce = 0.8F;

        [SerializeField]
        [Tooltip("사망/관전 중 격침 지점을 비추는 시간(초)")]
        [Min(0.0F)]
        private float kingFrameDuration = 1.5F;

        [Header("D7 Result")]

        [SerializeField]
        [Tooltip("판 종료 프레이밍에 넣을, 격침된 대장선 지점의 유효 시간(초)")]
        [Min(0.0F)]
        private float lostKingMemory = 3.0F;

        [Header("D8 Overheat")]

        [SerializeField]
        [Tooltip("시야 크기 1 당 임펄스 세기")]
        [Min(0.0F)]
        private float overheatForce = 0.004F;

        [SerializeField]
        [Tooltip("과열 중 임펄스 간격(초)")]
        [Min(0.05F)]
        private float overheatInterval = 0.4F;

        private FollowPlayerEntity follow;
        private World clientWorld;
        private EntityManager entityManager;
        private EntityQuery connectionQuery;
        private EntityQuery stateQuery;
        private EntityQuery modeQuery;
        private EntityQuery modeStateQuery;
        private EntityQuery shipQuery;
        private EntityQuery playerShipQuery;
        private EntityQuery kingQuery;
        private EntityQuery satelliteQuery;
        private EntityQuery heatQuery;
        private EntityQuery messageQuery;

        // 이벤트 카메라 대상. 필요한 만큼 만듦.
        private readonly List<Transform> proxies = new List<Transform>();

        // 팀별 대장선 위치. 직전 프레임과 비교해 격침을 감지함.
        private Dictionary<Team, Vector2> kings = new Dictionary<Team, Vector2>();
        private Dictionary<Team, Vector2> previousKings = new Dictionary<Team, Vector2>();

        // 위성 번호별 주인. 바뀌면 점령.
        private readonly Dictionary<byte, Team> satelliteOwners = new Dictionary<byte, Team>();

        private EventKind eventKind;
        private float eventUntil;
        private int killerNetworkId;

        private bool hadShip;
        private Team lastWinner;
        private Vector2 lostKingSpot;
        private float lostKingTime = float.NegativeInfinity;
        private float nextOverheatTime;
        private Tween zoomTween;

        private void Start()
        {
            follow = GetComponent<FollowPlayerEntity>();
            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            entityManager = clientWorld.EntityManager;
            connectionQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
            stateQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerStateComponent>());
            modeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameModeComponent>());
            modeStateQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameModeStateComponent>());
            shipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<PlayerSpaceshipMovementComponent>()
            );
            playerShipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerNameComponent>(),
                ComponentType.ReadOnly<GhostOwner>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
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
            heatQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<TurretHeatComponent>()
            );
            messageQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<CameraEventMessage>());
        }

        private void OnDisable()
        {
            zoomTween?.Kill();

            if (follow != null)
            {
                follow.ZoomKick = 0.0F;
            }

            StopEvent();
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            int localId = connectionQuery.IsEmpty ? 0 : connectionQuery.GetSingleton<NetworkId>().Value;
            bool hasShip = !shipQuery.IsEmpty;
            bool dead = !hasShip || (stateQuery.TryGetSingleton(out PlayerStateComponent playerState) && playerState.Dead);

            UpdateSpawn(hasShip);
            ConsumeMessages(localId);
            UpdateSatellites(dead);
            UpdateKings(dead);
            UpdateWinner();
            UpdateOverheat();
            UpdateEvent();
        }

        // D1
        private void UpdateSpawn(bool hasShip)
        {
            if (hasShip && !hadShip)
            {
                if (eventKind == EventKind.Death || eventKind == EventKind.King)
                {
                    StopEvent();
                }

                PlayZoomKick(spawnZoom, spawnZoomDuration, Ease.OutCubic);
            }

            hadShip = hasShip;
        }

        // D2, D3, D4
        private void ConsumeMessages(int localId)
        {
            if (messageQuery.IsEmpty)
            {
                return;
            }

            using var messages = messageQuery.ToComponentDataArray<CameraEventMessage>(Allocator.Temp);
            entityManager.DestroyEntity(messageQuery);

            foreach (CameraEventMessage message in messages)
            {
                Vector2 position = new Vector2(message.Position.x, message.Position.y);

                if (message.Kind == CameraEventKind.ProjectileExplosion)
                {
                    Impulse(explosionImpulse, position, message.Radius * projectileExplosionForcePerRadius);
                    continue;
                }

                Impulse(explosionImpulse, position, shipExplosionForce);

                if (localId <= 0)
                {
                    continue;
                }

                if (message.OwnerNetworkId == localId)
                {
                    StartDeath(position, message.KillerNetworkId);
                }
                else if (message.KillerNetworkId == localId)
                {
                    PlayZoomKick(-killPunch, killPunchDuration, Ease.OutQuad);
                }
            }
        }

        // D5
        private void UpdateSatellites(bool dead)
        {
            if (satelliteQuery.IsEmpty)
            {
                // 새 판에서 위성이 다시 만들어짐.
                satelliteOwners.Clear();
                return;
            }

            using var satellites = satelliteQuery.ToComponentDataArray<SatelliteComponent>(Allocator.Temp);
            using var transforms = satelliteQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (int i = 0; i < satellites.Length; i++)
            {
                SatelliteComponent satellite = satellites[i];

                if (satelliteOwners.TryGetValue(satellite.Index, out Team last)
                    && last != satellite.Owner
                    && satellite.Owner != Team.None)
                {
                    Vector2 position = new Vector2(transforms[i].Position.x, transforms[i].Position.y);
                    Impulse(explosionImpulse, position, captureForce);

                    if (dead)
                    {
                        follow.FocusSpectate(position);
                    }
                }

                satelliteOwners[satellite.Index] = satellite.Owner;
            }
        }

        // D6
        private void UpdateKings(bool dead)
        {
            (previousKings, kings) = (kings, previousKings);
            kings.Clear();

            bool regicide = !modeQuery.IsEmpty && modeQuery.GetSingleton<GameModeComponent>().Mode == GameMode.Regicide;
            bool loading = !modeStateQuery.IsEmpty
                           && modeStateQuery.GetSingleton<GameModeStateComponent>().LoadingUntil > clientWorld.Time.ElapsedTime;

            if (!regicide || loading)
            {
                previousKings.Clear();
                return;
            }

            using var entities = kingQuery.ToEntityArray(Allocator.Temp);

            foreach (Entity king in entities)
            {
                uint party = entityManager.GetComponentData<FactionComponent>(king).Party;

                if (Teams.IsKing(entityManager.GetComponentData<GhostOwner>(king).NetworkId, party, true))
                {
                    var position = entityManager.GetComponentData<LocalTransform>(king).Position;
                    kings[Teams.FromParty(party)] = new Vector2(position.x, position.y);
                }
            }

            foreach (KeyValuePair<Team, Vector2> previous in previousKings)
            {
                if (kings.ContainsKey(previous.Key))
                {
                    continue;
                }

                lostKingSpot = previous.Value;
                lostKingTime = Time.unscaledTime;
                Impulse(explosionImpulse, previous.Value, kingExplosionForce);

                if (dead && eventKind != EventKind.Result)
                {
                    SetTargets(previous.Value);
                    StartEvent(EventKind.King, kingFrameDuration);
                }
            }
        }

        // D7
        private void UpdateWinner()
        {
            Team winner = modeStateQuery.IsEmpty ? Team.None : modeStateQuery.GetSingleton<GameModeStateComponent>().Winner;

            if (winner != Team.None && lastWinner == Team.None)
            {
                StartResult(winner);
            }
            else if (winner == Team.None && eventKind == EventKind.Result)
            {
                StopEvent();
            }

            lastWinner = winner;
        }

        private void StartResult(Team winner)
        {
            var spots = new List<Vector2>();

            if (kings.TryGetValue(winner, out Vector2 king))
            {
                spots.Add(king);
            }

            if (Time.unscaledTime - lostKingTime <= lostKingMemory)
            {
                spots.Add(lostKingSpot);
            }

            if (!satelliteQuery.IsEmpty)
            {
                using var satellites = satelliteQuery.ToComponentDataArray<SatelliteComponent>(Allocator.Temp);
                using var transforms = satelliteQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

                for (int i = 0; i < satellites.Length; i++)
                {
                    if (satellites[i].Owner == winner)
                    {
                        spots.Add(new Vector2(transforms[i].Position.x, transforms[i].Position.y));
                    }
                }
            }

            if (spots.Count == 0)
            {
                return;
            }

            SetTargets(spots.ToArray());
            StartEvent(EventKind.Result, float.PositiveInfinity);
        }

        // D8
        private void UpdateOverheat()
        {
            if (heatQuery.IsEmpty || Time.unscaledTime < nextOverheatTime)
            {
                return;
            }

            using var heats = heatQuery.ToComponentDataArray<TurretHeatComponent>(Allocator.Temp);

            if (heats[0].Overheated)
            {
                nextOverheatTime = Time.unscaledTime + overheatInterval;
                overheatImpulse.GenerateImpulseWithVelocity(RandomDirection() * (overheatForce * follow.OrthographicSize));
            }
        }

        private void StartDeath(Vector2 wreck, int killerId)
        {
            if (eventKind == EventKind.Result || eventKind == EventKind.King)
            {
                return;
            }

            killerNetworkId = killerId;

            if (TryFindPlayerShip(killerId, out Vector2 killer))
            {
                SetTargets(wreck, killer);
            }
            else
            {
                SetTargets(wreck);
            }

            StartEvent(EventKind.Death, deathFrameDuration);
        }

        private void StartEvent(EventKind kind, float duration)
        {
            eventKind = kind;
            eventUntil = Time.unscaledTime + duration;
            eventCamera.gameObject.SetActive(true);
        }

        private void StopEvent()
        {
            eventKind = EventKind.None;

            if (eventCamera != null)
            {
                eventCamera.gameObject.SetActive(false);
            }
        }

        private void UpdateEvent()
        {
            if (eventKind == EventKind.None)
            {
                return;
            }

            // 킬러는 움직이므로 계속 따라감.
            if (eventKind == EventKind.Death && proxies.Count > 1 && eventGroup.Targets.Count > 1
                && TryFindPlayerShip(killerNetworkId, out Vector2 killer))
            {
                proxies[1].position = killer;
            }

            if (Time.unscaledTime >= eventUntil)
            {
                StopEvent();
            }
        }

        /// <summary>
        /// eventGroup 멤버를 주어진 위치들로 교체함.
        /// </summary>
        private void SetTargets(params Vector2[] spots)
        {
            eventGroup.Targets.Clear();

            for (int i = 0; i < spots.Length; i++)
            {
                if (proxies.Count <= i)
                {
                    var proxy = new GameObject($"EventTarget_{i:00}").transform;
                    // 그룹 자신은 멤버 중심으로 움직이므로 움직이지 않는 부모 아래에 둠.
                    proxy.SetParent(eventGroup.transform.parent, false);
                    proxies.Add(proxy);
                }

                proxies[i].position = spots[i];
                eventGroup.AddMember(proxies[i], 1.0F, targetRadius);
            }
        }

        private bool TryFindPlayerShip(int networkId, out Vector2 position)
        {
            position = default;

            if (networkId <= 0)
            {
                return false;
            }

            using var owners = playerShipQuery.ToComponentDataArray<GhostOwner>(Allocator.Temp);
            using var transforms = playerShipQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            for (int i = 0; i < owners.Length; i++)
            {
                if (owners[i].NetworkId == networkId)
                {
                    position = new Vector2(transforms[i].Position.x, transforms[i].Position.y);
                    return true;
                }
            }

            return false;
        }

        private void PlayZoomKick(float from, float duration, Ease ease)
        {
            zoomTween?.Kill();
            follow.ZoomKick = from;
            zoomTween = DOTween.To(() => follow.ZoomKick, value => follow.ZoomKick = value, 0.0F, duration)
                .SetEase(ease)
                .SetLink(gameObject);
        }

        private static void Impulse(CinemachineImpulseSource source, Vector2 position, float force)
        {
            if (force <= 0.0F)
            {
                return;
            }

            source.GenerateImpulseAtPositionWithVelocity(position, RandomDirection() * force);
        }

        private static Vector3 RandomDirection()
        {
            float angle = Random.value * Mathf.PI * 2.0F;
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0.0F);
        }
    }
}
