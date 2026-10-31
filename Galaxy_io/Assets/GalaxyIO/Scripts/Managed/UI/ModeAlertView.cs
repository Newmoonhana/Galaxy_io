using System.Collections.Generic;
using GalaxyIO.DOTS.Client.Components.Singleton;
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

namespace GalaxyIO.Scripts.Managed.UI
{
    /// <summary>
    /// 팀 모드 알림. 화면 가운데 글자 하나를 같이 씀.
    /// 대장선 잡기: 우리 기함이 상대 팀에게 공격받으면 서버가 보낸 <see cref="FlagshipAttackedRPC"/>마다 경보를 울리고
    /// "The flagship is under attack!" 을 빠르게 띄웠다 지움.
    /// 점령전: 위성 주인이 바뀌면 화면 가운데에 "{팀} captured {위성}" 을 띄웠다가 지우고 소리를 냄.
    /// 우리 기체나 함재기가 점령도를 올리는 동안 째깍 소리를 반복함.
    /// 우리 위성이 점령되는 동안 <see cref="CaptureZap.Period"/>(3초)마다 경보를 한 번씩 울리고, 울리는 순간
    /// <see cref="CaptureZap.Trigger"/>로 위성 글자 번쩍임을 시작함.
    /// </summary>
    public sealed class ModeAlertView : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("화면 가운데 알림(점령, 기함 피격)")]
        private TMP_Text captureLabel;

        [SerializeField]
        [Min(0.0F)]
        private float fadeIn = 0.3F;

        [SerializeField]
        [Tooltip("다 나타난 뒤 머무는 시간(초)")]
        [Min(0.0F)]
        private float hold = 2.0F;

        [SerializeField]
        [Min(0.0F)]
        private float fadeOut = 0.8F;

        [Header("Flagship")]

        [SerializeField]
        [Tooltip("기함 피격 알림이 나타나는 시간(초). 점령 알림보다 빠름.")]
        [Min(0.0F)]
        private float flagshipFadeIn = 0.1F;

        [SerializeField]
        [Tooltip("기함 피격 알림이 다 나타난 뒤 머무는 시간(초)")]
        [Min(0.0F)]
        private float flagshipHold = 0.8F;

        [SerializeField]
        [Min(0.0F)]
        private float flagshipFadeOut = 0.3F;

        [SerializeField]
        [Tooltip("기함 피격 알림 글자 색")]
        private Color flagshipColor = new Color(1.0F, 0.35F, 0.25F);

        [Header("Sounds")]

        [SerializeField]
        [Tooltip("우리 기체나 함재기가 점령도를 올리는 동안 반복")]
        private string tickingSound = "Ticking";

        [SerializeField]
        [Tooltip("우리 위성이 점령되는 동안 CaptureZap.Period 마다 한 번씩 재생. 반복 재생하지 않는 소리.")]
        private string alarmSound = "OccupyAlarm";

        [SerializeField]
        [Tooltip("위성 주인이 바뀌면 재생")]
        private string takeOverSound = "SatelliteTakeOver";

        [SerializeField]
        [Tooltip("반복음이나 경보를 멈출 때 줄이는 시간(초)")]
        [Min(0.0F)]
        private float loopFadeOut = 0.3F;

        [Header("Colors")]

        [SerializeField]
        private Color redColor = new Color(1.0F, 0.3F, 0.3F);

        [SerializeField]
        private Color blueColor = new Color(0.3F, 0.6F, 1.0F);

        private EntityQuery configQuery;
        private EntityQuery stateQuery;
        private EntityQuery connectionQuery;
        private EntityQuery satelliteQuery;
        private EntityQuery shipQuery;
        private EntityQuery craftQuery;
        private EntityQuery flagshipQuery;

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        // 위성 번호 -> 마지막으로 본 주인. 처음 본 위성은 알리지 않음.
        private readonly Dictionary<byte, Team> owners = new Dictionary<byte, Team>();

        // 알림을 띄운 시각(unscaledTime). 띄운 적 없으면 음의 무한대.
        private float shownAt = float.NegativeInfinity;

        // 지금 띄운 알림의 나타남 / 머묾 / 사라짐 시간(초)
        private float shownFadeIn;
        private float shownHold;
        private float shownFadeOut;

        private bool ticking;
        private bool alarm;

        // 다음 경보를 울릴 시각(unscaledTime)
        private float nextAlarmAt;

        private void Start()
        {
            captureLabel.alpha = 0.0F;
            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            EntityManager entityManager = clientWorld.EntityManager;
            configQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ConquestConfigComponent>());
            stateQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GameModeStateComponent>());
            connectionQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkId>());
            satelliteQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SatelliteComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            shipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GhostOwnerIsLocal>(),
                ComponentType.ReadOnly<PlayerSpaceshipMovementComponent>(),
                ComponentType.ReadOnly<SpaceshipSpecComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            craftQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<CarriedCraftComponent>(),
                ComponentType.ReadOnly<SpaceshipSpecComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            flagshipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<FlagshipAttackedRPC>(),
                ComponentType.ReadOnly<ReceiveRpcCommandRequest>()
            );
        }

        private void OnDisable()
        {
            SetLoop(ref ticking, false, tickingSound);
            SetLoop(ref alarm, false, alarmSound);
            CaptureZap.Stop();
        }

        private void LateUpdate()
        {
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                owners.Clear();
                captureLabel.alpha = 0.0F;
                SetLoop(ref ticking, false, tickingSound);
                SetLoop(ref alarm, false, alarmSound);
                CaptureZap.Stop();
                return;
            }

            UpdateFlagship();

            if (configQuery.IsEmpty || connectionQuery.IsEmpty)
            {
                owners.Clear();
                SetLoop(ref ticking, false, tickingSound);
                SetLoop(ref alarm, false, alarmSound);
                CaptureZap.Stop();
                UpdateLabel();
                return;
            }

            float radius = configQuery.GetSingleton<ConquestConfigComponent>().CaptureRadius;
            Team team = stateQuery.GetSingleton<GameModeStateComponent>().Team;
            int localId = connectionQuery.GetSingleton<NetworkId>().Value;

            using NativeArray<SatelliteComponent> satellites = satelliteQuery.ToComponentDataArray<SatelliteComponent>(Allocator.Temp);
            using NativeArray<LocalTransform> homes = satelliteQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using NativeArray<SpaceshipSpecComponent> shipSpecs = shipQuery.ToComponentDataArray<SpaceshipSpecComponent>(Allocator.Temp);
            using NativeArray<LocalTransform> shipTransforms = shipQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using NativeArray<CarriedCraftComponent> crafts = craftQuery.ToComponentDataArray<CarriedCraftComponent>(Allocator.Temp);
            using NativeArray<SpaceshipSpecComponent> craftSpecs = craftQuery.ToComponentDataArray<SpaceshipSpecComponent>(Allocator.Temp);
            using NativeArray<LocalTransform> craftTransforms = craftQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            bool occupying = false;
            bool attacked = false;

            // 새 판은 위성을 모두 새로 만듦. 주인 기록도 새로 시작함.
            if (satellites.Length == 0)
            {
                owners.Clear();
            }

            for (int i = 0; i < satellites.Length; i++)
            {
                SatelliteComponent satellite = satellites[i];

                if (owners.TryGetValue(satellite.Index, out Team last) && last != satellite.Owner && satellite.Owner != Team.None)
                {
                    Color color = satellite.Owner == Team.Red ? redColor : blueColor;
                    string name = L10n.Get(satellite.Owner == Team.Red ? "Team/Red" : "Team/Blue");

                    Show(
                        string.Format(
                            L10n.Get("Satellite/Captured"),
                            $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{name}</color>",
                            SatelliteComponent.Name(satellite.Index)
                        ),
                        fadeIn,
                        hold,
                        fadeOut
                    );

                    if (SoundManager.Instance != null)
                    {
                        SoundManager.Instance.PlaySfx(takeOverSound);
                    }
                }

                owners[satellite.Index] = satellite.Owner;

                if (team == Team.None)
                {
                    continue;
                }

                // 우리 위성에 상대 팀이 점령도를 쌓고 있음.
                attacked |= CaptureZap.IsAlarming(satellite, team);

                // 우리 팀이 점령도를 올리는 중이고 내 기체나 함재기가 범위 안에 있음.
                if (occupying || satellite.Occupier != team || satellite.Owner == team)
                {
                    continue;
                }

                float2 home = homes[i].Position.xy;

                for (int j = 0; j < shipSpecs.Length && !occupying; j++)
                {
                    occupying = InRange(shipTransforms[j], shipSpecs[j], home, radius);
                }

                for (int j = 0; j < crafts.Length && !occupying; j++)
                {
                    occupying = crafts[j].OwnerNetworkId == localId && InRange(craftTransforms[j], craftSpecs[j], home, radius);
                }
            }

            SetLoop(ref ticking, occupying, tickingSound);
            SetAlarm(attacked);
            UpdateLabel();
        }

        // 우리 기함이 공격받음. 서버가 간격을 두고 보내므로 받을 때마다 경보를 울리고 알림을 다시 띄움.
        private void UpdateFlagship()
        {
            if (flagshipQuery.IsEmpty)
            {
                return;
            }

            clientWorld.EntityManager.DestroyEntity(flagshipQuery);

            Show(
                $"<color=#{ColorUtility.ToHtmlStringRGB(flagshipColor)}>{L10n.Get("Flagship/UnderAttack")}</color>",
                flagshipFadeIn,
                flagshipHold,
                flagshipFadeOut
            );

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySfx(alarmSound);
            }
        }

        private void Show(string text, float inTime, float holdTime, float outTime)
        {
            captureLabel.text = text;
            shownAt = Time.unscaledTime;
            shownFadeIn = inTime;
            shownHold = holdTime;
            shownFadeOut = outTime;
        }

        // 나타났다가 머문 뒤 사라짐.
        private void UpdateLabel()
        {
            float elapsed = Time.unscaledTime - shownAt;
            captureLabel.alpha = elapsed < shownFadeIn ? elapsed / Mathf.Max(shownFadeIn, 0.0001F)
                : elapsed < shownFadeIn + shownHold ? 1.0F
                : Mathf.Clamp01(1.0F - (elapsed - shownFadeIn - shownHold) / Mathf.Max(shownFadeOut, 0.0001F));
        }

        private static bool InRange(in LocalTransform transform, in SpaceshipSpecComponent spec, float2 home, float radius)
        {
            float reach = radius + spec.Thickness * 0.5F;
            return spec.Health > 0.0F && math.distancesq(transform.Position.xy, home) <= reach * reach;
        }

        // 우리 위성이 점령되는 동안 주기마다 경보를 한 번씩 울리고 같은 순간 글자 번쩍임을 시작함.
        // 점령되기 시작하면 바로 첫 경보를 울림. 점령이 멈추면 울리던 소리를 줄이고 번쩍임도 멈춤.
        private void SetAlarm(bool attacked)
        {
            float now = Time.unscaledTime;

            if (attacked && (!alarm || now >= nextAlarmAt))
            {
                nextAlarmAt = now + CaptureZap.Period;
                CaptureZap.Trigger();

                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlaySfx(alarmSound);
                }
            }
            else if (!attacked && alarm)
            {
                CaptureZap.Stop();

                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.FadeOutSfx(alarmSound, loopFadeOut);
                }
            }

            alarm = attacked;
        }

        // 반복음을 켜고 끔. 바뀔 때만 소리를 건드림.
        private void SetLoop(ref bool playing, bool next, string sound)
        {
            if (playing == next)
            {
                return;
            }

            playing = next;

            if (SoundManager.Instance == null)
            {
                return;
            }

            if (next) SoundManager.Instance.PlaySfx(sound);
            else SoundManager.Instance.FadeOutSfx(sound, loopFadeOut);
        }
    }
}
