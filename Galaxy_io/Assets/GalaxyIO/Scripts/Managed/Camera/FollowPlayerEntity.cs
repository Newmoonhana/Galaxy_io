using System.Collections.Generic;
using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Client.Networking;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.DOTS.Spaceship;
using GalaxyIO.Scripts.Managed.Background;
using GalaxyIO.Tools;
using Unity.Collections;
using Unity.Cinemachine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GalaxyIO.Camera
{
    /// <summary>
    /// DOTS 오브젝트인 플레이어 우주선을 Cinemachine 카메라가 따라가도록 대상, 렌즈, 오프셋을 갱신하는 로직.
    /// 우주선을 화면 중앙에 고정하지 않고 진행 방향 쪽으로 카메라를 밀어 시야를 확보함.
    /// 마우스 휠로 시야 크기를 조절함. 축소 한계는 우주선마다의 최대 시야 크기를 따름.
    /// 마우스 조준 모드(휠 누르기로 켜고 끔)에서는 커서 쪽으로, 레이더 조준 모드와 함께 켜면 기수 방향으로 카메라를 밀어냄.
    /// 팀 모드에서 우주선이 없으면 전장을 비춤. 대장선 잡기는 우리 대장선, 점령전은 부활 지점과 우리 위성을 번갈아.
    /// </summary>
    public sealed class FollowPlayerEntity : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("최대 속력일 때 카메라가 기수 방향으로 앞서 나가는 거리")]
        [Min(0.0F)]
        private float lookAheadDistance = 5F;

        [SerializeField]
        [Tooltip("시야 방향이 목표 방향으로 전환되는 속도. 클수록 즉각적으로 반응한다.")]
        [Min(0.0F)]
        private float smoothSpeed = 10F;

        [SerializeField]
        [Tooltip("최대로 확대했을 때의 시야 크기 비율. 우주선의 최대 시야 크기(SpaceshipSpecAsset.MaxOrthographicSize) 기준.")]
        [Range(0.05F, 1.0F)]
        private float minZoomRatio = 0.5333F;

        [SerializeField]
        [Tooltip("휠 한 칸당 시야 크기 변화 비율")]
        [Range(0.01F, 0.5F)]
        private float zoomStep = 0.1F;

        [SerializeField]
        [Tooltip("시야 크기가 목표 크기로 전환되는 속도. 클수록 즉각적으로 반응한다.")]
        [Min(0.0F)]
        private float zoomSmoothSpeed = 10F;

        [SerializeField]
        [Tooltip("마우스 조준 모드 (마우스 휠 누르기). 켜져 있는 동안 커서 쪽으로 카메라를 밀어냄.")]
        private InputActionReference shiftAction;

        [SerializeField]
        [Tooltip("우주선이 화면 가장자리 쪽으로 밀려날 수 있는 최대 비율. 0.6 이면 화면을 8:2 로 나누는 자리까지.")]
        [Range(0.0F, 1.0F)]
        private float maxShift = 0.6F;

        [Header("Shake")]

        [SerializeField]
        [Tooltip("받은 데미지 / 최대 체력 1 당 쌓이는 흔들림 세기. 세기는 0 ~ 1.")]
        [Min(0.0F)]
        private float shakeGain = 5.0F;

        [SerializeField]
        [Tooltip("세기 1 일 때 흔들림 폭. 시야 크기 비율이라 확대해도 화면상 폭이 같음.")]
        [Min(0.0F)]
        private float maxShake = 0.05F;

        [SerializeField]
        [Tooltip("초당 줄어드는 흔들림 세기")]
        [Min(0.01F)]
        private float shakeDecay = 2.0F;

        [SerializeField]
        [Tooltip("흔들림 빠르기")]
        [Min(0.0F)]
        private float shakeFrequency = 25.0F;

        [Header("Spectate")]

        [SerializeField]
        [Tooltip("점령전에서 부활 지점과 우리 위성을 번갈아 비추는 간격(초)")]
        [Min(0.5F)]
        private float spectateInterval = 5.0F;

        [SerializeField]
        [Tooltip("비추는 곳을 서버에 알리는 간격(초). 서버가 그 주변을 복제해 줌.")]
        [Min(0.1F)]
        private float spectateSendInterval = 1.0F;

        [Header("Cinemachine")]

        [SerializeField]
        [Tooltip("우주선을 따라가는 카메라")]
        private CinemachineCamera playerCamera;

        [SerializeField]
        [Tooltip("시야 확보 오프셋. playerCamera 에 붙은 확장.")]
        private CinemachineCameraOffset cameraOffset;

        [SerializeField]
        [Tooltip("피격 흔들림(펄린 노이즈 방식). playerCamera 에 붙은 노이즈. 비우면 hitImpulse 로 흔듦.")]
        private CinemachineBasicMultiChannelPerlin shakeNoise;

        [SerializeField]
        [Tooltip("피격 흔들림(임펄스 방식). shakeNoise 가 비어 있을 때 씀.")]
        private CinemachineImpulseSource hitImpulse;

        [SerializeField]
        [Tooltip("임펄스 방식 흔들림 폭 배율. 펄린 노이즈 방식과 체감을 맞추는 값.")]
        [Min(0.0F)]
        private float hitImpulseScale = 1.0F;

        [SerializeField]
        [Tooltip("팀 모드에서 우주선이 없을 때 전장을 비추는 카메라. 켜지면 playerCamera 보다 우선함.")]
        private CinemachineCamera spectateCamera;

        [SerializeField]
        [Tooltip("playerCamera 가 따라가는 대상. 우주선 위치를 매 프레임 복사함.")]
        private Transform playerTarget;

        [SerializeField]
        [Tooltip("spectateCamera 가 따라가는 대상. 비출 곳을 매 프레임 복사함.")]
        private Transform spectateTarget;

        private EntityManager entityManager;
        private EntityQuery playerQuery;
        private EntityQuery stateQuery;
        private EntityQuery modeQuery;
        private EntityQuery modeStateQuery;
        private EntityQuery kingQuery;
        private EntityQuery satelliteQuery;

        // 우주선이 없을 때 비추는 곳. 매 프레임 다시 채움.
        private readonly List<Vector2> spots = new List<Vector2>();
        private int spotIndex;
        private float nextSpotTime;
        private float nextSpectateSendTime;

        // 화면 비율 계산용. 위치와 시야 크기는 CinemachineBrain 이 씀.
        private UnityEngine.Camera targetCamera;

        // 현재 시야 크기. 두 Cinemachine 카메라의 렌즈에 같이 적용함.
        private float orthographicSize;

        // 관전 중 잠시 우선해서 비출 곳 (위성 점령 등).
        private Vector2 focusSpot;
        private float focusUntil;

        /// <summary>
        /// 휠 줌과 별개로 렌즈에 곱하는 시야 비율 가감(0 = 없음). 스폰 줌인, 킬 펀치 줌에 씀.
        /// </summary>
        public float ZoomKick { get; set; }

        /// <summary>
        /// 가감 전 현재 시야 크기.
        /// </summary>
        public float OrthographicSize => orthographicSize;

        // 카메라를 스폰 위치로 한 번 옮겼는가. 배경을 그 뒤에 보여 주기 위한 1회성 플래그.
        private bool backgroundRevealed;

        // 탄 우주선의 최대 시야 크기. 우주선이 없으면 마지막 값을 유지함.
        private float maxZoom;

        // 휠로 정한 최대 시야 크기 대비 비율. 우주선을 바꿔도 휠 단계가 그대로 유지됨.
        private float zoomRatio = 1F;

        // 현재 적용 중인 오프셋. 목표 오프셋을 향해 매 프레임 수렴함.
        private Vector2 currentLookAhead;
        private Vector2 currentShift;

        // 흔들림 세기(0 ~ 1). 폭은 세기의 제곱이라 약한 흔들림은 거의 티가 안 남.
        private float shake;

        private void Awake()
        {
            // 서버에서 비활성화함
            if (Application.isBatchMode)
            {
                playerCamera.gameObject.SetActive(false);
                spectateCamera.gameObject.SetActive(false);
                gameObject.SetActive(false);
            }
        }

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
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<PlayerSpaceshipMovementComponent>(),
                ComponentType.ReadOnly<MovementComponent>(),
                ComponentType.ReadOnly<SpaceshipCameraComponent>()
            );
            stateQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<PlayerStateComponent>());
            modeQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameModeComponent>());
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

            targetCamera = GetComponent<UnityEngine.Camera>();
            orthographicSize = playerCamera.Lens.OrthographicSize;
            maxZoom = orthographicSize;
        }

        /// <summary>
        /// 받은 데미지 / 최대 체력만큼 카메라를 흔듦.
        /// </summary>
        public void Shake(float damageRatio)
        {
            shake = Mathf.Clamp01(shake + damageRatio * shakeGain);

            // 임펄스 방식: 피격마다 임펄스. 겹치면 더해짐.
            if (shakeNoise == null && hitImpulse != null)
            {
                float strength = Mathf.Clamp01(damageRatio * shakeGain);
                float angle = UnityEngine.Random.value * Mathf.PI * 2.0F;
                Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0.0F);

                hitImpulse.GenerateImpulseWithVelocity(direction * (strength * strength * maxShake * orthographicSize * hitImpulseScale));
            }
        }

        /// <summary>
        /// 관전 중이면 spectateInterval 동안 해당 위치를 우선해서 비춤.
        /// </summary>
        public void FocusSpectate(Vector2 spot)
        {
            focusSpot = spot;
            focusUntil = Time.unscaledTime + spectateInterval;
            nextSpotTime = focusUntil;
        }

        private void OnEnable()
        {
            shiftAction?.action.Enable();
        }

        private void OnDisable()
        {
            shiftAction?.action.Disable();
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                enabled = false;
                return;
            }

            // 메뉴, 인벤토리가 열린 동안엔 휠을 UI 가, 커서가 레이더 위에 있으면 레이더가 가짐.
            bool menuOpen = stateQuery.TryGetSingleton(out PlayerStateComponent playerState)
                            && (playerState.GameMenuOpened || playerState.InventoryOpened);

            // 카메라를 밀어내는 비율(-1 ~ 1). 마우스 조준 모드에서만.
            Vector2 shift = Vector2.zero;

            if (Mouse.current != null && Application.isFocused && !menuOpen)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;

                // 휠 위 = 확대. 비율로 바꿔 어느 배율에서도 한 칸의 체감이 같음.
                if (scroll != 0.0F && !playerState.RadarHovered)
                {
                    zoomRatio = Mathf.Clamp(zoomRatio * (1F - Mathf.Sign(scroll) * zoomStep), minZoomRatio, 1F);
                }

                if (shiftAction != null && shiftAction.action.WasPressedThisFrame() && !playerState.ChatFocused && !stateQuery.IsEmpty)
                {
                    playerState.MouseAim = !playerState.MouseAim;
                    stateQuery.GetSingletonRW<PlayerStateComponent>().ValueRW.MouseAim = playerState.MouseAim;
                }

                // 레이더 조준 모드와 함께 켜면 커서는 레이더 위에 있으므로 아래에서 기수 방향으로 밀어냄.
                if (playerState.MouseAim && !playerState.RadarAim && !playerState.ChatFocused)
                {
                    Vector2 half = new Vector2(Screen.width, Screen.height) * 0.5F;
                    Vector2 cursor = (Mouse.current.position.ReadValue() - half) / half;
                    shift = new Vector2(Mathf.Clamp(cursor.x, -1F, 1F), Mathf.Clamp(cursor.y, -1F, 1F));
                }
            }

            Entity player = Entity.Null;

            if (!playerQuery.IsEmpty)
            {
                using var players = playerQuery.ToEntityArray(Allocator.Temp);
                player = players[0];
                maxZoom = entityManager.GetComponentData<SpaceshipCameraComponent>(player).MaxOrthographicSize;
            }

            // 우주선을 바꾸면 새 최대 시야 크기로 보간됨.
            orthographicSize = Mathf.Lerp(
                orthographicSize,
                maxZoom * zoomRatio,
                1F - Mathf.Exp(-zoomSmoothSpeed * Time.deltaTime)
            );

            float lensSize = orthographicSize * (1F + ZoomKick);
            playerCamera.Lens.OrthographicSize = lensSize;
            spectateCamera.Lens.OrthographicSize = lensSize;

            // 팀 모드에서 우주선이 없으면 전장을 비춤. 자유 모드는 마지막 자리에 머묾.
            if (player == Entity.Null)
            {
                Team team = modeStateQuery.IsEmpty ? Team.None : modeStateQuery.GetSingleton<GameModeStateComponent>().Team;

                if (modeQuery.IsEmpty || team == Team.None)
                {
                    return;
                }

                GameModeComponent mode = modeQuery.GetSingleton<GameModeComponent>();
                spots.Clear();

                if (mode.Mode == GameMode.Regicide)
                {
                    using var kings = kingQuery.ToEntityArray(Allocator.Temp);

                    foreach (Entity king in kings)
                    {
                        uint party = entityManager.GetComponentData<FactionComponent>(king).Party;

                        if (Teams.IsKing(entityManager.GetComponentData<GhostOwner>(king).NetworkId, party, true)
                            && Teams.FromParty(party) == team)
                        {
                            spots.Add(entityManager.GetComponentData<LocalTransform>(king).Position.xy);
                        }
                    }
                }
                else if (mode.Mode == GameMode.Conquest)
                {
                    spots.Add(team == Team.Red ? mode.RedSpawn : mode.BlueSpawn);

                    using var satellites = satelliteQuery.ToComponentDataArray<SatelliteComponent>(Allocator.Temp);
                    using var satelliteTransforms = satelliteQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

                    // A, B, C 순서
                    for (int index = 0; index < satellites.Length; index++)
                    {
                        for (int i = 0; i < satellites.Length; i++)
                        {
                            if (satellites[i].Index == index && satellites[i].Owner == team)
                            {
                                spots.Add(satelliteTransforms[i].Position.xy);
                            }
                        }
                    }
                }

                // 대장선이 아직 복제되지 않음.
                if (spots.Count == 0)
                {
                    return;
                }

                if (Time.unscaledTime >= nextSpotTime)
                {
                    spotIndex++;
                    nextSpotTime = Time.unscaledTime + spectateInterval;
                }

                Vector2 spot = Time.unscaledTime < focusUntil ? focusSpot : spots[spotIndex % spots.Count];

                // 감쇠 이동은 spectateCamera 의 PositionComposer 가 담당.
                spectateTarget.position = new Vector3(spot.x, spot.y, 0.0F);
                SetSpectating(true);
                currentLookAhead = Vector2.zero;
                currentShift = Vector2.zero;

                if (Time.unscaledTime >= nextSpectateSendTime)
                {
                    nextSpectateSendTime = Time.unscaledTime + spectateSendInterval;
                    ClientNetworkRequestManager.RequestSpectate(spot);
                }

                if (!backgroundRevealed)
                {
                    backgroundRevealed = true;
                    BackgroundView.RevealAcrossScenes();
                }

                return;
            }

            LocalTransform playerTransform = entityManager.GetComponentData<LocalTransform>(player);
            PlayerSpaceshipMovementComponent movement =
                entityManager.GetComponentData<PlayerSpaceshipMovementComponent>(player);

            SetSpectating(false);

            // 두 조준 모드를 함께 켜면 우주선이 바라보는 쪽으로 밀어냄.
            if (playerState.MouseAim && playerState.RadarAim && !menuOpen)
            {
                float2 forward = math.normalizesafe(SpaceshipHeading.FromRotation(playerTransform.Rotation), float2.zero);
                shift = new Vector2(forward.x, forward.y);
            }

            // 오프셋만 보간함. 프레임률과 무관한 지수 감쇠.
            currentLookAhead = Vector2.Lerp(
                currentLookAhead,
                GetLookAhead(playerTransform, movement, entityManager.GetComponentData<MovementComponent>(player).Velocity),
                1F - Mathf.Exp(-smoothSpeed * Time.deltaTime)
            );

            currentShift = Vector2.Lerp(currentShift, shift, 1F - Mathf.Exp(-smoothSpeed * Time.deltaTime));

            // 화면 절반 크기(월드 단위). 우주선이 이 비율 넘게 가장자리로 밀려나지 않게 합친 오프셋을 자름.
            Vector2 extents = new Vector2(orthographicSize * targetCamera.aspect, orthographicSize) * maxShift;
            Vector2 offset = currentLookAhead + Vector2.Scale(currentShift, extents);

            offset = new Vector2(Mathf.Clamp(offset.x, -extents.x, extents.x), Mathf.Clamp(offset.y, -extents.y, extents.y));

            // 펄린 노이즈 방식: 흔들림 폭만 정함. 파형은 shakeNoise 의 노이즈 프로필. 세기는 시간에 따라 줄어듦.
            if (shakeNoise != null)
            {
                shakeNoise.AmplitudeGain = shake * shake * maxShake * orthographicSize;
                shakeNoise.FrequencyGain = shakeFrequency;
            }

            shake = Mathf.Max(shake - shakeDecay * Time.deltaTime, 0.0F);

            // 우주선의 위치는 보간 없이 그대로 따라가므로(Damping 0) 화면상 위치가 흔들리지 않음.
            playerTarget.position = new Vector3(playerTransform.Position.x, playerTransform.Position.y, 0.0F);
            cameraOffset.Offset = new Vector3(offset.x, offset.y, 0.0F);

            // 카메라를 스폰 위치로 처음 옮긴 뒤에야 배경을 보여 줌.
            // 접속 완료(NetworkStreamInGame)는 이보다 몇 프레임 빨라서, 그걸 신호로 쓰면
            // 아직 원점에 있는 배경이 잠깐 보였다가 이 순간이동으로 다시 가려짐.
            if (!backgroundRevealed)
            {
                backgroundRevealed = true;
                BackgroundView.RevealAcrossScenes();
            }
        }

        /// <summary>
        /// spectateCamera 를 켜고 끔. 켜면 직전 카메라 위치에서 출발해 비출 곳으로 감쇠 이동함.
        /// </summary>
        private void SetSpectating(bool on)
        {
            if (spectateCamera.gameObject.activeSelf == on)
            {
                return;
            }

            // 처음에는 바로 옮김(CenterOnActivate). 원점에서 날아가는 모습을 보이지 않음.
            spectateCamera.BlendHint = backgroundRevealed ? CinemachineCore.BlendHints.InheritPosition : default;
            spectateCamera.gameObject.SetActive(on);
        }

        /// <summary>
        /// 카메라를 우주선의 기수 방향으로 밀어낼 오프셋(월드 단위)을 계산함.
        /// 정지 상태면 0, 최대 속력이면 <see cref="lookAheadDistance"/>가 됨.
        /// </summary>
        private Vector2 GetLookAhead(
            in LocalTransform transform,
            in PlayerSpaceshipMovementComponent movement,
            float2 velocity)
        {
            if (movement.MaxSpeed <= math.EPSILON)
            {
                return Vector2.zero;
            }

            // 기수 방향 * (기수 방향 속력 / 최대 속력). 가속/감속을 따라 오프셋도 늘고 줆.
            float2 forward = math.normalizesafe(SpaceshipHeading.FromRotation(transform.Rotation), float2.zero);
            float speedRatio = math.saturate(math.dot(velocity, forward) / movement.MaxSpeed);

            return new Vector2(forward.x, forward.y) * (speedRatio * lookAheadDistance);
        }
    }
}