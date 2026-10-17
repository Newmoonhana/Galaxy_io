using GalaxyIO.DOTS.Components.Singleton;
using Unity.NetCode;

namespace GalaxyIO.DOTS.Components.Message
{
    /// <summary>
    /// 클라이언트가 자기 우주선의 포탑 칸에 포탑을 장착해 달라고 요청하는 RPC.
    /// 잠금 해제하지 않은 포탑이면 재료를 써서 잠금 해제부터 함.
    /// </summary>
    public struct TurretEquipRPC : IRpcCommand
    {
        // 인벤토리 포탑 칸 번호. 조종 포탑 총구가 있는 기종은 0 이 조종 포탑, 나머지는 자동 포탑 칸.
        public byte Slot;

        public ItemId Turret;
    }
}
