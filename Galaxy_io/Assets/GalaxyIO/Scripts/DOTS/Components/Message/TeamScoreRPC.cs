using GalaxyIO.DOTS.Components.Singleton;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 팀 점수와 승리 팀. 서버가 바뀔 때마다 모든 접속에 보냄.
    /// </summary>
    public struct TeamScoreRPC : IRpcCommand
    {
        public int Red;
        public int Blue;

        // 이긴 팀. 판이 진행 중이면 None.
        public Team Winner;

        // 판이 끝났을 때 다음 판까지 남은 시간(초)
        public float RestartIn;

        // 끝난 판 다음에 새 판을 시작함. 클라이언트는 맵을 다시 받는 동안 로딩 화면을 띄움.
        public bool NewRound;
    }
}
