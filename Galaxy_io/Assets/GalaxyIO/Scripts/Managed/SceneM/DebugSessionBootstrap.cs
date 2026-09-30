using GalaxyIO.DOTS.Networking;
using GalaxyIO.Tools.PopupUI.Popup;
using Newmoonhana.Runtime.Utils.DEBUG;
using Unity.NetCode;
using UnityEngine;

namespace GalaxyIO.Main
{
    /// <summary>
    /// 인게임 씬을 로비를 거치지 않고 직접 Play 했을 때만 쓰는 디버그 전용 진입점.
    /// 실제 게임 흐름에서는 로비가 세션을 만들기 때문에 이 코드는 동작하지 않음.
    /// 에디터에서만 컴파일됨.
    /// </summary>
    public sealed class DebugSessionBootstrap : MonoBehaviour
    {
#if UNITY_EDITOR
        // 디버그 진입으로 접속할 때 쓰는 이름.
        private const string DebugPlayerName = "DebugHost";

        private void Start()
        {
            // 로비를 거쳐 들어왔으면 이미 세션이 있음. 그때는 아무것도 하지 않음.
            if (GameSession.IsActive || ClientServerBootstrap.ClientWorlds.Count > 0) return;

            DebugLog.LogWarning("디버그 전용 진입: 로비를 거치지 않은 인게임 씬 직접 실행. 호스트 세션을 구성하고 씬을 다시 로드함.");
            _ = HostAsync();
        }

        /// <summary>
        /// SubScene 베이킹 데이터는 씬 로드 시점에 있는 월드로만 들어옴.
        /// 이미 로드된 씬은 재스트리밍되지 않으므로, 월드를 만든 뒤 같은 씬을 다시 로드함.
        /// </summary>
        private async Awaitable HostAsync()
        {
            // 씬 로드·스트리밍·접속 동안 빈 화면이 보이지 않게 가림. 인게임에서 치움.
            _ = CommonProgress.ShowAcrossScenes();

            if (!await GameSession.HostAndPlay(DebugPlayerName))
            {
                CommonProgress.HideAcrossScenes();
                DebugLog.LogError("디버그 전용 진입: 호스트 세션 구성 실패. 이미 7979 포트를 쓰는 프로세스가 있는지 확인할 것.");
            }
        }
#endif
    }
}
