using Unity.Mathematics;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 팀 모드에서 우주선이 없는 동안 카메라가 비추는 곳. 서버는 이 접속의 복제 범위 중심을 이곳으로 옮김.
    /// </summary>
    public struct SpectateRPC : IRpcCommand
    {
        public float2 Position;
    }
}
