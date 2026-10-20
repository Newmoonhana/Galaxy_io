using Unity.Entities;

namespace GalaxyIO.DOTS.Components.Actor
{
    /// <summary>
    /// 조종 포탑을 바꿔 낄 수 없는 우주선 표시. e.g. 순양함 함포.
    /// 인벤토리에 조종 포탑 칸이 없고, 바꿔 탈 때 조종 포탑을 옮기지 않음.
    /// </summary>
    public struct FixedTurretTag : IComponentData
    {
    }
}
