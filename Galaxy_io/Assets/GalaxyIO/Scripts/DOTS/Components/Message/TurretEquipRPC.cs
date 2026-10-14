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
        // 포탑 칸 번호. 0 이 플레이어가 쏘는 주 포탑.
        // 나머지 칸은 자동 포탑(AI 조종) 자리. 자동 포탑 시스템이 생기기 전까지 서버가 장착하지 않음.
        public byte Slot;

        public ItemId Turret;
    }
}
