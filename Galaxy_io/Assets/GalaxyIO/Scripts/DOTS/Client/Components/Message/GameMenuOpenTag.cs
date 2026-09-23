using Unity.Entities;

namespace GalaxyIO.DOTS.Client.Components.Message
{
    /// <summary>
    /// 게임 메뉴가 열려 있음을 나타내는 태그. 열린 동안 우주선 조작 입력을 막음.
    /// </summary>
    public struct GameMenuOpenTag : IComponentData
    {
    }
}
