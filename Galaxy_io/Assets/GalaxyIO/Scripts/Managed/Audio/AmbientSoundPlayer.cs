using GalaxyIO.Tools;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.Audio
{
    /// <summary>
    /// 배경음을 무작위 순서로 반복 재생하는 클라이언트 전용 플레이어.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class AmbientSoundPlayer : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("무작위로 반복 재생할 배경음")]
        private AudioClip[] clips;

        [SerializeField]
        [Tooltip("로비 음량")]
        [Range(0.0F, 1.0F)]
        private float lobbyVolume = 1.0F;

        [SerializeField]
        [Tooltip("인게임 음량")]
        [Range(0.0F, 1.0F)]
        private float inGameVolume = 0.5F;

        [SerializeField]
        [Tooltip("로비/인게임 전환 시 음량이 바뀌는 시간 (초)")]
        [Min(0.0F)]
        private float fadeTime = 1.0F;

        private AudioSource audioSource;
        // 서버 접속 중(=인게임)인지 판별하는 쿼리.
        private EntityQuery inGameQuery;
        private bool hasQuery;
        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;
        private int clipIndex = -1;

        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.loop = false;
            audioSource.volume = lobbyVolume;

            clientWorld = ClientServerBootstrap.ClientWorld;
            if (clientWorld != null)
            {
                inGameQuery = clientWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<NetworkStreamInGame>()
                );
                hasQuery = true;
            }

            PlayNext();
        }

        private void Update()
        {
            UpdateVolume();

            if (!audioSource.isPlaying) PlayNext();
        }

        // 로비/인게임에 맞춰 음량만 서서히 변경. 재생 중인 곡은 유지.
        private void UpdateVolume()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 그때부터는 로비 음량으로 돌아감.
            if (hasQuery && !ClientWorldScope.IsAlive(clientWorld)) hasQuery = false;

            bool inGame = hasQuery && !inGameQuery.IsEmpty;
            float target = inGame ? inGameVolume : lobbyVolume;

            float step = fadeTime <= 0.0F ? 1.0F : Time.unscaledDeltaTime / fadeTime;
            audioSource.volume = Mathf.MoveTowards(audioSource.volume, target, step);
        }

        private void PlayNext()
        {
            if (clips == null || clips.Length == 0) return;

            // 직전 곡은 제외하고 선택.
            if (clipIndex < 0 || clips.Length == 1)
            {
                clipIndex = Random.Range(0, clips.Length);
            }
            else
            {
                int next = Random.Range(0, clips.Length - 1);
                clipIndex = next >= clipIndex ? next + 1 : next;
            }

            audioSource.clip = clips[clipIndex];
            audioSource.Play();
        }
    }
}
