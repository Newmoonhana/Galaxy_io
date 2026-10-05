using System.Collections.Generic;
using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Tools;
using Newmoonhana.Runtime.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.Audio
{
    /// <summary>
    /// <see cref="SoundMessage"/>를 읽어 효과음을 재생하는 클라이언트 전용 플레이어.
    /// 우주선의 <see cref="TurretSwitchComponent"/>를 보고 포탑 사격 시작음과 반복음도 재생함.
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

        private EntityManager entityManager;
        private EntityQuery messageQuery;
        private EntityQuery shipQuery;
        private EntityQuery registryQuery;

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        // 우주선별 직전 사격 전환 상태와 재생 중인 반복음 그룹
        private readonly Dictionary<Entity, TurretSound> turretSounds = new Dictionary<Entity, TurretSound>();

        // 이번 프레임에 본 우주선. 사라진 우주선의 반복음을 끄는 데 씀.
        private readonly HashSet<Entity> seen = new HashSet<Entity>();
        private readonly List<Entity> gone = new List<Entity>();

        // 이번 프레임 반복음 그룹별 최소 거리. Sound Group 이 우주선별로 나뉘지 않으므로
        // 가장 가까운 쪽 위치를 그룹에 반영함
        private readonly Dictionary<FixedString64Bytes, float> loopDistances =
            new Dictionary<FixedString64Bytes, float>();

        private struct TurretSound
        {
            public TurretSwitchState State;

            // 재생 중인 반복음 Sound Group 이름. 비어 있으면 재생 중이 아님.
            public FixedString64Bytes Loop;
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
        }

        private void LateUpdate()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 캐시한 쿼리를 더 쓰면 안 됨.
            if (!ClientWorldScope.IsAlive(clientWorld))
            {
                foreach (TurretSound sound in turretSounds.Values)
                {
                    Release(sound.Loop);
                }

                turretSounds.Clear();
                enabled = false;
                return;
            }

            // 쉬다가 사격 전환이 시작되면 시작음을 한 번, 사격 중에는 반복음을 재생함.
            seen.Clear();
            loopDistances.Clear();

            if (!registryQuery.IsEmpty)
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

                    // 쉬다가 방아쇠를 당긴 순간.
                    if (sound.State == TurretSwitchState.Idle && current != TurretSwitchState.Idle)
                    {
                        Play(turret.LaunchSound, position);
                    }

                    FixedString64Bytes loop =
                        current == TurretSwitchState.Firing ? turret.LoopSound : default;

                    if (loop.IsEmpty)
                    {
                        Release(sound.Loop);
                        sound.Loop = default;
                    }
                    else
                    {
                        if (sound.Loop != loop)
                        {
                            Release(sound.Loop);
                            Play(loop, position);
                            sound.Loop = loop;
                        }

                        // 우주선이 움직이면 음량이 따라가야 함. Sound Group 은 공유되므로
                        // 같은 포탑을 쏘는 우주선 중 가장 가까운 쪽 음량을 씀
                        TrackLoopPosition(loop, position);
                    }

                    sound.State = current;
                    turretSounds[ships[i]] = sound;
                }
            }

            // 파괴됐거나 복제 범위를 벗어난 우주선.
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
                Release(turretSounds[ship].Loop);
                turretSounds.Remove(ship);
            }

            if (messageQuery.IsEmpty)
            {
                return;
            }

            using var messages = messageQuery.ToComponentDataArray<SoundMessage>(Allocator.Temp);

            foreach (SoundMessage message in messages)
            {
                Play(message.SoundKey, new Vector3(message.Position.x, message.Position.y, 0.0F));
            }

            entityManager.DestroyEntity(messageQuery);
        }

        // 감쇠는 Sound Group 의 3D 커브가 담당함.
        private static void Play(FixedString64Bytes _key, Vector3 _position)
        {
            if (_key.IsEmpty) return;
            if (SoundManager.Instance == null) return;

            SoundManager.Instance.PlaySfx3D(_key.ToString(), Flatten(_position));
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

        // 반복음은 한 번 재생하면 그 자리에 머무르므로 우주선을 따라가게 위치를 갱신함.
        // Sound Group 이 우주선별로 나뉘지 않아 같은 포탑을 쏘는 우주선 중 가장 가까운 쪽을 따름.
        private void TrackLoopPosition(FixedString64Bytes _key, Vector3 _position)
        {
            Vector3 flat = Flatten(_position);

            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            Vector3 listener = mainCamera == null ? Vector3.zero : mainCamera.transform.position;
            float distance = Vector3.Distance(listener, flat);

            if (loopDistances.TryGetValue(_key, out float prev) && prev <= distance) return;

            loopDistances[_key] = distance;

            Transform group = DarkTonic.MasterAudio.MasterAudio.Instance == null
                ? null
                : DarkTonic.MasterAudio.MasterAudio.Instance.transform.Find(_key.ToString());

            if (group == null || group.childCount == 0) return;

            group.GetChild(0).position = flat;
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
