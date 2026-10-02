using System.Collections.Generic;
using GalaxyIO.DOTS.Client.Components.Message;
using GalaxyIO.DOTS.Components.Actor;
using GalaxyIO.DOTS.Components.Singleton;
using GalaxyIO.Tools;
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
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class SoundEffectPlayer : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("카메라에서 이 거리 안이면 최대 음량")]
        [Min(0.0F)]
        private float fullVolumeRadius = 3.0F;

        [SerializeField]
        [Tooltip("카메라에서 이 거리만큼 떨어지면 소리가 0이 됨. 카메라 Orthographic Size와 맞춤.")]
        [Min(0.01F)]
        private float audibleRadius = 15.0F;

        [SerializeField]
        [Tooltip("포탑 반복음이 멈출 때 최대 음량에서 0 까지 줄어드는 시간(초)")]
        [Min(0.01F)]
        private float loopFadeOut = 0.3F;

        private AudioSource audioSource;
        private EntityManager entityManager;
        private EntityQuery messageQuery;
        private EntityQuery shipQuery;
        private EntityQuery registryQuery;

        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        // 우주선별 직전 사격 전환 상태와 재생 중인 반복음
        private readonly Dictionary<Entity, TurretSound> turretSounds = new Dictionary<Entity, TurretSound>();

        // 쉬고 있는 반복음 재생기
        private readonly Stack<AudioSource> idleLoops = new Stack<AudioSource>();

        // 멈추는 중인 반복음 재생기. 음량이 0 이 되면 쉬는 재생기로 돌아감.
        private readonly List<AudioSource> fadingLoops = new List<AudioSource>();

        // 이번 프레임에 본 우주선. 사라진 우주선의 반복음을 끄는 데 씀.
        private readonly HashSet<Entity> seen = new HashSet<Entity>();
        private readonly List<Entity> gone = new List<Entity>();

        private struct TurretSound
        {
            public TurretSwitchState State;

            // 반복음을 재생 중이면 그 재생기. 아니면 null.
            public AudioSource Loop;
        }

        private void Start()
        {
            clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            audioSource = GetComponent<AudioSource>();
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
                    if (sound.Loop != null)
                    {
                        sound.Loop.Stop();
                    }
                }

                foreach (AudioSource fading in fadingLoops)
                {
                    fading.Stop();
                }

                turretSounds.Clear();
                fadingLoops.Clear();
                enabled = false;
                return;
            }

            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            Vector2 listener = mainCamera == null ? Vector2.zero : (Vector2)mainCamera.transform.position;

            // 멈춘 반복음을 서서히 줄임.
            for (int i = fadingLoops.Count - 1; i >= 0; i--)
            {
                AudioSource fading = fadingLoops[i];
                fading.volume = Mathf.MoveTowards(fading.volume, 0.0F, Time.deltaTime / loopFadeOut);

                if (fading.volume <= 0.0F)
                {
                    fading.Stop();
                    idleLoops.Push(fading);
                    fadingLoops.RemoveAt(i);
                }
            }

            // 쉬다가 사격 전환이 시작되면 시작음을 한 번, 사격 중에는 반복음을 재생함. 우주선을 따라 음량이 바뀜.
            seen.Clear();

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
                    float volume = VolumeAt(listener, transforms[i].Position.xy);

                    turretSounds.TryGetValue(ships[i], out TurretSound sound);

                    // 쉬다가 방아쇠를 당긴 순간.
                    if (sound.State == TurretSwitchState.Idle && current != TurretSwitchState.Idle && volume > 0.0F)
                    {
                        AudioClip launch = turret.LaunchSound.Value;

                        if (launch != null)
                        {
                            audioSource.PlayOneShot(launch, volume);
                        }
                    }

                    AudioClip loop = current == TurretSwitchState.Firing ? turret.LoopSound.Value : null;

                    if (loop == null)
                    {
                        Release(ref sound);
                    }
                    else
                    {
                        if (sound.Loop == null && idleLoops.Count > 0)
                        {
                            sound.Loop = idleLoops.Pop();
                        }
                        else if (sound.Loop == null)
                        {
                            sound.Loop = gameObject.AddComponent<AudioSource>();
                            sound.Loop.loop = true;
                            sound.Loop.playOnAwake = false;
                            sound.Loop.spatialBlend = audioSource.spatialBlend;
                            sound.Loop.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
                        }

                        if (sound.Loop.clip != loop || !sound.Loop.isPlaying)
                        {
                            sound.Loop.clip = loop;
                            sound.Loop.Play();
                        }

                        sound.Loop.volume = volume;
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
                TurretSound sound = turretSounds[ship];
                Release(ref sound);
                turretSounds.Remove(ship);
            }

            if (messageQuery.IsEmpty)
            {
                return;
            }

            using var messages = messageQuery.ToComponentDataArray<SoundMessage>(Allocator.Temp);

            foreach (SoundMessage message in messages)
            {
                AudioClip clip = message.Clip.Value;

                if (clip == null)
                {
                    continue;
                }

                float volume = VolumeAt(listener, message.Position);

                if (volume > 0.0F)
                {
                    audioSource.PlayOneShot(clip, volume);
                }
            }

            entityManager.DestroyEntity(messageQuery);
        }

        // fullVolumeRadius ~ audibleRadius 구간에서 선형 감쇠.
        private float VolumeAt(Vector2 listener, Vector2 position)
        {
            return 1.0F - Mathf.InverseLerp(fullVolumeRadius, audibleRadius, Vector2.Distance(listener, position));
        }

        private void Release(ref TurretSound sound)
        {
            if (sound.Loop == null)
            {
                return;
            }

            // 바로 끊지 않고 서서히 줄여 멈춤.
            fadingLoops.Add(sound.Loop);
            sound.Loop = null;
        }
    }
}
