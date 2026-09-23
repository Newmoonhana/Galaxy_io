using GalaxyIO.DOTS.Client.Components.Message;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.Audio
{
    /// <summary>
    /// <see cref="SoundMessage"/>를 읽어 효과음을 재생하는 클라이언트 전용 플레이어.
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

        private AudioSource audioSource;
        private EntityManager entityManager;
        private EntityQuery messageQuery;

        private void Start()
        {
            World clientWorld = ClientServerBootstrap.ClientWorld;

            if (clientWorld == null)
            {
                enabled = false;
                return;
            }

            audioSource = GetComponent<AudioSource>();
            entityManager = clientWorld.EntityManager;
            messageQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SoundMessage>());
        }

        private void LateUpdate()
        {
            if (messageQuery.IsEmpty)
            {
                return;
            }

            UnityEngine.Camera mainCamera = UnityEngine.Camera.main;
            Vector2 listener = mainCamera == null ? Vector2.zero : (Vector2)mainCamera.transform.position;

            using var messages = messageQuery.ToComponentDataArray<SoundMessage>(Allocator.Temp);

            foreach (SoundMessage message in messages)
            {
                AudioClip clip = message.Clip.Value;

                if (clip == null)
                {
                    continue;
                }

                // fullVolumeRadius ~ audibleRadius 구간에서 선형 감쇠.
                float distance = Vector2.Distance(listener, message.Position);
                float volume = 1.0F - Mathf.InverseLerp(fullVolumeRadius, audibleRadius, distance);

                if (volume > 0.0F)
                {
                    audioSource.PlayOneShot(clip, volume);
                }
            }

            entityManager.DestroyEntity(messageQuery);
        }
    }
}
