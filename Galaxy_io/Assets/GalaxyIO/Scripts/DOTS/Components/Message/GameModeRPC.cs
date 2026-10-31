using GalaxyIO.DOTS.Components.Singleton;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 서버의 게임 모드. 서버가 접속마다 한 번 보냄. 클라이언트는 이걸 받고 모드 씬을 로드함.
    /// </summary>
    public struct GameModeRPC : IRpcCommand
    {
        public GameMode Mode;

        // 최대 인원이 차서 들어갈 수 없음.
        public bool Full;
    }
}
