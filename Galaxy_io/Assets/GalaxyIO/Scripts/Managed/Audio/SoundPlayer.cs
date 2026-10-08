using System;
using Newmoonhana.Runtime.Core;
using UnityEngine;

namespace GalaxyIO.Scripts.Managed.Audio
{
    /// <summary>
    /// 이 씬이 쓸 배경음 Playlist 와 음량을 지정하는 클라이언트 전용 플레이어.
    /// 곡 목록과 무작위 순서는 Master Audio Playlist 가 담당함.
    /// </summary>
    public sealed class SoundPlayer : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("재생할 Master Audio Playlist 이름")]
        private string playlist = "Game";

        [SerializeField]
        [Tooltip("이 씬에서의 배경음 음량")]
        [Range(0.0F, 1.0F)]
        private float volume = 1.0F;

        [SerializeField]
        [Tooltip("음량이 바뀌는 시간 (초)")]
        [Min(0.0F)]
        private float fadeTime = 1.0F;

        //SceneStack 이 COVER 로 씬 루트를 껐다 켜므로 Start 가 아니라 OnEnable 에서 처리함.
        //Pop 으로 로비에 돌아올 때 로비 Playlist 가 다시 걸려야 함.
        private async void OnEnable()
        {
            //최초 씬 로드에서는 SoundManager / PlaylistController 의 Awake 보다 먼저 올 수 있음.
            try { await Awaitable.NextFrameAsync(destroyCancellationToken); }
            catch (OperationCanceledException) { return; }

            //대기 중에 씬이 덮였으면 건너뜀. 비활성화는 토큰을 취소하지 않음.
            if (!isActiveAndEnabled) return;

            if (SoundManager.Instance == null || string.IsNullOrEmpty(playlist)) return;

            SoundManager.Instance.PlayBGM(playlist);

            //컨트롤러가 하나뿐이라 PlaylistController 이름을 받는 오버로드 대신 전체 대상을 씀.
            SoundManager.Instance.FadeBGM(volume, fadeTime);
        }
    }
}
