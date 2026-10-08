using GalaxyIO.Tools;
using Newmoonhana.Runtime.Core;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.Audio
{
    /// <summary>
    /// 배경음을 재생하는 클라이언트 전용 플레이어.
    /// 곡 목록과 무작위 순서는 Master Audio Playlist 가 담당하고,
    /// 여기서는 로비 / 인게임 음량 전환만 처리함.
    /// </summary>
    public sealed class AmbientSoundPlayer : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("재생할 Master Audio Playlist 이름")]
        private string playlist = "Ambient";

        [SerializeField]
        [Tooltip("로비 음량")]
        [Range(0.0F, 1.0F)]
        private float lobbyVolume = 1.0F;

        [SerializeField]
        [Tooltip("인게임 음량")]
        [Range(0.0F, 1.0F)]
        private float inGameVolume = 1.0F;

        [SerializeField]
        [Tooltip("로비/인게임 전환 시 음량이 바뀌는 시간 (초)")]
        [Min(0.0F)]
        private float fadeTime = 1.0F;

        // 서버 접속 중(=인게임)인지 판별하는 쿼리.
        private EntityQuery inGameQuery;
        private bool hasQuery;
        // 캐시한 쿼리가 속한 클라이언트 월드. 세션 종료 시 Dispose 되므로 매 프레임 확인함.
        private World clientWorld;

        // 직전에 요청한 음량. 같은 값을 반복해서 FadeBGM 하지 않도록 들고 있음.
        private float requested = -1.0F;

        private void Start()
        {
            clientWorld = ClientServerBootstrap.ClientWorld;
            if (clientWorld != null)
            {
                inGameQuery = clientWorld.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<NetworkStreamInGame>()
                );
                hasQuery = true;
            }

            if (SoundManager.Instance == null || string.IsNullOrEmpty(playlist)) return;

            SoundManager.Instance.PlayBGM(playlist);
        }

        private void Update()
        {
            // 세션을 나가면 클라이언트 월드가 Dispose 됨. 그때부터는 로비 음량으로 돌아감.
            if (hasQuery && !ClientWorldScope.IsAlive(clientWorld)) hasQuery = false;

            bool inGame = hasQuery && !inGameQuery.IsEmpty;
            float target = inGame ? inGameVolume : lobbyVolume;

            // 목표가 바뀐 순간에만 페이드를 걸음. Master Audio 가 보간을 돌림.
            if (Mathf.Approximately(requested, target)) return;
            if (SoundManager.Instance == null) return;

            requested = target;

            // 인자 있는 FadeBGM 은 Playlist 가 아니라 PlaylistController 이름을 받음.
            // 컨트롤러가 하나뿐이라 전체 대상 오버로드를 씀
            SoundManager.Instance.FadeBGM(target, fadeTime);
        }
    }
}
