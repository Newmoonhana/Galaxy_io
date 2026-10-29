using System.Collections.Generic;
using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Client.Components.Singleton;
using GalaxyIO.DOTS.Client.Systems;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Tools;
using Newmoonhana.Runtime.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.Audio
{
    /// <summary>
    /// <see cref="SoundMessage"/>를 읽어 효과음을 재생하는 클라이언트 전용 플레이어.
    /// 우주선의 <see cref="TurretSwitchComponent"/>를 보고 포탑 사격 시작음과 반복음도 재생함.
    /// 자동 포탑은 <see cref="AutoTurretElement.Firing"/>을 봄.
    /// 충전 포탑은 충전을 시작할 때 충전음을 내고, 떼면 충전음을 줄여 멈추며 다 차기 전이면 취소음을 냄.
    /// 충전음은 충전 중인 우주선을 따라감.
    /// <para/>
    /// 재생은 <see cref="SoundManager"/>를 거침. 거리 감쇠와 음성 풀링은 Master Audio 의
    /// Sound Group 설정이 담당함.
    /// </summary>
    public sealed class SoundEffectPlayer : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("포탑 반복음이 멈출 때 0 까지 줄어드는 시간(초)")]
        [Min(0.01F)]
        private float loopFadeOut = 0.3F;

        // 같은 효과음을 한 프레임에 최대 몇 개까지 재생할지. Sound Group 의 음성 수와 맞춤
        [Min(1)]
        private int maxPerKeyPerFrame = 16;

        private EntityManager entityManager;
        private EntityQuery messageQuery;
        private EntityQuery shipQuery;
        private EntityQuery registryQuery;
        private EntityQuery stateQuery;

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        // 우주선별 직전 사격 전환 상태. 시작음을 한 번만 내는 데 씀.
        private readonly Dictionary<Entity, TurretSound> turretSounds = new Dictionary<Entity, TurretSound>();

        // 이번 프레임에 본 우주선. 사라진 우주선을 지우는 데 씀.
        private readonly HashSet<Entity> seen = new HashSet<Entity>();
        private readonly List<Entity> gone = new List<Entity>();

        // 이번 프레임에 필요한 반복음과 재생 위치. Sound Group 이 우주선별로 나뉘지 않으므로
        // 같은 포탑을 쏘는 우주선 중 가장 가까운 쪽 위치를 씀.
        private readonly Dictionary<FixedString64Bytes, PendingSound> loopTargets =
            new Dictionary<FixedString64Bytes, PendingSound>();

        // 지금 재생 중인 반복음 그룹. FadeOutSfx 는 그룹 전체를 끄므로 우주선별이 아니라
        // 키 단위로 들고 있어야 함 - 한 대가 멈춰도 같은 포탑을 쏘는 다른 대가 있으면 유지함.
        private readonly HashSet<FixedString64Bytes> activeLoops = new HashSet<FixedString64Bytes>();
        private readonly List<FixedString64Bytes> stoppedLoops = new List<FixedString64Bytes>();

        // 이번 프레임 충전음 위치. 반복음처럼 가장 가까운 충전 중인 우주선을 따라감.
        private readonly Dictionary<FixedString64Bytes, PendingSound> chargeTargets =
            new Dictionary<FixedString64Bytes, PendingSound>();

        // 이번 프레임 키별 재생 후보. 리스트를 재사용해 매 프레임 할당을 피함
        private readonly Dictionary<FixedString64Bytes, List<PendingSound>> pending =
            new Dictionary<FixedString64Bytes, List<PendingSound>>();

        private struct PendingSound
        {
            public float SqrDistance;
            public Vector3 Position;
        }

        private struct TurretSound
        {
            public TurretSwitchState State;

            // 직전 남은 전환 시간. 충전 포탑은 모자란 충전 시간.
            public float Remaining;

            // 사격 중이던 자동 포탑. 순번별 비트.
            public uint AutoFiring;
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
            messageQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SoundMessage>());
            shipQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<TurretSwitchComponent>(),
                ComponentType.ReadOnly<EquippedTurretComponent>(),
                ComponentType.ReadOnly<LocalTransform>()
            );
            registryQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ItemRegistryElement>());
            stateQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerStateComponent>());
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                ReleaseAllLoops();
                turretSounds.Clear();
                enabled = false;
                return;
            }

            // 쉬다가 사격 전환이 시작되면 시작음을 한 번, 사격 중에는 반복음을 재생함.
            seen.Clear();
            loopTargets.Clear();
            chargeTargets.Clear();

            // 리스폰 대기 중에는 포탑 소리를 내지 않음. 사망 지점 주변 전투음까지 들리면 안 됨.
            // 후보가 비므로 ApplyLoops 가 재생 중인 반복음을 전부 페이드아웃시킴.
            // 메시지 재생은 막지 않음 - 자기 폭발음은 Dead 가 켜지는 프레임에 들어옴.
            bool dead = !stateQuery.IsEmpty && stateQuery.GetSingleton<PlayerStateComponent>().Dead;

            // 포탑 소리도 가청 범위 안에서만 냄. 범위 밖은 음량이 0인데 음성만 차지함.
            UnityEngine.Camera listenerCamera = UnityEngine.Camera.main;
            bool hasListener = listenerCamera != null;
            float2 listener = hasListener
                ? new float2(listenerCamera.transform.position.x, listenerCamera.transform.position.y)
                : default;

            if (!dead && !registryQuery.IsEmpty)
            {
                DynamicBuffer<ItemRegistryElement> registry = registryQuery.GetSingletonBuffer<ItemRegistryElement>(true);

                using var ships = shipQuery.ToEntityArray(Allocator.Temp);
                using var switches = shipQuery.ToComponentDataArray<TurretSwitchComponent>(Allocator.Temp);
                using var turrets = shipQuery.ToComponentDataArray<EquippedTurretComponent>(Allocator.Temp);
                using var transforms = shipQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

                for (int i = 0; i < ships.Length; i++)
                {
                    seen.Add(ships[i]);

                    ItemRegistryElement turret = registry.Get(turrets[i].Value);
                    TurretSwitchState current = switches[i].State;
                    Vector3 position = transforms[i].Position;

                    turretSounds.TryGetValue(ships[i], out TurretSound sound);

                    bool audible = SoundAudibleRange.IsAudible(
                        hasListener, listener, new float2(position.x, position.y)
                    );

                    // 쉬다가 방아쇠를 당긴 순간. 충전 포탑은 충전을 시작한 순간.
                    bool started = turret.ReleaseFire
                        ? current == TurretSwitchState.SpinningUp
                        : current != TurretSwitchState.Idle;

                    if (audible && sound.State == TurretSwitchState.Idle && started)
                    {
                        Play(turret.LaunchSound, position);
                    }

                    if (audible && turret.ReleaseFire && current == TurretSwitchState.SpinningUp && !turret.LaunchSound.IsEmpty)
                    {
                        TrackNearest(chargeTargets, turret.LaunchSound, position, listener);
                    }

                    // 충전 포탑을 뗀 순간. 한 틱만 머무는 Firing / SpinningDown 을 못 보고 Idle 로 넘어올 수 있어
                    // 마지막으로 본 모자란 충전 시간으로 다 찼는지 가름.
                    if (turret.ReleaseFire && sound.State == TurretSwitchState.SpinningUp && current != TurretSwitchState.SpinningUp)
                    {
                        Release(turret.LaunchSound);

                        if (audible && current != TurretSwitchState.Firing && sound.Remaining > 0.0F)
                        {
                            Play(turret.CancelSound, position);
                        }
                    }

                    // 범위 밖이면 사격 중이 아닌 것으로 다룸. 멀어지면 반복음이 페이드아웃됨.
                    FixedString64Bytes loop =
                        audible && current == TurretSwitchState.Firing ? turret.LoopSound : default;

                    if (!loop.IsEmpty)
                    {
                        TrackNearest(loopTargets, loop, position, listener);
                    }

                    // 자동 포탑은 사격을 시작할 때 시작음을 내고 사격 중에 반복음을 냄.
                    DynamicBuffer<AutoTurretElement> autoTurrets = entityManager.GetBuffer<AutoTurretElement>(ships[i], true);
                    uint autoFiring = 0;

                    for (int k = 0; k < autoTurrets.Length && k < 32; k++)
                    {
                        if (!autoTurrets[k].Firing) continue;

                        autoFiring |= 1u << k;
                        ItemRegistryElement autoTurret = registry.Get(autoTurrets[k].Turret);

                        if (!audible) continue;

                        if ((sound.AutoFiring & (1u << k)) == 0) Play(autoTurret.LaunchSound, position);
                        if (!autoTurret.LoopSound.IsEmpty) TrackNearest(loopTargets, autoTurret.LoopSound, position, listener);
                    }

                    sound.State = current;
                    sound.Remaining = switches[i].Remaining;
                    sound.AutoFiring = autoFiring;
                    turretSounds[ships[i]] = sound;
                }
            }

            // 파괴됐거나 복제 범위를 벗어난 우주선. 반복음은 키 단위로 아래에서 정리함.
            gone.Clear();

            foreach (KeyValuePair<Entity, TurretSound> pair in turretSounds)
            {
                if (!seen.Contains(pair.Key))
                {
                    gone.Add(pair.Key);
                }
            }

            foreach (Entity ship in gone)
            {
                turretSounds.Remove(ship);
            }

            ApplyLoops();

            foreach (KeyValuePair<FixedString64Bytes, PendingSound> pair in chargeTargets)
            {
                MoveLoop(pair.Key, pair.Value.Position);
            }

            if (messageQuery.IsEmpty)
            {
                return;
            }

            using var messages = messageQuery.ToComponentDataArray<SoundMessage>(Allocator.Temp);

            PlayBudgeted(messages);

            entityManager.DestroyEntity(messageQuery);
        }

        /// <summary>
        /// 같은 효과음이 한 프레임에 몰려도 가까운 것부터 <see cref="maxPerKeyPerFrame"/> 개만 재생함.
        /// 무한 맵 / 멀티플레이라 동시 발생 수에 상한이 없으므로, 오디오 비용을 여기서 고정함.
        /// </summary>
        private void PlayBudgeted(NativeArray<SoundMessage> _messages)
        {
            foreach (List<PendingSound> list in pending.Values)
            {
                list.Clear();
            }

            Vector3 listener = UnityEngine.Camera.main == null
                ? Vector3.zero
                : UnityEngine.Camera.main.transform.position;

            foreach (SoundMessage message in _messages)
            {
                if (message.SoundKey.IsEmpty) continue;

                Vector3 position = Flatten(new Vector3(message.Position.x, message.Position.y, 0.0F));

                if (!pending.TryGetValue(message.SoundKey, out List<PendingSound> list))
                {
                    list = new List<PendingSound>();
                    pending[message.SoundKey] = list;
                }

                list.Add(new PendingSound
                {
                    SqrDistance = (position - listener).sqrMagnitude,
                    Position = position,
                });
            }

            foreach (KeyValuePair<FixedString64Bytes, List<PendingSound>> pair in pending)
            {
                List<PendingSound> list = pair.Value;
                if (list.Count == 0) continue;

                // 가까운 것부터. 한 개뿐이면 정렬을 건너뜀
                if (list.Count > 1)
                {
                    list.Sort(CompareByDistance);
                }

                int count = Mathf.Min(list.Count, maxPerKeyPerFrame);
                for (int i = 0; i < count; i++)
                {
                    PlayFlattened(pair.Key, list[i].Position);
                }
            }
        }

        private static int CompareByDistance(PendingSound _a, PendingSound _b)
            => _a.SqrDistance.CompareTo(_b.SqrDistance);

        // 감쇠는 Sound Group 의 3D 커브가 담당함.
        private static void Play(FixedString64Bytes _key, Vector3 _position)
            => PlayFlattened(_key, Flatten(_position));

        // 이미 Flatten 된 좌표로 재생. 예산 계산에서 한 번만 변환하도록 분리함
        private static void PlayFlattened(FixedString64Bytes _key, Vector3 _position)
        {
            if (_key.IsEmpty) return;
            if (SoundManager.Instance == null) return;

            SoundManager.Instance.PlaySfx3D(_key.ToString(), _position);
        }

        /// <summary>
        /// 소리 위치의 z 를 리스너 평면으로 맞춤.
        /// <para/>
        /// 리스너(Main Camera)가 z = -10 에 있고 게임 오브젝트는 z = 0 이라, 좌표를 그대로 넘기면
        /// 3D 거리에 z 차이 10 이 상수로 섞여 같은 자리에서도 최대 음량이 안 나옴.
        /// z 를 맞춰 두면 거리가 xy 평면 거리와 같아지고, 오브젝트별 z 값에도 영향받지 않음.
        /// </summary>
        private static Vector3 Flatten(Vector3 _position)
        {
            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            float z = mainCamera == null ? _position.z : mainCamera.transform.position.z;

            return new Vector3(_position.x, _position.y, z);
        }

        // 이번 프레임에 이 소리를 낼 위치 후보. 가장 가까운 우주선이 이김.
        private static void TrackNearest(
            Dictionary<FixedString64Bytes, PendingSound> _targets,
            FixedString64Bytes _key,
            Vector3 _position,
            float2 _listener)
        {
            Vector3 flat = Flatten(_position);

            float dx = flat.x - _listener.x;
            float dy = flat.y - _listener.y;
            float sqrDistance = dx * dx + dy * dy;

            if (_targets.TryGetValue(_key, out PendingSound prev) && prev.SqrDistance <= sqrDistance) return;

            _targets[_key] = new PendingSound { SqrDistance = sqrDistance, Position = flat };
        }

        /// <summary>
        /// 이번 프레임에 필요한 반복음을 켜고, 더 이상 필요 없는 것을 끔.
        /// <para/>
        /// 반복음은 한 번 재생하면 그 자리에 머무르므로 재생 중인 것도 위치를 갱신함.
        /// </summary>
        private void ApplyLoops()
        {
            foreach (KeyValuePair<FixedString64Bytes, PendingSound> pair in loopTargets)
            {
                if (activeLoops.Add(pair.Key))
                {
                    PlayFlattened(pair.Key, pair.Value.Position);
                    continue;
                }

                MoveLoop(pair.Key, pair.Value.Position);
            }

            stoppedLoops.Clear();

            foreach (FixedString64Bytes key in activeLoops)
            {
                if (!loopTargets.ContainsKey(key))
                {
                    stoppedLoops.Add(key);
                }
            }

            foreach (FixedString64Bytes key in stoppedLoops)
            {
                Release(key);
                activeLoops.Remove(key);
            }
        }

        // 재생 중인 반복음, 충전음 그룹의 음원을 옮김. 그룹의 첫 변형만 씀(변형 1개로 충분함).
        private static void MoveLoop(FixedString64Bytes _key, Vector3 _flatPosition)
        {
            Transform group = DarkTonic.MasterAudio.MasterAudio.Instance == null
                ? null
                : DarkTonic.MasterAudio.MasterAudio.Instance.transform.Find(_key.ToString());

            if (group == null || group.childCount == 0) return;

            group.GetChild(0).position = _flatPosition;
        }

        private void ReleaseAllLoops()
        {
            foreach (FixedString64Bytes key in activeLoops)
            {
                Release(key);
            }

            activeLoops.Clear();
        }

        // 바로 끊지 않고 서서히 줄여 멈춤.
        private void Release(FixedString64Bytes _key)
        {
            if (_key.IsEmpty) return;
            if (SoundManager.Instance == null) return;

            SoundManager.Instance.FadeOutSfx(_key.ToString(), loopFadeOut);
        }
    }
}