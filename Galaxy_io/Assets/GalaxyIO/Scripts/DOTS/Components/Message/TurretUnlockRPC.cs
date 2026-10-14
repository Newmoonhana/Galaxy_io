using GalaxyIO.DOTS.Components.Singleton;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 서버가 인벤토리 주인에게 잠금 해제한 포탑 하나를 알리는 RPC.
    /// </summary>
    public struct TurretUnlockRPC : IRpcCommand
    {
        public ItemId Turret;
    }
}
