using GalaxyIO.DOTS.Components.Singleton;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 클라이언트가 재료로 우주선을 사서 바꿔 타게 해 달라고 요청하는 RPC.
    /// </summary>
    public struct SpaceshipPurchaseRPC : IRpcCommand
    {
        public SpaceshipId Spaceship;
    }
}
